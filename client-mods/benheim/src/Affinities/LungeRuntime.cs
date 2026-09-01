using System;
using System.Collections;
using System.Runtime.CompilerServices;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Affinities;

internal sealed class LungeSwingState
{
    internal LungeSwingState(string operationId, ItemDrop.ItemData weapon)
    {
        OperationId = operationId;
        Weapon = weapon;
    }

    internal string OperationId { get; }
    internal ItemDrop.ItemData Weapon { get; }
    internal bool Consumed { get; set; }
}

internal static class LungeRuntime
{
    internal const float DefaultForce = 10f;
    internal const float MinimumVerticalVelocity = 3f;
    private static readonly ConditionalWeakTable<Attack, LungeSwingState> Swings = new();
    private static float force = DefaultForce;

    internal static float Force => force;

    internal static void ResetSession()
    {
        force = DefaultForce;
    }

    internal static bool TrySetForce(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f || value > 30f)
        {
            return false;
        }
        force = value;
        return true;
    }

    internal static void ObserveAttackStarted(
        Humanoid character,
        Attack attack,
        bool secondaryAttack)
    {
        Player? player = Player.m_localPlayer;
        if (player == null || character != player) return;

        ItemDrop.ItemData? weapon = player.GetCurrentWeapon();
        if (AffinityState.Read(weapon) != AffinityLoadResult.Lunge) return;
        if (AffinityState.Load(weapon, "attack_start") != AffinityLoadResult.Lunge) return;

        string operationId = Diagnostics.NewOperationId();
        if (secondaryAttack)
        {
            EmitRejected(operationId, weapon, "secondary_attack");
            return;
        }
        if (!IsMeleeAttack(attack.m_attackType))
        {
            EmitRejected(operationId, weapon, "not_melee");
            return;
        }

        Swings.Remove(attack);
        Swings.Add(attack, new LungeSwingState(operationId, weapon!));
    }

    internal static void Consume(Attack attack, Humanoid character)
    {
        if (!Swings.TryGetValue(attack, out LungeSwingState? state) || state.Consumed)
        {
            return;
        }
        state.Consumed = true;

        Player? player = Player.m_localPlayer;
        string rejection = Validate(player, character, attack, state);
        if (!string.Equals(rejection, "accepted", StringComparison.Ordinal)
            || player == null)
        {
            EmitRejected(state.OperationId, state.Weapon, rejection);
            return;
        }

        Rigidbody? body = player.GetComponent<Rigidbody>();
        if (body == null)
        {
            EmitRejected(state.OperationId, state.Weapon, "missing_rigidbody");
            return;
        }

        Vector3 before = body.linearVelocity;
        Vector3 forward = player.transform.forward;
        if (!AffinityRules.TryPlanarImpulse(
                forward.x,
                forward.z,
                force,
                out float impulseX,
                out float impulseZ))
        {
            EmitRejected(state.OperationId, state.Weapon, "missing_planar_forward");
            return;
        }
        Vector3 impulse = new Vector3(
            impulseX,
            AffinityRules.RequiredVerticalImpulse(before.y, MinimumVerticalVelocity),
            impulseZ);
        body.AddForce(impulse, ForceMode.VelocityChange);
        player.StartCoroutine(EmitAcceptedAfterPhysics(
            body,
            state.OperationId,
            state.Weapon,
            force,
            before,
            impulse));
    }

    private static IEnumerator EmitAcceptedAfterPhysics(
        Rigidbody body,
        string operationId,
        ItemDrop.ItemData weapon,
        float appliedForce,
        Vector3 before,
        Vector3 impulse)
    {
        yield return new WaitForFixedUpdate();
        Vector3 after = body != null ? body.linearVelocity : before + impulse;
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "lunge_attempt_accepted")
                .String("operation_id", operationId)
                .String("item_prefab", AffinityState.ItemPrefab(weapon))
                .Number("force", appliedForce)
                .Number("velocity_before_x", before.x)
                .Number("velocity_before_y", before.y)
                .Number("velocity_before_z", before.z)
                .Number("impulse_x", impulse.x)
                .Number("impulse_y", impulse.y)
                .Number("impulse_z", impulse.z)
                .Number("velocity_after_x", after.x)
                .Number("velocity_after_y", after.y)
                .Number("velocity_after_z", after.z));
    }

    private static string Validate(
        Player? player,
        Humanoid character,
        Attack attack,
        LungeSwingState state)
    {
        if (player == null || character != player) return "wrong_player";
        bool sameWeapon = ReferenceEquals(player.GetCurrentWeapon(), state.Weapon)
            && ReferenceEquals(attack.GetWeapon(), state.Weapon);
        bool hasLunge = AffinityState.Load(state.Weapon, "lunge_attempt") == AffinityLoadResult.Lunge;
        return AffinityRules.ResolveLunge(
            player.IsOwner(),
            sameWeapon,
            hasLunge,
            player.IsOnGround(),
            player.IsSwimming(),
            player.IsFlying(),
            player.IsAttached());
    }

    private static bool IsMeleeAttack(Attack.AttackType attackType)
    {
        return attackType == Attack.AttackType.Horizontal
            || attackType == Attack.AttackType.Vertical
            || attackType == Attack.AttackType.Area;
    }

    private static void EmitRejected(
        string operationId,
        ItemDrop.ItemData? weapon,
        string reason)
    {
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "lunge_attempt_rejected")
                .String("operation_id", operationId)
                .String("item_prefab", AffinityState.ItemPrefab(weapon))
                .String("reason", reason));
    }
}
