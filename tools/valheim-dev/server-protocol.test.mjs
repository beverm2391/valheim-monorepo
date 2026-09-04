import assert from "node:assert/strict";
import { readFile, readdir, rm, writeFile } from "node:fs/promises";
import { basename, join } from "node:path";
import test from "node:test";

import { createService, runCompiler } from "./server.mjs";
import {
  SOURCE,
  bridgeIdentity,
  digest,
  runStdio,
  startBridge,
  temporaryRoot,
  writeDescriptor,
} from "./test-helpers.mjs";

test("stdio MCP lifecycle exposes exactly the three bounded tools", async (t) => {
  const { root } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  const responses = await runStdio(root, [
    { jsonrpc: "2.0", id: 1, method: "initialize", params: { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "test", version: "1" } } },
    { jsonrpc: "2.0", method: "notifications/initialized" },
    { jsonrpc: "2.0", id: 2, method: "tools/list", params: {} },
    { jsonrpc: "2.0", id: 3, method: "tools/call", params: { name: "lab_status", arguments: {} } },
    { jsonrpc: "2.0", id: 4, method: "tools/call", params: { name: "apply_experiment", arguments: { source: SOURCE } } },
  ]);
  assert.equal(responses.length, 4);
  assert.equal(responses[0].result.protocolVersion, "2025-06-18");
  assert.deepEqual(responses[0].result.capabilities, { tools: { listChanged: false } });
  const tools = responses[1].result.tools;
  assert.deepEqual(tools.map((tool) => tool.name), ["lab_status", "apply_experiment", "read_ledger"]);
  assert.deepEqual(tools[0].inputSchema, { type: "object", properties: {}, additionalProperties: false });
  assert.deepEqual(tools[1].inputSchema.required, ["source"]);
  assert.deepEqual(Object.keys(tools[1].inputSchema.properties), ["source", "targets", "inputs", "evidence_events", "evidence_timeout_ms"]);
  assert.equal(tools[1].inputSchema.properties.evidence_timeout_ms.default, 30_000);
  assert.equal(tools[1].inputSchema.properties.evidence_timeout_ms.maximum, 120_000);
  assert.deepEqual(Object.keys(tools[2].inputSchema.properties), ["operation_id", "limit"]);
  for (const response of responses.slice(2)) {
    assert.deepEqual(JSON.parse(response.result.content[0].text), response.result.structuredContent);
  }
  assert.equal(responses[2].result.structuredContent.authorized, false);
  assert.equal(responses[3].result.isError, true);
  assert.match(responses[3].result.structuredContent.error, /experiment refused/);
});

test("initialize negotiates the supported version for a well-formed newer client", async (t) => {
  const { root } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  const responses = await runStdio(root, [
    { jsonrpc: "2.0", id: 1, method: "initialize", params: { protocolVersion: "2026-01-15", capabilities: {}, clientInfo: { name: "future-client", version: "2" } } },
    { jsonrpc: "2.0", method: "notifications/initialized" },
    { jsonrpc: "2.0", id: 2, method: "tools/list", params: {} },
  ]);
  assert.equal(responses[0].result.protocolVersion, "2025-06-18");
  assert.equal(responses[1].result.tools.length, 3);

  const malformed = await runStdio(root, [
    { jsonrpc: "2.0", id: 3, method: "initialize", params: { protocolVersion: 20250618, capabilities: {}, clientInfo: { name: "bad", version: "1" } } },
  ]);
  assert.equal(malformed[0].error.code, -32602);
});

test("descriptor validation rejects non-loopback and opaque-generation violations", async (t) => {
  const { root, reference } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  await writeDescriptor(root, reference, 12345, { host: "localhost" });
  let service = createService({ root });
  assert.match((await service.call("lab_status")).error, /127\.0\.0\.1/);
  await writeDescriptor(root, reference, 12345, { generation: 2 });
  service = createService({ root });
  assert.match((await service.call("lab_status")).error, /generation/);
});

test("compiler invocation uses direct Roslyn arguments and exactly the curated references", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const secondReference = join(fixture.root, "second reference.dll");
  const sourcePath = join(fixture.root, "source.cs");
  const assemblyPath = join(fixture.root, "result.dll");
  const fakeCsc = join(fixture.root, "fake-csc.mjs");
  await writeFile(secondReference, "second reference");
  await writeFile(sourcePath, SOURCE);
  await writeFile(fakeCsc, `
import { writeFile } from "node:fs/promises";
const output = process.argv.find((value) => value.startsWith("-out:")).slice(5);
await writeFile(output, "fake assembly");
`);
  const outcome = await runCompiler({
    descriptor: { compiler_references: [fixture.reference, secondReference] },
    sourcePath,
    assemblyPath,
    compiler: { dotnetPath: process.execPath, cscDll: fakeCsc },
  });
  assert.equal(outcome.code, 0, outcome.stderr);
  assert.deepEqual(outcome.arguments, [
    "-noconfig",
    "-nostdlib+",
    "-target:library",
    "-langversion:latest",
    `-out:${assemblyPath}`,
    `-reference:${fixture.reference}`,
    `-reference:${secondReference}`,
    sourcePath,
  ]);
  assert.equal(await readFile(assemblyPath, "utf8"), "fake assembly");
});

test("apply preserves exact source and hashes, writes pending first, and records selected evidence", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  const bridge = await startBridge(async (request) => {
    assert.equal(request.protocol, 1);
    assert.equal(request.token, descriptor.token);
    assert.equal(request.generation, descriptor.generation);
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true });
    assert.equal(request.source, SOURCE);
    assert.equal(request.source_sha256, digest(SOURCE));
    assert.equal(request.entry_type, "ValheimDevExperiment");
    assert.equal(request.assembly_sha256, digest(Buffer.from("assembly")));
    assert.equal(Buffer.from(request.assembly, "base64").toString(), "assembly");
    assert.deepEqual(request.evidence_events, ["Movement:Velocity"]);
    assert.equal(request.targets, undefined);
    assert.equal(request.inputs, undefined);
    return bridgeIdentity(descriptor, {
      operation_id: request.operation_id,
      started_utc: "2026-09-01T00:00:00.000Z",
      finished_utc: "2026-09-01T00:00:00.010Z",
      result: "variant-a",
      exception: null,
      cleanup_state: "cleaned",
      evidence_selected: true,
      evidence_exhaustive: false,
      evidence_events: [JSON.stringify({ domain: "Movement", event: "Velocity", value: 12 })],
    });
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const assemblyNames = [];
  const compilerRunner = async ({ sourcePath, assemblyPath }) => {
    const ledgerNames = await readdir(join(fixture.root, "ledger"));
    assert.equal(ledgerNames.length, 1);
    const pending = JSON.parse(await readFile(join(fixture.root, "ledger", ledgerNames[0]), "utf8"));
    assert.equal(pending.state, "pending");
    assert.equal(pending.terminal, false);
    assert.equal(await readFile(sourcePath, "utf8"), SOURCE);
    assemblyNames.push(basename(assemblyPath));
    await writeFile(assemblyPath, "assembly");
    return { code: 0, signal: null, stdout: "compiled", stderr: "", timed_out: false, output_overflow: false };
  };
  const service = createService({ root: fixture.root, compilerRunner });
  const record = await service.call("apply_experiment", {
    source: SOURCE,
    targets: { actor: "local-player" },
    inputs: { impulse: 12 },
    evidence_events: ["Movement:Velocity"],
    evidence_timeout_ms: 50,
  });
  assert.equal(record.state, "succeeded");
  assert.equal(record.source, SOURCE);
  assert.equal(record.source_sha256, digest(SOURCE));
  assert.equal(record.artifact_sha256, digest(Buffer.from("assembly")));
  assert.deepEqual(record.targets, { actor: "local-player" });
  assert.deepEqual(record.inputs, { impulse: 12 });
  assert.equal(record.evidence_selected, true);
  assert.equal(record.evidence_exhaustive, false);
  assert.equal(record.cleanup_state, "cleaned");
  assert.match(assemblyNames[0], new RegExp(`^${record.operation_id}-${record.source_sha256.slice(0, 16)}\\.dll$`));
  const persisted = await service.call("read_ledger", { operation_id: record.operation_id });
  assert.deepEqual(persisted.record, record);
});

test("same source gets unique operation-scoped assembly names", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor, { authorized: true });
    return bridgeIdentity(descriptor, {
      operation_id: request.operation_id,
      started_utc: new Date().toISOString(),
      finished_utc: new Date().toISOString(),
      result: "ok",
      exception: null,
      cleanup_state: "not_applicable",
      evidence_selected: false,
      evidence_exhaustive: false,
      evidence_events: [],
    });
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const names = [];
  const service = createService({
    root: fixture.root,
    compilerRunner: async ({ assemblyPath }) => {
      names.push(basename(assemblyPath));
      await writeFile(assemblyPath, "assembly");
      return { code: 0, signal: null, stdout: "", stderr: "", timed_out: false, output_overflow: false };
    },
  });
  const first = await service.call("apply_experiment", { source: SOURCE });
  const second = await service.call("apply_experiment", { source: SOURCE });
  assert.notEqual(first.operation_id, second.operation_id);
  assert.notEqual(names[0], names[1]);
  assert.ok(names.every((name) => name.endsWith(`-${digest(SOURCE).slice(0, 16)}.dll`)));
});
