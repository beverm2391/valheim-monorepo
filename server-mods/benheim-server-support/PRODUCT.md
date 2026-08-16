# Benheim Server Support

Benheim Server Support owns small production rules that require one authority
for the whole dedicated-server session. It is not a general RPC framework and
does not own test or administrator commands.

## In Development

The first candidate owns one global Put Away lease. A compatible client must
receive the lease before it scans chests or starts Valheim's native **Stack
all** flow. The server grants one connected peer and rejects every overlapping
request as busy. The client releases the lease when Put Away finishes, cancels,
or times out. The server also releases it when the owning peer disconnects.

The lease does not inspect items, containers, distance, access, capacity, or
ownership. Valheim still decides each chest transfer after the lease grant.
The lease has no retry, persistence, transaction journal, or inventory recovery
behavior.

This candidate is Benheim Server Support `0.1.0`. It is not deployed or
runtime-proven yet. The owning [Inventory product](../../mods/benheim-qol/src/Inventory/PRODUCT.md)
defines Put Away's player-visible behavior and acceptance boundary.
