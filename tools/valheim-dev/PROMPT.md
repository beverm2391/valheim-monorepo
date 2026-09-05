# Valheim Dev development workflow

Read [PRODUCT.md](PRODUCT.md) for the product promise and [SPEC.md](SPEC.md) for
the technical contract before changing this workbench. Keep tool schemas,
bridge behavior, tests, and the spec consistent. Do not copy product behavior
or technical contracts into this file.

The MCP server is repository-scoped through `../../.codex/config.toml`.
`launch.sh` locates `tools/valheim-dev` from the launcher's own path. It accepts
only the `VALHEIM_DEV_ROOT` and `VALHEIM_GAME_DIR` environment overrides. Keep
registration independent of the repository's absolute path. After changing the
launcher, config, protocol, or tool list, verify registration in a fresh Codex
process.

Run the focused MCP proof with:

```bash
safe node --test tools/valheim-dev/server.test.mjs
```

Run the in-process bridge proof with:

```bash
safe client-mods/benheim/tests/valheim-dev-runtime-test.sh
```

After changing either the MCP server or the in-process bridge, run the
canonical Benheim verification:

```bash
safe client-mods/benheim/scripts/verify.sh
```

The focused suites must prove:

- exact build identity and authorization generation;
- main-thread execution;
- bounded compilation and transport;
- persistent ledger records;
- managed installation, replacement, and removal;
- cleanup uncertainty;
- the MCP schema.

When Valheim is not running, use an equivalent runtime test as a stand-in for
changes that a player would normally see. Actual Unity targets still require an
authorized live inspection.

Unless the task explicitly includes a listed action, do not:

- install, launch, quit, or restart Valheim;
- enter a world or enable Lab mode;
- run a live operation.

Package a private-test build only after focused tests, canonical verification,
and the required independent review pass. Packaging does not authorize
installation.
