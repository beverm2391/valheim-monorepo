import { execFile } from "node:child_process";
import { randomUUID } from "node:crypto";
import { mkdir, open, readFile, rm } from "node:fs/promises";
import { join, resolve } from "node:path";
import { promisify } from "node:util";
import { buildIdentity, plainObject, readSmallJson, requestJson, validateKeys } from "./bridge-compiler.mjs";
import { SHA256_PATTERN } from "./constants.mjs";
import { writeLedger } from "./ledger.mjs";

const exec = promisify(execFile);
const APP_PROTOCOL = 1;
const NAME = /^Lab-[A-Za-z0-9_-]{1,44}$/;
export const APP_TOOLS = new Set(["session_status", "list_saves", "create_lab_world", "create_lab_character", "open_lab", "close_lab"]);

export async function gameProcesses() {
  const found = new Set();
  for (const name of ["Valheim", "valheim", "valheim.x86_64"]) {
    try {
      const { stdout } = await exec("pgrep", ["-x", name], { timeout: 5000, maxBuffer: 64 * 1024 });
      for (const pid of stdout.trim().split(/\s+/)) if (/^[1-9][0-9]*$/.test(pid)) found.add(Number(pid));
    } catch (error) {
      if (error.code !== 1) throw error;
    }
  }
  return [...found].sort((a, b) => a - b);
}

async function launchManaged({ gameDir, root, launchId }) {
  if (process.platform !== "darwin") throw new Error("managed Lab launch currently supports macOS");
  if (!gameDir) throw new Error("VALHEIM_GAME_DIR is required to launch the managed profile");
  // Invoke the existing Steam/BepInEx launcher, preserving its readiness and log
  // archival behavior. No second game-launch implementation lives in Lab.
  await exec("/bin/sh", [resolve(import.meta.dirname, "../../client-mods/benheim/scripts/macos-launcher.sh")], {
    env: { ...process.env, BENHEIM_QOL_GAME_DIR: gameDir, VALHEIM_DEV_ROOT: root, VALHEIM_DEV_LAUNCH_ID: launchId },
    timeout: 100_000, maxBuffer: 256 * 1024,
  });
}

function saveName(value, field) {
  if (typeof value !== "string" || !NAME.test(value)) throw new Error(`${field} must be a disposable Lab- name (1–44 ASCII letters, digits, underscores or hyphens after Lab-)`);
}

function validateArguments(name, args) {
  const fields = {
    session_status: [], list_saves: [],
    create_lab_world: ["app_id", "name", "seed"],
    create_lab_character: ["app_id", "name"],
    open_lab: ["app_id", "world", "character"],
    close_lab: ["app_id"],
  };
  validateKeys(args, new Set(fields[name]));
  if (args.app_id !== undefined && (typeof args.app_id !== "string" || !/^[a-f0-9]{32}$/.test(args.app_id))) throw new Error("app_id is invalid");
  if (name === "create_lab_world" || name === "create_lab_character") {
    saveName(args.name, "name");
    if (!args.app_id) throw new Error("app_id from session_status is required");
  }
  if (args.seed !== undefined && (typeof args.seed !== "string" || args.seed.length > 10 || !/^[A-Za-z0-9]*$/.test(args.seed))) throw new Error("seed must be at most 10 ASCII letters or digits");
  if ((args.world === undefined) !== (args.character === undefined)) throw new Error("choose both world and character");
  if (args.world !== undefined) { saveName(args.world, "world"); saveName(args.character, "character"); }
  if (name === "close_lab" && !args.app_id) throw new Error("app_id from session_status is required");
}

export function createAppManagement({
  root, gameDir = process.env.VALHEIM_GAME_DIR, processes = gameProcesses,
  launch = launchManaged, transport = requestJson, timeoutMs = 120_000,
  pause = (ms) => new Promise((done) => setTimeout(done, ms)),
} = {}) {
  let busy = false;
  async function acquireMutationLock() {
    await mkdir(root, { recursive: true, mode: 0o700 });
    const path = join(root, "lifecycle.lock");
    try {
      const handle = await open(path, "wx", 0o600);
      await handle.writeFile(JSON.stringify({ pid: process.pid }));
      await handle.close();
      return async () => rm(path, { force: true });
    } catch (error) {
      if (error.code !== "EEXIST") throw error;
      // Separate Codex processes can share this profile. Never steal their
      // lock. A crash leaves a visible owner PID for deliberate recovery after
      // inspecting app status, rather than racing automatic stale removals.
      let owner;
      try { owner = JSON.parse(await readFile(path, "utf8")); } catch {}
      if (Number.isInteger(owner?.pid) && owner.pid > 0) {
        try { process.kill(owner.pid, 0); } catch (probe) {
          if (probe.code === "ESRCH") throw new Error(`abandoned lifecycle.lock (owner PID ${owner.pid}); inspect session_status before removing this local lock`);
        }
      }
      throw new Error("another MCP process owns the Lab lifecycle operation");
    }
  }
  async function descriptor() {
    const value = await readSmallJson(join(root, "app.json"), 256 * 1024);
    if (!plainObject(value) || value.protocol !== APP_PROTOCOL || !/^[a-f0-9]{32}$/.test(value.app_id ?? "")
      || value.host !== "127.0.0.1" || !Number.isInteger(value.port) || value.port < 1 || value.port > 65535
      || !Number.isInteger(value.pid) || value.pid < 1 || resolve(value.data_root ?? "") !== resolve(root)
      || typeof value.launch_id !== "string" || !Number.isFinite(Date.parse(value.process_started_utc))) throw new Error("invalid app descriptor");
    for (const label of ["valheim", "valheim_dev"]) {
      if (typeof value[`${label}_version`] !== "string" || !SHA256_PATTERN.test(value[`${label}_sha256`] ?? "")) throw new Error("invalid app build identity");
    }
    return value;
  }
  async function request(app, action, fields = {}, requestId = randomUUID()) {
    let response;
    try {
      response = await transport(app, { ...fields, protocol: APP_PROTOCOL, app_id: app.app_id, request_id: requestId, action }, 20_000);
    } catch (error) {
      if (action !== "status" && action !== "list_saves") error.code = "APP_OUTCOME_UNCONFIRMED";
      throw error;
    }
    const uncertain = (message) => {
      const error = new Error(message);
      if (action !== "status" && action !== "list_saves") error.code = "APP_OUTCOME_UNCONFIRMED";
      return error;
    };
    if (!plainObject(response) || response.protocol !== APP_PROTOCOL || response.app_id !== app.app_id
      || response.request_id !== requestId || typeof response.ok !== "boolean") throw uncertain("app response identity mismatch");
    if (!response.ok) {
      if (response.error === "runtime_unresolved") throw uncertain(response.error);
      throw new Error(response.error ?? "app action failed");
    }
    if (!plainObject(response.result)) throw uncertain("app response result must be an object");
    return response.result;
  }
  async function status() {
    const pids = await processes();
    if (!pids.length) return { running: false, connected: false, state: "stopped", processes: [] };
    try {
      const app = await descriptor();
      if (pids.length !== 1 || pids[0] !== app.pid) throw new Error("app descriptor does not match the sole running Valheim process");
      const result = await request(app, "status");
      if (result.pid !== app.pid || result.launch_id !== app.launch_id) throw new Error("app process identity mismatch");
      return { ...result, running: true, connected: true, processes: pids, app_id: app.app_id,
        process_started_utc: app.process_started_utc, ...buildIdentity(app) };
    } catch (error) {
      return { running: true, connected: false, state: "unavailable", processes: pids, error: error.message };
    }
  }
  async function current(expected) {
    const observed = await status();
    if (!observed.connected) throw new Error(observed.error ?? "Valheim app bridge is not running; open_lab launches the managed menu");
    if (!expected || observed.app_id !== expected) throw new Error("app_id from the current session_status is required; process changed or was not selected");
    const app = await descriptor();
    if (app.app_id !== expected || app.pid !== observed.pid) throw new Error("Valheim app descriptor changed after observation");
    return { app, observed };
  }
  async function waitFor(appId, predicate) {
    const deadline = Date.now() + timeoutMs;
    let last;
    do {
      last = await status();
      if (last.connected && last.app_id !== appId) {
        const error = new Error("Valheim process changed during transition; prior outcome is unconfirmed");
        error.code = "APP_OUTCOME_UNCONFIRMED";
        throw error;
      }
      if (predicate(last)) return last;
      await pause(500);
    } while (Date.now() < deadline);
    const error = new Error(`transition not confirmed before timeout (last state: ${last?.state}); inspect session_status before retrying`);
    error.code = "APP_OUTCOME_UNCONFIRMED";
    throw error;
  }
  async function mutate(name, args, operationId) {
    if (name === "open_lab") {
      let observed = await status();
      let app;
      if (!observed.running) {
        if (args.app_id) throw new Error("selected Valheim process has exited");
        const launchId = randomUUID();
        // Recheck immediately before launch: an occupied session is never an
        // implicit invitation to replace it.
        if ((await processes()).length) throw new Error("Valheim became occupied before launch");
        await launch({ gameDir, root, launchId });
        const deadline = Date.now() + timeoutMs;
        do {
          observed = await status();
          if (observed.connected) break;
          await pause(500);
        } while (Date.now() < deadline);
        if (!observed.connected || observed.launch_id !== launchId) {
          const error = new Error("managed launch not confirmed; inspect session_status before retrying");
          error.code = "APP_OUTCOME_UNCONFIRMED";
          throw error;
        }
        ({ app, observed } = await current(observed.app_id));
        if (app.launch_id !== launchId) throw new Error("managed launch identity changed before selection");
      } else {
        ({ app, observed } = await current(args.app_id));
      }
      if (observed.state !== "menu") throw new Error("open_lab requires the main menu; coordinate the occupied session with its owner");
      if (args.world === undefined) return observed;
      await request(app, "open_lab", { world: args.world, character: args.character }, operationId);
      return waitFor(app.app_id, (s) => s.state === "local_world" && s.world === args.world
        && s.character === args.character && s.lab_access === true && s.owned === true);
    }
    const { app, observed } = await current(args.app_id);
    if (name === "close_lab") {
      if (!observed.owned) throw new Error("the tools do not own this Lab session");
      try { await request(app, "close_lab", {}, operationId); }
      catch (error) {
        // Native quit may tear down the socket before its acknowledgement is
        // flushed. Process exit is still conclusive; a native refusal is not.
        if (error.code !== "APP_OUTCOME_UNCONFIRMED") throw error;
      }
      // Quit acknowledgement is only a request. Confirm the exact process has
      // exited; another Valheim process is reported instead of silently closed.
      return waitFor(app.app_id, (s) => !s.processes.includes(app.pid));
    }
    if (observed.state !== "menu") throw new Error("save creation requires the main menu");
    return request(app, name === "create_lab_world" ? "create_world" : "create_character",
      { name: args.name, ...(args.seed !== undefined ? { seed: args.seed } : {}) }, operationId);
  }
  return {
    async call(name, args = {}) {
      validateArguments(name, args);
      if (name === "session_status") return status();
      if (name === "list_saves") {
        const observed = await status();
        if (!observed.connected) throw new Error(observed.error ?? "open_lab first to reach the managed main menu");
        const { app } = await current(observed.app_id);
        return request(app, "list_saves");
      }
      if (busy) throw new Error("another Lab lifecycle operation is in progress");
      busy = true;
      let release;
      const operationId = randomUUID();
      const record = {
        schema_version: 6, operation_id: operationId, action: name, label: name.replaceAll("_", " "),
        created_utc: new Date().toISOString(), state: "pending", terminal: false,
        inputs: args, result: null, error: null, log_cursor: null,
      };
      try {
        release = await acquireMutationLock();
        record.before = await status();
        await writeLedger(join(root, "ledger"), record);
        const result = await mutate(name, args, operationId);
        record.state = "succeeded"; record.result = JSON.stringify(result);
      } catch (error) {
        record.state = error.code === "APP_OUTCOME_UNCONFIRMED" ? "outcome_unconfirmed" : "failed"; record.error = error.message;
      } finally {
        record.terminal = true; record.terminal_utc = new Date().toISOString();
        record.duration_ms = Math.max(0, Date.parse(record.terminal_utc) - Date.parse(record.created_utc));
        try { await writeLedger(join(root, "ledger"), record); }
        finally { try { if (release) await release(); } finally { busy = false; } }
      }
      return { state: record.state, operation_id: operationId, result: record.result === null ? null : JSON.parse(record.result), error: record.error };
    },
  };
}
