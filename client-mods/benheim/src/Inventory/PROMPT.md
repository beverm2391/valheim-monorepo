# Inventory development

Before you change Put Away, read [this directory's PRODUCT.md](PRODUCT.md) and
the [shared protocol](../../../../shared/benheim-inventory-protocol/PROTOCOL.md).

Item integrity is the optimization target. Line count and abstraction count
are not. Consolidation or deletion is allowed only when an executable
regression proves that it preserves owner authority, exact-count conservation,
request correlation, and peer convergence. Changing any of those guarantees
requires Ben's explicit product decision.

Run the protocol's stale-payload, lease-contention, and receipt checks before
`client-mods/benheim/scripts/verify.sh`.
