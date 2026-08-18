# Benheim Server Support

Benheim Server Support owns Put Away coordination that requires the dedicated
server. It also owns the confirmed-kill ordering that Player Combat needs for
multiplayer kill chains. It does not own test or administrator commands.

## Current Behavior

Deployed Benheim Server Support `0.1.1` grants one global Put Away lease. A
compatible client must receive the lease before it scans chests. The server
grants one connected peer and rejects every overlapping request as busy. The
client releases the lease when Put Away finishes, cancels, or times out. The
server also releases it when the owning peer disconnects.

The lease does not inspect items, containers, distance, access, capacity, or
ownership. The three-client test confirmed that the server rejects a
simultaneous Put Away before the losing client scans or moves items.

## In Development

Version `0.1.1` also implements the shared owner-authoritative Put Away protocol.
The server correlates and routes each immutable deposit request to the chest's
current owner. That owner validates and changes the authoritative inventory.
The requester restores rejected remainders from the accepted result. The server
does not inspect or mutate chest contents itself.

Version `0.1.1` is deployed and its exact plugin load is runtime-confirmed. The
owner-routing adaptation passes automated checks but remains gameplay-unproven. The owning
[Inventory product](../../mods/benheim-qol/src/Inventory/PRODUCT.md) defines Put
Away's player-visible behavior and acceptance boundary.

The [shared protocol](../../shared/benheim-inventory-protocol/PROTOCOL.md) owns
the transaction runtime and typed-event lifecycle. Automated source and build
gates cover the server boundary.

Server Support accepts a direct Player lethal-hit report only from the
authenticated peer that is connected and currently owns the defeated
non-player Character. It validates that the victim is non-player and that the
reported killer Character belongs to a connected player. It rejects duplicate
victim identities. It assigns each accepted report one server time and an order
for that killer. Only the confirmed killer's client receives the confirmation.
Player Combat decides what the confirmed kill earns.

This first feed covers only direct Player lethal-hit reports. It does not infer
credit for damage over time, kills made by tames, turrets, traps, environmental
deaths, assists, or kill steals. It trusts Benheim's authenticated victim owner
as Valheim's damage authority. This feed does not provide hostile-client proof
or serve as an anti-cheat boundary. Automated checks pass for authority,
message format, duplicate handling, ordering, and the build. Multiplayer
gameplay remains unproven.

Server Support owns one non-persistent experimental chain for each confirmed
killer. Each qualifying kill adds one to that chain and moves its deadline to
ten seconds after the kill. Server Support produces an activation transition
for the `BERSERKER` tier after three qualifying kills and an escalation
transition for the `SLAUGHTERHOUSE` tier after six. Other qualifying kills
produce `Progressed` or `Refreshed` transitions without requesting another
activation presentation. Player Combat decides what those transitions earn.
Kills received together still advance the chain one at a time in server order.

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
killer's client. Automated checks also pass for qualification, thresholds, the
rolling window, expiry, and reset behavior.
