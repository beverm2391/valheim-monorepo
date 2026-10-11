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
        return "{\"trigger\":\"approach with sightline or native E Talk\",\"display\":\"native NPC dialogue\",\"contextual\":true}";
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
        public string @event = "approach", visitorName, dayPart, weather, biome;
        public bool wet, cold, tubBurning, playerSeated;
        public string[] recentRemarks;
    }
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
    GameObject talkTarget;
    Player pending;
    UnityWebRequest request;
    string endpoint;
    int viewMask;
    float expires, nextCheck, nextAllowed, nextTalkAllowed;
    string sessionId, currentRequestId;
    float triggerStarted, requestStarted;
    bool preview, talk;
    public bool IsBusy => currentRequestId != null;
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
            AttachTalkTarget();
        }
        catch { Record(null, "session_failed", "receiver_setup_failed"); throw; }
        Record(null, "session_started", "receiver_attached");
    }

    void AttachTalkTarget()
    {
        // Reuse the durable ResidentInteraction's native head-following trigger.
        // The imported head's scale must not change the target's world radius.
        int layer = LayerMask.NameToLayer("character");
        if (layer < 0) throw new Exception("Native character layer is unavailable.");
        talkTarget = new GameObject("Lab_GeorgeTalkTarget") { layer = layer };
        talkTarget.transform.SetParent(transform, false);
        var collider = talkTarget.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = .22f;
        talkTarget.AddComponent<GeorgeTalkInteraction>().Configure(this, head);
        Record(null, Physics.queriesHitTriggers ? "interaction_available" : "interaction_unavailable",
            Physics.queriesHitTriggers ? "native_hover_attached" : "trigger_queries_disabled");
    }

    public void OnResidentApproach(Player player)
    {
        Begin(player, false);
    }

    // Lab previews exercise the same snapshot, transport and native display.
    // They skip encounter cadence/range only, so prompt iteration needs no
    // artificial walk-away loop. Death, teleport, expiry and teardown still cancel.
    public void OnLabDialogueTest(Player player)
    {
        Begin(player, true);
    }

    public bool Talk(Player player) => Begin(player, false, true);

    bool Begin(Player player, bool isPreview, bool isTalk = false)
    {
        string id = Guid.NewGuid().ToString();
        string trigger = isPreview ? "lab_dialogue_test" : isTalk ? "talk" : "approach";
        Record(id, "trigger", trigger);
        string suppressed = !player ? "player_missing" : player != Player.m_localPlayer ? "nonlocal_player" :
            currentRequestId != null ? "encounter_pending" :
            !isPreview && Time.unscaledTime < (isTalk ? nextTalkAllowed : nextAllowed) ? "speech_cooldown" : null;
        if (suppressed == null && isTalk)
        {
            var delta = player.transform.position - transform.position;
            if (new Vector2(delta.x, delta.z).magnitude > 6f || Mathf.Abs(delta.y) > 3f)
                suppressed = "visitor_left";
            else if (Physics.Linecast(player.GetHeadPoint(), head.position, viewMask, QueryTriggerInteraction.Ignore))
                suppressed = "sightline_lost";
        }
        if (suppressed != null) { Record(id, "suppressed", suppressed); return false; }
        currentRequestId = id;
        triggerStarted = Time.unscaledTime;
        pending = player;
        preview = isPreview;
        talk = isTalk;
        expires = Time.unscaledTime + 12f;
        nextCheck = 0f;
        approachCount++;
        state = isPreview ? "preview ready" : "waiting for sightline";
        Record(id, "accepted", trigger);
        if (isTalk) nextTalkAllowed = Time.unscaledTime + 2f;
        return true;
    }

    string IrrelevantReason()
    {
        if (!pending) return "player_missing";
        if (pending != Player.m_localPlayer) return "player_replaced";
        if (pending.IsDead()) return "player_dead";
        if (pending.IsTeleporting()) return "player_teleporting";
        if (!Chat.instance) return "chat_unavailable";
        if (Time.unscaledTime >= expires) return state == "requesting" ? "encounter_expired" : "sightline_timeout";
        if (preview) return null;
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
            @event = preview || talk ? "talk" : "approach",
            visitorName = SafeVisitorName(player.GetHoverName()),
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
        if (reason == null && !preview && request != null && !Visible()) reason = "sightline_lost";
        if (reason != null)
        {
            discardedCount++; Cancel(reason); return;
        }
        if (request == null && (preview || Visible())) StartCoroutine(FetchRemark());
    }

    IEnumerator FetchRemark()
    {
        if (!preview && !talk) nextAllowed = Time.unscaledTime + 45f;
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
            // JsonUtility omits nested custom context in this hot-loaded recipe.
            // The UUID is generated here; the context is already serialized JSON.
            string envelope = "{\"requestId\":\"" + currentRequestId + "\",\"context\":" + lastContextJson + "}";
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(envelope));
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
        if (irrelevant == null && !preview && !Visible()) irrelevant = "sightline_lost";
        if (irrelevant != null) { discardedCount++; Record(currentRequestId, "discarded", irrelevant); }
        else if (failure != null) { silenceCount++; Record(currentRequestId, "silence", failure, httpStatus: httpStatus); }
        else if (reply.speak)
        {
            bool submitted = false;
            try
            {
                lastText = reply.text.Trim();
                Chat.instance.SetNpcText(gameObject, head.position - transform.position +
                    Vector3.up * .35f, 20f, Mathf.Clamp(6f + lastText.Length / 20f, 10f, 26f), "George", lastText, false);
                recent.Enqueue(lastText);
                while (recent.Count > 3) recent.Dequeue();
                speechCount++; Record(currentRequestId, "display_submitted", "native_chat_submitted");
                submitted = true;
            }
            catch { silenceCount++; Record(currentRequestId, "silence", "display_failed"); }
            // Optional awareness owns the addressee only after native display.
            // An observer failure cannot turn successful speech into silence.
            if (submitted)
            {
                try { gameObject.SendMessage("OnResidentSpeechShown", pending,
                    SendMessageOptions.DontRequireReceiver); }
                catch { Record(currentRequestId, "observer_failed", "speech_look_callback_failed"); }
            }
        }
        else { silenceCount++; Record(currentRequestId, "silence", reply.reason); }
        Finish();
    }

    static string SafeVisitorName(string name)
    {
        var filtered = new string((name ?? "").Where(c => c != '<' && c != '>' && !char.IsControl(c)).Take(64).ToArray()).Trim();
        return filtered.Length == 0 ? "visitor" : filtered;
    }
    static bool ValidText(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 400 &&
        !text.Any(c => c == '<' || c == '>' || char.IsControl(c));
    static bool SafeReason(string reason) => reason != null && (new[] { "model_speech", "model_silence",
        "bridge_busy", "call_limit", "provider_timeout", "client_disconnected", "invalid_context",
        "invalid_remark", "invalid_request_id", "invalid_request_envelope", "request_json", "request_too_large", "provider_response_json",
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
                httpStatus = httpStatus, elapsedMs = id != null && id == currentRequestId ?
                    (Time.unscaledTime - triggerStarted) * 1000f : 0f }) + "\n");
        }
        catch { TraceFailed(); }
    }
    void Finish() { pending = null; currentRequestId = null; preview = false; talk = false; state = "idle"; }
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
        if (talkTarget) UnityEngine.Object.DestroyImmediate(talkTarget);
        talkTarget = null;
        if (Chat.instance) Chat.instance.ClearNpcText(gameObject);
    }
}

public sealed class GeorgeTalkInteraction : MonoBehaviour, Hoverable, Interactable
{
    GeorgeContextualSpeech speech;
    Transform head;

    public void Configure(GeorgeContextualSpeech receiver, Transform nativeHead)
    {
        speech = receiver;
        head = nativeHead;
        FollowHead();
    }

    void LateUpdate() { if (speech && head) FollowHead(); }

    void FollowHead()
    {
        transform.position = head.position;
        transform.rotation = Quaternion.identity;
        var scale = speech.transform.lossyScale;
        if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f) return;
        transform.localScale = new Vector3(1f / Mathf.Abs(scale.x), 1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
    }

    public string GetHoverName() => "George";
    public float GetHoverOffset() => .25f;
    public string GetHoverText()
    {
        if (!speech || !speech.isActiveAndEnabled) return "";
        if (speech.IsBusy) return "George\nThinking…";
        const string prompt = "[<color=yellow><b>$KEY_Use</b></color>] Talk";
        return "George\n" + (Localization.instance != null ? Localization.instance.Localize(prompt) : "[Use] Talk");
    }

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        // Native held-Use callbacks and repeated presses cannot queue requests.
        var player = user as Player;
        return !hold && speech && speech.isActiveAndEnabled && player &&
            player == Player.m_localPlayer && speech.Talk(player);
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
}
