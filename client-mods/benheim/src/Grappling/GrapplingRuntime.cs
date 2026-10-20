using System;
using System.Runtime.CompilerServices;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Grappling;

internal static class GrapplingRuntime
{
    private static readonly ConditionalWeakTable<Projectile, Traversal> Shots = new();
    private static readonly ConditionalWeakTable<GrapplingPoint, Traversal> Ropes = new();
    private static Traversal? spawning;

    internal sealed class Traversal
    {
        internal readonly string Id = Guid.NewGuid().ToString("N");
        internal readonly Player Player;
        internal readonly Vector3 FiredFrom;
        internal readonly float Level;
        internal Vector3 AttachedFrom;
        internal float AttachmentDistance;
        internal GrapplingPoint? ActivePoint;
        internal bool Attached;
        internal bool Finished;
        internal bool HasFlightTimer;

        internal Traversal(Player player)
        {
            Player = player;
            FiredFrom = player.transform.position;
            Level = player.GetSkillLevel(GrapplingSkill.Type);
        }
    }

    internal static bool IsHook(ItemDrop.ItemData? item) => item != null
        && (SpawnsRope(item.m_shared.m_attack) || SpawnsRope(item.m_shared.m_secondaryAttack));

    private static bool SpawnsRope(Attack attack)
    {
        if (attack.m_spawnOnHit && attack.m_spawnOnHit.GetComponent<GrapplingPoint>()) return true;
        Projectile? projectile = attack.m_attackProjectile
            ? attack.m_attackProjectile.GetComponent<Projectile>() : null;
        return projectile && projectile.m_spawnOnHit && projectile.m_spawnOnHit.GetComponent<GrapplingPoint>();
    }

    internal static void Fired(Projectile projectile, Character owner, ItemDrop.ItemData? item)
    {
        if (!(owner is Player player) || player != Player.m_localPlayer || !IsHook(item)
            || Shots.TryGetValue(projectile, out _)) return;

        var traversal = new Traversal(player);
        Shots.Add(projectile, traversal);
        float before = projectile.m_ttl;
        traversal.HasFlightTimer = before > 0f;
        // Flight and attached-rope limits are independent in native Valheim.
        // Extend the fired instance only; keep projectile physics and the
        // shared prefab untouched. Nonpositive TTL means native has no timer.
        if (before > 0f) projectile.m_ttl = before * GrapplingRules.ReachMultiplier(traversal.Level);
        Diagnostics.Emit(Event(traversal, "shot_fired")
            .Number("native_flight_seconds", before).Number("flight_seconds", projectile.m_ttl)
            .Number("reach_multiplier", GrapplingRules.ReachMultiplier(traversal.Level)));
    }

    internal static Traversal? EnterHit(Projectile projectile)
    {
        Traversal? previous = spawning;
        spawning = Shots.TryGetValue(projectile, out Traversal traversal) && !traversal.Finished
            ? traversal : null;
        return previous;
    }

    internal readonly struct BreakScope
    {
        internal readonly Traversal? Previous;
        internal readonly bool Accepted;

        internal BreakScope(Traversal? previous, bool accepted)
        {
            Previous = previous;
            Accepted = accepted;
        }
    }

    internal static BreakScope EnterBreak(GrapplingPoint point, ZNetView view, Character character)
    {
        Traversal? previous = spawning;
        spawning = Ropes.TryGetValue(point, out Traversal traversal) && !traversal.Finished
            ? traversal : null;
        // ZNetScene.Destroy resets the view's ZDO synchronously inside native
        // Break. Snapshot its entry guard now; a postfix ownership check would
        // reject every successfully destroyed rope and award no traversal XP.
        return new BreakScope(previous, view && view.IsOwner() && character && character == Player.m_localPlayer);
    }

    internal static void RestoreScope(Traversal? previous) => spawning = previous;

    internal static void Activate(GrapplingPoint point, Character character)
    {
        Traversal? traversal = spawning;
        if (traversal == null || traversal.Finished || character != traversal.Player
            || character != Player.m_localPlayer || Ropes.TryGetValue(point, out _)) return;

        // Native Break(false) can create a second hanging rope and Activate it
        // synchronously. Carry one traversal through that handoff, including
        // its range, rather than granting XP twice or reverting the hang cap.
        Ropes.Add(point, traversal);
        traversal.ActivePoint = point;
        float before = point.m_maxLength;
        point.m_maxLength = before * GrapplingRules.ReachMultiplier(traversal.Level);
        if (!traversal.Attached)
        {
            traversal.Attached = true;
            traversal.AttachedFrom = traversal.Player.transform.position;
            // Judge a nearby shot from the firing origin, even if the player
            // moved while the projectile was in flight.
            traversal.AttachmentDistance = Vector3.Distance(point.transform.position, traversal.FiredFrom);
        }
        Diagnostics.Emit(Event(traversal, "rope_attached")
            .Number("attachment_distance", traversal.AttachmentDistance)
            .Number("native_rope_length", before).Number("rope_length", point.m_maxLength));
    }

    internal static void Released(GrapplingPoint point, bool early, bool accepted)
    {
        if (!accepted || !Ropes.TryGetValue(point, out Traversal traversal)) return;
        Ropes.Remove(point);
        // Reentrant Break calls and pull-to-hang transitions end the old rope,
        // but the traversal remains owned by the newly activated rope.
        if (traversal.Finished || traversal.ActivePoint != point) return;

        traversal.Finished = true;
        Player player = traversal.Player;
        if (player == null)
        {
            Diagnostics.Emit(Event(traversal, "traversal_finished")
                .String("reason", "player_unavailable").Number("experience", 0f));
            return;
        }
        Vector3 releasedAt = player.transform.position;
        float displacement = Vector3.Distance(traversal.FiredFrom, releasedAt);
        float movement = Vector3.Distance(traversal.AttachedFrom, releasedAt);
        float height = releasedAt.y - traversal.FiredFrom.y;
        bool eligible = !player.IsDead() && !player.IsTeleporting();
        float experience = eligible
            ? GrapplingRules.Experience(traversal.AttachmentDistance, movement, displacement, height) : 0f;
        Skills.Skill? skillBefore = player.GetSkills().GetSkillList().Find(skill => skill.m_info.m_skill == GrapplingSkill.Type);
        float levelBefore = skillBefore?.m_level ?? 0f;
        float accumulatorBefore = skillBefore?.m_accumulator ?? 0f;
        if (experience > 0f) player.RaiseSkill(GrapplingSkill.Type, experience);
        Skills.Skill? skillAfter = player.GetSkills().GetSkillList().Find(skill => skill.m_info.m_skill == GrapplingSkill.Type);
        Diagnostics.Emit(Event(traversal, "traversal_finished")
            .String("reason", !eligible ? "player_unavailable" : experience > 0f ? "traversal" : "insufficient_movement")
            .Boolean("early_release", early).Number("displacement", displacement)
            .Number("movement_since_attachment", movement).Number("height_gained", Math.Max(0f, height))
            .Number("experience", experience).Number("level_before", levelBefore)
            .Number("level_after", skillAfter?.m_level ?? 0f)
            .Number("accumulator_before", accumulatorBefore).Number("accumulator_after", skillAfter?.m_accumulator ?? 0f));
    }

    internal static void FlightUpdated(Projectile projectile, bool didHit)
    {
        if (!Shots.TryGetValue(projectile, out Traversal traversal)) return;
        if (traversal.Attached)
        {
            Shots.Remove(projectile);
            return;
        }
        if (!didHit && (!traversal.HasFlightTimer || projectile.m_ttl > 0f)) return;
        Shots.Remove(projectile);
        traversal.Finished = true;
        Diagnostics.Emit(Event(traversal, "traversal_finished")
            .String("reason", didHit ? "no_attachment" : "miss").Number("experience", 0f));
    }

    internal static float Reload(ItemDrop.ItemData item, float nativeDuration)
    {
        Player player = Player.m_localPlayer;
        if (!player || !IsHook(item) || !item.m_shared.m_attack.m_requiresReload) return nativeDuration;
        float level = player.GetSkillLevel(GrapplingSkill.Type);
        return nativeDuration * GrapplingRules.ReloadMultiplier(level);
    }

    internal static void ObserveReload(Player player, int previousCount,
        System.Collections.Generic.List<Player.MinorActionData> actions)
    {
        if (player != Player.m_localPlayer || actions.Count <= previousCount) return;
        Player.MinorActionData action = actions[actions.Count - 1];
        if (action.m_type != Player.MinorActionData.ActionType.Reload || !IsHook(action.m_item)) return;
        Diagnostics.Emit(DiagnosticEvent.Create("Grappling", "reload_queued")
            .Number("level", player.GetSkillLevel(GrapplingSkill.Type)).Number("duration", action.m_duration));
    }

    private static DiagnosticEvent Event(Traversal traversal, string name) =>
        DiagnosticEvent.Create("Grappling", name).String("operation_id", traversal.Id).Number("level", traversal.Level);
}
