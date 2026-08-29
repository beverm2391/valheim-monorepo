using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.ShipSprint;

// The controller broadcasts only changes and a bounded heartbeat for the native
// logical Run control. Every compatible peer caches that transient state, while
// the current ship owner alone validates and consumes it for physics.
internal static class ShipSprintRuntime
{
    private const string RequestRpc = "Benheim_ShipSprint_Request";
    private static readonly FieldInfo NetViewField = AccessTools.Field(typeof(Ship), "m_nview");
    private static readonly MethodInfo GetUserMethod = AccessTools.Method(typeof(ShipControlls), "GetUser");
    private static readonly Dictionary<int, ShipState> States = new Dictionary<int, ShipState>();

    internal static void RegisterNetwork()
    {
        ZRoutedRpc.instance.Register<ZDOID, long, bool>(RequestRpc, ReceiveRequest);
    }

    internal static void SampleLocalControl(ShipControlls controls, bool requested)
    {
        Ship ship = controls.m_ship;
        if (Player.m_localPlayer == null || Player.m_localPlayer.GetControlledShip() != ship)
        {
            return;
        }

        float now = Time.unscaledTime;
        ShipState state = StateFor(ship);
        if (!state.RequestCadence.ShouldSend(requested, now))
        {
            return;
        }

        SendRequest(ship, Player.m_localPlayer.GetPlayerID(), requested);
    }

    internal static void StopLocalControl(ShipControlls controls)
    {
        Ship ship = controls.m_ship;
        ShipState state = StateFor(ship);
        long playerId = Player.m_localPlayer == null ? 0L : Player.m_localPlayer.GetPlayerID();
        SendRequest(ship, playerId, requested: false);
        state.RequestCadence.Reset();
        ShipSprintHud.Hide(ship);
    }

    internal static bool IsLocalRequestActive(Ship ship)
    {
        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null
            || !States.TryGetValue(ship.GetInstanceID(), out ShipState? state))
        {
            return false;
        }

        return ShipSprintRules.IsAuthenticatedLocalRequest(
            localPlayer.GetControlledShip() == ship,
            state.RequestState.Requested,
            localPlayer.GetPlayerID(),
            state.RequestState.PlayerId,
            localPlayer.GetOwner(),
            state.RequestState.PeerId,
            ship.GetSpeedSetting());
    }

    internal static Vector3 GaugeVelocity(Ship ship, Rigidbody body)
    {
        ZNetView? view = View(ship);
        if (view?.IsValid() != true)
        {
            return Vector3.zero;
        }

        if (view.IsOwner())
        {
            return body.linearVelocity;
        }

        // ZSyncTransform.OwnerSync publishes the physics owner's world
        // velocity here. A non-owner Rigidbody is interpolation state and is
        // not the authoritative measurement for the helmsman's readout.
        ZDO? zdo = view.GetZDO();
        return zdo == null
            ? Vector3.zero
            : zdo.GetVec3(ZDOVars.s_velHash, Vector3.zero);
    }

    internal static ShipSprintPhysicsScope BeginPhysics(Ship ship)
    {
        ShipState state = StateFor(ship);
        ShipSprintDecision decision = Decide(ship);
        ShipSprintOutcome? outcome = state.Observation.Observe(
            decision.Active,
            Time.unscaledTime,
            ship.GetSpeed(),
            ShipType(ship),
            ThrottleName(ship.GetSpeedSetting()),
            decision.Reason,
            Diagnostics.NewOperationId);
        Emit(outcome);

        float nativePaddleForce = ship.m_backwardForce;
        bool scalePaddle = decision.Active && ship.GetSpeedSetting() == Ship.Speed.Slow;
        if (scalePaddle)
        {
            ship.m_backwardForce = nativePaddleForce * ShipSprintRules.ThrustMultiplier(decision.Active);
        }

        return new ShipSprintPhysicsScope(nativePaddleForce, scalePaddle);
    }

    internal static void EndPhysics(Ship ship, ShipSprintPhysicsScope scope)
    {
        if (scope.PaddleScaled)
        {
            ship.m_backwardForce = scope.NativePaddleForce;
        }

        StateFor(ship).Observation.RecordPeak(ship.GetSpeed());
    }

    internal static void MultiplySailForce(Ship ship, float sailSize, ref Vector3 force)
    {
        if (sailSize > 0f && ShipSprintRules.IsSailThrottle(ship.GetSpeedSetting()) && Decide(ship).Active)
        {
            // GetSailForce has already calculated and stored the native smoothed
            // wind force. Multiply only the returned force for this physics step.
            force *= ShipSprintRules.ThrustMultiplier(shouldBoost: true);
        }
    }

    internal static void Teardown(Ship ship, string reason)
    {
        ShipSprintHud.Hide(ship);
        ClearLocalRequestIfControlling(ship);
        if (States.TryGetValue(ship.GetInstanceID(), out ShipState? state))
        {
            Emit(state.Observation.Finish(Time.unscaledTime, ship.GetSpeed(), reason));
            States.Remove(ship.GetInstanceID());
        }
    }

    internal static void Reset(string reason)
    {
        foreach (ShipState state in new List<ShipState>(States.Values))
        {
            Ship? ship = state.Ship;
            if (ship == null)
            {
                continue;
            }

            ClearLocalRequestIfControlling(ship);
            Emit(state.Observation.Finish(Time.unscaledTime, ship.GetSpeed(), reason));
        }

        States.Clear();
        ShipSprintHud.Destroy();
    }

    private static void SendRequest(Ship ship, long playerId, bool requested)
    {
        ZNetView? view = View(ship);
        if (playerId != 0L && view?.IsValid() == true)
        {
            // Broadcast transient input to every compatible peer that may
            // become the ship owner. A global RPC lets an unmodded dedicated
            // server forward it without logging a missing object handler. No
            // state enters the ship ZDO or save.
            ZRoutedRpc.instance.InvokeRoutedRPC(
                ZRoutedRpc.Everybody,
                RequestRpc,
                view.GetZDO().m_uid,
                playerId,
                requested);
        }
    }

    private static void ReceiveRequest(
        long sender,
        ZDOID shipId,
        long playerId,
        bool requested)
    {
        GameObject? shipObject = ZNetScene.instance?.FindInstance(shipId);
        Ship? ship = shipObject == null ? null : shipObject.GetComponent<Ship>();
        if (ship == null)
        {
            return;
        }

        ZNetView? view = View(ship);
        if (view?.IsValid() != true || !SenderControlsShip(ship, sender, playerId))
        {
            return;
        }

        StateFor(ship).RequestState.Update(playerId, sender, requested);
    }

    private static bool SenderControlsShip(Ship ship, long sender, long playerId)
    {
        ShipControlls controls = ship.m_shipControlls;
        if (controls == null || !controls.HaveValidUser())
        {
            return false;
        }

        long currentUser = Convert.ToInt64(GetUserMethod.Invoke(controls, null));
        Player driver = Player.GetPlayer(playerId);
        bool controllerValid = driver != null && ship.IsPlayerInBoat(driver);
        long controllingPeer = driver == null ? 0L : driver.GetOwner();
        return ShipSprintRules.IsAuthorizedSender(
            currentUser,
            playerId,
            controllingPeer,
            sender,
            controllerValid);
    }

    private static ShipSprintDecision Decide(Ship ship)
    {
        ZNetView? view = View(ship);
        if (view?.IsValid() != true)
        {
            return ShipSprintDecision.Stopped("network_lost");
        }
        if (!view.IsOwner())
        {
            return StateFor(ship).RequestState.Decide(
                physicsOwner: false,
                controllerValid: false,
                ship.GetSpeedSetting());
        }

        ShipState state = StateFor(ship);
        bool controllerValid = !state.RequestState.Requested
            || SenderControlsShip(ship, state.RequestState.PeerId, state.RequestState.PlayerId);
        return state.RequestState.Decide(
            physicsOwner: true,
            controllerValid,
            ship.GetSpeedSetting());
    }

    private static void ClearLocalRequestIfControlling(Ship ship)
    {
        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer != null && localPlayer.GetControlledShip() == ship)
        {
            SendRequest(ship, localPlayer.GetPlayerID(), requested: false);
        }
    }

    private static void Emit(ShipSprintOutcome? outcome)
    {
        if (outcome != null)
        {
            Diagnostics.Emit(ShipSprintDiagnostics.CreateEvent(outcome));
        }
    }

    private static ShipState StateFor(Ship ship)
    {
        int id = ship.GetInstanceID();
        if (!States.TryGetValue(id, out ShipState? state))
        {
            state = new ShipState(ship);
            States.Add(id, state);
        }
        return state;
    }

    private static ZNetView? View(Ship ship) => NetViewField.GetValue(ship) as ZNetView;

    private static string ShipType(Ship ship)
    {
        string prefab = Utils.GetPrefabName(ship.gameObject);
        return string.IsNullOrEmpty(prefab) ? Diagnostics.Flatten(ship.gameObject.name) : prefab;
    }

    private static string ThrottleName(Ship.Speed speed) => speed switch
    {
        Ship.Speed.Slow => "slow",
        Ship.Speed.Half => "half",
        Ship.Speed.Full => "full",
        Ship.Speed.Back => "back",
        _ => "stop"
    };

    private sealed class ShipState
    {
        internal ShipState(Ship ship)
        {
            Ship = ship;
        }

        internal Ship Ship { get; }
        internal ShipSprintObservation Observation { get; } = new ShipSprintObservation();
        internal ShipSprintRequestCadence RequestCadence { get; } = new ShipSprintRequestCadence();
        internal ShipSprintRequestState RequestState { get; } = new ShipSprintRequestState();
    }
}

internal readonly struct ShipSprintPhysicsScope
{
    internal ShipSprintPhysicsScope(float nativePaddleForce, bool paddleScaled)
    {
        NativePaddleForce = nativePaddleForce;
        PaddleScaled = paddleScaled;
    }

    internal float NativePaddleForce { get; }
    internal bool PaddleScaled { get; }
}
