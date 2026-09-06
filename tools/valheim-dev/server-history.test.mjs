import assert from "node:assert/strict";
import { appendFile, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import test from "node:test";

import { createService } from "./server.mjs";
import {
  SOURCE, bridgeIdentity, operationResponse, startBridge, temporaryRoot, writeDescriptor,
} from "./test-helpers.mjs";

test("structured inputs, compact history, and time-correlated logs compose through the existing tools", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const logPath = join(fixture.root, "LogOutput.log");
  await writeFile(logPath, "[Info   : Unity Log] before run\n");
  let descriptor;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { active_changes: [] });
    assert.deepEqual(JSON.parse(request.input_json), { item: "Wood", amount: 2 });
    await appendFile(logPath,
      "[Warning: Unity Log] repeated warning\n"
      + "[Warning: Unity Log] repeated warning\n"
      + "[Error  : Benheim] callback failed\n");
    return operationResponse(descriptor, request, { result: "{\"given\":2}" });
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    logPath,
    compilerRunner: async ({ assemblyPath }) => {
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
    },
  });
  const record = await service.call("run_once", {
    label: "Give two wood",
    source: SOURCE,
    inputs: { item: "Wood", amount: 2 },
  });
  await appendFile(logPath, "[Warning: Unity Log] repeated warning\n");

  const history = await service.call("read_ledger", { limit: 10 });
  assert.equal(history.runs.length, 1);
  assert.equal(history.runs[0].label, "Give two wood");
  assert.equal(history.runs[0].outcome, "succeeded");
  assert.deepEqual(history.runs[0].result, { given: 2 });
  assert.equal(Number.isInteger(history.runs[0].duration_ms), true);

  const detail = await service.call("read_ledger", { operation_id: record.operation_id });
  assert.deepEqual(detail.run.result, { given: 2 });
  assert.equal(detail.logs_since_run.warning_count, 3);
  assert.equal(detail.logs_since_run.error_count, 1);
  assert.deepEqual(detail.logs_since_run.entries, [
    { level: "warning", source: "Unity Log", message: "repeated warning", count: 3 },
    { level: "error", source: "Benheim", message: "callback failed", count: 1 },
  ]);
  assert.match(detail.logs_since_run.association, /does not prove causation/);
});
