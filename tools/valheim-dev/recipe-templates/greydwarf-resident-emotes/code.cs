using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public static class ValheimDevChange
{
    static ResidentEmotePalette palette;
    public static string Run(string json)
    {
        var resident = GameObject.Find("Lab_GreydwarfResident");
        if (!resident) throw new Exception("Spawn George first.");
        palette = resident.AddComponent<ResidentEmotePalette>();
        palette.Configure();
        return "{\"emotes\":25,\"defaultMode\":\"upper\",\"automaticReturn\":true}";
    }
    public static void Cleanup()
    {
        if (palette) UnityEngine.Object.DestroyImmediate(palette);
        palette = null;
    }
}

public class ResidentEmotePalette : MonoBehaviour
{
    [Serializable] public sealed class Request
    {
        public string emote;
        public string mode = "upper";
        public float seconds = 6f;
        public float speed = 1f;
        public string speech;
    }
    Animator animator;
    bool originalEvents, originalRootMotion;
    AnimatorCullingMode originalCulling;
    PlayableGraph graph;
    AnimatorControllerPlayable lounge, gestures;
    AnimationLayerMixerPlayable mixer;
    AvatarMask upperMask, fullMask;
    Dictionary<string, AnimatorControllerParameterType> commands;
    Vector3 originalPosition;
    Quaternion originalRotation;
    float until, weight;
    bool wantsGesture, ownsSpeech;
    public string current = "idle", mode = "upper";
    public int starts, finishes;
    public float placementError;
    public string[] available;
    public string[] playing;
    public string[] controllerStates;
    bool enteredEmote;
    int requestedFromState;
    float requestedFromTime;

    public void Configure()
    {
        animator = GetComponentsInChildren<Animator>(true).Single(a => a.isHuman);
        var playerAnimator = Player.m_localPlayer.GetComponentsInChildren<Animator>(true)
            .First(a => a.isHuman && a.runtimeAnimatorController);
        available = Enum.GetValues(typeof(Emotes)).Cast<Emotes>()
            .Where(e => e != Emotes.Count).Select(e => e.GetCommandName()).ToArray();
        commands = playerAnimator.parameters.Where(p => available.Contains(
            p.name.StartsWith("emote_") ? p.name.Substring(6) : ""))
            .ToDictionary(p => p.name.Substring(6), p => p.type);
        if (commands.Count != available.Length)
            throw new Exception("Native player emote parameters do not match the catalog.");
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
        originalEvents = animator.fireEvents;
        originalRootMotion = animator.applyRootMotion;
        originalCulling = animator.cullingMode;
        // This is a visual controller. Native attack/footstep AnimationEvents must
        // never invoke gameplay callbacks on George's bare donor hierarchy.
        animator.fireEvents = false;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        upperMask = MakeMask(false);
        fullMask = MakeMask(true);
        graph = PlayableGraph.Create("Lab George emote palette");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        lounge = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
        lounge.SetBool("george_vibing", true);
        gestures = AnimatorControllerPlayable.Create(graph, playerAnimator.runtimeAnimatorController);
        mixer = AnimationLayerMixerPlayable.Create(graph, 2);
        mixer.ConnectInput(0, lounge, 0, 1f);
        mixer.ConnectInput(1, gestures, 0, 0f);
        mixer.SetLayerMaskFromAvatarMask(1, upperMask);
        AnimationPlayableOutput.Create(graph, "George", animator).SetSourcePlayable(mixer);
        graph.Play();
    }

    static AvatarMask MakeMask(bool full)
    {
        var mask = new AvatarMask();
        for (int n = 0; n < (int)AvatarMaskBodyPart.LastBodyPart; n++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)n, full);
        // Even full-body preview stays at George's accepted world transform.
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, false);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, false);
        if (!full)
        {
            // Preserve torso/hips/legs: only the native arm and head gesture transfers.
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        }
        return mask;
    }

    public void OnResidentEmote(string json)
    {
        var request = JsonUtility.FromJson<Request>(json);
        if (request.emote == "stop")
        {
            wantsGesture = false;
            return;
        }
        if (!commands.ContainsKey(request.emote))
            throw new ArgumentException("Unknown native emote: " + request.emote);
        if (request.mode != "upper" && request.mode != "full")
            throw new ArgumentException("Mode must be upper or full.");
        if (request.speed < .1f || request.speed > 3f || float.IsNaN(request.speed))
            throw new ArgumentException("Playback speed must be between .1 and 3.");
        if (request.seconds <= 0 || request.seconds > 60 || float.IsNaN(request.seconds))
            throw new ArgumentException("Choose a duration between zero and 60 seconds.");
        // Reset the native one-shot/loop state before another request; do not
        // maintain a duplicate list of emote clip names or imitate the player's AI.
        foreach (var command in commands)
        {
            if (command.Value == AnimatorControllerParameterType.Bool)
                gestures.SetBool("emote_" + command.Key, false);
            else gestures.ResetTrigger("emote_" + command.Key);
        }
        gestures.SetTrigger("emote_stop");
        graph.Evaluate(0);
        gestures.ResetTrigger("emote_stop");
        if (commands[request.emote] == AnimatorControllerParameterType.Bool)
            gestures.SetBool("emote_" + request.emote, true);
        else gestures.SetTrigger("emote_" + request.emote);
        mixer.SetLayerMaskFromAvatarMask(1, request.mode == "full" ? fullMask : upperMask);
        gestures.SetSpeed(request.speed);
        var previousState = gestures.GetCurrentAnimatorStateInfo(0);
        requestedFromState = previousState.fullPathHash;
        requestedFromTime = previousState.normalizedTime;
        enteredEmote = false;
        current = request.emote;
        mode = request.mode;
        until = Time.time + request.seconds;
        wantsGesture = true;
        starts++;
        if (ownsSpeech && Chat.instance) Chat.instance.ClearNpcText(gameObject);
        ownsSpeech = false;
        if (!string.IsNullOrWhiteSpace(request.speech) && Chat.instance)
        {
            var head = animator.GetBoneTransform(HumanBodyBones.Head);
            Chat.instance.SetNpcText(gameObject, head.position - transform.position +
                Vector3.up * .35f, 20f, request.seconds, "George", request.speech, false);
            ownsSpeech = true;
        }
        Debug.Log("[GreydwarfResident] {\"action\":\"emote_start\",\"emote\":\"" +
            current + "\",\"mode\":\"" + mode + "\"}");
    }

    void Update()
    {
        if (!graph.IsValid()) return;
        bool inEmote = Enumerable.Range(0, gestures.GetLayerCount())
            .Any(n => gestures.GetCurrentAnimatorStateInfo(n).IsTag("emote"));
        var state = gestures.GetCurrentAnimatorStateInfo(0);
        // A replacement request can briefly retain the old emote state during its
        // transition. Only the newly entered/restarted state can finish this request.
        if (inEmote && (state.fullPathHash != requestedFromState ||
            state.normalizedTime < requestedFromTime)) enteredEmote = true;
        // Native one-shots finish themselves. Looping poses still obey the bound.
        if (wantsGesture && (Time.time >= until ||
            (enteredEmote && !inEmote))) wantsGesture = false;
        weight = Mathf.MoveTowards(weight, wantsGesture ? 1f : 0f, Time.deltaTime / .4f);
        mixer.SetInputWeight(1, weight);
        if (!wantsGesture && weight == 0f && current != "idle")
        {
            gestures.SetTrigger("emote_stop");
            foreach (var command in commands.Where(c =>
                c.Value == AnimatorControllerParameterType.Bool))
                gestures.SetBool("emote_" + command.Key, false);
            current = "idle";
            if (ownsSpeech && Chat.instance) Chat.instance.ClearNpcText(gameObject);
            ownsSpeech = false;
            finishes++;
            Debug.Log("[GreydwarfResident] {\"action\":\"emote_end\"}");
        }
        placementError = Vector3.Distance(transform.localPosition, originalPosition) +
            Quaternion.Angle(transform.localRotation, originalRotation);
        controllerStates = Enumerable.Range(0, gestures.GetLayerCount()).Select(n =>
            gestures.GetLayerName(n) + " tag=" + gestures.GetCurrentAnimatorStateInfo(n).tagHash +
            " time=" + gestures.GetCurrentAnimatorStateInfo(n).normalizedTime +
            " weight=" + gestures.GetLayerWeight(n)).ToArray();
        playing = Enumerable.Range(0, gestures.GetLayerCount())
            .SelectMany(n => gestures.GetCurrentAnimatorClipInfo(n))
            .Where(c => c.weight > .01f).Select(c => c.clip.name).Distinct().ToArray();
    }

    void OnDisable()
    {
        if (ownsSpeech && Chat.instance) Chat.instance.ClearNpcText(gameObject);
        if (graph.IsValid()) graph.Destroy();
        if (upperMask) UnityEngine.Object.DestroyImmediate(upperMask);
        if (fullMask) UnityEngine.Object.DestroyImmediate(fullMask);
        if (!animator) return;
        animator.fireEvents = originalEvents;
        animator.applyRootMotion = originalRootMotion;
        animator.cullingMode = originalCulling;
        animator.Rebind();
        animator.SetBool("george_vibing", true);
        animator.Update(0);
    }
}
