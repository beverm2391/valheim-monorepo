# Benheim Server Support

Benheim Server Support owns Put Away coordination that requires the dedicated
server. It does not own test or administrator commands.

## Current Behavior

Deployed Benheim Server Support `0.1.0` grants one global Put Away lease. A
compatible client must receive the lease before it scans chests. The server
grants one connected peer and rejects every overlapping request as busy. The
client releases the lease when Put Away finishes, cancels, or times out. The
server also releases it when the owning peer disconnects.

The lease does not inspect items, containers, distance, access, capacity, or
ownership. The three-client test confirmed that the server rejects a
simultaneous Put Away before the losing client scans or moves items.

## In Development

The next candidate implements the shared owner-authoritative Put Away protocol.
The server correlates and routes each immutable deposit request to the chest's
current owner. That owner validates and changes the authoritative inventory.
The requester restores rejected remainders from the accepted result. The server
does not inspect or mutate chest contents itself.

The owner-routing adaptation passes automated checks but is not deployed or
gameplay-proven. The owning
[Inventory product](../../mods/benheim-qol/src/Inventory/PRODUCT.md) defines Put
Away's player-visible behavior and acceptance boundary.

The [shared protocol](../../shared/benheim-inventory-protocol/PROTOCOL.md) owns
the transaction runtime and typed-event lifecycle. Automated source and build
gates cover the server boundary.
