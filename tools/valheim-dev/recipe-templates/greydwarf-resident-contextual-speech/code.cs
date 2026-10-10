using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class ValheimDevChange
{
    static GeorgeContextualSpeech behaviour;
    [Serializable] public sealed class Input { public int port = 18741; }
    public static string Run(string json)
    {
        var input = JsonUtility.FromJson<Input>(json) ?? new Input();
        if (input.port < 1024 || input.port > 65535) throw new Exception("Invalid loopback port.");
        var resident = GameObject.Find("Lab_GreydwarfResident");
        if (!resident || !Chat.instance) throw new Exception("Resident and native Chat must exist.");
        behaviour = resident.AddComponent<GeorgeContextualSpeech>();
        try { behaviour.Configure(input.port); }
        catch { Cleanup(); throw; }
        return "{\"trigger\":\"existing approach with sightline\",\"display\":\"native NPC dialogue\",\"contextual\":true}";
    }
    public static void Cleanup()
    {
        if (behaviour) UnityEngine.Object.DestroyImmediate(behaviour);
        behaviour = null;
    }
}

public sealed class GeorgeContextualSpeech : MonoBehaviour
{
    [Serializable] public sealed class Context
    {
        public string @event = "approach", dayPart, weather, biome;
        public bool wet, cold, tubBurning, playerSeated;
        public string[] recentRemarks;
    }
    [Serializable] public sealed class Request { public string requestId; public Context context; }
    [Serializable] public sealed class Reply
    {
        public string requestId, reason, model, text;
        public bool speak;
        public float durationMs;
        public int tokens = -1;
    }
    [Serializable] sealed class Event
    {
        public string sessionId, requestId, phase, reason;
        public int requests, spoken, discarded, silent;
    }
    [Serializable] sealed class LocalTrace
    {
        public string utc, source = "game", sessionId, requestId, phase, reason;
        public string contextJson, replyJson;
        public float elapsedMs;
        public long httpStatus;
    }
    Transform head;
    Player pending;
    UnityWebRequest request;
    string endpoint;
    int viewMask;
    float expires, nextCheck, nextAllowed;
    string sessionId, currentRequestId;
    float triggerStarted, requestStarted;
    readonly Queue<string> recent = new Queue<string>();
    public int approachCount, requestCount, speechCount, discardedCount, silenceCount;
    public string state = "idle", lastText = "", lastContextJson = "";
    public string traceFile;
    public int traceFailures;

    public void Configure(int port)
    {
        endpoint = "http://127.0.0.1:" + port + "/remark";
        sessionId = Guid.NewGuid().ToString();
        try
        {
            var directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "ValheimDev", "contextual-speech-traces");
            Directory.CreateDirectory(directory);
            traceFile = Path.Combine(directory, "game-" + sessionId + ".jsonl");
            using (var file = new FileStream(traceFile, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { }
        }
        catch { traceFile = null; TraceFailed(); }
        try
        {
            head = GetComponentsInChildren<Transform>(true).First(t => t.name == "head");
            viewMask = LayerMask.GetMask("Default", "static_solid", "Default_small",
                "piece", "terrain", "vehicle", "viewblock");
        }
        catch { Record(null, "session_failed", "receiver_setup_failed"); throw; }
        Record(null, "session_started", "receiver_attached");
    }

    public void OnResidentApproach(Player player)
    {
        string id = Guid.NewGuid().ToString();
        Record(id, "trigger", "approach");
        string suppressed = !player ? "player_missing" : player != Player.m_localPlayer ? "nonlocal_player" :
            currentRequestId != null ? "encounter_pending" : Time.unscaledTime < nextAllowed ? "speech_cooldown" : null;
        if (suppressed != null) { Record(id, "suppressed", suppressed); return; }
        currentRequestId = id;
        triggerStarted = Time.unscaledTime;
        pending = player;
        expires = Time.unscaledTime + 12f;
        nextCheck = 0f;
        approachCount++;
        state = "waiting for sightline";
        Record(id, "waiting_for_sightline", "approach_accepted");
    }

    string IrrelevantReason()
    {
        if (!pending) return "player_missing";
        if (pending != Player.m_localPlayer) return "player_replaced";
        if (pending.IsDead()) return "player_dead";
        if (pending.IsTeleporting()) return "player_teleporting";
        if (!Chat.instance) return "chat_unavailable";
        if (Time.unscaledTime >= expires) return state == "requesting" ? "encounter_expired" : "sightline_timeout";
        var delta = pending.transform.position - transform.position;
        return new Vector2(delta.x, delta.z).magnitude > 6f || Mathf.Abs(delta.y) > 3f ? "visitor_left" : null;
    }
    bool Visible() => !Physics.Linecast(pending.GetHeadPoint(), head.position,
        viewMask, QueryTriggerInteraction.Ignore);

    public Context Snapshot(Player player)
    {
        var environment = EnvMan.instance.GetCurrentEnvironment();
        // The native bathtub is a fuel-only Smelter, not a Fireplace. Query
        // its activation path, including fuel, roof and smoke conditions.
        var tub = GetComponentInParent<Smelter>();
        if (!tub) throw new Exception("Native bathtub Smelter is missing.");
        var effects = player.GetSEMan();
        float dayFraction = EnvMan.instance.GetDayFraction();
        return new Context {
            // Native night bounds are .25/.75. Split daylight coarsely so the
            // prompt sees useful periods without claiming an exact clock time.
            dayPart = EnvMan.IsNight() ? "night" : dayFraction < .4f ? "morning" :
                dayFraction > .6f ? "evening" : "day",
            weather = environment != null ? environment.m_name : "unknown",
            biome = player.GetCurrentBiome().ToString(),
            wet = effects.HaveStatusEffect(SEMan.s_statusEffectWet),
            cold = effects.HaveStatusEffect(SEMan.s_statusEffectCold),
            tubBurning = tub.IsActive(), playerSeated = player.IsSitting(),
            recentRemarks = recent.ToArray()
        };
    }

    void Update()
    {
        if (currentRequestId == null || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        // Invalidate promptly even during HTTP: leaving and returning cannot revive
        // an old remark. Movement, animation and look components never wait on this.
        string reason = IrrelevantReason();
        if (reason == null && request != null && !Visible()) reason = "sightline_lost";
        if (reason != null)
        {
            discardedCount++; Cancel(reason); return;
        }
        if (request == null && Visible()) StartCoroutine(FetchRemark());
    }

    IEnumerator FetchRemark()
    {
        nextAllowed = Time.unscaledTime + 45f;
        state = "requesting";
        requestCount++;
        UnityWebRequestAsyncOperation operation = null;
        string setupPhase = "context_unavailable";
        try
        {
            var context = Snapshot(pending);
            lastContextJson = JsonUtility.ToJson(context);
            Record(currentRequestId, "context", "snapshot", lastContextJson);
            setupPhase = "request_setup_failed";
            request = new UnityWebRequest(endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Request {
                requestId = currentRequestId, context = context })));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-George-Request-Id", currentRequestId);
            request.timeout = 9;
            requestStarted = Time.unscaledTime;
            operation = request.SendWebRequest();
            Record(currentRequestId, "request_sent", "loopback");
        }
        catch
        {
            // Scene teardown or a failed request setup must also fail to silence.
            if (request != null) { request.Dispose(); request = null; }
            silenceCount++; Record(currentRequestId, "silence", setupPhase);
            Finish();
        }
        if (operation == null) yield break;
        yield return operation;
        Reply reply = null;
        string failure = null;
        long httpStatus = request.responseCode;
        if (request.result != UnityWebRequest.Result.Success)
            failure = request.result == UnityWebRequest.Result.ProtocolError ? "loopback_http_error" :
                Time.unscaledTime - requestStarted >= 9f ? "loopback_timeout" : "loopback_network_failure";
        else if (request.downloadHandler.text.Length > 1024) failure = "reply_too_large";
        else
        {
            try { reply = JsonUtility.FromJson<Reply>(request.downloadHandler.text); }
            catch { failure = "reply_json"; }
            if (reply == null) failure = "reply_json";
            else if (reply.requestId != currentRequestId) failure = "reply_id_mismatch";
            else if (!SafeReason(reply.reason) || string.IsNullOrEmpty(reply.model) || reply.model.Length > 128 ||
                reply.model.Any(c => !(char.IsLetterOrDigit(c) || "_./:-".Contains(c)))) failure = "reply_metadata_invalid";
            else if (reply.speak ? !ValidText(reply.text) : reply.text != "") failure = "reply_text_invalid";
        }
        request.Dispose(); request = null;
        if (failure == null) Record(currentRequestId, "reply_received", reply.reason,
            replyJson: JsonUtility.ToJson(reply), httpStatus: httpStatus);
        string irrelevant = IrrelevantReason();
        if (irrelevant == null && !Visible()) irrelevant = "sightline_lost";
        if (irrelevant != null) { discardedCount++; Record(currentRequestId, "discarded", irrelevant); }
        else if (failure != null) { silenceCount++; Record(currentRequestId, "silence", failure, httpStatus: httpStatus); }
        else if (reply.speak)
        {
            try
            {
                lastText = reply.text.Trim();
                Chat.instance.SetNpcText(gameObject, head.position - transform.position +
                    Vector3.up * .35f, 20f, 10f, "George", lastText, false);
                recent.Enqueue(lastText);
                while (recent.Count > 3) recent.Dequeue();
                speechCount++; Record(currentRequestId, "display_submitted", "native_chat_submitted");
            }
            catch { silenceCount++; Record(currentRequestId, "silence", "display_failed"); }
        }
        else { silenceCount++; Record(currentRequestId, "silence", reply.reason); }
        Finish();
    }

    static bool ValidText(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 140 &&
        !text.Any(c => c == '<' || c == '>' || char.IsControl(c));
    static bool SafeReason(string reason) => reason != null && (new[] { "model_speech", "model_silence",
        "bridge_busy", "call_limit", "provider_timeout", "client_disconnected", "invalid_context",
        "invalid_remark", "invalid_request_id", "request_json", "request_too_large", "provider_response_json",
        "model_content_missing", "model_reply_json", "provider_network_failure" }.Contains(reason) ||
        reason.StartsWith("provider_http_") && reason.Length == 17 && reason.Substring(14).All(char.IsDigit));
    void TraceFailed()
    {
        traceFailures++;
        if (traceFailures == 1) Debug.LogWarning("[GeorgeContextualSpeech] trace_write_failed");
    }
    void Record(string id, string phase, string reason, string contextJson = null,
        string replyJson = null, long httpStatus = 0)
    {
        Debug.Log("[GeorgeContextualSpeech] " + JsonUtility.ToJson(new Event {
            sessionId = sessionId, requestId = id, phase = phase, reason = reason,
            requests = requestCount, spoken = speechCount, discarded = discardedCount, silent = silenceCount }));
        try
        {
            if (string.IsNullOrEmpty(traceFile)) return;
            File.AppendAllText(traceFile, JsonUtility.ToJson(new LocalTrace {
                utc = DateTime.UtcNow.ToString("O"), sessionId = sessionId, requestId = id,
                phase = phase, reason = reason, contextJson = contextJson, replyJson = replyJson,
                httpStatus = httpStatus, elapsedMs = id == currentRequestId ?
                    (Time.unscaledTime - triggerStarted) * 1000f : 0f }) + "\n");
        }
        catch { TraceFailed(); }
    }
    void Finish() { pending = null; currentRequestId = null; state = "idle"; }
    void Cancel(string reason)
    {
        if (currentRequestId != null) Record(currentRequestId, "discarded", reason);
        StopAllCoroutines();
        if (request != null) { request.Abort(); request.Dispose(); request = null; }
        Finish();
    }
    void OnDisable()
    {
        Cancel("receiver_disabled");
        Record(null, "session_ended", "receiver_disabled");
        if (Chat.instance) Chat.instance.ClearNpcText(gameObject);
    }
}
