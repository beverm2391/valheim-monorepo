import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { rm, unlink, writeFile } from "node:fs/promises";
import { join } from "node:path";
import test from "node:test";

import { createService, discoverCompiler, runCompiler } from "./server.mjs";
import {
  SOURCE,
  bridgeIdentity,
  buildOfflineLungeHarness,
  digest,
  executeOfflineVariant,
  installedDotnetReferenceSet,
  lungeVariantSource,
  startBridge,
  temporaryRoot,
  writeDescriptor,
} from "./test-helpers.mjs";

test("offline first-proof executes two real Lunge variants while preserving bridge and ledger correlation", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const compiler = await discoverCompiler("dotnet", null);
  const harness = await buildOfflineLungeHarness(fixture.root);
  const compilerReferences = [
    ...await installedDotnetReferenceSet(compiler.cscDll),
    harness.fixtureAssembly,
  ];
  let descriptor;
  const transported = [];
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true });
    const assembly = Buffer.from(request.assembly, "base64");
    assert.equal(assembly.subarray(0, 2).toString("ascii"), "MZ");
    const peOffset = assembly.readUInt32LE(0x3c);
    assert.equal(assembly.subarray(peOffset, peOffset + 4).toString("hex"), "50450000");
    assert.equal(request.evidence_timeout_ms, 30_000);
    const force = request.source.includes("12f") ? 12 : 18;
    const experimentAssembly = join(fixture.root, `transported-experiment-${force}.dll`);
    await writeFile(experimentAssembly, assembly);
    // This synthetic event proves only selection and ledger correlation. The
    // separate runner below is the evidence that Run and Cleanup executed.
    const evidence = JSON.stringify({
      domain: "OfflineBridge",
      event: "transport_correlated",
      force,
      execution_observed: false,
      operation_id: request.operation_id,
    });
    transported.push({ request, assembly, experimentAssembly, force, evidence });
    return bridgeIdentity(descriptor, {
      operation_id: request.operation_id,
      started_utc: new Date().toISOString(),
      finished_utc: new Date().toISOString(),
      result: `transported:${force}`,
      exception: null,
      cleanup_state: "cleaned",
      evidence_selected: true,
      evidence_exhaustive: false,
      evidence_events: [evidence],
    });
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port, { compiler_references: compilerReferences });
  const service = createService({
    root: fixture.root,
    compilerRunner: (arguments_) => runCompiler({ ...arguments_, compiler }),
  });
  const firstSource = lungeVariantSource(12);
  const secondSource = lungeVariantSource(18);
  const first = await service.call("apply_experiment", {
    source: firstSource,
    targets: { feature: "Lunge" },
    inputs: { force: 12 },
    evidence_events: ["OfflineBridge:transport_correlated"],
  });
  const second = await service.call("apply_experiment", {
    source: secondSource,
    targets: { feature: "Lunge" },
    inputs: { force: 18 },
    evidence_events: ["OfflineBridge:transport_correlated"],
  });

  assert.equal(first.state, "succeeded", first.compiler.stderr);
  assert.equal(second.state, "succeeded", second.compiler.stderr);
  assert.equal(transported.length, 2);
  assert.notEqual(first.operation_id, second.operation_id);
  assert.notEqual(first.source_sha256, second.source_sha256);
  assert.notEqual(first.artifact_sha256, second.artifact_sha256);
  assert.equal(first.source_sha256, digest(firstSource));
  assert.equal(second.source_sha256, digest(secondSource));
  for (const [index, record] of [first, second].entries()) {
    const transfer = transported[index];
    assert.equal(record.operation_id, transfer.request.operation_id);
    assert.equal(record.artifact_sha256, digest(transfer.assembly));
    assert.equal(record.cleanup_state, "cleaned");
    assert.equal(record.evidence_selected, true);
    assert.equal(record.evidence_exhaustive, false);
    assert.deepEqual(record.evidence_events, [transfer.evidence]);
    assert.equal(record.inputs.force, transfer.force);
  }
  const firstExecution = await executeOfflineVariant(harness, transported[0].experimentAssembly);
  const secondExecution = await executeOfflineVariant(harness, transported[1].experimentAssembly);
  assert.deepEqual(
    [firstExecution.fixtureName, firstExecution.before, firstExecution.afterRun, firstExecution.afterCleanup],
    ["BenheimQoL", 10, 12, 10],
  );
  assert.deepEqual(
    [secondExecution.fixtureName, secondExecution.before, secondExecution.afterRun, secondExecution.afterCleanup],
    ["BenheimQoL", 10, 18, 10],
  );
  assert.match(firstExecution.result, /previous=10; applied=True; force=12/);
  assert.match(secondExecution.result, /previous=10; applied=True; force=18/);
  const ledger = await service.call("read_ledger", { limit: 10 });
  assert.equal(ledger.records.length, 2);
  const recordsByOperation = new Map(ledger.records.map((record) => [record.operation_id, record]));
  assert.deepEqual(recordsByOperation.get(first.operation_id), first);
  assert.deepEqual(recordsByOperation.get(second.operation_id), second);
});

test("compile failures are terminal and persist full diagnostics without calling apply", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  const bridge = await startBridge(async () => bridgeIdentity(descriptor, { authorized: true }));
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    compilerRunner: async () => ({ code: 1, signal: null, stdout: "compiler banner", stderr: "CS1002: ; expected", timed_out: false, output_overflow: false }),
  });
  const record = await service.call("apply_experiment", { source: "broken exact source" });
  assert.equal(record.state, "compile_failed");
  assert.equal(record.terminal, true);
  assert.equal(record.compiler.exit_code, 1);
  assert.equal(record.compiler.stdout, "compiler banner");
  assert.equal(record.compiler.stderr, "CS1002: ; expected");
  assert.equal(bridge.requests.filter((request) => request.kind === "apply").length, 0);
  const persisted = await service.call("read_ledger", { operation_id: record.operation_id });
  assert.deepEqual(persisted.record, record);
});

test("runtime exceptions and restart-required cleanup remain explicit terminal records", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  let applyCount = 0;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true });
    applyCount += 1;
    return bridgeIdentity(descriptor, {
      operation_id: request.operation_id,
      started_utc: new Date().toISOString(),
      finished_utc: new Date().toISOString(),
      result: applyCount === 1 ? null : "changed",
      exception: applyCount === 1 ? "System.InvalidOperationException: boom" : null,
      cleanup_state: applyCount === 1 ? "cleaned" : "restart_required",
      evidence_selected: false,
      evidence_exhaustive: false,
      evidence_events: [],
    });
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    compilerRunner: async ({ assemblyPath }) => {
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
    },
  });
  const exception = await service.call("apply_experiment", { source: SOURCE });
  assert.equal(exception.state, "runtime_failed");
  assert.match(exception.exception, /boom/);
  const restart = await service.call("apply_experiment", { source: `${SOURCE}\n// second` });
  assert.equal(restart.state, "succeeded");
  assert.equal(restart.cleanup_state, "restart_required");
});

test("a response timeout leaves the experiment unresolved instead of inventing a final result", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const descriptor = await writeDescriptor(fixture.root, fixture.reference, 12345);
  let requestCount = 0;
  const service = createService({
    root: fixture.root,
    bridgeRequest: async (_descriptor, request) => {
      requestCount += 1;
      if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true });
      const error = new Error("bridge request timed out");
      error.code = "BRIDGE_TIMEOUT";
      throw error;
    },
    compilerRunner: async ({ assemblyPath }) => {
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
    },
  });
  const record = await service.call("apply_experiment", { source: SOURCE });
  assert.equal(requestCount, 2);
  assert.equal(record.state, "runtime_unresolved");
  assert.equal(record.terminal, false);
  assert.equal(record.result, null);
  assert.match(record.error, /final result is unknown/);
  const persisted = await service.call("read_ledger", { operation_id: record.operation_id });
  assert.deepEqual(persisted.record, record);
});

test("a runtime main-thread timeout also remains unresolved", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const descriptor = await writeDescriptor(fixture.root, fixture.reference, 12345);
  const service = createService({
    root: fixture.root,
    bridgeRequest: async (_descriptor, request) => request.kind === "status"
      ? bridgeIdentity(descriptor, { authorized: true })
      : bridgeIdentity(descriptor, { ok: false, error: "main_thread_timeout", authorized: true }),
    compilerRunner: async ({ assemblyPath }) => {
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
    },
  });
  const record = await service.call("apply_experiment", { source: SOURCE });
  assert.equal(record.state, "runtime_unresolved");
  assert.equal(record.terminal, false);
  assert.match(record.error, /final result is unknown/);
});

test("bridge identity mismatch is unauthorized and disconnects become terminal; ledger remains readable", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  const wrongBridge = await startBridge(async () => bridgeIdentity(descriptor, { generation: randomUUID() }));
  descriptor = await writeDescriptor(fixture.root, fixture.reference, wrongBridge.port);
  let service = createService({ root: fixture.root });
  assert.match((await service.call("lab_status")).error, /generation mismatch/);
  await wrongBridge.close();

  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true });
    throw new Error("simulated disconnect");
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  service = createService({
    root: fixture.root,
    compilerRunner: async ({ assemblyPath }) => {
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
    },
  });
  const failed = await service.call("apply_experiment", { source: SOURCE });
  assert.equal(failed.state, "runtime_failed");
  assert.match(failed.error, /bridge apply failed/);
  await unlink(join(fixture.root, "session.json"));
  const exact = await service.call("read_ledger", { operation_id: failed.operation_id });
  assert.equal(exact.record.operation_id, failed.operation_id);
  const listed = await service.call("read_ledger", { limit: 1 });
  assert.equal(listed.records.length, 1);
  assert.equal(listed.records[0].operation_id, failed.operation_id);
});
