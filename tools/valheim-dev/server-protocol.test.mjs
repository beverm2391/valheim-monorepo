import assert from "node:assert/strict";
import { readFile, rm, writeFile } from "node:fs/promises";
import { join, resolve } from "node:path";
import test from "node:test";

import { Client } from "@modelcontextprotocol/client";
import { StdioClientTransport } from "@modelcontextprotocol/client/stdio";

import { createService, runCompiler } from "./server.mjs";
import { SOURCE, bridgeIdentity, managedChange, temporaryRoot, writeDescriptor } from "./test-helpers.mjs";

const SERVER_PATH = resolve(import.meta.dirname, "server.mjs");

async function connectClient(root) {
  const env = Object.fromEntries(Object.entries(process.env).filter((entry) => typeof entry[1] === "string"));
  env.VALHEIM_DEV_ROOT = root;
  const transport = new StdioClientTransport({
    command: process.execPath,
    args: [SERVER_PATH],
    cwd: import.meta.dirname,
    env,
    stderr: "pipe",
  });
  const client = new Client({ name: "valheim-dev-test", version: "1.0.0" });
  await client.connect(transport);
  return { client, transport };
}

test("official MCP client crosses spawned stdio boundary and exposes the five workbench tools", async (t) => {
  const { root } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  const { client } = await connectClient(root);
  t.after(() => client.close());

  assert.deepEqual(client.getServerVersion(), { name: "valheim-dev", version: "0.1.0" });
  const listed = await client.listTools();
  assert.deepEqual(listed.tools.map((tool) => tool.name), [
    "lab_status", "inspect_runtime", "install_change", "remove_change", "read_ledger",
  ]);
  assert.deepEqual(listed.tools[1].inputSchema.required, ["source"]);
  assert.deepEqual(listed.tools[2].inputSchema.required, ["change_id", "source"]);
  assert.deepEqual(listed.tools[3].inputSchema.required, ["change_id"]);
  assert.equal(listed.tools.every((tool) => tool.inputSchema.additionalProperties === false), true);

  const status = await client.callTool({ name: "lab_status", arguments: {} });
  assert.equal(status.structuredContent.authorized, false);
  assert.deepEqual(status.structuredContent.active_changes, []);
  assert.equal(status.content[0].text, JSON.stringify(status.structuredContent));

  const inspection = await client.callTool({ name: "inspect_runtime", arguments: { source: SOURCE } });
  assert.equal(inspection.isError, true);
  assert.match(inspection.structuredContent.error, /inspect_runtime refused/);
  assert.equal(inspection.content[0].text, JSON.stringify(inspection.structuredContent));
});

test("official SDK validates tool input before the service callback", async (t) => {
  const { root } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  const { client } = await connectClient(root);
  t.after(() => client.close());

  const missingSource = await client.callTool({ name: "inspect_runtime", arguments: {} });
  assert.equal(missingSource.isError, true);
  assert.equal(missingSource.structuredContent, undefined);
  assert.match(missingSource.content[0].text, /Input validation error.*source/s);

  const unknownArgument = await client.callTool({ name: "lab_status", arguments: { enable: true } });
  assert.equal(unknownArgument.isError, true);
  assert.equal(unknownArgument.structuredContent, undefined);
  assert.match(unknownArgument.content[0].text, /Input validation error.*unrecognized key/is);
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
