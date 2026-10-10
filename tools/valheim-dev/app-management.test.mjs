import assert from "node:assert/strict";
import { readFile, rm, writeFile } from "node:fs/promises";
import { join } from "node:path";
import test from "node:test";
import { createAppManagement } from "./app-management.mjs";
import { readLedger } from "./ledger.mjs";
import { startBridge, temporaryRoot } from "./test-helpers.mjs";

async function fixture(t, options = {}) {
  const { root } = await temporaryRoot();
  t.after(() => rm(root, { recursive: true, force: true }));
  let pids = [1234];
  const status = { pid: 1234, state: "menu", world: null, character: null, lab_access: false, lab_session_id: null, owned: false, launch_id: "" };
  const app = {
    protocol: 1, app_id: "a".repeat(32), pid: 1234, host: "127.0.0.1", port: 0,
    data_root: root, launch_id: "", valheim_version: "1.0.17", valheim_sha256: "b".repeat(64),
    process_started_utc: "2026-10-10T12:00:00Z",
    valheim_dev_version: "0.5.0", valheim_dev_sha256: "c".repeat(64),
  };
  const bridge = await startBridge(async (request) => {
    let result = { ...status }, error = null;
    if (request.action === "open_lab") {
      Object.assign(status, { state: "local_world", world: request.world, character: request.character,
        owned: true, lab_access: true, lab_session_id: "d".repeat(32) });
      result = { requested: true };
    }
    if (request.action === "close_lab" && options.exitOnClose !== false) pids = [];
    if (request.action === "create_world") {
      if (request.name === "Lab-Exists") error = "save_already_exists";
      else result = { name: request.name, source: "local", created: true };
    }
    if (request.action === "list_saves") result = {
      worlds: [{ name: "Lab-Test", source: "local", disposable: true }, { name: "Personal", source: "cloud", disposable: false }],
      characters: [{ name: "Lab-Tester", source: "local", disposable: true }],
    };
    return { protocol: 1, app_id: app.app_id, request_id: request.request_id, ok: !error, error, result };
  });
  t.after(() => bridge.close());
  app.port = bridge.port;
  await writeFile(join(root, "app.json"), JSON.stringify(app));
  let launches = 0;
  const service = createAppManagement({
    root, processes: async () => pids, timeoutMs: options.timeoutMs ?? 25, pause: async () => {},
    launch: async ({ launchId }) => {
      launches++;
      app.launch_id = launchId; status.launch_id = launchId; status.owned = true; pids = [1234];
      await writeFile(join(root, "app.json"), JSON.stringify(app));
    },
  });
  return { root, app, status, service, requests: bridge.requests,
    setPids: (value) => { pids = value; }, launches: () => launches };
}

test("app bridge works without a world descriptor and uses native save listing", async (t) => {
  const f = await fixture(t);
  const status = await f.service.call("session_status");
  assert.equal(status.state, "menu");
  assert.equal(status.connected, true);
  assert.equal(status.valheim_dev_version, "0.5.0");
  const saves = await f.service.call("list_saves");
  assert.equal(saves.worlds[1].name, "Personal");
  assert.equal(saves.worlds[1].disposable, false);
  assert.equal(f.launches(), 0);
});

test("open confirms actual selected world, character, authorization and ownership across socket boundary", async (t) => {
  const f = await fixture(t);
  const result = await f.service.call("open_lab", { app_id: f.app.app_id, world: "Lab-Test", character: "Lab-Tester" });
  assert.equal(result.state, "succeeded");
  assert.equal(result.result.state, "local_world");
  assert.equal(result.result.world, "Lab-Test");
  assert.equal(result.result.lab_access, true);
  const detail = await readLedger(join(f.root, "ledger"), { operation_id: result.operation_id });
  assert.equal(detail.details.terminal, true);
  assert.equal(detail.run.result.owned, true);
  assert.equal(f.requests.find((r) => r.action === "open_lab").request_id, result.operation_id);
});

test("stopped launch uses owned managed launch token and only confirms the menu", async (t) => {
  const f = await fixture(t);
  f.setPids([]);
  assert.equal((await f.service.call("session_status")).state, "stopped");
  const result = await f.service.call("open_lab");
  assert.equal(result.state, "succeeded");
  assert.equal(result.result.state, "menu");
  assert.equal(f.launches(), 1);
  assert.equal(f.requests.some((r) => r.action === "open_lab"), false);
});

test("occupied unavailable game and stale process requests never launch or mutate", async (t) => {
  const f = await fixture(t);
  await rm(join(f.root, "app.json"));
  const occupied = await f.service.call("open_lab");
  assert.equal(occupied.state, "failed");
  assert.equal(f.launches(), 0);
  assert.equal(f.requests.length, 0);
  await writeFile(join(f.root, "app.json"), JSON.stringify(f.app));
  const stale = await f.service.call("open_lab", { app_id: "e".repeat(32), world: "Lab-Test", character: "Lab-Tester" });
  assert.equal(stale.state, "failed");
  assert.match(stale.error, /process changed/);
  assert.equal(f.requests.some((r) => r.action === "open_lab"), false);
});

test("ordinary and multiplayer sessions refuse close and save mutation", async (t) => {
  const f = await fixture(t);
  f.status.state = "multiplayer";
  const close = await f.service.call("close_lab", { app_id: f.app.app_id });
  assert.equal(close.state, "failed");
  const create = await f.service.call("create_lab_character", { app_id: f.app.app_id, name: "Lab-Test" });
  assert.equal(create.state, "failed");
  assert.equal(f.requests.every((r) => r.action === "status"), true);
});

test("quit request is not completion; only process exit completes close", async (t) => {
  const f = await fixture(t, { exitOnClose: false, timeoutMs: 15 });
  f.status.owned = true;
  const result = await f.service.call("close_lab", { app_id: f.app.app_id });
  assert.equal(result.state, "outcome_unconfirmed");
  assert.match(result.error, /not confirmed/);
  assert.equal(f.requests.filter((r) => r.action === "close_lab").length, 1);
  const exited = await fixture(t);
  exited.status.owned = true;
  const success = await exited.service.call("close_lab", { app_id: exited.app.app_id });
  assert.equal(success.state, "succeeded");
  assert.equal(success.result.running, false);
});

test("native save collision stays a failed operation and names cannot escape disposable scope", async (t) => {
  const f = await fixture(t);
  const result = await f.service.call("create_lab_world", { app_id: f.app.app_id, name: "Lab-Exists", seed: "seed" });
  assert.equal(result.state, "failed");
  assert.match(result.error, /already_exists/);
  await assert.rejects(() => f.service.call("create_lab_world", { app_id: f.app.app_id, name: "../Personal" }), /Lab-/);
  const ledger = await readLedger(join(f.root, "ledger"), { operation_id: result.operation_id });
  assert.equal(ledger.run.outcome, "failed");
});

test("app response and sole process identities must match before control", async (t) => {
  const f = await fixture(t);
  f.setPids([1234, 4567]);
  assert.equal((await f.service.call("session_status")).connected, false);
  assert.equal(f.requests.length, 0);
  f.setPids([1234]);
  const wrong = createAppManagement({ root: f.root, processes: async () => [1234],
    transport: async () => ({ protocol: 1, app_id: "f".repeat(32), request_id: "wrong", ok: true, result: f.status }) });
  const status = await wrong.call("session_status");
  assert.equal(status.connected, false);
  assert.match(status.error, /identity mismatch/);
});

test("separate MCP services serialize launch through the profile lock", async (t) => {
  const f = await fixture(t);
  f.setPids([]);
  let entered;
  const ready = new Promise((done) => { entered = done; });
  let resume;
  const hold = new Promise((done) => { resume = done; });
  const first = createAppManagement({
    root: f.root, processes: async () => f.status.launch_id ? [1234] : [],
    launch: async ({ launchId }) => {
      f.app.launch_id = launchId; f.status.launch_id = launchId; f.status.owned = true;
      await writeFile(join(f.root, "app.json"), JSON.stringify(f.app));
      entered(); await hold;
    },
  });
  let secondLaunches = 0;
  const second = createAppManagement({ root: f.root, processes: async () => [], launch: async () => { secondLaunches++; } });
  const opening = first.call("open_lab");
  await ready;
  const blocked = await second.call("open_lab");
  assert.equal(blocked.state, "failed");
  assert.match(blocked.error, /another MCP process/);
  assert.equal(secondLaunches, 0);
  resume();
  assert.equal((await opening).state, "succeeded");
  await assert.rejects(() => readFile(join(f.root, "lifecycle.lock")), { code: "ENOENT" });
});

test("descriptor replacement after status cannot redirect a prepared mutation", async (t) => {
  const f = await fixture(t);
  const actions = [];
  const racing = createAppManagement({
    root: f.root, processes: async () => [1234],
    transport: async (app, request) => {
      actions.push(request.action);
      // First status captures ledger context. Replace the descriptor during
      // the second observation, immediately before the prepared action.
      if (actions.length === 2)
        await writeFile(join(f.root, "app.json"), JSON.stringify({ ...f.app, app_id: "f".repeat(32) }));
      return { protocol: 1, app_id: app.app_id, request_id: request.request_id, ok: true, result: f.status };
    },
  });
  const result = await racing.call("create_lab_character", { app_id: f.app.app_id, name: "Lab-Tester" });
  assert.equal(result.state, "failed");
  assert.match(result.error, /descriptor changed/);
  assert.deepEqual(actions, ["status", "status"]);
});

test("native timeout after execution starts is an unconfirmed outcome", async (t) => {
  const f = await fixture(t);
  const service = createAppManagement({
    root: f.root, processes: async () => [1234],
    transport: async (app, request) => ({
      protocol: 1, app_id: app.app_id, request_id: request.request_id,
      ok: request.action === "status", error: request.action === "status" ? null : "runtime_unresolved",
      result: f.status,
    }),
  });
  const result = await service.call("create_lab_world", { app_id: f.app.app_id, name: "Lab-Test" });
  assert.equal(result.state, "outcome_unconfirmed");
  assert.equal(result.error, "runtime_unresolved");
});
