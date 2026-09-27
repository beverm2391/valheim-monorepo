using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
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
        "Benheim sends typed gameplay diagnostics, your character name, and a connection ID " +
        "to Axiom for our group. No chat or full logs are sent. Check delivery in Left Shift+B.";

    private static AxiomEventSink? sink;

    internal static bool IsConfigured => sink != null;
    internal static string DeliveryStatus => sink?.Status ??
        "Axiom is not configured. Events remain in local logs; reinstall the group package.";

    internal static void Begin(string configRootPath)
    {
        Reset();
        string path = Path.Combine(configRootPath, PrivateConfigFileName);
        if (TryReadPrivateConfig(path, out AxiomIngestConfig? config) && config != null)
        {
            sink = new AxiomEventSink(config, DiagnosticsClientSettings.ClientId);
            sink.Enable();
        }
    }

    internal static void Update()
    {
        if (sink == null || DiagnosticsClientSettings.NoticeShown)
        {
            return;
        }

        if (Player.m_localPlayer == null || MessageHud.instance == null)
        {
            return;
        }

        MessageHud.instance.ShowMessage(MessageHud.MessageType.TopLeft, Notice);
        DiagnosticsClientSettings.MarkNoticeShown();
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
                Plugin.Log.LogWarning("Benheim diagnostics config has an invalid size; Axiom delivery is unavailable.");
                return false;
            }

            string[] lines = File.ReadAllLines(path);
            if (lines.Length != 5 || lines[0] != ConfigMarker)
            {
                Plugin.Log.LogWarning("Benheim diagnostics config has an invalid format; Axiom delivery is unavailable.");
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
                !BuildIdMatchesLoadedPlugin(buildId))
            {
                Plugin.Log.LogWarning("Benheim diagnostics config is invalid; Axiom delivery is unavailable.");
                return false;
            }

            config = new AxiomIngestConfig(endpointUri, dataset, token, buildId);
            return true;
        }
        catch (Exception exception)
        {
            Plugin.Log.LogWarning(
                $"Benheim diagnostics config could not be read; Axiom delivery is unavailable ({exception.GetType().Name}).");
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

    private static bool BuildIdMatchesLoadedPlugin(string buildId)
    {
        if (!buildId.StartsWith("sha256:", StringComparison.Ordinal) || buildId.Length != 71)
        {
            return false;
        }

        using SHA256 sha256 = SHA256.Create();
        using FileStream plugin = File.OpenRead(typeof(Plugin).Assembly.Location);
        byte[] hash = sha256.ComputeHash(plugin);
        return string.Equals(
            buildId,
            "sha256:" + BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(),
            StringComparison.Ordinal);
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
        private DateTime? lastAcceptedUtc;
        private string? lastFailure;

        internal AxiomEventSink(AxiomIngestConfig config, string clientId)
        {
            this.config = config;
            this.clientId = clientId;
        }

        internal bool Enabled { get; private set; }

        internal string Status
        {
            get
            {
                lock (gate)
                {
                    if (!Enabled)
                    {
                        return "Axiom delivery stopped. Events remain in local logs.";
                    }
                    if (lastFailure != null)
                    {
                        return lastAcceptedUtc.HasValue
                            ? $"Axiom accepted events at {lastAcceptedUtc.Value:HH:mm:ss} UTC, but some delivery failed ({lastFailure}). Check local logs."
                            : $"Axiom delivery failed ({lastFailure}). Events remain in local logs.";
                    }
                    if (lastAcceptedUtc.HasValue)
                    {
                        return $"Axiom accepted events at {lastAcceptedUtc.Value:HH:mm:ss} UTC.";
                    }
                    return "Axiom configured; waiting for its first accepted event.";
                }
            }
        }

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
                RecordFailure("queue full");
                Plugin.Log.LogWarning(
                    "Benheim diagnostics queue is full; remote copies are being dropped while local diagnostics continue.");
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
                // Plugin teardown abandons queued remote copies without
                // touching local diagnostics or shutdown.
            }
            catch (Exception exception)
            {
                RecordFailure(exception.GetType().Name);
                lock (gate)
                {
                    Enabled = false;
                }
                Plugin.Log.LogWarning(
                    $"Benheim Axiom delivery stopped after {exception.GetType().Name}; local diagnostics continue.");
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
                        RecordFailure("oversized event");
                        Plugin.Log.LogWarning(
                            "Benheim Axiom delivery dropped an oversized event; local diagnostics continue.");
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
                    lock (gate)
                    {
                        lastAcceptedUtc = DateTime.UtcNow;
                    }
                    return true;
                }

                RecordFailure($"HTTP {(int)response.StatusCode}");
                Plugin.Log.LogWarning(
                    $"Benheim Axiom delivery dropped {batch.Count} remote events after HTTP {(int)response.StatusCode}; local diagnostics continue.");
                return false;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                RecordFailure(exception.GetType().Name);
                Plugin.Log.LogWarning(
                    $"Benheim Axiom delivery dropped {batch.Count} remote events after {exception.GetType().Name}; local diagnostics continue.");
                return false;
            }
        }

        private void RecordFailure(string reason)
        {
            lock (gate)
            {
                lastFailure = reason;
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
