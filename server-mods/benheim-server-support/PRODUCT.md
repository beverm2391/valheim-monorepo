# Benheim Server Support

Benheim Server Support owns Put Away coordination that requires the dedicated
server. It also owns the confirmed-kill ordering that Player Combat needs for
multiplayer kill chains. It does not own test or administrator commands.

## Current Behavior

The supported installer deployed Benheim Server Support `0.1.2`. The deployment
check confirmed its exact plugin load, hash, world, and readiness. The deployed
plugin grants one global Put Away lease. A compatible client must receive the
lease before it scans chests. The server grants one connected peer and rejects
every overlapping request as busy. The client releases the lease when Put Away
finishes, cancels, or times out. The server also releases it when the owning
peer disconnects.

The lease does not inspect items, containers, distance, access, capacity, or
ownership. The three-client test confirmed that the server rejects a
simultaneous Put Away before the losing client scans or moves items.

## In Development

The deployed `0.1.2` routes immutable deposit requests to the chest's current
owner and forwards owner results. The owner validates and changes the chest;
the server does not inspect or mutate chest contents. Live `0.1.64` gameplay
proved owner mutation, result forwarding, and exact requester settlement. It
also showed that the old receipt-removal gate could retain the completed batch
and lease until disconnect.

The current source correction removes receipt cleanup from the completion gate.
It passes automated checks but is not deployed or gameplay-proven. The owning
[Inventory product](../../mods/benheim-qol/src/Inventory/PRODUCT.md) defines
Put Away's player-visible behavior and acceptance boundary. The [shared
protocol](../../shared/benheim-inventory-protocol/PROTOCOL.md) owns cleanup
correlation, routing, the transaction runtime, and typed-event lifecycle.
Automated source and build gates cover the server boundary.

Version `0.1.2` also implements Kill Attribution V2 and the server-authoritative
confirmed-kill chain described below. See the root
[Gameplay Breakdown](../../PRODUCT.md#gameplay-breakdown) for its required
client version and compatibility boundary.

Server Support accepts a direct Player lethal-hit report only from the
authenticated peer that is connected and currently owns the defeated
non-player Character. It validates that the victim is non-player and that the
reported killer Character belongs to a connected player. It rejects duplicate
victim identities. It assigns each accepted report one server time and an order
for that killer. Only the confirmed killer's client receives the confirmation.
Player Combat decides what the confirmed kill earns.

Kill Attribution V2 covers only direct Player lethal-hit reports. It does not
infer credit for damage over time, kills by tames, turrets, traps,
environmental deaths, assists, or kill steals. It treats Benheim's
authenticated victim owner as Valheim's damage authority. The protocol does not
prove behavior against a hostile client and is not an anti-cheat boundary.
Automated checks cover authority, message format, duplicate handling, ordering,
and the build. Multiplayer gameplay remains unproven.

Server Support keeps one non-persistent experimental chain for each confirmed
killer. Each qualifying kill increments the chain and resets its deadline to
ten seconds after that kill. Three qualifying kills activate `BERSERKER`; six
escalate it to `SLAUGHTERHOUSE`. Other qualifying kills extend or refresh the
chain without replaying the activation presentation. Player Combat owns the
bonuses and presentation for both tiers. Kills received together advance one at
a time in server order.

Server Support qualifies a victim only when its authoritative state is
untamed. A loaded boss qualifies. Any other creature must have native
`MonsterAI` and a native Valheim monster faction. The canonical Boar remains
excluded even though it has that same AI-and-faction combination. Deer do not
qualify because they use `AnimalAI`. Installed bird prefabs do not advance the
chain because they have no `Character`. Stars and level do not affect the count.
Boss status is an exception to the AI-and-faction requirement, but each boss
still adds only one. If the victim's prefab or `Character` data is missing, the
chain does not advance. This qualification rule does not treat a neutral
creature, such as an aggravated Dverger, as hostile.

Ten seconds without another qualifying kill expires the chain. The killer's
death resets the chain. The killer's disconnect, a world reset, or plugin
teardown clears the chain. Server Support sends each transition only to that
killer's client. Automated checks pass for qualification, thresholds, the
rolling window, expiry, and reset behavior. The `0.1.2` confirmed-kill chain
remains gameplay-unproven.

Kill Attribution V2 uses Valheim's reliable, ordered transport while the peer
remains connected. Benheim sends reports, confirmations, resets, and
transitions only over an active RPC connection. Disconnecting tears down the
peer and clears that peer's server chain.

After the killer dies, that client ignores chain transitions until the server
acknowledges the chain reset. Valheim's send order places the acknowledgment
after every pre-reset transition and before every post-reset transition.
Reconnecting ends the wait because the server creates new state for the new
peer.
