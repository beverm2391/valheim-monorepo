import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import test from "node:test";

import { createService } from "./server.mjs";
import { writeLedger } from "./ledger.mjs";
import {
  SOURCE, bridgeIdentity, operationResponse, startBridge, temporaryRoot, writeDescriptor,
} from "./test-helpers.mjs";

test("dependent commands require successful preparation in the same Lab authorization", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  let preparationFails = true;
  let compilations = 0;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor);
    return operationResponse(descriptor, request, preparationFails ? {
      ok: false, error: "entrypoint_exception", exception: "fixture preparation failed", result: null,
    } : {});
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    compilerRunner: async ({ assemblyPath }) => {
      compilations++;
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "" };
    },
  });
  const prepare = await service.call("run_once", { label: "Prepare fixture", source: SOURCE });
  assert.equal(prepare.state, "runtime_failed");
  const operations = () => bridge.requests.filter((request) => request.kind !== "status");
  const before = operations().length;
  const blocked = await service.call("run_once", {
    label: "Fire against fixture", source: SOURCE, requires_operation_id: prepare.operation_id,
  });
  assert.equal(blocked.state, "prerequisite_failed");
  assert.equal(blocked.requires_operation_id, prepare.operation_id);
  assert.match(blocked.error, /runtime_failed/);
  assert.equal(blocked.compiler.outcome, "not_run");
  assert.equal(compilations, 1);
  assert.equal(operations().length, before, "failed preparation must not dispatch the shot");
  assert.deepEqual((await service.call("read_ledger", { operation_id: blocked.operation_id })).details, blocked);

  preparationFails = false;
  const ready = await service.call("run_once", { label: "Prepare again", source: SOURCE });
  const shot = await service.call("run_once", {
    label: "Fire after successful preparation", source: SOURCE, requires_operation_id: ready.operation_id,
  });
  assert.equal(shot.state, "succeeded");
  assert.equal(compilations, 3);

  // Reuse the real successful record, changing only the boundary under test.
  const rejected = [
    ["missing", null],
    ["pending", { ...ready, state: "pending", terminal: false }],
    ["unresolved", { ...ready, state: "runtime_unresolved", terminal: false }],
    ["compile failure", { ...ready, state: "compile_failed" }],
    ["not terminal", { ...ready, terminal: false }],
    ["old session", { ...ready, session_id: "previous-authorization" }],
    ["wrong build", { ...ready, valheim_dev_sha256: "f".repeat(64) }],
    ["restart required", { ...ready, restart_required: true }],
    ["blocked predecessor", blocked],
  ];
  for (const [label, record] of rejected) {
    await t.test(label, async () => {
      const operationId = randomUUID();
      if (record) await writeLedger(join(fixture.root, "ledger"), { ...record, operation_id: operationId });
      const refused = await service.call("run_once", {
        label, source: SOURCE, requires_operation_id: operationId,
      });
      assert.equal(refused.state, "prerequisite_failed");
      assert.equal(refused.terminal, true);
      assert.equal(compilations, 3);
      assert.equal(operations().length, 3);
    });
  }
  const corruptId = randomUUID();
  await writeFile(join(fixture.root, "ledger", `${corruptId}.json`), "{broken");
  const corrupt = await service.call("run_once", {
    label: "Unreadable preparation record", source: SOURCE, requires_operation_id: corruptId,
  });
  assert.equal(corrupt.state, "prerequisite_failed");
  assert.match(corrupt.error, /could not be verified/);
  assert.equal(compilations, 3);
  assert.equal(operations().length, 3);
  await assert.rejects(service.call("run_once", {
    source: SOURCE, requires_operation_id: "../bad-id",
  }), /requires_operation_id/);
});
