using System;
using System.Text.Json;
using BenheimQoL.Infrastructure;
using BenheimQoL.ShipSprint;

ExpectFalse(ShipSprintRules.ShouldBoost(true, true, true, Ship.Speed.Stop), "stopped throttle remains native");
ExpectFalse(ShipSprintRules.ShouldBoost(true, true, true, Ship.Speed.Back), "reverse remains native");
ExpectTrue(ShipSprintRules.ShouldBoost(true, true, true, Ship.Speed.Slow), "forward paddle boosts");
ExpectTrue(ShipSprintRules.ShouldBoost(true, true, true, Ship.Speed.Half), "half sail boosts");
ExpectTrue(ShipSprintRules.ShouldBoost(true, true, true, Ship.Speed.Full), "full sail boosts");
ExpectFalse(ShipSprintRules.ShouldBoost(false, true, true, Ship.Speed.Full), "released Run remains native");
ExpectFalse(ShipSprintRules.ShouldBoost(true, false, true, Ship.Speed.Full), "non-owner never applies physics");
ExpectFalse(ShipSprintRules.ShouldBoost(true, true, false, Ship.Speed.Full), "lost controller clears boost");
ExpectNear(3f, ShipSprintRules.ThrustMultiplier(true), "active thrust multiplier");
ExpectNear(1f, ShipSprintRules.ThrustMultiplier(false), "native thrust multiplier");
ExpectTrue(
    ShipSprintRules.IsAuthorizedSender(10L, 10L, 20L, 20L, true),
    "native controller's peer is authorized");
ExpectFalse(
    ShipSprintRules.IsAuthorizedSender(10L, 11L, 20L, 20L, true),
    "different player cannot request boost");
ExpectFalse(
    ShipSprintRules.IsAuthorizedSender(10L, 10L, 20L, 21L, true),
    "different peer cannot spoof the controller");
ExpectFalse(
    ShipSprintRules.IsAuthorizedSender(10L, 10L, 20L, 20L, false),
    "invalid native controller is rejected");
ExpectTrue(
    ShipSprintRules.IsAuthenticatedLocalRequest(
        true, true, 10L, 10L, 20L, 20L, Ship.Speed.Full),
    "accepted local request renders sprint state");
ExpectFalse(
    ShipSprintRules.IsAuthenticatedLocalRequest(
        true, true, 10L, 11L, 20L, 20L, Ship.Speed.Full),
    "another player's accepted request does not render local sprint state");
ExpectFalse(
    ShipSprintRules.IsAuthenticatedLocalRequest(
        true, true, 10L, 10L, 20L, 21L, Ship.Speed.Full),
    "another peer's accepted request does not render local sprint state");
ExpectFalse(
    ShipSprintRules.IsAuthenticatedLocalRequest(
        true, true, 10L, 10L, 20L, 20L, Ship.Speed.Back),
    "accepted request does not label native reverse as sprinting");

ShipSprintRequestCadence cadence = new ShipSprintRequestCadence();
ExpectTrue(cadence.ShouldSend(false, 1f), "first controller sample clears stale owner state");
ExpectFalse(cadence.ShouldSend(false, 1.1f), "unchanged release does not spam RPCs");
ExpectTrue(cadence.ShouldSend(true, 2f), "Run press sends immediately");
ExpectFalse(cadence.ShouldSend(true, 2.24f), "held Run waits for heartbeat interval");
ExpectTrue(cadence.ShouldSend(true, 2.25f), "held Run broadcasts a bounded heartbeat");
ExpectTrue(cadence.ShouldSend(false, 2.26f), "Run release sends immediately");
cadence.Reset();
ExpectTrue(cadence.ShouldSend(false, 3f), "new control lifecycle clears stale state again");

ShipSprintRequestState replicatedRequest = new ShipSprintRequestState();
replicatedRequest.Update(playerId: 10L, peerId: 20L, requested: true);
ShipSprintDecision activeOwner = replicatedRequest.Decide(true, true, Ship.Speed.Full);
ExpectTrue(activeOwner.Active, "validated physics owner applies boost");
ShipSprintDecision ownershipLost = replicatedRequest.Decide(false, true, Ship.Speed.Full);
ExpectFalse(ownershipLost.Active, "old owner stops applying physics immediately");
ExpectEqual("ownership_lost", ownershipLost.Reason, "old owner terminal reason");
ExpectTrue(
    replicatedRequest.Decide(true, true, Ship.Speed.Full).Active,
    "new owner consumes the already replicated transient request");
ExpectFalse(
    replicatedRequest.Decide(true, true, Ship.Speed.Back).Active,
    "replicated request never boosts reverse");

ShipSprintDecision controllerLost = replicatedRequest.Decide(true, false, Ship.Speed.Half);
ExpectFalse(controllerLost.Active, "control loss stops boost");
ExpectEqual("controller_lost", controllerLost.Reason, "control loss terminal reason");
ExpectFalse(replicatedRequest.Requested, "control loss clears transient peer state");
replicatedRequest.Update(playerId: 10L, peerId: 20L, requested: true);
replicatedRequest.Update(playerId: 10L, peerId: 20L, requested: false);
ExpectFalse(
    replicatedRequest.Decide(true, true, Ship.Speed.Full).Active,
    "release clears transient peer state");

ShipSprintObservation observation = new ShipSprintObservation();
int operation = 0;
Func<string> newOperation = () => $"sprint-{++operation}";
ExpectNull(
    observation.Observe(true, 10f, 2f, "Karve", "half", string.Empty, newOperation),
    "start has no log event");
ExpectNull(
    observation.Observe(true, 11f, 5f, "Karve", "half", string.Empty, newOperation),
    "active sample has no log event");
observation.RecordPeak(7f);
ShipSprintOutcome released = Require(
    observation.Observe(false, 12.5f, 6f, "Karve", "half", "released", newOperation),
    "release emits one terminal summary");
ExpectEqual("sprint-1", released.OperationId, "operation identity");
ExpectEqual("Karve", released.ShipType, "ship identity");
ExpectEqual("half", released.StartingThrottle, "starting throttle");
ExpectEqual("released", released.Reason, "terminal reason");
ExpectNear(2.5f, released.Duration, "duration");
ExpectNear(2f, released.StartingSpeed, "starting speed");
ExpectNear(7f, released.PeakSpeed, "peak speed");
ExpectNull(observation.Finish(13f, 8f, "duplicate"), "finished observation cannot emit twice");

DiagnosticEvent terminal = ShipSprintDiagnostics.CreateEvent(released);
terminal.Prepare(new DateTime(2026, 8, 22, 4, 5, 6, DateTimeKind.Utc), "ship-session", "candidate");
using JsonDocument json = JsonDocument.Parse(terminal.ToJsonLine());
JsonElement root = json.RootElement;
ExpectEqual("ShipSprint", root.GetProperty("domain").GetString(), "diagnostic domain");
ExpectEqual("ship_sprint_finished", root.GetProperty("event").GetString(), "diagnostic event");
ExpectEqual("terminal", root.GetProperty("operation_phase").GetString(), "terminal phase");
ExpectEqual("Karve", root.GetProperty("ship_type").GetString(), "diagnostic ship type");
ExpectNear(2.5f, root.GetProperty("duration").GetSingle(), "diagnostic duration");
ExpectNear(2f, root.GetProperty("starting_speed").GetSingle(), "diagnostic starting speed");
ExpectNear(7f, root.GetProperty("peak_speed").GetSingle(), "diagnostic peak speed");
ExpectNear(3f, root.GetProperty("thrust_multiplier").GetSingle(), "diagnostic tuning");

ExpectNear(3f, ShipSprintTuning.ThrustMultiplier, "first tuning candidate");

ExpectNear(5f, ShipSprintGaugeRules.PlanarSpeed(3f, 4f), "world-planar speed ignores vertical motion");
ExpectEqual("5.0 m/s", ShipSprintGaugeRules.Format(5f, sprintActive: false), "native speed label");
ExpectEqual(
    "5.0 m/s  <alpha=#A0>SPRINT</alpha>",
    ShipSprintGaugeRules.Format(5.04f, sprintActive: true),
    "active request adds restrained sprint state");
ExpectEqual("0.0 m/s", ShipSprintGaugeRules.Format(-1f, sprintActive: false), "speed never renders negative");

Console.WriteLine("Ship Sprint forward-throttle, lifecycle, and diagnostic checks passed");
return;

static ShipSprintOutcome Require(ShipSprintOutcome? outcome, string scenario)
{
    return outcome ?? throw new InvalidOperationException($"{scenario}: expected outcome");
}

static void ExpectNull(object? value, string scenario)
{
    if (value != null)
    {
        throw new InvalidOperationException($"{scenario}: expected no value");
    }
}

static void ExpectTrue(bool actual, string scenario)
{
    if (!actual)
    {
        throw new InvalidOperationException($"{scenario}: expected true");
    }
}

static void ExpectFalse(bool actual, string scenario)
{
    if (actual)
    {
        throw new InvalidOperationException($"{scenario}: expected false");
    }
}

static void ExpectNear(float expected, float actual, string scenario)
{
    if (Math.Abs(expected - actual) > 0.0001f)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}

static void ExpectEqual(string? expected, string? actual, string scenario)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}
