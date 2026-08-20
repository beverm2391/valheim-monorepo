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

KillChainTransitionMessage originalTransition = new KillChainTransitionMessage(
    killerId,
    KillChainTransitionKind.Activated,
    KillChainTier.Berserker,
    killCount: 6,
    serverSequence: 9L,
    serverTimeSeconds: 200d,
    expiresAtServerTimeSeconds: 230d);
ZPackage transitionBytes = KillAttributionProtocol.BuildChainTransition(originalTransition);
Assert(
    KillAttributionProtocol.TryReadChainTransition(
        new ZPackage(transitionBytes.GetArray()),
        out KillChainTransitionMessage transition)
    && transition.KillerId == killerId
    && transition.Kind == KillChainTransitionKind.Activated
    && transition.Tier == KillChainTier.Berserker
    && transition.KillCount == 6
    && transition.ServerSequence == 9L
    && transition.ServerTimeSeconds == 200d
    && transition.ExpiresAtServerTimeSeconds == 230d,
    "the typed chain transition should round-trip exactly");

ZPackage legacyV2Transition = new ZPackage();
legacyV2Transition.Write(2);
legacyV2Transition.Write(killerId);
legacyV2Transition.Write((int)KillChainTransitionKind.Activated);
legacyV2Transition.Write((int)KillChainTier.Berserker);
legacyV2Transition.Write(3);
legacyV2Transition.Write(8L);
legacyV2Transition.Write(190d);
legacyV2Transition.Write(200d);
Assert(
    !KillAttributionProtocol.TryReadChainTransition(
        new ZPackage(legacyV2Transition.GetArray()),
        out _),
    "the 6/12/30 chain contract must reject a legacy V2 transition");

KillChainTransitionMessage invalidTransition = new KillChainTransitionMessage(
    killerId,
    KillChainTransitionKind.Escalated,
    KillChainTier.Berserker,
    killCount: 12,
    serverSequence: 10L,
    serverTimeSeconds: 201d,
    expiresAtServerTimeSeconds: 211d);
Assert(
    !KillAttributionProtocol.TryReadChainTransition(
        new ZPackage(KillAttributionProtocol.BuildChainTransition(invalidTransition).GetArray()),
        out _),
    "the wire parser must reject a tier that contradicts the transition kind");

KillChainDeliveryCursor deliveryCursor = new KillChainDeliveryCursor();
Assert(
    deliveryCursor.TryAccept(KillChainTransitionKind.Progressed, 4L)
    && !deliveryCursor.TryAccept(KillChainTransitionKind.Progressed, 4L)
    && !deliveryCursor.TryAccept(KillChainTransitionKind.Activated, 3L)
    && deliveryCursor.TryAccept(KillChainTransitionKind.Activated, 5L)
    && deliveryCursor.TryAccept(KillChainTransitionKind.Expired, 5L)
    && !deliveryCursor.TryAccept(KillChainTransitionKind.Expired, 5L)
    && !deliveryCursor.TryAccept(KillChainTransitionKind.Refreshed, 5L),
    "the client cursor should reject replayed, stale, and duplicate-terminal transition facts");
deliveryCursor.Reset();
Assert(
    deliveryCursor.TryAccept(KillChainTransitionKind.Progressed, 1L),
    "a reset cursor should accept a fresh server chain");

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

Assert(
    VictimQualification.IsHostileCreature(
        Character.Faction.ForestMonsters,
        isBoss: false,
        isTamed: false,
        hasMonsterAi: true,
        isCanonicalBoar: false),
    "native hostile monster factions should qualify");
Assert(
    VictimQualification.IsHostileCreature(
        Character.Faction.Boss,
        isBoss: true,
        isTamed: false,
        hasMonsterAi: true,
        isCanonicalBoar: false),
    "native bosses should qualify even though IsMonsterFaction excludes Boss");
Assert(
    !VictimQualification.IsHostileCreature(
        Character.Faction.ForestMonsters,
        isBoss: false,
        isTamed: false,
        hasMonsterAi: false,
        isCanonicalBoar: false),
    "AnimalAI hunting creatures such as deer must not qualify even when Valheim gives them a monster faction");
Assert(
    !VictimQualification.IsHostileCreature(
        Character.Faction.ForestMonsters,
        isBoss: false,
        isTamed: false,
        hasMonsterAi: true,
        isCanonicalBoar: true),
    "the canonical passive Boar prefab must not qualify despite its MonsterAI and monster faction");
Assert(
    !VictimQualification.IsHostileCreature(
        Character.Faction.PlainsMonsters,
        isBoss: false,
        isTamed: true,
        hasMonsterAi: true,
        isCanonicalBoar: false),
    "a tamed creature must never qualify even when its prefab has a hostile faction");
Assert(
    !VictimQualification.IsHostileCreature(
        Character.Faction.Dverger,
        isBoss: false,
        isTamed: false,
        hasMonsterAi: true,
        isCanonicalBoar: false),
    "neutral native factions must not qualify");

KillChainState<string> chains = new KillChainState<string>();
List<KillChainTransition<string>> chain = new List<KillChainTransition<string>>();
double rollingKillTime = 100d;
for (int count = 1; count <= 13; count++)
{
    KillChainTransition<string> current = chains.Advance(
        "killer-a",
        count,
        rollingKillTime);
    chain.Add(current);
    Assert(
        current.KillCount == count
        && current.ExpiresAtServerTimeSeconds == rollingKillTime + 30d,
        $"qualifying kill {count} should reset the rolling deadline to thirty seconds");
    rollingKillTime += 29d;
}

Assert(
    chain.GetRange(0, 5).TrueForAll(
        item => item.Kind == KillChainTransitionKind.Progressed
            && item.Tier == KillChainTier.None)
    && chain[5].Kind == KillChainTransitionKind.Activated
    && chain[5].Tier == KillChainTier.Berserker,
    "kills one through five progress and kill six activates BERSERKER");
Assert(
    chain.GetRange(6, 5).TrueForAll(
        item => item.Kind == KillChainTransitionKind.Refreshed
            && item.Tier == KillChainTier.Berserker)
    && chain[11].Kind == KillChainTransitionKind.Escalated
    && chain[11].Tier == KillChainTier.Slaughterhouse
    && chain[12].Kind == KillChainTransitionKind.Refreshed
    && chain[12].Tier == KillChainTier.Slaughterhouse,
    "kills seven through eleven refresh BERSERKER; twelve escalates and later kills refresh SLAUGHTERHOUSE");

KillChainTransition<string> otherKiller = chains.Advance("killer-b", 1L, 448d);
Assert(
    otherKiller.KillCount == 1 && otherKiller.Tier == KillChainTier.None,
    "chains must remain individual per killer");

List<KillChainTransition<string>> expired = new List<KillChainTransition<string>>();
chains.CollectExpired(477.999d, expired);
Assert(expired.Count == 0, "a chain should remain active before its deadline");
chains.CollectExpired(478d, expired);
Assert(
    expired.Count == 2
    && expired.TrueForAll(item => item.Kind == KillChainTransitionKind.Expired)
    && expired.TrueForAll(item => item.KillCount == 0),
    "thirty seconds without another qualifying kill should expire every due chain");
Assert(
    chains.Advance("killer-a", 14L, 478d).KillCount == 1,
    "a kill at or after the deadline should begin a new chain");

chains.RemoveKiller("killer-a");
Assert(
    chains.Advance("killer-a", 15L, 480d).KillCount == 1,
    "death should clear the active server chain before acknowledgment");
chains.Advance("killer-b", 2L, 480d);
chains.RemoveKiller("killer-a");
Assert(
    chains.Advance("killer-a", 16L, 481d).KillCount == 1
    && chains.Advance("killer-b", 3L, 481d).KillCount == 2,
    "disconnect should clear only the disconnected killer's chain");
chains.Reset();
Assert(
    chains.Advance("killer-b", 4L, 482d).KillCount == 1,
    "world or plugin reset should clear every chain");

int transportSends = 0;
Assert(
    !KillAttributionRpcAttempt.TrySend(
        isConnected: false,
        () => transportSends++,
        out string disconnectedFailure)
    && transportSends == 0
    && disconnectedFailure == "rpc_disconnected",
    "a disconnected Valheim RPC must fail before its silent no-op Invoke can discard the transition");
Assert(
    !KillAttributionRpcAttempt.TrySend(
        isConnected: true,
        () => throw new InvalidOperationException(),
        out string exceptionFailure)
    && exceptionFailure == "delivery_failed_InvalidOperationException",
    "a thrown send must remain an explicit failed invocation");
Assert(
    KillAttributionRpcAttempt.TrySend(
        isConnected: true,
        () => transportSends++,
        out string sentFailure)
    && transportSends == 1
    && sentFailure == string.Empty,
    "a connected successful send should be the only transport path marked delivered");

KillAttributionCapabilityRetry capabilityRetry =
    new KillAttributionCapabilityRetry(timeoutSeconds: 5f, retryIntervalSeconds: 1f);
Assert(
    !capabilityRetry.TryBeginAttempt(20f, out _),
    "capability requests must not start before Valheim exposes the current server RPC");
capabilityRetry.Begin(20f);
Assert(
    capabilityRetry.TryBeginAttempt(20f, out int firstAttempt)
    && firstAttempt == 1
    && !capabilityRetry.TryBeginAttempt(20.5f, out _)
    && capabilityRetry.TryBeginAttempt(21f, out int secondAttempt)
    && secondAttempt == 2,
    "capability discovery should send immediately and retry at the bounded cadence");
Assert(
    !capabilityRetry.HasTimedOut(24.999f)
    && capabilityRetry.HasTimedOut(25f),
    "the warning boundary should begin five seconds after the current server RPC is established");
capabilityRetry.Finish();
Assert(
    !capabilityRetry.TryBeginAttempt(26f, out _)
    && !capabilityRetry.HasTimedOut(26f),
    "an accepted or explicitly incompatible response should stop retries and timeout replacement");
capabilityRetry.Reset();
Assert(
    !capabilityRetry.Started && !capabilityRetry.Finished && capabilityRetry.Attempts == 0,
    "disconnect should clear all capability retry state");

Console.WriteLine("Kill attribution authority and server ordering checks passed");
