# Valheim Dev development workflow

Read [PRODUCT.md](PRODUCT.md) for the product promise and [SPEC.md](SPEC.md) for
the technical contract before changing Valheim Dev. Keep tool schemas,
bridge behavior, tests, and the spec consistent. Do not copy product behavior
or technical contracts into this file.

Read [COMMON_OPERATIONS.md](COMMON_OPERATIONS.md) before a live operation. Use
`run_once` when the entrypoint can finish. Use installed code only when behavior
must keep running after `Run()` returns. Check the current decompiled Valheim
source when an example does not cover the API you need.

The MCP server is repository-scoped through `../../.codex/config.toml`.
`launch.sh` locates `tools/valheim-dev` from the launcher's own path. It accepts
only the `VALHEIM_DEV_ROOT` and `VALHEIM_GAME_DIR` environment overrides. Keep
registration independent of the repository's absolute path. After changing the
launcher, config, protocol, or tool list, verify registration in a fresh Codex
process.

The scoped `package.json` and `package-lock.json` own the exact MCP SDK
dependency versions. Restore those dependencies before running the focused MCP
test or checking registration in a fresh process:

```bash
safe npm ci --prefix tools/valheim-dev
```

Run the focused MCP test with:

```bash
safe node --test tools/valheim-dev/server.test.mjs
```

Run the focused in-process bridge test with:

```bash
safe client-mods/benheim/tests/valheim-dev-runtime-test.sh
```

The bridge test compiles the production `DiagnosticEvent` implementation. Do not
reproduce its fields or envelope rules in a test double.

After changing either the MCP server or the in-process bridge, run the
canonical Benheim verification:

```bash
safe client-mods/benheim/scripts/verify.sh
```

The focused suites must prove:

- exact build and session identity;
- main-thread execution;
- bounded compilation and transport;
- persistent ledger records;
- structured inputs and results;
- compact labeled history with duration;
- warnings and errors observed since a run, with repeated messages collapsed;
- one-time commands for observation and action;
- installed-code persistence across Lab off and removal after Lab is re-enabled
  in the same world;
- cleanup when the tracked world ends;
- installed-change replacement and removal;
- cleanup uncertainty;
- concise MCP responses with complete ledger records;
- the Zod tool schemas and the official client-to-server transport.

When Valheim is not running, use an equivalent runtime test as a stand-in for
changes that a player would normally see. Behavior that depends on the live
Unity runtime still requires an authorized live inspection.

Unless the task explicitly includes a listed action, do not:

- install, launch, quit, or restart Valheim;
- enter a world or enable Lab mode;
- run a live operation.

Package a private-test build only after focused tests, canonical verification,
and the required independent review pass. Packaging does not authorize
installation.
