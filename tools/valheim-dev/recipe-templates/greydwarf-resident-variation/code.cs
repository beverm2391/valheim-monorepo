using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class ValheimDevChange
{
    static GreydwarfLoungeVariation behaviour;
    public static string Run(string json)
    {
        var resident = GameObject.Find("Lab_GreydwarfResident");
        if (!resident) throw new Exception("Spawn the greydwarf resident first.");
        behaviour = resident.AddComponent<GreydwarfLoungeVariation>();
        behaviour.Configure();
        return "{\"nativeLoop\":\"George Vibing\",\"awareness\":true,\"placementChanged\":false}";
    }
    public static void Cleanup()
    {
        if (behaviour) UnityEngine.Object.DestroyImmediate(behaviour);
        behaviour = null;
    }
}

public class GreydwarfLoungeVariation : MonoBehaviour
{
    Animator[] animators;
    float[] originalSpeeds;
    Transform head;
    Vector3 originalPosition;
    Quaternion originalRotation;
    Quaternion baseHead, appliedHead;
    bool headOffsetApplied, returningToNeutral;
    float nextPace, paceTarget = .78f, paceVelocity;
    float lookStarted, lookDuration, nextLook;
    Vector2 lookFrom, lookTarget, look;
    sealed class Presence
    {
        public bool visiting, seated, acknowledged;
        public float awaySince = -1, lastSeen;
    }
    [Serializable] sealed class ReactionEvent
    {
        public string action;
        public float time, yaw, pitch;
    }
    readonly Dictionary<int, Presence> visitors = new Dictionary<int, Presence>();
    float nextScan, reactionStarted, nextNotice;
    // Reactions share the same bone writer as idle glances, avoiding competing
    // components. A visitor is remembered until they leave the wider radius for
    // eight seconds; crossing the near boundary or sitting repeatedly cannot spam.
    public string state = "idle", lastReaction = "none";
    public int noticeCount, acknowledgementCount;
    public Vector3 reactionTarget;
    // Public observations let the Lab inspect real updates without per-frame logs.
    public int paceChanges, lookChanges, frames;
    public float pace = 1, minPace = 1, maxPace = 1, minYaw, maxYaw, placementError;

    public void Configure()
    {
        animators = GetComponentsInChildren<Animator>(true)
            .Where(a => a.runtimeAnimatorController).ToArray();
        if (animators.Length == 0) throw new Exception("Resident Animator missing.");
        head = GetComponentsInChildren<Transform>(true).First(t => t.name == "head");
        originalSpeeds = animators.Select(a => a.speed).ToArray();
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
        nextPace = Time.time + UnityEngine.Random.Range(6f, 10f);
        ChooseLook();
        ObservePlayers(true);
    }

    bool SittingBeside(Player player)
    {
        var point = player.GetAttachPoint();
        return player.IsAttached() && point && point.IsChildOf(transform.parent)
            && point.GetComponentInParent<Chair>();
    }

    void ObservePlayers(bool seed)
    {
        Player candidate = null;
        int priority = 0;
        float candidateDistance = float.MaxValue;
        foreach (var player in Player.GetAllPlayers())
        {
            if (!player || player.IsDead()) continue;
            int id = player.GetInstanceID();
            Presence presence;
            if (!visitors.TryGetValue(id, out presence))
                visitors[id] = presence = new Presence();
            presence.lastSeen = Time.time;
            var delta = player.transform.position - transform.position;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            bool outside = distance > 6f || Mathf.Abs(delta.y) > 3f;
            bool seated = SittingBeside(player);
            if (outside)
            {
                if (presence.awaySince < 0) presence.awaySince = Time.time;
                if (Time.time - presence.awaySince >= 8f)
                {
                    presence.visiting = false;
                    presence.acknowledged = false;
                }
                presence.seated = seated;
                continue;
            }
            presence.awaySince = -1;
            int eventPriority = 0;
            if (distance <= 4.5f && !presence.visiting)
            {
                presence.visiting = true;
                if (!seed && Time.time >= nextNotice && state == "idle")
                    eventPriority = 1;
            }
            if (seated && !presence.seated && !presence.acknowledged)
            {
                presence.acknowledged = true;
                if (!seed) eventPriority = 2;
            }
            presence.seated = seated;
            // Installing while someone is already soaking should not greet them.
            if (seed && seated) presence.acknowledged = true;
            if (eventPriority > priority ||
                (eventPriority > 0 && eventPriority == priority && distance < candidateDistance))
            {
                candidate = player;
                priority = eventPriority;
                candidateDistance = distance;
            }
        }
        foreach (int id in visitors.Where(v => Time.time - v.Value.lastSeen > 60f)
            .Select(v => v.Key).ToArray()) visitors.Remove(id);
        if (candidate) React(candidate, priority == 2);
    }

    void BeginLook(Vector2 target, float duration)
    {
        lookFrom = look;
        lookTarget = target;
        lookStarted = Time.time;
        lookDuration = duration;
    }

    void React(Player player, bool seated)
    {
        // Snapshot the visitor once. This is a brief acknowledgement, not tracking.
        reactionTarget = player.GetHeadPoint();
        var delta = reactionTarget - head.position;
        var flat = Vector3.ProjectOnPlane(delta, transform.up);
        float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, transform.up), -55f, 55f);
        float pitch = Mathf.Clamp(-Mathf.Atan2(delta.y, flat.magnitude) * Mathf.Rad2Deg, -10f, 10f);
        BeginLook(new Vector2(yaw, pitch), .9f);
        reactionStarted = Time.time;
        state = seated ? "acknowledge" : "notice";
        lastReaction = state;
        if (seated) acknowledgementCount++; else {
            noticeCount++;
            // Optional speech receiver shares the existing visit edge and cooldown.
            gameObject.SendMessage("OnResidentApproach", player,
                SendMessageOptions.DontRequireReceiver);
        }
        nextNotice = Time.time + 12f;
        Debug.Log("[GreydwarfResident] " + JsonUtility.ToJson(new ReactionEvent {
            action = state, time = Time.time, yaw = yaw, pitch = pitch }));
    }

    void ChooseLook()
    {
        lookFrom = look;
        // Alternate quiet rests and glances. Holds and transitions have independent
        // timings, so they do not land on the same point of the native loop.
        lookTarget = returningToNeutral ? Vector2.zero :
            new Vector2(UnityEngine.Random.Range(9f, 16f) *
                (UnityEngine.Random.value < .5f ? -1f : 1f),
                UnityEngine.Random.Range(-3f, 4f));
        returningToNeutral = !returningToNeutral;
        lookStarted = Time.time;
        lookDuration = UnityEngine.Random.Range(2f, 3.6f);
        nextLook = lookStarted + lookDuration + UnityEngine.Random.Range(3f, 6f);
        lookChanges++;
    }

    void UndoHeadOffset()
    {
        // Remove only our previous contribution before Animator evaluates. If the
        // Animator has already overwritten the bone, leave its fresh pose alone.
        // This also prevents accumulating twists when animation is culled.
        if (headOffsetApplied && head &&
            Quaternion.Angle(head.localRotation, appliedHead) < .01f)
            head.localRotation = baseHead;
        headOffsetApplied = false;
    }

    void Update()
    {
        UndoHeadOffset();
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .2f;
            ObservePlayers(false);
        }
        if (Time.time >= nextPace)
        {
            paceTarget = UnityEngine.Random.Range(.65f, 1.05f);
            nextPace = Time.time + UnityEngine.Random.Range(6f, 12f);
            paceChanges++;
        }
        pace = Mathf.SmoothDamp(pace, paceTarget, ref paceVelocity, 2f);
        for (int n = 0; n < animators.Length; n++)
            if (animators[n]) animators[n].speed = originalSpeeds[n] * pace;
        if ((state == "notice" || state == "acknowledge") &&
            Time.time - reactionStarted >= 2.6f)
        {
            BeginLook(Vector2.zero, 1.6f);
            state = "settling";
        }
        else if (state == "settling" && Time.time - lookStarted >= lookDuration)
        {
            state = "idle";
            returningToNeutral = false;
            nextLook = Time.time + UnityEngine.Random.Range(3f, 6f);
        }
        else if (state == "idle" && Time.time >= nextLook) ChooseLook();
        float t = Mathf.Clamp01((Time.time - lookStarted) / lookDuration);
        look = Vector2.Lerp(lookFrom, lookTarget, t * t * (3f - 2f * t));
    }

    void LateUpdate()
    {
        if (!head) return;
        baseHead = head.localRotation;
        // Use the resident's upright/right axes, not the imported bone's axes.
        // Only the head changes: hips, legs and the accepted seat transform stay native.
        // One small nod while acknowledging a seat, eased back to the same pose.
        float nod = state == "acknowledge" ? 6f * Mathf.Sin(Mathf.PI *
            Mathf.Clamp01((Time.time - reactionStarted - .9f) / 1f)) : 0f;
        head.rotation = Quaternion.AngleAxis(look.x, transform.up) *
            Quaternion.AngleAxis(look.y + nod, transform.right) * head.rotation;
        appliedHead = head.localRotation;
        headOffsetApplied = true;
        frames++;
        minPace = Mathf.Min(minPace, pace); maxPace = Mathf.Max(maxPace, pace);
        minYaw = Mathf.Min(minYaw, look.x); maxYaw = Mathf.Max(maxYaw, look.x);
        placementError = Vector3.Distance(transform.localPosition, originalPosition) +
            Quaternion.Angle(transform.localRotation, originalRotation);
    }

    void OnDisable()
    {
        UndoHeadOffset();
        if (animators != null)
            for (int n = 0; n < animators.Length; n++)
                if (animators[n]) animators[n].speed = originalSpeeds[n];
    }
}
