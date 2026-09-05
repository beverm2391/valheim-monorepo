import { randomUUID } from "node:crypto";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { isAbsolute, join } from "node:path";

import {
  buildIdentity, isoNow, loadDescriptor, plainObject, requestBridge, runCompiler,
  sameAuthorization, sha256, validateKeys,
} from "./bridge-compiler.mjs";
import {
  DEFAULT_EVIDENCE_TIMEOUT_MS, EVENT_PATTERN, IDENTIFIER_PATTERN,
  MAX_ASSEMBLY_BYTES, MAX_EVIDENCE_EVENTS,
  MAX_EVIDENCE_TIMEOUT_MS, MAX_LEDGER_LIST, MAX_SOURCE_BYTES,
} from "./constants.mjs";
import { optionalArtifactHash, readLedger, writeLedger } from "./ledger.mjs";
import { validateOperationResponse, validateStatusResponse } from "./response-validation.mjs";

const evidenceProperties = {
  targets: { type: ["object", "array"], description: "Target selectors or live handles recorded with this operation." },
  inputs: { type: ["object", "array"], description: "Inputs recorded with this operation." },
  evidence_events: { type: "array", maxItems: MAX_EVIDENCE_EVENTS, items: { type: "string", maxLength: 128, pattern: "^[^:\\s]+:[^:\\s]+$" } },
  evidence_timeout_ms: { type: "integer", minimum: 0, maximum: MAX_EVIDENCE_TIMEOUT_MS, default: DEFAULT_EVIDENCE_TIMEOUT_MS },
};

const inspectionSourceProperty = {
  type: "string",
  description: "Exact trusted C# source defining public static ValheimDevInspection.Run(): string for observation. The bridge does not enforce read-only behavior.",
};

const changeSourceProperty = {
  type: "string",
  description: "Exact trusted C# source defining public static ValheimDevChange.Run(): string and Cleanup(): void.",
};

const TOOLS = Object.freeze([
  {
    name: "lab_status",
    description: "Read authorization, exact build identity, and every active managed change in the current Valheim Lab session.",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "inspect_runtime",
    description: "Compile and run one trusted C# inspection against the authorized live runtime for observation. The bridge does not enforce read-only behavior.",
    inputSchema: {
      type: "object",
      properties: { source: inspectionSourceProperty, ...evidenceProperties },
      required: ["source"],
      additionalProperties: false,
    },
  },
  {
    name: "install_change",
    description: "Install or replace one managed live C# change. Failed-compile preservation is reported only after re-reading the same authorization.",
    inputSchema: {
      type: "object",
      properties: {
        change_id: { type: "string", maxLength: 128, pattern: "^[A-Za-z0-9._-]+$" },
        source: changeSourceProperty,
        ...evidenceProperties,
      },
      required: ["change_id", "source"],
      additionalProperties: false,
    },
  },
  {
    name: "remove_change",
    description: "Run Cleanup for one active managed change and remove it only after cleanup succeeds.",
    inputSchema: {
      type: "object",
      properties: { change_id: { type: "string", maxLength: 128, pattern: "^[A-Za-z0-9._-]+$" } },
      required: ["change_id"],
      additionalProperties: false,
    },
  },
  {
    name: "read_ledger",
    description: "Read persistent Valheim Lab operation records, including after the session disconnects.",
    inputSchema: {
      type: "object",
      properties: {
        operation_id: { type: "string", pattern: "^[a-f0-9-]{36}$" },
        limit: { type: "integer", minimum: 1, maximum: MAX_LEDGER_LIST, default: 20 },
      },
      additionalProperties: false,
    },
  },
]);

function validateIdentifier(value, name) {
  if (typeof value !== "string" || !IDENTIFIER_PATTERN.test(value)) {
    throw new Error(`${name} must contain 1-128 letters, digits, dots, underscores, or hyphens`);
  }
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
  const allowed = new Set(["source", "targets", "inputs", "evidence_events", "evidence_timeout_ms"]);
  if (action === "install_change") allowed.add("change_id");
  const input = validateEvidence(args, allowed);
  if (typeof args.source !== "string" || args.source.length === 0) throw new Error("source must be a non-empty string");
  if (Buffer.byteLength(args.source, "utf8") > MAX_SOURCE_BYTES) throw new Error("source exceeds size limit");
  if (action === "install_change") validateIdentifier(args.change_id, "change_id");
  return { ...input, source: args.source, change_id: args.change_id ?? null };
}

function unavailable(error) {
  return { authorized: false, connected: false, active_changes: [], error: error instanceof Error ? error.message : String(error) };
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

function baseRecord(descriptor, operationId, action, input, previous) {
  const source = input.source ?? null;
  return {
    schema_version: 2, state: "pending", terminal: false, action,
    session_id: descriptor.session_id, generation: descriptor.generation, operation_id: operationId,
    change_id: input.change_id ?? null, source, source_sha256: source === null ? null : sha256(source),
    artifact_sha256: null, ...buildIdentity(descriptor), targets: input.targets ?? null, inputs: input.inputs ?? null,
    previous_active_change: previous ?? null, previous_change_preserved: previous ? null : false,
    requested_evidence_events: input.evidence_events ?? [], evidence_timeout_ms: input.evidence_timeout_ms ?? 0,
    created_utc: isoNow(), compile_started_utc: null, compile_finished_utc: null,
    runtime_started_utc: null, runtime_finished_utc: null, terminal_utc: null,
    compiler: source === null ? { outcome: "not_applicable" } : { outcome: "pending", exit_code: null, signal: null, stdout: "", stderr: "" },
    result: null, exception: null, error: null, cleanup_state: "not_run", restart_required: false,
    active_changes: [], evidence_events: [],
    evidence_selected: (input.evidence_events ?? []).length > 0, evidence_exhaustive: false,
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
  return { ...record, ...fields, state, terminal: true, terminal_utc: isoNow() };
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
    evidence_truncated: response.evidence_truncated,
    dropped_evidence_events: response.dropped_evidence_events,
  };
}

async function compileFailureRuntimeState(root, descriptor, previous, bridgeRequest) {
  try {
    const current = await readSessionStatus(root, bridgeRequest);
    if (!sameAuthorization(descriptor, current.descriptor)
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

export function createService({ root, bridgeRequest = requestBridge, compilerRunner = runCompiler } = {}) {
  if (!root) throw new Error("VALHEIM_DEV_ROOT is required");
  if (!isAbsolute(root)) throw new Error("VALHEIM_DEV_ROOT must be absolute");

  async function labStatus() {
    try {
      const { descriptor, status } = await readSessionStatus(root, bridgeRequest);
      if (status.ok !== true || status.authorized === false) {
        return {
          authorized: false, connected: true, session_id: descriptor.session_id,
          generation: descriptor.generation, authorized_at: descriptor.authorized_at,
          ...buildIdentity(descriptor), restart_required: status.restart_required === true,
          active_changes: status.active_changes, error: status.error ?? "Lab authorization is unavailable",
        };
      }
      return {
        authorized: true, connected: true, session_id: descriptor.session_id, generation: descriptor.generation,
        authorized_at: descriptor.authorized_at, ...buildIdentity(descriptor),
        restart_required: status.restart_required === true, active_changes: status.active_changes,
      };
    } catch (error) { return unavailable(error); }
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
    let record = baseRecord(descriptor, operationId, action, input, previous);
    await writeLedger(root, record);
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
        await writeLedger(root, record);
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
        await writeLedger(root, record);
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
        await writeLedger(root, record);
        return record;
      }
      const assemblyHash = sha256(artifact);
      record = { ...record, artifact_sha256: assemblyHash };
      if (artifact.byteLength > MAX_ASSEMBLY_BYTES) {
        const runtimeState = await compileFailureRuntimeState(root, descriptor, previous, bridgeRequest);
        record = terminalRecord(record, "compile_failed", {
          error: "compiled assembly exceeds size limit", ...runtimeState,
        });
        await writeLedger(root, record);
        return record;
      }
      try {
        const current = await loadDescriptor(root);
        if (!sameAuthorization(descriptor, current)) throw new Error("Lab authorization changed during compilation");
      } catch (error) {
        record = terminalRecord(record, "runtime_failed", {
          error: `operation revoked before load: ${error.message}`, previous_change_preserved: null,
          active_changes: null,
        });
        await writeLedger(root, record);
        return record;
      }

      let response;
      try {
        response = await bridgeRequest(descriptor, {
          kind: action === "inspect_runtime" ? "inspect" : "install_change",
          operation_id: operationId, change_id: input.change_id ?? undefined,
          expected_operation_id: action === "install_change" ? previous?.operation_id ?? null : undefined,
          source: input.source, source_sha256: record.source_sha256, assembly_sha256: assemblyHash,
          assembly: artifact.toString("base64"),
          entry_type: action === "inspect_runtime" ? "ValheimDevInspection" : "ValheimDevChange",
          evidence_events: input.evidence_events, evidence_timeout_ms: input.evidence_timeout_ms,
        }, input.evidence_timeout_ms + 15_000);
        validateOperationResponse(response, descriptor, record, input);
      } catch (error) {
        record = { ...record, state: "runtime_unresolved", terminal: false,
          error: `${error.message}; the operation final result is unknown`, previous_change_preserved: null,
          active_changes: null };
        await writeLedger(root, record);
        return record;
      }
      record = terminalRecord(record, response.ok === true && !response.exception ? "succeeded" : "runtime_failed", runtimeFields(response));
      await writeLedger(root, record);
      return record;
    } finally { await rm(workRoot, { recursive: true, force: true }); }
  }

  async function removeChange(args) {
    validateKeys(args, new Set(["change_id"]));
    validateIdentifier(args.change_id, "change_id");
    let descriptor;
    let status;
    try { ({ descriptor, status } = await authorizedSession(root, bridgeRequest)); }
    catch (error) { throw new Error(`remove_change refused: ${error.message}`); }
    const previous = status.active_changes.find((change) => change.change_id === args.change_id) ?? null;
    const operationId = randomUUID();
    const input = { change_id: args.change_id, evidence_events: [], evidence_timeout_ms: 0 };
    let record = baseRecord(descriptor, operationId, "remove_change", input, previous);
    await writeLedger(root, record);
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
    await writeLedger(root, record);
    return record;
  }

  return {
    tools: TOOLS,
    async call(name, args = {}) {
      if (name === "lab_status") { validateKeys(args, new Set()); return labStatus(); }
      if (name === "inspect_runtime") return codeOperation(args, name);
      if (name === "install_change") return codeOperation(args, name);
      if (name === "remove_change") return removeChange(args);
      if (name === "read_ledger") return readLedger(root, args);
      throw new Error(`unknown tool: ${name}`);
    },
  };
}
