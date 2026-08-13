using BenheimQoL.Infrastructure;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

internal static class BoarTierIdentity
{
    private const string BoarPrefabName = "Boar";
    private static readonly ConditionalWeakTable<LevelEffects, BoarTierApplicationState> ApplicationStates = new();

    internal static DiagnosticEvent? Apply(LevelEffects levelEffects, string source)
    {
        Character? character = levelEffects.GetComponentInParent<Character>();
        if (character == null || Utils.GetPrefabName(character.gameObject) != BoarPrefabName)
        {
            return null;
        }

        int level = character.GetLevel();
        if (!BoarTierPhysicalProfile.TryForLevel(level, out BoarTierPhysicalProfile profile))
        {
            return RestoreNativeProfileIfNeeded(levelEffects, character, level, source);
        }

        CapsuleCollider? collider = character.GetCollider();
        BaseAI? ai = character.GetBaseAI();
        if (collider == null || ai == null)
        {
            return Rejected(character, level, source, collider == null ? "missing_capsule" : "missing_ai");
        }

        // Native LevelEffects has already applied the Boar prefab's color and
        // fangs. Replace only its authored 1.1/1.2 visual scale, then set the
        // root capsule from the same absolute factor. Absolute assignments make
        // spawn, reload, breeding/growth level changes, and repeated callbacks
        // deterministic instead of multiplying prior state.
        levelEffects.transform.localScale = Vector3.one * profile.VisualScale;
        collider.center = new Vector3(0f, profile.ColliderCenterY, 0f);
        collider.radius = profile.ColliderRadius;
        collider.height = profile.ColliderHeight;

        // HorseSize is Valheim's closest existing navigation profile: its
        // 0.8m radius and 2.5m height are conservative for level two and only
        // 0.05m narrower than the level-three capsule. That is at least as
        // coherent as native Boar's 0.5m capsule with a 0.4m Humanoid agent,
        // without inventing a custom path-agent type.
        ai.m_pathAgentType = Pathfinding.AgentType.HorseSize;
        ApplicationStates.GetOrCreateValue(levelEffects).MarkApplied();

        return DiagnosticEvent.Create("EnemyTiers", "boar_tier_profile_applied")
            .String("source", source)
            .String("creature", BoarPrefabName)
            .String("creature_id", character.GetZDOID().ToString())
            .Integer("level", level)
            .Integer("stars", level - 1)
            .Number("visual_scale", profile.VisualScale)
            .Number("collider_center_y", profile.ColliderCenterY)
            .Number("collider_radius", profile.ColliderRadius)
            .Number("collider_height", profile.ColliderHeight)
            .String("path_agent", Pathfinding.AgentType.HorseSize.ToString())
            .Boolean("tamed", character.IsTamed())
            .Boolean("owner", character.IsOwner());
    }

    private static DiagnosticEvent? RestoreNativeProfileIfNeeded(
        LevelEffects levelEffects,
        Character character,
        int level,
        string source)
    {
        if (!ApplicationStates.TryGetValue(levelEffects, out BoarTierApplicationState? state) ||
            !state.ProfileApplied)
        {
            // A fresh ordinary Boar stays entirely in Valheim's native setup.
            return null;
        }

        CapsuleCollider? collider = character.GetCollider();
        BaseAI? ai = character.GetBaseAI();
        if (collider == null || ai == null)
        {
            return Rejected(
                character,
                level,
                source,
                collider == null ? "missing_capsule_on_restore" : "missing_ai_on_restore");
        }

        // Native LevelEffects has no level-one reset branch. Restore only an
        // instance that Benheim previously enlarged; fresh zero-star Boars
        // never enter this path. If a future native Boar level has an authored
        // setup, retain the scale that native code just selected.
        float nativeVisualScale = 1f;
        int setupIndex = level - 2;
        if (setupIndex >= 0 && setupIndex < levelEffects.m_levelSetups.Count)
        {
            nativeVisualScale = levelEffects.m_levelSetups[setupIndex].m_scale;
        }

        levelEffects.transform.localScale = Vector3.one * nativeVisualScale;
        collider.center = new Vector3(0f, BoarTierPhysicalProfile.NativeColliderCenterY, 0f);
        collider.radius = BoarTierPhysicalProfile.NativeColliderRadius;
        collider.height = BoarTierPhysicalProfile.NativeColliderHeight;
        ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;
        state.MarkRestored();

        return DiagnosticEvent.Create("EnemyTiers", "boar_tier_profile_restored")
            .String("source", source)
            .String("creature", BoarPrefabName)
            .String("creature_id", character.GetZDOID().ToString())
            .Integer("level", level)
            .Number("visual_scale", nativeVisualScale)
            .String("path_agent", Pathfinding.AgentType.Humanoid.ToString())
            .Boolean("tamed", character.IsTamed())
            .Boolean("owner", character.IsOwner());
    }

    private static DiagnosticEvent Rejected(Character character, int level, string source, string reason)
    {
        return DiagnosticEvent.Create("EnemyTiers", "boar_tier_profile_rejected")
            .String("source", source)
            .String("creature", BoarPrefabName)
            .String("creature_id", character.GetZDOID().ToString())
            .Integer("level", level)
            .String("reason", reason)
            .Boolean("owner", character.IsOwner());
    }
}
