import assert from "node:assert/strict";
import { rm, unlink, writeFile } from "node:fs/promises";
import { join } from "node:path";
import test from "node:test";

import { createService, discoverCompiler, runCompiler } from "./server.mjs";
import { MAX_EVIDENCE_BYTES } from "./constants.mjs";
import {
  CHANGE_SOURCE, SOURCE, bridgeIdentity, buildOfflineIconHarness, digest,
  executeOfflineVariant, iconVariantSource, installedDotnetReferenceSet,
  managedChange, startBridge, temporaryRoot, writeDescriptor,
} from "./test-helpers.mjs";

function operationResponse(descriptor, request, extra = {}) {
  return bridgeIdentity(descriptor, {
    authorized: true,
    action: request.kind,
    operation_id: request.operation_id,
    change_id: request.change_id ?? "",
    started_utc: "2026-09-04T00:00:00.000Z",
    finished_utc: "2026-09-04T00:00:00.010Z",
    result: "ok",
    exception: null,
    cleanup_state: request.kind === "install_change" ? "active" : "not_applicable",
    previous_change_preserved: false,
    evidence_selected: request.evidence_events?.length > 0,
    evidence_exhaustive: false,
    evidence_truncated: false,
    dropped_evidence_events: 0,
    evidence_events: [],
    active_changes: [],
    ...extra,
  });
}

test("offline first proof compiles and transports two managed Affinity icon variants", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const compiler = await discoverCompiler("dotnet", null);
  const harness = await buildOfflineIconHarness(fixture.root);
  const references = [...await installedDotnetReferenceSet(compiler.cscDll), harness.fixtureAssembly];
  let descriptor;
  let active = [];
  const transported = [];
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true, active_changes: active });
    assert.equal(request.kind, "install_change");
    assert.equal(request.change_id, "affinity.weapon-icon");
    assert.equal(request.entry_type, "ValheimDevChange");
    const assembly = Buffer.from(request.assembly, "base64");
    assert.equal(assembly.subarray(0, 2).toString("ascii"), "MZ");
    const variant = request.source.includes("pulse-a") ? "pulse-a" : "pulse-b";
    const assemblyPath = join(fixture.root, `transported-${variant}.dll`);
    await writeFile(assemblyPath, assembly);
    transported.push({ request, assembly, assemblyPath, variant });
    active = [{
      change_id: request.change_id, operation_id: request.operation_id,
      source_sha256: request.source_sha256, assembly_sha256: request.assembly_sha256,
      installed_utc: new Date().toISOString(), result: variant, cleanup_state: "active",
    }];
    return operationResponse(descriptor, request, { result: variant, active_changes: active });
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port, { compiler_references: references });
  const service = createService({
    root: fixture.root,
    compilerRunner: (arguments_) => runCompiler({ ...arguments_, compiler }),
  });
  const firstSource = iconVariantSource("pulse-a");
  const secondSource = iconVariantSource("pulse-b");
  const first = await service.call("install_change", {
    change_id: "affinity.weapon-icon", source: firstSource, targets: { surface: "inventory-and-hotbar" },
  });
  const second = await service.call("install_change", {
    change_id: "affinity.weapon-icon", source: secondSource, targets: { surface: "inventory-and-hotbar" },
  });
  assert.equal(first.state, "succeeded", first.compiler.stderr);
  assert.equal(second.state, "succeeded", second.compiler.stderr);
  assert.equal(first.cleanup_state, "active");
  assert.equal(second.cleanup_state, "active");
  assert.notEqual(first.source_sha256, second.source_sha256);
  assert.notEqual(first.artifact_sha256, second.artifact_sha256);
  assert.equal(first.source_sha256, digest(firstSource));
  assert.equal(second.source_sha256, digest(secondSource));
  assert.equal(second.previous_active_change.operation_id, first.operation_id);
  for (const [index, record] of [first, second].entries()) {
    assert.equal(record.operation_id, transported[index].request.operation_id);
    assert.equal(record.artifact_sha256, digest(transported[index].assembly));
    assert.equal(record.active_changes[0].change_id, "affinity.weapon-icon");
  }
  const firstExecution = await executeOfflineVariant(harness, transported[0].assemblyPath);
  const secondExecution = await executeOfflineVariant(harness, transported[1].assemblyPath);
  assert.deepEqual([firstExecution.before, firstExecution.afterRun, firstExecution.afterCleanup],
    ["baseline", "pulse-a", "baseline"]);
  assert.deepEqual([secondExecution.before, secondExecution.afterRun, secondExecution.afterCleanup],
    ["baseline", "pulse-b", "baseline"]);
  assert.match(secondExecution.result, /previous=baseline; variant=pulse-b/);
  const ledger = await service.call("read_ledger", { limit: 10 });
  assert.equal(ledger.records.length, 2);
});

test("compile failure preserves and records the working managed version", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const active = [managedChange()];
  let descriptor;
  const bridge = await startBridge(async () => bridgeIdentity(descriptor, { authorized: true, active_changes: active }));
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    compilerRunner: async () => ({
      code: 1, signal: null, stdout: "compiler banner", stderr: "CS1002: ; expected",
      timed_out: false, output_overflow: false,
    }),
  });
  const record = await service.call("install_change", {
    change_id: "affinity.weapon-icon", source: "broken exact source",
  });
  assert.equal(record.state, "compile_failed");
  assert.equal(record.previous_change_preserved, true);
  assert.deepEqual(record.previous_active_change, active[0]);
  assert.deepEqual(record.active_changes, active);
  assert.equal(bridge.requests.filter((request) => request.kind === "install_change").length, 0);
  assert.deepEqual((await service.call("read_ledger", { operation_id: record.operation_id })).record, record);
});

test("compile failure does not claim preservation after authorization changes during compilation", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const active = [managedChange()];
  let descriptor;
  const bridge = await startBridge(async () => bridgeIdentity(descriptor, { authorized: true, active_changes: active }));
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    compilerRunner: async () => {
      await unlink(join(fixture.root, "session.json"));
      return { code: 1, signal: null, stdout: "", stderr: "CS1002", timed_out: false, output_overflow: false };
    },
  });
  const record = await service.call("install_change", {
    change_id: "affinity.weapon-icon", source: "broken exact source",
  });
  assert.equal(record.state, "compile_failed");
  assert.equal(record.previous_change_preserved, null);
  assert.equal(record.active_changes, null);
});

test("inspection preserves exact source, hashes, selected evidence, and current active changes", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const active = [managedChange()];
  let descriptor;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true, active_changes: active });
    assert.equal(request.kind, "inspect");
    assert.equal(request.source, SOURCE);
    assert.equal(request.source_sha256, digest(SOURCE));
    assert.equal(request.entry_type, "ValheimDevInspection");
    const evidence = JSON.stringify({ domain: "Affinity", event: "icon_observed", visible: true });
    return operationResponse(descriptor, request, {
      result: "{\"target\":\"inventory.icon\"}", cleanup_state: "not_applicable",
      evidence_events: [evidence], evidence_truncated: true, dropped_evidence_events: 3,
      active_changes: active,
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
  const record = await service.call("inspect_runtime", {
    source: SOURCE, targets: { selector: "hovered-interface" }, evidence_events: ["Affinity:icon_observed"],
  });
  assert.equal(record.state, "succeeded");
  assert.equal(record.source_sha256, digest(SOURCE));
  assert.equal(record.cleanup_state, "not_applicable");
  assert.deepEqual(record.active_changes, active);
  assert.equal(record.evidence_events.length, 1);
  assert.equal(record.evidence_truncated, true);
  assert.equal(record.dropped_evidence_events, 3);
});

test("service accepts the exact serialized evidence-array byte boundary", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const empty = JSON.stringify({ domain: "Test", event: "boundary", blob: "" });
  const fixedBytes = Buffer.byteLength(JSON.stringify([empty]), "utf8");
  const evidence = JSON.stringify({
    domain: "Test", event: "boundary", blob: "x".repeat(MAX_EVIDENCE_BYTES - fixedBytes),
  });
  assert.equal(Buffer.byteLength(JSON.stringify([evidence]), "utf8"), MAX_EVIDENCE_BYTES);
  let descriptor;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { active_changes: [] });
    return operationResponse(descriptor, request, { evidence_events: [evidence] });
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
  const record = await service.call("inspect_runtime", {
    source: SOURCE, evidence_events: ["Test:boundary"],
  });
  assert.equal(record.state, "succeeded");
  assert.equal(record.evidence_events[0], evidence);
});

test("incomplete fields, wrong evidence, and inconsistent outcomes remain unresolved after dispatch", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const active = [managedChange()];
  let descriptor;
  let operationIndex = 0;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true, active_changes: active });
    operationIndex += 1;
    if (operationIndex === 1) {
      return operationResponse(descriptor, request, {
        active_changes: [{ change_id: "affinity.weapon-icon", operation_id: "incomplete", cleanup_state: "active" }],
      });
    }
    if (operationIndex === 2) {
      return operationResponse(descriptor, request, {
        evidence_events: [JSON.stringify({ domain: "Other", event: "unrequested" })],
      });
    }
    if (operationIndex === 3) {
      return operationResponse(descriptor, request, { ok: true, error: "impossible success" });
    }
    if (operationIndex === 4) return operationResponse(descriptor, request);
    if (operationIndex === 5) {
      return operationResponse(descriptor, request, { cleanup_state: "cleaned", active_changes: active });
    }
    if (operationIndex === 6) {
      return operationResponse(descriptor, request, {
        ok: false, error: "cleanup uncertain", cleanup_state: "restart_required", restart_required: false,
      });
    }
    if (operationIndex === 7) {
      return operationResponse(descriptor, request, { cleanup_state: "active" });
    }
    return operationResponse(descriptor, request, {
      started_utc: "2026-09-04T00:00:00.010Z", finished_utc: "2026-09-04T00:00:00.000Z",
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
  const installed = await service.call("install_change", {
    change_id: "affinity.weapon-icon", source: CHANGE_SOURCE,
  });
  const inspected = await service.call("inspect_runtime", {
    source: SOURCE, evidence_events: ["Affinity:selected"],
  });
  const removed = await service.call("remove_change", { change_id: "affinity.weapon-icon" });
  const missingInstalledRegistry = await service.call("install_change", {
    change_id: "affinity.weapon-icon", source: CHANGE_SOURCE,
  });
  const retainedRemovedRegistry = await service.call("remove_change", { change_id: "affinity.weapon-icon" });
  const missingTopLevelRestart = await service.call("install_change", {
    change_id: "affinity.weapon-icon", source: CHANGE_SOURCE,
  });
  const wrongCleanupState = await service.call("inspect_runtime", { source: SOURCE });
  const reversedTimestamps = await service.call("inspect_runtime", { source: SOURCE });
  for (const record of [
    installed, inspected, removed, missingInstalledRegistry, retainedRemovedRegistry,
    missingTopLevelRestart, wrongCleanupState, reversedTimestamps,
  ]) {
    assert.equal(record.state, "runtime_unresolved");
    assert.equal(record.terminal, false);
    assert.equal(record.previous_change_preserved, null);
    assert.equal(record.active_changes, null);
  }
});

test("runtime restoration and managed removal remain explicit ledger outcomes", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const active = [managedChange()];
  let descriptor;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true, active_changes: active });
    if (request.kind === "remove_change") {
      return operationResponse(descriptor, request, { ok: true, result: null, cleanup_state: "cleaned", active_changes: [] });
    }
    return operationResponse(descriptor, request, {
      ok: false, error: "entrypoint_exception", exception: "boom", cleanup_state: "restored",
      previous_change_preserved: true, active_changes: active,
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
  const failed = await service.call("install_change", { change_id: "affinity.weapon-icon", source: CHANGE_SOURCE });
  assert.equal(failed.state, "runtime_failed");
  assert.equal(failed.cleanup_state, "restored");
  assert.equal(failed.previous_change_preserved, true);
  const removed = await service.call("remove_change", { change_id: "affinity.weapon-icon" });
  assert.equal(removed.state, "succeeded");
  assert.equal(removed.cleanup_state, "cleaned");
  assert.deepEqual(removed.active_changes, []);
  assert.equal(removed.compiler.outcome, "not_applicable");
});

test("two service instances refuse stale installs and removals without touching the newer version", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  let active = [managedChange()];
  const bridgeRequest = async (_descriptor, request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { active_changes: active });
    const current = active.find((change) => change.change_id === request.change_id) ?? null;
    if (request.expected_operation_id !== (current?.operation_id ?? null)) {
      return operationResponse(descriptor, request, {
        ok: false, error: "stale_change_state", cleanup_state: "not_applicable", active_changes: active,
      });
    }
    if (request.kind === "install_change") {
      active = [managedChange(request.change_id, request.operation_id, {
        source_sha256: request.source_sha256, assembly_sha256: request.assembly_sha256,
      })];
      return operationResponse(descriptor, request, { active_changes: active });
    }
    active = [];
    return operationResponse(descriptor, request, { cleanup_state: "cleaned", active_changes: [] });
  };
  descriptor = await writeDescriptor(fixture.root, fixture.reference, 12345);
  const compiler = async ({ assemblyPath }) => {
    await writeFile(assemblyPath, "assembly");
    return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
  };
  let releaseCompile;
  let reportCompileStarted;
  const compileStarted = new Promise((resolve) => { reportCompileStarted = resolve; });
  const compileGate = new Promise((resolve) => { releaseCompile = resolve; });
  const slowInstallService = createService({
    root: fixture.root,
    bridgeRequest,
    compilerRunner: async (args) => {
      reportCompileStarted();
      await compileGate;
      return compiler(args);
    },
  });
  const fastService = createService({ root: fixture.root, bridgeRequest, compilerRunner: compiler });
  const slowInstall = slowInstallService.call("install_change", {
    change_id: "affinity.weapon-icon", source: CHANGE_SOURCE + " // slow",
  });
  await compileStarted;
  const newerInstall = await fastService.call("install_change", {
    change_id: "affinity.weapon-icon", source: CHANGE_SOURCE + " // newer",
  });
  releaseCompile();
  const staleInstall = await slowInstall;
  assert.equal(newerInstall.state, "succeeded");
  assert.equal(staleInstall.state, "runtime_failed");
  assert.equal(staleInstall.error, "stale_change_state");
  assert.equal(active[0].operation_id, newerInstall.operation_id);

  active = [managedChange()];
  let releaseRemove;
  let reportRemoveStarted;
  const removeStarted = new Promise((resolve) => { reportRemoveStarted = resolve; });
  const removeGate = new Promise((resolve) => { releaseRemove = resolve; });
  const slowRemoveService = createService({
    root: fixture.root,
    compilerRunner: compiler,
    bridgeRequest: async (currentDescriptor, request) => {
      if (request.kind !== "remove_change") return bridgeRequest(currentDescriptor, request);
      reportRemoveStarted();
      await removeGate;
      return bridgeRequest(currentDescriptor, request);
    },
  });
  const slowRemove = slowRemoveService.call("remove_change", { change_id: "affinity.weapon-icon" });
  await removeStarted;
  const installedDuringRemove = await fastService.call("install_change", {
    change_id: "affinity.weapon-icon", source: CHANGE_SOURCE + " // during remove",
  });
  releaseRemove();
  const staleRemove = await slowRemove;
  assert.equal(installedDuringRemove.state, "succeeded");
  assert.equal(staleRemove.state, "runtime_failed");
  assert.equal(staleRemove.error, "stale_change_state");
  assert.equal(active[0].operation_id, installedDuringRemove.operation_id);
});

test("bridge timeouts remain unresolved and the persistent ledger remains readable offline", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const descriptor = await writeDescriptor(fixture.root, fixture.reference, 12345);
  const service = createService({
    root: fixture.root,
    bridgeRequest: async (_descriptor, request) => {
      if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true, active_changes: [] });
      const error = new Error("bridge request timed out");
      error.code = "BRIDGE_TIMEOUT";
      throw error;
    },
    compilerRunner: async ({ assemblyPath }) => {
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
    },
  });
  const record = await service.call("inspect_runtime", { source: SOURCE });
  assert.equal(record.state, "runtime_unresolved");
  assert.equal(record.terminal, false);
  assert.equal(record.previous_change_preserved, null);
  await unlink(join(fixture.root, "session.json"));
  assert.deepEqual((await service.call("read_ledger", { operation_id: record.operation_id })).record, record);
});
