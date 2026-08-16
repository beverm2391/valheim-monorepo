using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace BenheimQoL.Infrastructure;

internal static class RemoteDiagnostics
{
    internal const string PrivateConfigFileName = "BenheimPrivateDiagnostics.cfg";
    private const string ConfigMarker = "BENHEIM_PRIVATE_DIAGNOSTICS_V1";
    private const string Notice =
        "Benheim is sharing typed gameplay diagnostics, your character name, and a connection ID " +
        "for this private test. No chat or full logs are sent. Change Share Diagnostics in Left Shift+B.";

    private static AxiomEventSink? sink;

    internal static bool IsConfigured => sink != null;

    internal static void Begin(string configRootPath)
    {
        Reset();
        string path = Path.Combine(configRootPath, PrivateConfigFileName);
        if (TryReadPrivateConfig(path, out AxiomIngestConfig? config) && config != null)
        {
            sink = new AxiomEventSink(config, DiagnosticsSharingSettings.ClientId);
            if (DiagnosticsSharingSettings.ShareDiagnostics && DiagnosticsSharingSettings.NoticeShown)
            {
                sink.Enable();
            }
        }
    }

    internal static void Update()
    {
        if (sink == null || !DiagnosticsSharingSettings.ShareDiagnostics || sink.Enabled)
        {
            return;
        }

        if (!DiagnosticsSharingSettings.NoticeShown)
        {
            if (Player.m_localPlayer == null || MessageHud.instance == null)
            {
                return;
            }

            MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, Notice);
            DiagnosticsSharingSettings.MarkNoticeShown();
        }

        sink.Enable();
    }

    internal static void SetSharingEnabled(bool enabled)
    {
        if (!enabled)
        {
            sink?.Disable();
            return;
        }

        Update();
    }

    internal static void TryEnqueue(DiagnosticEvent diagnosticEvent)
    {
        sink?.TryEnqueue(diagnosticEvent);
    }

    internal static void Reset()
    {
        sink?.Stop();
        sink = null;
    }

    private static bool TryReadPrivateConfig(string path, out AxiomIngestConfig? config)
    {
        config = null;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            FileInfo info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > 4096)
            {
                Plugin.Log.LogWarning("Benheim private diagnostics config has an invalid size; sharing is disabled.");
                return false;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length != 5 || lines[0] != ConfigMarker)
            {
                Plugin.Log.LogWarning("Benheim private diagnostics config has an invalid format; sharing is disabled.");
                return false;
            }

            string endpoint = ReadValue(lines[1], "endpoint=");
            string dataset = ReadValue(lines[2], "dataset=");
            string token = ReadValue(lines[3], "token=");
            string buildId = ReadValue(lines[4], "build_id=");
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? endpointUri) ||
                endpointUri.Scheme != Uri.UriSchemeHttps ||
                !string.IsNullOrEmpty(endpointUri.Query) ||
                !string.IsNullOrEmpty(endpointUri.Fragment) ||
                !ValidDataset(dataset) ||
                string.IsNullOrWhiteSpace(token) ||
                token.Length > 1024 ||
                string.IsNullOrWhiteSpace(buildId) ||
                buildId.Length > 128)
            {
                Plugin.Log.LogWarning("Benheim private diagnostics config is invalid; sharing is disabled.");
                return false;
            }

            config = new AxiomIngestConfig(endpointUri, dataset, token, buildId);
            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Benheim private diagnostics config could not be read; sharing is disabled ({exception.GetType().Name}).");
            return false;
        }
    }

    private static string ReadValue(string line, string prefix)
    {
        return line.StartsWith(prefix, StringComparison.Ordinal)
            ? line.Substring(prefix.Length)
            : string.Empty;
    }

    private static bool ValidDataset(string dataset)
    {
        if (dataset.Length == 0 || dataset.Length > 200)
        {
            return false;
        }

        foreach (char character in dataset)
        {
            if (!char.IsLetterOrDigit(character) &&
                character != '_' &&
                character != '-' &&
                character != '.')
            {
                return false;
            }
        }
        return true;
    }

    private sealed class AxiomIngestConfig
    {
        internal AxiomIngestConfig(Uri endpoint, string dataset, string token, string buildId)
        {
            Endpoint = new Uri(
                endpoint.AbsoluteUri.TrimEnd('/') + "/v1/ingest/" + Uri.EscapeDataString(dataset));
            Token = token;
            BuildId = buildId;
        }

        internal Uri Endpoint { get; }
        internal string Token { get; }
        internal string BuildId { get; }
    }

    private sealed class AxiomEventSink
    {
        private const int MaximumQueuedEvents = 512;
        private const int MaximumBatchEvents = 100;
        private const int MaximumEventCharacters = 16384;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

        private readonly object gate = new object();
        private readonly Queue<QueuedRemoteEvent> queue = new Queue<QueuedRemoteEvent>();
        private readonly AxiomIngestConfig config;
        private readonly string clientId;
        private readonly HttpClient httpClient = new HttpClient { Timeout = RequestTimeout };
        private CancellationTokenSource? cancellation;
        private bool overflowLogged;
        private bool oversizeLogged;

        internal AxiomEventSink(AxiomIngestConfig config, string clientId)
        {
            this.config = config;
            this.clientId = clientId;
        }

        internal bool Enabled { get; private set; }

        internal void Enable()
        {
            lock (gate)
            {
                if (Enabled)
                {
                    return;
                }

                Enabled = true;
                cancellation = new CancellationTokenSource();
                _ = Task.Run(() => Pump(cancellation.Token));
            }
        }

        internal void Disable()
        {
            CancellationTokenSource? toCancel;
            lock (gate)
            {
                Enabled = false;
                queue.Clear();
                toCancel = cancellation;
                cancellation = null;
            }
            toCancel?.Cancel();
        }

        internal void TryEnqueue(DiagnosticEvent diagnosticEvent)
        {
            Player? player = Player.m_localPlayer;
            string playerName = player?.GetPlayerName() ??
                Game.instance?.GetPlayerProfile()?.GetName() ??
                string.Empty;
            string peerId = ZNet.instance?.GetServerRPC() == null
                ? string.Empty
                : ZNet.GetUID().ToString(CultureInfo.InvariantCulture);
            bool overflow = false;
            lock (gate)
            {
                if (!Enabled)
                {
                    return;
                }

                if (queue.Count >= MaximumQueuedEvents)
                {
                    overflow = !overflowLogged;
                    overflowLogged = true;
                }
                else
                {
                    queue.Enqueue(new QueuedRemoteEvent(diagnosticEvent, playerName, peerId));
                }
            }

            if (overflow)
            {
                Plugin.Log.LogWarning(
                    "Benheim private diagnostics queue is full; remote copies are being dropped while local diagnostics continue.");
            }
        }

        internal void Stop()
        {
            Disable();
        }

        private async Task Pump(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(FlushInterval, cancellationToken).ConfigureAwait(false);
                    while (TryTakeBatch(out List<QueuedRemoteEvent>? batch) && batch != null)
                    {
                        if (!await TrySend(batch, cancellationToken).ConfigureAwait(false))
                        {
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Toggle-off and plugin teardown intentionally abandon queued
                // remote copies without touching local diagnostics or shutdown.
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning(
                    $"Benheim private diagnostics stopped after {exception.GetType().Name}; local diagnostics continue.");
            }
        }

        private bool TryTakeBatch(out List<QueuedRemoteEvent>? batch)
        {
            lock (gate)
            {
                if (!Enabled || queue.Count == 0)
                {
                    batch = null;
                    return false;
                }

                int count = Math.Min(queue.Count, MaximumBatchEvents);
                batch = new List<QueuedRemoteEvent>(count);
                for (int index = 0; index < count; index++)
                {
                    batch.Add(queue.Dequeue());
                }
                return true;
            }
        }

        private async Task<bool> TrySend(List<QueuedRemoteEvent> batch, CancellationToken cancellationToken)
        {
            StringBuilder payload = new StringBuilder(batch.Count * 256);
            payload.Append('[');
            int appended = 0;
            for (int index = 0; index < batch.Count; index++)
            {
                QueuedRemoteEvent queued = batch[index];
                string json = queued.Event.ToRemoteJsonLine(
                    clientId,
                    queued.PlayerName,
                    queued.PeerId,
                    config.BuildId);
                if (json.Length > MaximumEventCharacters)
                {
                    if (!oversizeLogged)
                    {
                        oversizeLogged = true;
                        Plugin.Log.LogWarning(
                            "Benheim private diagnostics dropped an oversized remote event; local diagnostics continue.");
                    }
                    continue;
                }

                if (appended > 0)
                {
                    payload.Append(',');
                }
                payload.Append(json);
                appended++;
            }
            payload.Append(']');

            if (appended == 0)
            {
                return true;
            }

            try
            {
                using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, config.Endpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.Token);
                request.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                using HttpResponseMessage response = await httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                Plugin.Log.LogWarning(
                    $"Benheim private diagnostics dropped {batch.Count} remote events after Axiom HTTP {(int)response.StatusCode}; local diagnostics continue.");
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Plugin.Log.LogWarning(
                    $"Benheim private diagnostics dropped {batch.Count} remote events after {exception.GetType().Name}; local diagnostics continue.");
                return false;
            }
        }

        private readonly struct QueuedRemoteEvent
        {
            internal QueuedRemoteEvent(DiagnosticEvent diagnosticEvent, string playerName, string peerId)
            {
                Event = diagnosticEvent;
                PlayerName = playerName;
                PeerId = peerId;
            }

            internal DiagnosticEvent Event { get; }
            internal string PlayerName { get; }
            internal string PeerId { get; }
        }
    }
}
