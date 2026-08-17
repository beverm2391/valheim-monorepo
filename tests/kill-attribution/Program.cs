using BenheimQoL.KillAttribution;
using BenheimServerSupport;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

ZDOID victimId = new ZDOID(10L, 1u);
ZDOID killerId = new ZDOID(20L, 2u);
string operationId = Guid.NewGuid().ToString("N");

ZPackage reportBytes = KillAttributionProtocol.BuildReport(operationId, victimId, killerId);
Assert(
    KillAttributionProtocol.TryReadReport(
        new ZPackage(reportBytes.GetArray()),
        out KillReport report)
    && report.OperationId == operationId
    && report.VictimId == victimId
    && report.KillerId == killerId,
    "the fixed report wire shape should round-trip exactly");

ZPackage reportWithTrailingData = KillAttributionProtocol.BuildReport(operationId, victimId, killerId);
reportWithTrailingData.Write(99);
Assert(
    !KillAttributionProtocol.TryReadReport(
        new ZPackage(reportWithTrailingData.GetArray()),
        out _),
    "the report parser must reject trailing fields from another protocol shape");

ZPackage invalidReport = KillAttributionProtocol.BuildReport(operationId, victimId, victimId);
Assert(
    !KillAttributionProtocol.TryReadReport(
        new ZPackage(invalidReport.GetArray()),
        out _),
    "the wire parser must reject a victim reported as its own killer");

ConfirmedKillMessage originalConfirmation = new ConfirmedKillMessage(
    operationId,
    victimId,
    killerId,
    12345,
    "Boar",
    3,
    victimIsBoss: false,
    victimIsTamed: true,
    new UnityEngine.Vector3(1f, 2f, 3f),
    7L,
    2048.5d);
ZPackage confirmationBytes = KillAttributionProtocol.BuildConfirmation(originalConfirmation);
Assert(
    KillAttributionProtocol.TryReadConfirmation(
        new ZPackage(confirmationBytes.GetArray()),
        out ConfirmedKillMessage confirmation)
    && confirmation.OperationId == operationId
    && confirmation.VictimId == victimId
    && confirmation.KillerId == killerId
    && confirmation.VictimPrefabHash == 12345
    && confirmation.VictimPrefabName == "Boar"
    && confirmation.VictimLevel == 3
    && !confirmation.VictimIsBoss
    && confirmation.VictimIsTamed
    && confirmation.Position.x == 1f
    && confirmation.ServerSequence == 7L
    && confirmation.ServerTimeSeconds == 2048.5d,
    "the server-owned confirmation facts should round-trip exactly");

Player killer = new Player
{
    Owner = true,
    PlayerCharacter = true,
    Health = 100f,
    Id = killerId
};
Character victim = new Character
{
    Owner = true,
    PlayerCharacter = false,
    Health = 25f,
    Id = victimId
};

LethalHitObservation direct = LethalHitObservation.Capture(
    victim,
    new HitData { Attacker = killer });
Assert(direct.Eligible, "owner-accepted direct Player damage should be eligible");
Assert(!direct.BecameLethal(victim), "nonlethal damage must not confirm a kill");
victim.Health = 0f;
Assert(direct.BecameLethal(victim), "the captured direct hit should confirm only after health crosses zero");

victim.Health = 25f;
victim.Owner = false;
Assert(
    !LethalHitObservation.Capture(victim, new HitData { Attacker = killer }).Eligible,
    "a non-owner observer must not report a kill");

victim.Owner = true;
Assert(
    !LethalHitObservation.Capture(victim, new HitData()).Eligible,
    "attackerless DOT or environmental damage must not report a direct Player kill");
Assert(
    !LethalHitObservation.Capture(victim, new HitData { Attacker = new Character() }).Eligible,
    "non-Player attackers must not report a direct Player kill");

victim.PlayerCharacter = true;
Assert(
    !LethalHitObservation.Capture(victim, new HitData { Attacker = killer }).Eligible,
    "Player victims are outside the confirmed non-player feed");

victim.PlayerCharacter = false;
victim.Health = 0f;
Assert(
    !LethalHitObservation.Capture(victim, new HitData { Attacker = killer }).Eligible,
    "an already-dead victim must not produce another candidate");

ConfirmedKillState<string, string> state = new ConfirmedKillState<string, string>(2);
Assert(state.TryConfirm("victim-a", "killer-a", out long first) && first == 1L,
    "the first confirmed kill should have sequence one");
Assert(!state.TryConfirm("victim-a", "killer-a", out long duplicate) && duplicate == 0L,
    "the same victim must not increment a chain twice");
Assert(state.TryConfirm("victim-b", "killer-a", out long second) && second == 2L,
    "one killer's server order should increase monotonically");
Assert(state.TryConfirm("victim-c", "killer-b", out long other) && other == 1L,
    "different killers should have independent server order");
Assert(state.TryConfirm("victim-a", "killer-a", out long afterEviction) && afterEviction == 3L,
    "bounded duplicate memory may accept an old evicted identity without resetting killer order");

Assert(state.TryConfirm("victim-failed", "killer-a", out long failedDelivery) && failedDelivery == 4L,
    "the server should reserve one order before attempting delivery");
state.ReleaseFailedDelivery("victim-failed");
Assert(state.TryConfirm("victim-failed", "killer-a", out long replayedDelivery) && replayedDelivery == 5L,
    "a failed delivery must release victim dedupe so a replay can succeed");

state.RemoveKiller("killer-a");
Assert(state.TryConfirm("victim-d", "killer-a", out long afterDisconnect) && afterDisconnect == 1L,
    "disconnect should reset the ephemeral per-killer sequence");

state.Reset();
Assert(state.TryConfirm("victim-d", "killer-a", out long afterReset) && afterReset == 1L,
    "world teardown should clear duplicate and sequence state");

Console.WriteLine("Kill attribution authority and server ordering checks passed");
