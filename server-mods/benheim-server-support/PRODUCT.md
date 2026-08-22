# Benheim Server Support

Benheim Server Support owns Put Away coordination that requires the dedicated
server. It also owns the confirmed-kill ordering that Player Combat needs for
multiplayer kill chains. It does not own test or administrator commands.

## Current Behavior

The supported installer deployed Benheim Server Support `0.1.6`. The deployment
check confirmed its exact plugin load, hash, world, and readiness. The deployed
plugin grants one global Put Away lease. A compatible client must receive the
lease before it scans chests. The server grants one connected peer and rejects
every overlapping request as busy. The client releases the lease when Put Away
finishes, cancels, or times out. The server also releases it when the owning
peer disconnects.

Version `0.1.3` uses lease generation `v2` and transaction generation `v4`.
Put Away stops before chest scanning or item reservation unless every connected
peer has announced the current lease generation. Before each container
reservation, Server Support confirms that the set of connected peers has not
changed since it granted the lease. A change to that set causes validation to
fail. The lease remains with the holder until the holder releases it.

The lease does not inspect items, containers, distance, access, capacity, or
ownership. The server routes immutable deposit requests to the chest's current
owner and forwards owner results. The owner validates and changes the chest;
the server does not inspect or mutate chest contents. The three-client
`0.1.66` review proved owner mutation, result forwarding, exact requester
settlement, cleanup after receipt presentation, and immediate lease reuse. It
also proved that the server rejects simultaneous Put Away before the losing
client scans or moves items.

Kill Attribution V3 capability discovery and direct confirmed-kill delivery
passed multiplayer gameplay review with the active group. Matching Kill
Attribution V3 capability responses reached the active clients.
Server-confirmed kills reached the credited player's client. The Controls
warning presentation remains under review.

## In Development

Version `0.1.6` keeps the accepted Put Away protocol, lease generation `v2`,
transaction generation `v4`, and Kill Attribution V3 behavior from `0.1.4`.
It includes the server-side part of the bounded Put Away timing telemetry and
the schema-2 typed-diagnostics correction compiled from client-shared source.
Neither change affects protocol decisions or transaction progress.

Version `0.1.4` changes the experimental confirmed-kill chain to six kills for
BERSERKER, twelve kills for SLAUGHTERHOUSE, and a 30-second rolling deadline.
It advances Kill Attribution to V3 so capability discovery fails visibly when
the client and server use different chain rules. The legacy rules activate
BERSERKER at three kills and SLAUGHTERHOUSE at six kills, with a 10-second
deadline. Version `0.1.4` does not change the accepted Put Away protocol.
Diagnostic failures cannot interrupt result delivery or settlement. When a
requester disconnects, the server removes that requester's pending and
completed route entries.

The owning [Inventory product](../../mods/benheim-qol/src/Inventory/PRODUCT.md) defines
Put Away's player-visible behavior and acceptance boundary. The [shared
protocol](../../shared/benheim-inventory-protocol/PROTOCOL.md) owns cleanup
correlation, routing, the transaction runtime, and typed-event lifecycle.
Automated source and build gates cover the server boundary.

Version `0.1.2` introduced Kill Attribution V2 and the server-authoritative
confirmed-kill chain described below. In version `0.1.2`, the server sent one
capability response when the client connected. Version `0.1.3` replaced that
behavior with client capability requests and server responses. Version `0.1.4`
keeps that flow and advances the protocol to Kill Attribution V3. The server
validates each Kill Attribution V3 capability request and sends the matching
response over the same connection. The client retries its request for up to
five seconds.
Automated checks cover requests, responses, retries, and timeouts. See the root
[Gameplay Breakdown](../../PRODUCT.md#gameplay-breakdown) for its required
client version and compatibility boundary.

Server Support accepts a direct Player lethal-hit report only from the
authenticated peer that is connected and currently owns the defeated
non-player Character. It validates that the victim is non-player and that the
reported killer Character belongs to a connected player. It rejects duplicate
victim identities. It assigns each accepted report one server time and an order
for that killer. Only the confirmed killer's client receives the confirmation.
Player Combat decides what the confirmed kill earns.

Kill Attribution V3 covers only direct Player lethal-hit reports. It does not
infer credit for damage over time, kills by tames, turrets, traps,
environmental deaths, assists, or kill steals. It treats Benheim's
authenticated victim owner as Valheim's damage authority. The protocol does not
prove behavior against a hostile client and is not an anti-cheat boundary.
Automated checks cover authority, message format, duplicate handling, ordering,
and the build. Gameplay has proved direct confirmation delivery, but not every
qualification, rejection, ordering, or reset case.

Server Support keeps one non-persistent experimental chain for each confirmed
killer. Each qualifying kill increments the chain and resets the chain deadline
to 30 seconds after that kill. Six qualifying kills activate `BERSERKER`;
twelve qualifying kills escalate it to `SLAUGHTERHOUSE`. Kills seven through
eleven refresh `BERSERKER`, and kills after twelve refresh `SLAUGHTERHOUSE`
without replaying the activation presentation. Player Combat owns the bonuses
and presentation for both tiers. Kills received together advance one at a time
in server order.

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

Thirty seconds without another qualifying kill expires the chain. The killer's
death resets the chain. The killer's disconnect, a world reset, or plugin
teardown clears the chain. Server Support sends each transition only to that
killer's client. Automated checks pass for qualification, thresholds, the
rolling window, expiry, and reset behavior. Ben accepted BERSERKER's title and
native status-bar icon. Client events prove that its native effect
applied, appeared in the HUD, refreshed, and expired. The 6/12 thresholds,
30-second rolling chain, and SLAUGHTERHOUSE remain gameplay-unproven.

Kill Attribution V3 uses Valheim's reliable, ordered transport while the peer
remains connected. Benheim sends reports, confirmations, resets, and
transitions only over an active RPC connection. Disconnecting tears down the
peer and clears that peer's server chain.

After the killer dies, that client ignores chain transitions until the server
acknowledges the chain reset. Valheim's send order places the acknowledgment
after every pre-reset transition and before every post-reset transition.
Reconnecting ends the wait because the server creates new state for the new
peer.
