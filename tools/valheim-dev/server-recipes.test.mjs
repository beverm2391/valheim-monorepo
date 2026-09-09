import assert from "node:assert/strict";
import { mkdir, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import test from "node:test";

import { createService } from "./server.mjs";
import {
  CHANGE_SOURCE, SOURCE, bridgeIdentity, managedChange, operationResponse,
  startBridge, temporaryRoot, writeDescriptor,
} from "./test-helpers.mjs";

async function writeRecipe(root, id, source, presets) {
  const directory = join(root, id);
  await mkdir(directory, { recursive: true });
  await writeFile(join(directory, "code.cs"), source);
  if (presets !== undefined) {
    await writeFile(join(directory, "presets.json"), `${JSON.stringify(presets)}\n`);
  }
}

function fakeCompiler() {
  return async ({ assemblyPath }) => {
    await writeFile(assemblyPath, "assembly");
    return {
      code: 0,
      signal: null,
      stdout: "",
      stderr: "",
      timed_out: false,
      output_overflow: false,
    };
  };
}

test("run_recipes reads current files and applies indexed and ad hoc inputs in request order", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const recipesRoot = join(fixture.root, "registry");
  await writeRecipe(recipesRoot, "inspect", SOURCE, [{ value: "first" }, { value: "second" }]);
  await writeRecipe(recipesRoot, "ongoing", CHANGE_SOURCE, [
    { value: "managed first" },
    { value: "managed second" },
  ]);

  let descriptor;
  let active = [];
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") {
      return bridgeIdentity(descriptor, { active_changes: active });
    }
    if (request.kind === "install_change") {
      active = [managedChange(request.change_id, request.operation_id, {
        source_sha256: request.source_sha256,
        assembly_sha256: request.assembly_sha256,
        result: request.input_json,
      })];
    }
    return operationResponse(descriptor, request, {
      result: request.input_json,
      active_changes: active,
    });
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    recipesRoot,
    compilerRunner: fakeCompiler(),
  });

  const batch = await service.call("run_recipes", {
    recipes: [
      { id: "inspect", preset_index: 1 },
      { id: "missing" },
      { id: "ongoing", preset_index: 0 },
      { id: "ongoing", preset_index: 1 },
    ],
  });
  assert.deepEqual(batch.recipes.map((outcome) => outcome.record?.state ?? outcome.state), [
    "succeeded", "failed", "succeeded", "succeeded",
  ]);
  const operations = bridge.requests.filter((request) => request.kind !== "status");
  assert.deepEqual(operations.map((request) => request.kind), [
    "run_once", "install_change", "install_change",
  ]);
  assert.equal(operations[0].input_json, JSON.stringify({ value: "second" }));
  assert.equal(operations[1].input_json, JSON.stringify({ value: "managed first" }));
  assert.equal(operations[2].input_json, JSON.stringify({ value: "managed second" }));
  assert.equal(operations[1].change_id, "ongoing");
  assert.equal(operations[2].change_id, "ongoing");
  assert.equal(operations[2].expected_operation_id, batch.recipes[2].record.operation_id);
  assert.deepEqual(batch.active_changes.map((change) => change.change_id), ["ongoing"]);

  const changedSource = `${SOURCE}\n// changed without restarting the MCP server`;
  await writeRecipe(recipesRoot, "inspect", changedSource, [{ value: "fresh" }]);
  const fresh = await service.call("run_recipes", {
    recipes: [{ id: "inspect" }],
  });
  assert.equal(fresh.recipes[0].record.state, "succeeded");
  const latestOperation = bridge.requests.filter((request) => request.kind !== "status").at(-1);
  assert.equal(latestOperation.source, changedSource);
  assert.equal(latestOperation.input_json, JSON.stringify({}));
});

test("run_recipes stops before later mutations when a runtime outcome becomes unknown", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const recipesRoot = join(fixture.root, "registry");
  await writeRecipe(recipesRoot, "first", SOURCE);
  await writeRecipe(recipesRoot, "second", CHANGE_SOURCE);

  let descriptor;
  const bridge = await startBridge(async (request) => {
    if (request.kind === "status") return bridgeIdentity(descriptor);
    const error = new Error("bridge request timed out");
    error.code = "BRIDGE_TIMEOUT";
    throw error;
  });
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    recipesRoot,
    compilerRunner: fakeCompiler(),
  });

  const batch = await service.call("run_recipes", {
    recipes: [{ id: "first" }, { id: "second" }],
  });
  assert.equal(batch.recipes[0].record.state, "runtime_unresolved");
  assert.equal(batch.recipes[1].state, "not_attempted");
  assert.match(batch.recipes[1].error, /runtime state uncertain/);
  assert.equal(bridge.requests.filter((request) => request.kind !== "status").length, 1);
});

test("run_recipes rejects conflicting input selection without dispatching that recipe", async (t) => {
  const fixture = await temporaryRoot();
  t.after(() => rm(fixture.root, { recursive: true, force: true }));
  const recipesRoot = join(fixture.root, "registry");
  await writeRecipe(recipesRoot, "inspect", SOURCE, [{ value: "preset" }]);

  let descriptor;
  const bridge = await startBridge(async (request) => bridgeIdentity(descriptor, {
    active_changes: [],
    ...(request.kind === "status" ? {} : operationResponse(descriptor, request)),
  }));
  t.after(() => bridge.close());
  descriptor = await writeDescriptor(fixture.root, fixture.reference, bridge.port);
  const service = createService({
    root: fixture.root,
    recipesRoot,
    compilerRunner: fakeCompiler(),
  });

  const batch = await service.call("run_recipes", {
    recipes: [{ id: "inspect", preset_index: 0, inputs: { value: "conflict" } }],
  });
  assert.equal(batch.recipes[0].state, "failed");
  assert.match(batch.recipes[0].error, /not both/);
  assert.equal(bridge.requests.filter((request) => request.kind !== "status").length, 0);
});
