import assert from "node:assert/strict";
import { readFile, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import test from "node:test";

import { createService, runCompiler } from "./server.mjs";
import { SOURCE, bridgeIdentity, managedChange, runStdio, temporaryRoot, writeDescriptor } from "./test-helpers.mjs";

test("stdio MCP lifecycle exposes the five workbench tools", async (t) => {
  const { root } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  const responses = await runStdio(root, [
    { jsonrpc: "2.0", id: 1, method: "initialize", params: { protocolVersion: "2025-06-18", capabilities: {}, clientInfo: { name: "test", version: "1" } } },
    { jsonrpc: "2.0", method: "notifications/initialized" },
    { jsonrpc: "2.0", id: 2, method: "tools/list", params: {} },
    { jsonrpc: "2.0", id: 3, method: "tools/call", params: { name: "lab_status", arguments: {} } },
    { jsonrpc: "2.0", id: 4, method: "tools/call", params: { name: "inspect_runtime", arguments: { source: SOURCE } } },
  ]);
  assert.equal(responses.length, 4);
  assert.equal(responses[0].result.protocolVersion, "2025-06-18");
  assert.deepEqual(responses[1].result.tools.map((tool) => tool.name), [
    "lab_status", "inspect_runtime", "install_change", "remove_change", "read_ledger",
  ]);
  assert.deepEqual(responses[1].result.tools[1].inputSchema.required, ["source"]);
  assert.deepEqual(responses[1].result.tools[2].inputSchema.required, ["change_id", "source"]);
  assert.deepEqual(responses[1].result.tools[3].inputSchema.required, ["change_id"]);
  assert.equal(responses[2].result.structuredContent.authorized, false);
  assert.deepEqual(responses[2].result.structuredContent.active_changes, []);
  assert.equal(responses[3].result.isError, true);
  assert.match(responses[3].result.structuredContent.error, /inspect_runtime refused/);
});

test("initialize negotiates a supported version and rejects malformed clients", async (t) => {
  const { root } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  const responses = await runStdio(root, [
    { jsonrpc: "2.0", id: 1, method: "initialize", params: { protocolVersion: "2026-01-15", capabilities: {}, clientInfo: { name: "future", version: "2" } } },
    { jsonrpc: "2.0", method: "notifications/initialized" },
    { jsonrpc: "2.0", id: 2, method: "tools/list", params: {} },
  ]);
  assert.equal(responses[0].result.protocolVersion, "2025-06-18");
  assert.equal(responses[1].result.tools.length, 5);
  const malformed = await runStdio(root, [
    { jsonrpc: "2.0", id: 3, method: "initialize", params: { protocolVersion: 20250618, capabilities: {}, clientInfo: { name: "bad", version: "1" } } },
  ]);
  assert.equal(malformed[0].error.code, -32602);
});

test("descriptor validation rejects non-loopback and opaque-generation violations", async (t) => {
  const { root, reference } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  await writeDescriptor(root, reference, 12345, { host: "localhost" });
  assert.match((await createService({ root }).call("lab_status")).error, /127\.0\.0\.1/);
  await writeDescriptor(root, reference, 12345, { generation: 2 });
  assert.match((await createService({ root }).call("lab_status")).error, /generation/);
});

test("compiler invocation uses direct Roslyn arguments and curated references", async (t) => {
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
    "-noconfig", "-nostdlib+", "-target:library", "-langversion:latest",
    `-out:${assemblyPath}`, `-reference:${fixture.reference}`, `-reference:${secondReference}`, sourcePath,
  ]);
  assert.equal(await readFile(assemblyPath, "utf8"), "fake assembly");
});

test("lab status returns the runtime's active managed changes", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const active = [managedChange()];
  let descriptor;
  const service = createService({
    root: fixture.root,
    bridgeRequest: async () => bridgeIdentity(descriptor, { authorized: true, active_changes: active }),
  });
  descriptor = await writeDescriptor(fixture.root, fixture.reference, 12345);
  const status = await service.call("lab_status");
  assert.equal(status.authorized, true);
  assert.deepEqual(status.active_changes, active);
});

test("lab status rejects an active restart-required change without the top-level flag", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  let descriptor;
  const service = createService({
    root: fixture.root,
    bridgeRequest: async () => bridgeIdentity(descriptor, {
      active_changes: [managedChange("affinity.weapon-icon", "working", { cleanup_state: "restart_required" })],
      restart_required: false,
    }),
  });
  descriptor = await writeDescriptor(fixture.root, fixture.reference, 12345);
  const status = await service.call("lab_status");
  assert.equal(status.authorized, false);
  assert.equal(status.connected, false);
  assert.match(status.error, /restart-required state/);
});
