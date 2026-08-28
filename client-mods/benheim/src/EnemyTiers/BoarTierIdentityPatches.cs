using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

[HarmonyPatch]
internal static class BoarTierIdentityPatches
{
    private static bool profileObservationFailureLogged;
    private static bool hitObservationFailureLogged;

    [HarmonyPatch(typeof(LevelEffects), "Start")]
    [HarmonyPostfix]
    private static void AfterLevelEffectsStart(LevelEffects __instance)
    {
        Emit(BoarTierIdentity.Apply(__instance, "level_effects_start"));
        ObserveProfileSafely(__instance, "level_effects_start");
    }

    [HarmonyPatch(typeof(LevelEffects), "OnLevelSet")]
    [HarmonyPostfix]
    private static void AfterLevelSet(LevelEffects __instance)
    {
        Emit(BoarTierIdentity.Apply(__instance, "level_changed"));
        ObserveProfileSafely(__instance, "level_changed");
    }

    [HarmonyPatch(typeof(Character), nameof(Character.ApplyPushback), typeof(Vector3), typeof(float))]
    [HarmonyPrefix]
    private static void BeforeApplyPushback(Character __instance, ref float pushForce)
    {
        BoarTierCombat.AdjustIncomingPush(__instance, ref pushForce);
    }

    [HarmonyPatch(typeof(Character), nameof(Character.Damage), typeof(HitData))]
    [HarmonyPrefix]
    private static void BeforeDamage(Character __instance, HitData hit)
    {
        BoarTierCombat.AdjustOutgoingPush(__instance, hit);
        ObserveHitSafely(__instance, hit);
    }

    [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]
    [HarmonyPrefix]
    private static void BeforeMonsterUpdate(
        MonsterAI __instance,
        float dt,
        ref float ___m_timeSinceSensedTargetCreature,
        ref float ___m_timeSinceAttacking)
    {
        BoarTierCombat.ExtendPursuit(
            __instance,
            dt,
            ref ___m_timeSinceSensedTargetCreature,
            ref ___m_timeSinceAttacking);
    }

    private static void Emit(DiagnosticEvent? diagnosticEvent)
    {
        if (diagnosticEvent != null)
        {
            Diagnostics.Emit(diagnosticEvent);
        }
    }

    private static void ObserveProfileSafely(LevelEffects levelEffects, string source)
    {
        try
        {
            Emit(BoarTierGeometryDiagnostics.ObserveAppliedProfile(levelEffects, source));
        }
        catch (Exception exception)
        {
            ReportObservationFailureOnce(ref profileObservationFailureLogged, "profile", exception);
        }
    }

    private static void ObserveHitSafely(Character target, HitData hit)
    {
        try
        {
            Emit(BoarTierGeometryDiagnostics.ObserveLocalPlayerMeleeHit(target, hit));
        }
        catch (Exception exception)
        {
            ReportObservationFailureOnce(ref hitObservationFailureLogged, "player_hit", exception);
        }
    }

    private static void ReportObservationFailureOnce(ref bool logged, string stage, Exception exception)
    {
        if (logged)
        {
            return;
        }

        logged = true;
        try
        {
            Plugin.Log.LogWarning(
                $"Benheim Boar geometry observation failed: stage={stage} " +
                $"reason={Diagnostics.Flatten(exception.Message)}");
        }
        catch (Exception)
        {
            // Diagnostics must never interrupt profile application or damage.
        }
    }
}
