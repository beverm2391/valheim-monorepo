import { randomUUID } from "node:crypto";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { isAbsolute, join } from "node:path";

import {
  buildIdentity, isoNow, loadDescriptor, plainObject, requestBridge, runCompiler,
  sameSession, sha256, validateKeys,
} from "./bridge-compiler.mjs";
import {
  DEFAULT_EVIDENCE_TIMEOUT_MS, EVENT_PATTERN, IDENTIFIER_PATTERN,
  MAX_ASSEMBLY_BYTES, MAX_EVIDENCE_EVENTS,
  MAX_EVIDENCE_TIMEOUT_MS, MAX_RUN_LABEL_BYTES, MAX_SOURCE_BYTES,
} from "./constants.mjs";
import { captureLogCursor, optionalArtifactHash, readLedger, writeLedger } from "./ledger.mjs";
import { createRecipeRunner } from "./recipes.mjs";
import { validateOperationResponse, validateStatusResponse } from "./response-validation.mjs";

function validateIdentifier(value, name) {
  if (typeof value !== "string" || !IDENTIFIER_PATTERN.test(value)) {
    throw new Error(`${name} must contain 1-128 letters, digits, dots, underscores, or hyphens`);
  }
}

function validateLabel(value) {
  if (typeof value !== "string" || value.trim().length === 0
      || Buffer.byteLength(value.trim(), "utf8") > MAX_RUN_LABEL_BYTES) {
    throw new Error(`label must contain 1-${MAX_RUN_LABEL_BYTES} UTF-8 bytes`);
  }
  return value.trim();
}

function validateEvidence(args, allowed) {
  validateKeys(args, allowed);
  if (args.targets !== undefined && !plainObject(args.targets) && !Array.isArray(args.targets)) {
    throw new Error("targets must be a JSON object or array");
  }
  if (args.inputs !== undefined && !plainObject(args.inputs) && !Array.isArray(args.inputs)) {
    throw new Error("inputs must be a JSON object or array");
  }
  const evidenceEvents = args.evidence_events ?? [];
  if (!Array.isArray(evidenceEvents) || evidenceEvents.length > MAX_EVIDENCE_EVENTS ||
      evidenceEvents.some((value) => typeof value !== "string" || value.length > 128 || !EVENT_PATTERN.test(value))) {
    throw new Error("evidence_events must contain at most 64 Domain:event strings");
  }
  const evidenceTimeoutMs = args.evidence_timeout_ms ?? DEFAULT_EVIDENCE_TIMEOUT_MS;
  if (!Number.isInteger(evidenceTimeoutMs) || evidenceTimeoutMs < 0 || evidenceTimeoutMs > MAX_EVIDENCE_TIMEOUT_MS) {
    throw new Error(`evidence_timeout_ms must be an integer from 0 through ${MAX_EVIDENCE_TIMEOUT_MS}`);
  }
  return {
    targets: args.targets ?? null,
    inputs: args.inputs ?? null,
    evidence_events: [...evidenceEvents],
    evidence_timeout_ms: evidenceTimeoutMs,
  };
}

function validateCodeArguments(args, action) {
  const allowed = new Set(["label", "source", "targets", "inputs", "evidence_events", "evidence_timeout_ms"]);
  if (action === "install_change") allowed.add("change_id");
  const input = validateEvidence(args, allowed);
  if (typeof args.source !== "string" || args.source.length === 0) throw new Error("source must be a non-empty string");
  if (Buffer.byteLength(args.source, "utf8") > MAX_SOURCE_BYTES) throw new Error("source exceeds size limit");
  if (action === "install_change") validateIdentifier(args.change_id, "change_id");
  return {
    ...input,
    label: validateLabel(args.label ?? action.replaceAll("_", " ")),
    source: args.source,
    change_id: args.change_id ?? null,
  };
}

function unavailable(error, root) {
  const message = error instanceof Error ? error.message : String(error);
  let guidance = message;
  if (error?.code === "ENOENT") {
    guidance = `Valheim Lab root is unavailable at ${root}. Enter the disposable local world and run 'bh lab on'.`;
  } else if (message.includes("unsupported bridge protocol")) {
    guidance = "Valheim Lab and this MCP process use different bridge versions. Install matching Valheim Dev code, then refresh Codex.";
  } else if (error?.code === "ECONNREFUSED") {
    guidance = "The Lab descriptor exists, but its bridge is unavailable. Check 'bh lab status', then re-enable Lab if needed.";
  }
  return { authorized: false, connected: false, active_changes: [], error: guidance };
}

async function readSessionStatus(root, bridgeRequest) {
  const descriptor = await loadDescriptor(root);
  const status = await bridgeRequest(descriptor, { kind: "status" }, 5_000);
  validateStatusResponse(status, descriptor);
  return { descriptor, status };
}

async function authorizedSession(root, bridgeRequest) {
  const { descriptor, status } = await readSessionStatus(root, bridgeRequest);
  if (status.ok !== true || status.authorized === false) throw new Error(status.error ?? "Lab authorization is unavailable");
  return { descriptor, status };
}

function baseRecord(descriptor, operationId, action, input, previous, logCursor) {
  const source = input.source ?? null;
  return {
    schema_version: 5, state: "pending", terminal: false, action, label: input.label,
    session_id: descriptor.session_id, operation_id: operationId,
    data_root: descriptor.data_root, log_path: descriptor.log_path,
    change_id: input.change_id ?? null, source, source_sha256: source === null ? null : sha256(source),
    artifact_sha256: null, ...buildIdentity(descriptor), targets: input.targets ?? null, inputs: input.inputs ?? null,
    previous_active_change: previous ?? null, previous_change_preserved: previous ? null : false,
    requested_evidence_events: input.evidence_events ?? [], evidence_timeout_ms: input.evidence_timeout_ms ?? 0,
    created_utc: logCursor?.captured_utc ?? isoNow(), duration_ms: null, log_cursor: logCursor,
    compile_started_utc: null, compile_finished_utc: null,
    runtime_started_utc: null, runtime_finished_utc: null, terminal_utc: null,
    compiler: source === null ? { outcome: "not_applicable" } : { outcome: "pending", exit_code: null, signal: null, stdout: "", stderr: "" },
    result: null, exception: null, error: null, cleanup_state: "not_run", restart_required: false,
    active_changes: [], evidence_events: [],
    evidence_selected: (input.evidence_events ?? []).length > 0, evidence_exhaustive: false,
    evidence_available: false,
    evidence_unavailable_reason: (input.evidence_events ?? []).length > 0 ? "operation_not_started" : null,
    evidence_truncated: false, dropped_evidence_events: 0,
  };
}

function compilerLedgerOutcome(outcome) {
  return {
    outcome: outcome.code === 0 && !outcome.timed_out && !outcome.output_overflow ? "succeeded" : "failed",
    exit_code: outcome.code, signal: outcome.signal, stdout: outcome.stdout, stderr: outcome.stderr,
    timed_out: outcome.timed_out, output_overflow: outcome.output_overflow,
  };
}

function terminalRecord(record, state, fields = {}) {
  const terminalUtc = isoNow();
  return {
    ...record,
    ...fields,
    state,
    terminal: true,
    terminal_utc: terminalUtc,
    duration_ms: Math.max(0, Date.parse(terminalUtc) - Date.parse(record.created_utc)),
  };
}

function runtimeFields(response) {
  return {
    runtime_started_utc: response.started_utc ?? null, runtime_finished_utc: response.finished_utc ?? null,
    result: response.result ?? null, exception: response.exception ?? null,
    error: response.ok === true ? null : (response.error ?? "runtime rejected operation"),
    cleanup_state: response.cleanup_state, restart_required: response.restart_required === true,
    previous_change_preserved: response.previous_change_preserved === true,
    active_changes: response.active_changes, evidence_events: response.evidence_events,
    evidence_selected: response.evidence_selected, evidence_exhaustive: false,
    evidence_available: response.evidence_available,
    evidence_unavailable_reason: response.evidence_unavailable_reason,
    evidence_truncated: response.evidence_truncated,
    dropped_evidence_events: response.dropped_evidence_events,
  };
}

async function compileFailureRuntimeState(root, descriptor, previous, bridgeRequest) {
  try {
    const current = await readSessionStatus(root, bridgeRequest);
    if (!sameSession(descriptor, current.descriptor)
        || current.status.ok !== true || current.status.authorized !== true) {
      throw new Error("authorization changed");
    }
    const preserved = previous === null
      ? false
      : current.status.active_changes.some((change) =>
        change.change_id === previous.change_id && change.operation_id === previous.operation_id);
    return { previous_change_preserved: preserved, active_changes: current.status.active_changes };
  } catch {
    return { previous_change_preserved: previous === null ? false : null, active_changes: null };
  }
}

export function createService({
  root,
  recipesRoot,
  logPath,
  bridgeRequest = requestBridge,
  compilerRunner = runCompiler,
} = {}) {
  if (!root) throw new Error("VALHEIM_DEV_ROOT is required");
  if (!isAbsolute(root)) throw new Error("VALHEIM_DEV_ROOT must be absolute");

  async function labStatus() {
    try {
      const { descriptor, status } = await readSessionStatus(root, bridgeRequest);
      if (status.ok !== true || status.authorized === false) {
        return {
          authorized: false, connected: true, session_id: descriptor.session_id,
          authorized_at: descriptor.authorized_at,
          ...buildIdentity(descriptor), restart_required: status.restart_required === true,
          active_changes: status.active_changes, error: status.error ?? "Lab authorization is unavailable",
        };
      }
      return {
        authorized: true, connected: true, session_id: descriptor.session_id,
        authorized_at: descriptor.authorized_at, ...buildIdentity(descriptor),
        restart_required: status.restart_required === true, active_changes: status.active_changes,
      };
    } catch (error) { return unavailable(error, root); }
  }

  async function codeOperation(rawArguments, action) {
    const input = validateCodeArguments(rawArguments, action);
    let descriptor;
    let status;
    try { ({ descriptor, status } = await authorizedSession(root, bridgeRequest)); }
    catch (error) { throw new Error(`${action} refused: ${error.message}`); }

    const previous = input.change_id
      ? status.active_changes.find((change) => change.change_id === input.change_id) ?? null
      : null;
    const operationId = randomUUID();
    const operationLogPath = logPath ?? descriptor.log_path;
    const ledgerRoot = join(root, "ledger");
    const logCursor = await captureLogCursor(operationLogPath);
    let record = baseRecord(descriptor, operationId, action, input, previous, logCursor);
    await writeLedger(ledgerRoot, record);
    const workRoot = await mkdtemp(join(tmpdir(), "valheim-dev-"));
    const stem = `${operationId}-${record.source_sha256.slice(0, 16)}`;
    const sourcePath = join(workRoot, `${stem}.cs`);
    const assemblyPath = join(workRoot, `${stem}.dll`);
    try {
      record = { ...record, compile_started_utc: isoNow() };
      await writeFile(sourcePath, input.source, { encoding: "utf8", mode: 0o600, flag: "wx" });
      let compilation;
      try { compilation = await compilerRunner({ descriptor, sourcePath, assemblyPath }); }
      catch (error) {
        const runtimeState = await compileFailureRuntimeState(root, descriptor, previous, bridgeRequest);
        record = terminalRecord(record, "compile_failed", {
          artifact_sha256: await optionalArtifactHash(assemblyPath), compile_finished_utc: isoNow(),
          compiler: { outcome: "failed", exit_code: null, signal: null, stdout: "", stderr: "", error: error.message },
          error: `compiler failed: ${error.message}`, ...runtimeState,
        });
        await writeLedger(ledgerRoot, record);
        return record;
      }
      const compiler = compilerLedgerOutcome(compilation);
      record = { ...record, compile_finished_utc: isoNow(), compiler };
      if (compiler.outcome !== "succeeded") {
        const runtimeState = await compileFailureRuntimeState(root, descriptor, previous, bridgeRequest);
        record = terminalRecord(record, "compile_failed", {
          artifact_sha256: await optionalArtifactHash(assemblyPath), error: "C# compilation failed",
          ...runtimeState,
        });
        await writeLedger(ledgerRoot, record);
        return record;
      }
      let artifact;
      try { artifact = await readFile(assemblyPath); }
      catch (error) {
        const runtimeState = await compileFailureRuntimeState(root, descriptor, previous, bridgeRequest);
        record = terminalRecord(record, "compile_failed", {
          error: `compiler emitted no readable assembly: ${error.message}`,
          ...runtimeState,
        });
        await writeLedger(ledgerRoot, record);
        return record;
      }
      const assemblyHash = sha256(artifact);
      record = { ...record, artifact_sha256: assemblyHash };
      if (artifact.byteLength > MAX_ASSEMBLY_BYTES) {
        const runtimeState = await compileFailureRuntimeState(root, descriptor, previous, bridgeRequest);
        record = terminalRecord(record, "compile_failed", {
          error: "compiled assembly exceeds size limit", ...runtimeState,
        });
        await writeLedger(ledgerRoot, record);
        return record;
      }
      try {
        const current = await loadDescriptor(root);
        if (!sameSession(descriptor, current)) throw new Error("Lab authorization changed during compilation");
      } catch (error) {
        record = terminalRecord(record, "runtime_failed", {
          error: `operation revoked before load: ${error.message}`, previous_change_preserved: null,
          active_changes: null,
        });
        await writeLedger(ledgerRoot, record);
        return record;
      }

      let response;
      try {
        response = await bridgeRequest(descriptor, {
          kind: action === "run_once" ? "run_once" : "install_change",
          operation_id: operationId, change_id: input.change_id ?? undefined,
          expected_operation_id: action === "install_change" ? previous?.operation_id ?? null : undefined,
          source: input.source, source_sha256: record.source_sha256, assembly_sha256: assemblyHash,
          assembly: artifact.toString("base64"),
          entry_type: action === "run_once" ? "ValheimDevCommand" : "ValheimDevChange",
          input_json: JSON.stringify(input.inputs ?? {}),
          evidence_events: input.evidence_events, evidence_timeout_ms: input.evidence_timeout_ms,
        }, input.evidence_timeout_ms + 15_000);
        validateOperationResponse(response, descriptor, record, input);
      } catch (error) {
        record = { ...record, state: "runtime_unresolved", terminal: false,
          error: `${error.message}; the operation final result is unknown`, previous_change_preserved: null,
          active_changes: null };
        await writeLedger(ledgerRoot, record);
        return record;
      }
      record = terminalRecord(record, response.ok === true && !response.exception ? "succeeded" : "runtime_failed", runtimeFields(response));
      await writeLedger(ledgerRoot, record);
      return record;
    } finally { await rm(workRoot, { recursive: true, force: true }); }
  }

  async function removeChange(args) {
    validateKeys(args, new Set(["label", "change_id"]));
    validateIdentifier(args.change_id, "change_id");
    const label = validateLabel(args.label ?? `remove ${args.change_id}`);
    let descriptor;
    let status;
    try { ({ descriptor, status } = await authorizedSession(root, bridgeRequest)); }
    catch (error) { throw new Error(`remove_change refused: ${error.message}`); }
    const previous = status.active_changes.find((change) => change.change_id === args.change_id) ?? null;
    const operationId = randomUUID();
    const input = { label, change_id: args.change_id, evidence_events: [], evidence_timeout_ms: 0 };
    const operationLogPath = logPath ?? descriptor.log_path;
    const ledgerRoot = join(root, "ledger");
    const logCursor = await captureLogCursor(operationLogPath);
    let record = baseRecord(descriptor, operationId, "remove_change", input, previous, logCursor);
    await writeLedger(ledgerRoot, record);
    try {
      const response = await bridgeRequest(descriptor, {
        kind: "remove_change", operation_id: operationId, change_id: args.change_id,
        expected_operation_id: previous?.operation_id ?? null,
      }, 15_000);
      validateOperationResponse(response, descriptor, record, input);
      record = terminalRecord(record, response.ok === true ? "succeeded" : "runtime_failed", runtimeFields(response));
    } catch (error) {
      record = {
        ...record, state: "runtime_unresolved", terminal: false,
        error: `${error.message}; the removal outcome is unknown`, previous_change_preserved: null,
        active_changes: null,
      };
    }
    await writeLedger(ledgerRoot, record);
    return record;
  }

  const runRecipes = createRecipeRunner({
    recipesRoot: recipesRoot ?? join(root, "registry"),
    runCodeOperation: codeOperation,
    readStatus: labStatus,
  });

  return {
    async call(name, args = {}) {
      if (name === "lab_status") { validateKeys(args, new Set()); return labStatus(); }
      if (name === "run_once") return codeOperation(args, name);
      if (name === "install_change") return codeOperation(args, name);
      if (name === "remove_change") return removeChange(args);
      if (name === "run_recipes") return runRecipes(args);
      if (name === "read_ledger") {
        return readLedger(join(root, "ledger"), args, logPath);
      }
      throw new Error(`unknown tool: ${name}`);
    },
  };
}
