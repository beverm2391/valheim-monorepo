using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// Makes one direct OpenRouter request for an already-approved approach.
/// The coroutine keeps Unity state access and the completion callback on the
/// main thread; callers own encounter relevance and cancel this operation when
/// their encounter ends.
/// </summary>
internal static class ResidentSpeech
{
    private const string Endpoint = "https://openrouter.ai/api/v1/chat/completions";
    private const string PromptResourceName = "BenheimQoL.GreydwarfResident.george.txt";
    private const int TimeoutMilliseconds = 8000;
    private const int MaximumResponseBytes = 65536;

    internal static IDisposable Begin(
        MonoBehaviour runner,
        string requestId,
        Player visitor,
        Smelter tub,
        string[] recentRemarks,
        Func<bool> stillRelevant,
        Action<bool, string, string> completed)
    {
        if (completed is null) throw new ArgumentNullException(nameof(completed));
        var operation = new RequestOperation(runner, requestId, visitor, tub,
            recentRemarks, stillRelevant, completed);
        operation.Start();
        return operation;
    }

    private sealed class RequestOperation : IDisposable
    {
        private readonly MonoBehaviour runner;
        private readonly string requestId;
        private readonly Player visitor;
        private readonly Smelter tub;
        private readonly string[] recentRemarks;
        private readonly Func<bool> stillRelevant;
        private readonly Action<bool, string, string> completed;
        private readonly Stopwatch triggerTimer = Stopwatch.StartNew();
        private Coroutine? coroutine;
        private UnityWebRequest? request;
        private Stopwatch? providerTimer;
        private bool finished;
        private bool timeoutRequested;

        internal RequestOperation(
            MonoBehaviour runner,
            string requestId,
            Player visitor,
            Smelter tub,
            string[] recentRemarks,
            Func<bool> stillRelevant,
            Action<bool, string, string> completed)
        {
            this.runner = runner;
            this.requestId = string.IsNullOrWhiteSpace(requestId)
                ? Guid.NewGuid().ToString("D")
                : requestId;
            this.visitor = visitor;
            this.tub = tub;
            this.recentRemarks = recentRemarks ?? Array.Empty<string>();
            this.stillRelevant = stillRelevant;
            this.completed = completed;
        }

        internal void Start()
        {
            ResidentSpeechTrace.Record(requestId, "trigger", new { trigger = "approach" });
            if (!runner)
            {
                FinishSilently("runner_unavailable");
                return;
            }

            try
            {
                coroutine = runner.StartCoroutine(Run());
            }
            catch
            {
                FinishSilently("request_start_failed");
            }
        }

        public void Dispose()
        {
            if (finished) return;
            try { request?.Abort(); }
            catch { }
            DisposeRequest();
            if (runner && coroutine is not null)
            {
                try { runner.StopCoroutine(coroutine); }
                catch { }
            }
            Finish(false, string.Empty, "cancelled", cancelled: true);
        }

        private System.Collections.IEnumerator Run()
        {
            if (!CheckRelevance(out string relevanceReason))
            {
                FinishSilently(relevanceReason);
                yield break;
            }

            ResidentSpeechContext context;
            string prompt;
            string body;
            try
            {
                context = CaptureContext(visitor, tub, recentRemarks);
                prompt = LoadPrompt();
                body = ResidentSpeechContract.BuildRequest(context, prompt).ToString(Formatting.None);
            }
            catch (ResidentSpeechContractException exception)
            {
                FinishSilently(exception.Reason);
                yield break;
            }
            catch (FileNotFoundException)
            {
                FinishSilently("prompt_resource_missing");
                yield break;
            }
            catch
            {
                FinishSilently("context_unavailable");
                yield break;
            }

            ResidentSpeechTrace.Record(requestId, "context", ResidentSpeechContract.ContextJson(context));
            if (!CheckRelevance(out relevanceReason))
            {
                FinishSilently(relevanceReason);
                yield break;
            }

            string? apiKey = ResidentSpeechSettings.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                FinishSilently("api_key_unavailable");
                yield break;
            }

            try
            {
                request = new UnityWebRequest(Endpoint, "POST")
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = 8
                };
                request.SetRequestHeader("Authorization", "Bearer " + apiKey);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-OpenRouter-Title", "Valheim Benheim George");
            }
            catch
            {
                DisposeRequest();
                FinishSilently("request_setup_failed");
                yield break;
            }

            if (!CheckRelevance(out relevanceReason))
            {
                DisposeRequest();
                FinishSilently(relevanceReason);
                yield break;
            }

            UnityWebRequestAsyncOperation? sendOperation;
            try
            {
                providerTimer = Stopwatch.StartNew();
                sendOperation = request.SendWebRequest();
            }
            catch
            {
                DisposeRequest();
                FinishSilently("request_start_failed");
                yield break;
            }

            // Keep exactly the prompt and serialized body sent above. The trace
            // intentionally omits headers and the API key.
            ResidentSpeechTrace.Record(requestId, "provider_request", new
            {
                model = ResidentSpeechContract.Model,
                prompt,
                body
            });

            if (sendOperation is null)
            {
                DisposeRequest();
                FinishSilently("request_start_failed");
                yield break;
            }

            while (!sendOperation.isDone)
            {
                if (!CheckRelevance(out relevanceReason))
                {
                    DisposeRequest(abort: true);
                    FinishSilently(relevanceReason);
                    yield break;
                }
                if (providerTimer!.ElapsedMilliseconds >= TimeoutMilliseconds)
                {
                    timeoutRequested = true;
                    request.Abort();
                    break;
                }
                yield return null;
            }

            long responseCode = request.responseCode;
            double durationMs = providerTimer?.Elapsed.TotalMilliseconds ?? 0d;
            ResidentSpeechProviderResponse? parsedResponse = null;
            string? failureReason = null;
            if (timeoutRequested)
                failureReason = "provider_timeout";
            else if (request.result != UnityWebRequest.Result.Success)
                failureReason = providerTimer!.ElapsedMilliseconds >= TimeoutMilliseconds - 100
                    ? "provider_timeout"
                    : request.result == UnityWebRequest.Result.ProtocolError && responseCode > 0
                    ? "provider_http_" + responseCode.ToString(CultureInfo.InvariantCulture)
                    : "provider_network_failure";
            else if (request.downloadHandler is DownloadHandlerBuffer buffer &&
                buffer.data is { Length: > MaximumResponseBytes })
                failureReason = "provider_response_too_large";
            else
            {
                try
                {
                    parsedResponse = ResidentSpeechContract.ParseProviderResponse(
                        request.downloadHandler.text, ResidentSpeechContract.Model);
                    failureReason = parsedResponse.FailureReason;
                }
                catch
                {
                    failureReason = "provider_response_json";
                }
            }

            DisposeRequest();
            if (parsedResponse is not null)
            {
                ResidentSpeechTrace.Record(requestId, "provider_completed", new
                {
                    durationMs,
                    model = parsedResponse.Model,
                    usage = parsedResponse.Usage,
                    content = parsedResponse.ContentExcerpt,
                    contentTruncated = parsedResponse.ContentTruncated,
                    parsedReply = parsedResponse.ParsedReply
                });
            }
            else
            {
                ResidentSpeechTrace.Record(requestId, "provider_failed", new
                {
                    durationMs,
                    reason = failureReason,
                    httpStatus = responseCode > 0 ? responseCode : (long?)null
                });
            }

            if (failureReason is not null || parsedResponse?.Remark is null)
            {
                FinishSilently(failureReason ?? "model_content_missing");
                yield break;
            }

            if (!CheckRelevance(out relevanceReason))
            {
                ResidentSpeechTrace.Record(requestId, "discarded", new { reason = relevanceReason, durationMs });
                FinishSilently(relevanceReason);
                yield break;
            }

            ResidentSpeechRemark remark = parsedResponse.Remark;
            if (!remark.Speak)
            {
                FinishSilently("model_silence");
                yield break;
            }

            Finish(true, remark.Text, "model_speech");
        }

        private bool CheckRelevance(out string reason)
        {
            try
            {
                if (stillRelevant is not null && stillRelevant())
                {
                    reason = string.Empty;
                    return true;
                }
                reason = "no_longer_relevant";
                return false;
            }
            catch
            {
                reason = "relevance_check_failed";
                return false;
            }
        }

        private void FinishSilently(string reason) => Finish(false, string.Empty, reason);

        private void Finish(bool speak, string text, string reason, bool cancelled = false)
        {
            if (finished) return;
            finished = true;
            if (cancelled)
            {
                ResidentSpeechTrace.Record(requestId, "request_cancelled", new
                {
                    reason = "cancelled",
                    durationMs = triggerTimer.Elapsed.TotalMilliseconds
                });
                speak = false;
                text = string.Empty;
                reason = "cancelled";
            }
            string safeText = speak ? text : string.Empty;
            ResidentSpeechTrace.Record(requestId, "final_outcome", new
            {
                speak,
                text = safeText,
                reason,
                durationMs = triggerTimer.Elapsed.TotalMilliseconds
            });

            // Begin and every coroutine continuation run on Unity's main thread.
            try
            {
                completed(speak, safeText, reason);
            }
            catch
            {
                ResidentSpeechTrace.Record(requestId, "callback_failed", new { reason = "callback_failed" });
                ResidentDiagnostics.Emit("speech_callback_failed", "callback_failed");
            }
        }

        private void DisposeRequest(bool abort = false)
        {
            UnityWebRequest? active = request;
            request = null;
            if (active is null) return;
            if (abort)
            {
                try { active.Abort(); }
                catch { }
            }
            try { active.Dispose(); }
            catch { }
        }
    }

    private static ResidentSpeechContext CaptureContext(Player visitor, Smelter tub, string[] recentRemarks)
    {
        if (!visitor) throw new ResidentSpeechContractException("visitor_unavailable");
        if (!tub) throw new ResidentSpeechContractException("tub_unavailable");
        if (!EnvMan.instance) throw new ResidentSpeechContractException("environment_unavailable");

        var environment = EnvMan.instance.GetCurrentEnvironment();
        SEMan effects = visitor.GetSEMan();
        float dayFraction = EnvMan.instance.GetDayFraction();
        string dayPart = EnvMan.IsNight() ? "night" :
            dayFraction < .4f ? "morning" : dayFraction > .6f ? "evening" : "day";
        string weather = environment != null ? environment.m_name : "unknown";
        string biome = visitor.GetCurrentBiome().ToString();
        string[] recent = recentRemarks.Length <= 3
            ? recentRemarks.ToArray()
            : recentRemarks.Skip(recentRemarks.Length - 3).ToArray();

        // Use the current addressee's displayed character name, preserving
        // Valheim's name filtering before George can repeat it in shared speech.
        return ResidentSpeechContract.CreateContext(visitor.GetHoverName(), dayPart, weather, biome,
            effects.HaveStatusEffect(SEMan.s_statusEffectWet),
            effects.HaveStatusEffect(SEMan.s_statusEffectCold),
            tub.IsActive(), visitor.IsSitting(), recent);
    }

    private static string LoadPrompt()
    {
        using Stream? stream = typeof(ResidentSpeech).Assembly.GetManifestResourceStream(PromptResourceName);
        if (stream is null) throw new FileNotFoundException("Embedded George prompt was not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
