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
        MonsterAI? monsterAI = ai as MonsterAI;
        if (collider == null || ai == null || monsterAI == null)
        {
            string reason = collider == null
                ? "missing_capsule"
                : ai == null
                    ? "missing_ai"
                    : "missing_monster_ai";
            return Rejected(character, level, source, reason);
        }

        BoarTierApplicationState state = ApplicationStates.GetOrCreateValue(levelEffects);
        state.CaptureBaseline(
            character.m_runSpeed,
            character.m_runTurnSpeed,
            ai.m_viewRange,
            ai.m_hearRange,
            monsterAI.m_alertRange,
            monsterAI.m_fleeIfNotAlerted);

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

        // These are per-instance prefab fields. Apply absolute values from the
        // captured native Boar baseline so reloads and level callbacks cannot
        // compound them or mutate shared attack/item definitions.
        character.m_runSpeed = state.NativeRunSpeed * profile.RunSpeedMultiplier;
        character.m_runTurnSpeed = state.NativeRunTurnSpeed * profile.RunTurnSpeedMultiplier;
        ai.m_viewRange = state.NativeViewRange * profile.DetectionMultiplier;
        ai.m_hearRange = state.NativeHearRange * profile.DetectionMultiplier;
        monsterAI.m_alertRange = state.NativeAlertRange * profile.AlertRangeMultiplier;
        monsterAI.m_fleeIfNotAlerted = false;
        state.MarkApplied();

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
            .Number("incoming_push_multiplier", profile.IncomingPushMultiplier)
            .Number("outgoing_push_multiplier", profile.OutgoingPushMultiplier)
            .Number("view_range", ai.m_viewRange)
            .Number("hear_range", ai.m_hearRange)
            .Number("alert_range", monsterAI.m_alertRange)
            .Number("run_speed", character.m_runSpeed)
            .Number("run_turn_speed", character.m_runTurnSpeed)
            .Number("pursuit_duration_multiplier", profile.PursuitDurationMultiplier)
            .Boolean("flee_if_not_alerted", monsterAI.m_fleeIfNotAlerted)
            .Boolean("tamed", character.IsTamed())
            .Boolean("owner", character.IsOwner());
    }

    internal static bool HasAppliedProfile(LevelEffects levelEffects)
    {
        return ApplicationStates.TryGetValue(levelEffects, out BoarTierApplicationState? state) &&
            state.ProfileApplied;
    }

    internal static bool HasAppliedProfile(Character character)
    {
        LevelEffects? levelEffects = character.GetComponentInChildren<LevelEffects>(includeInactive: true);
        return levelEffects != null && HasAppliedProfile(levelEffects);
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
        MonsterAI? monsterAI = ai as MonsterAI;
        if (collider == null || ai == null || monsterAI == null)
        {
            string reason = collider == null
                ? "missing_capsule_on_restore"
                : ai == null
                    ? "missing_ai_on_restore"
                    : "missing_monster_ai_on_restore";
            return Rejected(
                character,
                level,
                source,
                reason);
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
        character.m_runSpeed = state.NativeRunSpeed;
        character.m_runTurnSpeed = state.NativeRunTurnSpeed;
        ai.m_viewRange = state.NativeViewRange;
        ai.m_hearRange = state.NativeHearRange;
        monsterAI.m_alertRange = state.NativeAlertRange;
        monsterAI.m_fleeIfNotAlerted = state.NativeFleeIfNotAlerted;
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
