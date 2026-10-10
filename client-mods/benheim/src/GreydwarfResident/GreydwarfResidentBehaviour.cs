using System;
using System.Linq;
using SoftReferenceableAssets;
using UnityEngine;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// Native George Vibing remains the whole-body animation. One bone writer adds
/// idle glances and brief awareness without shifting the accepted seated pose.
/// Ported from the resolved Lab baseline, not the separate emote experiments.
/// </summary>
internal sealed class GreydwarfResidentBehaviour : MonoBehaviour
{
    private Chair seat = null!;
    private Transform tub = null!;
    private Transform head = null!;
    private Animator[] animators = Array.Empty<Animator>();
    private float[] originalSpeeds = Array.Empty<float>();
    private SoftReference<GameObject> reference;
    private bool held;
    private bool configured;
    private readonly ResidentVisit visit = new();
    private readonly ResidentDrowsiness drowsiness = new();
    private Quaternion baseHead, appliedHead;
    private bool headOffsetApplied, returningToNeutral;
    private float nextPace, paceTarget = .78f, pace = 1f, paceVelocity;
    private float lookStarted, lookDuration, nextLook;
    private Vector2 lookFrom, lookTarget, look;
    private float nextScan, reactionStarted, nextNotice;
    private string state = "idle";
    private Player? pendingSpeech;
    private float speechExpires, nextSpeechCheck;
    private bool sightlineBlocked;
    private int viewMask;
    private Player? speechPlayer;
    private float speechLookUntil;

    internal Chair Seat => seat;

    internal void Configure(Chair occupiedSeat, Transform nativeTub)
    {
        seat = occupiedSeat;
        tub = nativeTub;
        animators = GetComponentsInChildren<Animator>(true)
            .Where(a => a.runtimeAnimatorController).ToArray();
        if (animators.Length == 0) throw new InvalidOperationException("Resident Animator missing.");
        originalSpeeds = animators.Select(a => a.speed).ToArray();
        head = GetComponentsInChildren<Transform>(true).First(t => t.name == "head");
        // Native AI view-blocking layers exclude characters and triggers.
        viewMask = LayerMask.GetMask("Default", "static_solid", "Default_small",
            "piece", "terrain", "vehicle", "viewblock");
        configured = true;
        BeginLounge();
    }

    internal void HoldAssets(SoftReference<GameObject> assetReference)
    {
        // The decorative donor has no ZNetView holding its soft assets. Keep
        // this lease until the clone dies, including while it is disabled.
        reference = assetReference;
        held = true;
    }

    private void OnEnable()
    {
        if (configured) BeginLounge();
    }

    private void BeginLounge()
    {
        nextPace = Time.time + UnityEngine.Random.Range(6f, 10f);
        drowsiness.Reset(Time.time);
        state = "idle";
        ChooseLook();
        ObservePlayer(seed: true);
        ResidentDiagnostics.Emit("seat_reserved", "resident_enabled", gameObject.GetInstanceID());
    }

    private bool SittingBeside(Player player)
    {
        Transform point = player.GetAttachPoint();
        return player.IsAttached() && point && point.IsChildOf(tub)
            && point.GetComponentInParent<Chair>();
    }

    private void ObservePlayer(bool seed)
    {
        Player player = Player.m_localPlayer;
        if (!player || player.IsDead()) return;
        Vector3 delta = player.transform.position - transform.position;
        ResidentReaction reaction = visit.Observe(Time.time,
            new Vector2(delta.x, delta.z).magnitude, delta.y, SittingBeside(player),
            seed, Time.time >= nextNotice && (state == "idle" || state == "drowsy"));
        if (reaction != ResidentReaction.None) React(player, reaction);
    }

    private void BeginLook(Vector2 target, float duration)
    {
        lookFrom = look;
        lookTarget = target;
        lookStarted = Time.time;
        lookDuration = duration;
    }

    private void React(Player player, ResidentReaction reaction)
    {
        Wake(reaction == ResidentReaction.Acknowledge ? "seated_visitor" : "approaching_visitor");
        // Snapshot once: brief awareness, not continuous tracking.
        Vector3 delta = player.GetHeadPoint() - head.position;
        Vector3 flat = Vector3.ProjectOnPlane(delta, transform.up);
        float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, transform.up), -55f, 55f);
        float pitch = Mathf.Clamp(-Mathf.Atan2(delta.y, flat.magnitude) * Mathf.Rad2Deg, -10f, 10f);
        BeginLook(new Vector2(yaw, pitch), .9f);
        reactionStarted = Time.time;
        state = reaction == ResidentReaction.Acknowledge ? "acknowledge" : "notice";
        nextNotice = Time.time + 12f;
        ResidentDiagnostics.Emit("reaction_started", state, gameObject.GetInstanceID());
        if (reaction == ResidentReaction.Notice)
        {
            pendingSpeech = player;
            speechExpires = Time.time + 12f;
            nextSpeechCheck = Time.time;
            sightlineBlocked = false;
            ResidentDiagnostics.Emit("speech_pending", "approach", gameObject.GetInstanceID());
        }
    }

    private void ChooseLook()
    {
        lookFrom = look;
        // Quiet rests alternate with glances, independently of native-loop pace.
        lookTarget = returningToNeutral ? Vector2.zero :
            new Vector2(UnityEngine.Random.Range(9f, 16f) *
                (UnityEngine.Random.value < .5f ? -1f : 1f), UnityEngine.Random.Range(-3f, 4f));
        returningToNeutral = !returningToNeutral;
        lookStarted = Time.time;
        lookDuration = UnityEngine.Random.Range(2f, 3.6f);
        nextLook = lookStarted + lookDuration + UnityEngine.Random.Range(3f, 6f);
    }

    private void UndoHeadOffset()
    {
        // Remove only our own last contribution. If Animator has already
        // overwritten it, preserve the fresh native pose. This also prevents
        // twists accumulating when animation evaluation is culled.
        if (headOffsetApplied && head && Quaternion.Angle(head.localRotation, appliedHead) < .01f)
            head.localRotation = baseHead;
        headOffsetApplied = false;
    }

    private void Update()
    {
        if (!configured) return;
        if (!GreydwarfResidentRuntime.IsEnabled || !ResidentClient.Available || !seat || !tub || !head)
        {
            GreydwarfResidentRuntime.Remove(gameObject);
            return;
        }
        UndoHeadOffset();
        if (Time.time >= nextScan)
        {
            nextScan = Time.time + .2f;
            ObservePlayer(seed: false);
        }
        if (Time.time >= nextPace)
        {
            paceTarget = UnityEngine.Random.Range(.65f, 1.05f);
            nextPace = Time.time + UnityEngine.Random.Range(6f, 12f);
        }
        if ((state == "notice" || state == "acknowledge") && Time.time - reactionStarted >= 2.6f)
        {
            BeginLook(Vector2.zero, 1.6f);
            state = "settling";
            ResidentDiagnostics.Emit("reaction_settling", "return_to_lounge", gameObject.GetInstanceID());
        }
        else if (state == "settling" && Time.time - lookStarted >= lookDuration)
        {
            state = "idle";
            returningToNeutral = false;
            nextLook = Time.time + UnityEngine.Random.Range(3f, 6f);
            ResidentDiagnostics.Emit("reaction_finished", "idle", gameObject.GetInstanceID());
        }
        else if (state == "idle" && Time.time >= nextLook) ChooseLook();
        float t = Mathf.Clamp01((Time.time - lookStarted) / lookDuration);
        // Speech owns the same bone while a visible remark follows its
        // addressee. Do not reset its tracking interpolation to an idle target.
        if (speechLookUntil == 0f) look = Vector2.Lerp(lookFrom, lookTarget, t * t * (3f - 2f * t));
        UpdateSpeech();
        UpdateSpeechLook();
        UpdateDrowsiness();
        pace = Mathf.SmoothDamp(pace, Mathf.Lerp(paceTarget, .48f, drowsiness.Weight), ref paceVelocity, 2f);
        for (int n = 0; n < animators.Length; n++)
            if (animators[n]) animators[n].speed = originalSpeeds[n] * pace;
    }

    private void Wake(string reason)
    {
        if (drowsiness.Wake(Time.time))
            ResidentDiagnostics.Emit("drowsiness_finished", reason, gameObject.GetInstanceID());
    }

    private void UpdateDrowsiness()
    {
        // Use the loaded world's native night boundary rather than wall-clock
        // time or darkness from a storm. Speech and reactions retain priority.
        bool night = EnvMan.instance && EnvMan.IsNight();
        if (!drowsiness.Update(Time.time, Time.deltaTime, night, state == "idle" || state == "drowsy")) return;
        if (drowsiness.IsDrowsy)
        {
            state = "drowsy";
            BeginLook(Vector2.zero, 3f);
            ResidentDiagnostics.Emit("drowsiness_started", "native_night", gameObject.GetInstanceID());
        }
        else
        {
            state = "idle";
            BeginLook(Vector2.zero, 2f);
            nextLook = Time.time + 4f;
            ResidentDiagnostics.Emit("drowsiness_finished", "daytime", gameObject.GetInstanceID());
        }
    }

    private void UpdateSpeech()
    {
        if (pendingSpeech is null || Time.time < nextSpeechCheck) return;
        nextSpeechCheck = Time.time + .2f;
        if (!pendingSpeech || pendingSpeech != Player.m_localPlayer || pendingSpeech.IsDead())
        {
            FinishSpeech("visitor_unavailable");
            return;
        }
        Vector3 delta = pendingSpeech.transform.position - transform.position;
        if (Time.time >= speechExpires || ResidentVisit.IsOutside(new Vector2(delta.x, delta.z).magnitude, delta.y))
        {
            FinishSpeech(Time.time >= speechExpires ? "expired" : "visitor_left");
            return;
        }
        if (!Chat.instance || Physics.Linecast(pendingSpeech.GetHeadPoint(), head.position,
            viewMask, QueryTriggerInteraction.Ignore))
        {
            // One observation explains the wait without emitting per-frame logs.
            if (!sightlineBlocked)
                ResidentDiagnostics.Emit("speech_waiting", Chat.instance ? "sightline_blocked" : "chat_unavailable",
                    gameObject.GetInstanceID());
            sightlineBlocked = true;
            return;
        }
        ResidentTubClient client = tub.GetComponent<ResidentTubClient>();
        if (client) ResidentClient.Approach(client, this, pendingSpeech);
        FinishSpeech("encounter_requested");
    }

    internal bool CanSpeakTo(Player player)
    {
        if (!configured || !head || !player || player.IsDead() || player.IsTeleporting()) return false;
        Vector3 delta = player.transform.position - transform.position;
        return !ResidentVisit.IsOutside(new Vector2(delta.x, delta.z).magnitude, delta.y) &&
            !Physics.Linecast(player.GetHeadPoint(), head.position, viewMask, QueryTriggerInteraction.Ignore);
    }

    internal void ShowSpeech(Player player, string text)
    {
        // A remote player's shared remark must wake this client's George too.
        Wake("shared_speech");
        Chat.instance.SetNpcText(gameObject, head.position - transform.position + Vector3.up * .35f,
            20f, 10f, "George", text, false);
        speechPlayer = player;
        speechLookUntil = Time.time + 11f;
        state = "speaking";
        ResidentDiagnostics.Emit("speech_look_started", "shared_addressee", gameObject.GetInstanceID());
    }

    private void UpdateSpeechLook()
    {
        // Unity's destroyed-object equality must still enter cleanup. A null
        // reference alone does not mean a previously active look has ended.
        if (speechLookUntil == 0f) return;
        if (!speechPlayer || Time.time >= speechLookUntil || !CanSpeakTo(speechPlayer) ||
            !Chat.instance || !Chat.instance.IsDialogVisible(gameObject))
        {
            speechPlayer = null;
            speechLookUntil = 0f;
            BeginLook(Vector2.zero, 1.6f);
            state = "settling";
            ResidentDiagnostics.Emit("speech_look_finished", "dialog_or_visitor_ended", gameObject.GetInstanceID());
            return;
        }
        Vector3 delta = speechPlayer.GetHeadPoint() - head.position;
        Vector3 flat = Vector3.ProjectOnPlane(delta, transform.up);
        Vector2 target = new(Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, transform.up), -55f, 55f),
            Mathf.Clamp(-Mathf.Atan2(delta.y, flat.magnitude) * Mathf.Rad2Deg, -10f, 10f));
        look = Vector2.Lerp(look, target, Mathf.Clamp01(Time.deltaTime * 3f));
    }

    private void FinishSpeech(string reason)
    {
        pendingSpeech = null;
        ResidentDiagnostics.Emit("speech_finished", reason, gameObject.GetInstanceID());
    }

    private void LateUpdate()
    {
        if (!configured || !head) return;
        baseHead = head.localRotation;
        // Use upright/right axes from the resident, not the imported bone.
        // Hips and legs remain entirely native. The seat acknowledgement adds
        // one small nod and eases back to the accepted seated pose.
        float nod = state == "acknowledge" ? 6f * Mathf.Sin(Mathf.PI *
            Mathf.Clamp01((Time.time - reactionStarted - .9f) / 1f)) : 0f;
        float droop = drowsiness.Weight * (14f + 2f * Mathf.Sin(Time.time * .6f));
        head.rotation = Quaternion.AngleAxis(look.x, transform.up) *
            Quaternion.AngleAxis(look.y + nod + droop, transform.right) * head.rotation;
        appliedHead = head.localRotation;
        headOffsetApplied = true;
    }

    private void OnDisable()
    {
        UndoHeadOffset();
        for (int n = 0; n < animators.Length; n++)
            if (animators[n]) animators[n].speed = originalSpeeds[n];
        if (pendingSpeech is not null) FinishSpeech("resident_disabled");
        speechPlayer = null;
        speechLookUntil = 0f;
        if (Chat.instance) Chat.instance.ClearNpcText(gameObject);
        if (configured)
            ResidentDiagnostics.Emit("seat_released", "resident_disabled", gameObject.GetInstanceID());
    }

    private void OnDestroy()
    {
        if (configured) GreydwarfResidentRuntime.Forget(seat, this);
        if (held)
        {
            // Stop referencing the controller before releasing its location lease.
            foreach (Animator animator in animators)
                if (animator) animator.runtimeAnimatorController = null;
            reference.Release();
            held = false;
        }
        ResidentDiagnostics.Emit("removed", "clone_destroyed", gameObject.GetInstanceID());
    }
}
