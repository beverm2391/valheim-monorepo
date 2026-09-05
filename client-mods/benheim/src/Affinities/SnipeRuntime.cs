using System.Runtime.CompilerServices;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Affinities;

internal static class SnipeRuntime
{
    private sealed class SnipeShot { }
    private static readonly ConditionalWeakTable<Projectile, SnipeShot> Shots = new();

    internal static bool IsEquipped(Player? player)
    {
        return HealthReporting.GameplayActionsEnabled
            && player != null
            && player == Player.m_localPlayer
            && AffinityState.IsSnipe(player.GetCurrentWeapon());
    }

    internal static float ClampDrawPercentage(float nativeProgress, Humanoid character)
    {
        // The caller has already resolved native draw duration and skill, but
        // has not clamped elapsed/duration yet. Scaling after Clamp01 would
        // strand a Snipe bow at 80% forever, even after holding for longer.
        if (character is Player player && IsEquipped(player))
        {
            nativeProgress /= SnipeRules.DrawDurationMultiplier;
        }

        return Mathf.Clamp01(nativeProgress);
    }

    internal static void ObserveShot(Projectile projectile, Character owner, ItemDrop.ItemData? weapon)
    {
        // Snapshot the exact firing item, not the player's weapon at impact.
        // A weak key lives only as long as its native projectile. This does not
        // add saved item data, a network field, or a damage-result protocol.
        Shots.Remove(projectile);
        if (HealthReporting.GameplayActionsEnabled
            && owner is Player
            && AffinityState.IsSnipe(weapon))
        {
            Shots.Add(projectile, new SnipeShot());
        }
    }

    internal static bool IsSnipeShot(Projectile projectile)
    {
        return HealthReporting.GameplayActionsEnabled && Shots.TryGetValue(projectile, out _);
    }
}
