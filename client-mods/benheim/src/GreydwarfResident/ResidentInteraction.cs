using System;
using UnityEngine;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// Gives the decorative native George a small native hover target without
/// adding a character, rigidbody, network view, or physical collision. Valheim
/// decides whether trigger colliders participate in hover raycasts through its
/// global Physics.queriesHitTriggers setting; this component never changes it.
/// </summary>
internal sealed class ResidentInteraction : MonoBehaviour, Hoverable, Interactable
{
    private const float TargetRadius = .22f;

    private ResidentTubClient tub = null!;
    private GreydwarfResidentBehaviour resident = null!;
    private Transform head = null!;
    private bool? triggerQueriesAvailable;

    internal static void Attach(Transform head, ResidentTubClient tub, GreydwarfResidentBehaviour resident)
    {
        if (!head) throw new ArgumentNullException(nameof(head));
        if (!tub) throw new ArgumentNullException(nameof(tub));
        if (!resident) throw new ArgumentNullException(nameof(resident));

        // Player.FindHoverObject includes the native "character" layer. Keep
        // the target under George's root, not the imported head bone: donor
        // bone scale varies by rig and must not change the world hit radius.
        int characterLayer = LayerMask.NameToLayer("character");
        if (characterLayer < 0)
            throw new InvalidOperationException("Valheim's character layer is unavailable.");

        var target = new GameObject("Benheim_GeorgeTalkTarget")
        {
            layer = characterLayer
        };
        target.transform.SetParent(resident.transform, worldPositionStays: false);

        SphereCollider collider = target.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = TargetRadius;

        ResidentInteraction interaction = target.AddComponent<ResidentInteraction>();
        interaction.Configure(head, tub, resident);
    }

    private void Configure(Transform nativeHead, ResidentTubClient tubClient, GreydwarfResidentBehaviour george)
    {
        head = nativeHead;
        tub = tubClient;
        resident = george;
        ObserveTriggerCapability();
        FollowHead();
    }

    private void Update()
    {
        if (!tub || !resident) return;
        ObserveTriggerCapability();
    }

    private void LateUpdate()
    {
        if (!head || !resident) return;

        FollowHead();
    }

    private void FollowHead()
    {
        // Follow the animated head in world space while compensating for any
        // resident-root scale. The sphere remains a 0.22 m trigger even if a
        // future cosmetic scale changes George or the donor's bone hierarchy.
        transform.position = head.position;
        transform.rotation = Quaternion.identity;
        Vector3 scale = resident.transform.lossyScale;
        if (Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f)
            return;
        transform.localScale = new Vector3(1f / Mathf.Abs(scale.x),
            1f / Mathf.Abs(scale.y), 1f / Mathf.Abs(scale.z));
    }

    public string GetHoverText()
    {
        if (!tub || !resident) return string.Empty;
        if (ResidentClient.IsSpeechPending(tub)) return "George\nThinking…";

        const string usePrompt = "[<color=yellow><b>$KEY_Use</b></color>] Talk";
        return "George\n" + (Localization.instance != null
            ? Localization.instance.Localize(usePrompt)
            : "[Use] Talk");
    }

    public string GetHoverName() => "George";

    public float GetHoverOffset() => .25f;

    public bool Interact(Humanoid user, bool hold, bool alt)
    {
        // Player.Interact calls again while Use is held. Only the first press
        // should request speech, and only the local player can own that request.
        if (hold || !tub || !resident ||
            user is not Player player || !player || player != Player.m_localPlayer)
            return false;

        return ResidentClient.Talk(tub, resident, player);
    }

    public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

    private void ObserveTriggerCapability()
    {
        bool available = Physics.queriesHitTriggers;
        if (triggerQueriesAvailable == available) return;

        bool wasKnown = triggerQueriesAvailable.HasValue;
        bool wasAvailable = triggerQueriesAvailable.GetValueOrDefault();
        triggerQueriesAvailable = available;

        int residentId = resident ? resident.gameObject.GetInstanceID() : 0;
        if (!available)
        {
            ResidentDiagnostics.Emit("interaction_unavailable", "trigger_queries_disabled", residentId);
            Plugin.Log.LogWarning("George's native hover interaction is unavailable because this client ignores trigger colliders.");
        }
        else if (wasKnown && !wasAvailable)
        {
            ResidentDiagnostics.Emit("interaction_available", "trigger_queries_enabled", residentId);
        }
    }
}
