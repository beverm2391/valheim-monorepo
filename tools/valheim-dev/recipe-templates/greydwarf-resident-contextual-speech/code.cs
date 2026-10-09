using System;
using System.Collections;
using System.Collections.Generic;
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
        behaviour.Configure(input.port);
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
    [Serializable] public sealed class Remark { public bool speak; public string text; }
    [Serializable] sealed class Event
    {
        public string action;
        public int requests, spoken, discarded, silent;
    }
    Transform head;
    Player pending;
    UnityWebRequest request;
    string endpoint;
    int viewMask;
    float expires, nextCheck, nextAllowed;
    readonly Queue<string> recent = new Queue<string>();
    public int approachCount, requestCount, speechCount, discardedCount, silenceCount;
    public string state = "idle", lastText = "", lastContextJson = "";

    public void Configure(int port)
    {
        endpoint = "http://127.0.0.1:" + port + "/remark";
        head = GetComponentsInChildren<Transform>(true).First(t => t.name == "head");
        viewMask = LayerMask.GetMask("Default", "static_solid", "Default_small",
            "piece", "terrain", "vehicle", "viewblock");
    }

    public void OnResidentApproach(Player player)
    {
        if (!player || player != Player.m_localPlayer || pending ||
            Time.unscaledTime < nextAllowed) return;
        pending = player;
        expires = Time.unscaledTime + 12f;
        nextCheck = 0f;
        approachCount++;
        state = "waiting for sightline";
    }

    bool Relevant()
    {
        if (!pending || pending != Player.m_localPlayer || pending.IsDead() ||
            pending.IsTeleporting() || !Chat.instance || Time.unscaledTime >= expires) return false;
        var delta = pending.transform.position - transform.position;
        return new Vector2(delta.x, delta.z).magnitude <= 6f && Mathf.Abs(delta.y) <= 3f;
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
        if (!pending || Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + .2f;
        // Invalidate promptly even during HTTP: leaving and returning cannot revive
        // an old remark. Movement, animation and look components never wait on this.
        if (!Relevant() || (request != null && !Visible()))
        {
            Cancel(); discardedCount++; Record("discard"); return;
        }
        if (request == null && Visible()) StartCoroutine(FetchRemark());
    }

    IEnumerator FetchRemark()
    {
        nextAllowed = Time.unscaledTime + 45f;
        state = "requesting";
        requestCount++;
        UnityWebRequestAsyncOperation operation = null;
        try
        {
            lastContextJson = JsonUtility.ToJson(Snapshot(pending));
            request = new UnityWebRequest(endpoint, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(lastContextJson));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 9;
            operation = request.SendWebRequest();
        }
        catch
        {
            // Scene teardown or a failed request setup must also fail to silence.
            if (request != null) { request.Dispose(); request = null; }
            pending = null; state = "idle";
            silenceCount++; Record("silence");
        }
        if (operation == null) yield break;
        yield return operation;
        Remark remark = null;
        if (request.result == UnityWebRequest.Result.Success && request.downloadHandler.text.Length <= 1024)
        {
            try { remark = JsonUtility.FromJson<Remark>(request.downloadHandler.text); }
            catch { /* A malformed response is silence, never a UI exception. */ }
        }
        request.Dispose(); request = null;
        if (!Relevant() || !Visible()) { discardedCount++; Record("discard"); }
        else if (remark != null && remark.speak && ValidText(remark.text))
        {
            lastText = remark.text.Trim();
            Chat.instance.SetNpcText(gameObject, head.position - transform.position +
                Vector3.up * .35f, 20f, 10f, "George", lastText, false);
            recent.Enqueue(lastText);
            while (recent.Count > 3) recent.Dequeue();
            speechCount++; Record("speech");
        }
        else { silenceCount++; Record("silence"); }
        pending = null; state = "idle";
    }

    static bool ValidText(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 140 &&
        !text.Any(c => c == '<' || c == '>' || char.IsControl(c));
    void Record(string action) => Debug.Log("[GeorgeContextualSpeech] " + JsonUtility.ToJson(new Event {
        action = action, requests = requestCount, spoken = speechCount,
        discarded = discardedCount, silent = silenceCount }));
    void Cancel()
    {
        StopAllCoroutines();
        if (request != null) { request.Abort(); request.Dispose(); request = null; }
        pending = null; state = "idle";
    }
    void OnDisable()
    {
        Cancel();
        if (Chat.instance) Chat.instance.ClearNpcText(gameObject);
    }
}
