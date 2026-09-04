import { randomUUID } from "node:crypto";
import { mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { isAbsolute, join } from "node:path";

import {
  buildIdentity,
  isoNow,
  loadDescriptor,
  plainObject,
  requestBridge,
  runCompiler,
  sameAuthorization,
  sha256,
  validateBridgeIdentity,
  validateKeys,
} from "./bridge-compiler.mjs";
import {
  CLEANUP_STATES,
  DEFAULT_EVIDENCE_TIMEOUT_MS,
  EVENT_PATTERN,
  MAX_ASSEMBLY_BYTES,
  MAX_EVIDENCE_BYTES,
  MAX_EVIDENCE_EVENTS,
  MAX_EVIDENCE_TIMEOUT_MS,
  MAX_LEDGER_LIST,
  MAX_SOURCE_BYTES,
} from "./constants.mjs";
import { optionalArtifactHash, readLedger, writeLedger } from "./ledger.mjs";

function validateApplyArguments(args) {
  validateKeys(args, new Set(["source", "targets", "inputs", "evidence_events", "evidence_timeout_ms"]));
  if (typeof args.source !== "string" || args.source.length === 0) {
    throw new Error("source must be a non-empty string");
  }
  if (Buffer.byteLength(args.source, "utf8") > MAX_SOURCE_BYTES) throw new Error("source exceeds size limit");
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
    source: args.source,
    targets: args.targets ?? null,
    inputs: args.inputs ?? null,
    evidence_events: [...evidenceEvents],
    evidence_timeout_ms: evidenceTimeoutMs,
  };
}

function unavailable(error) {
  return {
    authorized: false,
    connected: false,
    error: error instanceof Error ? error.message : String(error),
  };
}

function baseRecord(descriptor, operationId, input, sourceHash) {
  return {
    schema_version: 1,
    state: "pending",
    terminal: false,
    session_id: descriptor.session_id,
    generation: descriptor.generation,
    operation_id: operationId,
    source: input.source,
    source_sha256: sourceHash,
    artifact_sha256: null,
    ...buildIdentity(descriptor),
    targets: input.targets,
    inputs: input.inputs,
    requested_evidence_events: input.evidence_events,
    evidence_timeout_ms: input.evidence_timeout_ms,
    created_utc: isoNow(),
    compile_started_utc: null,
    compile_finished_utc: null,
    runtime_started_utc: null,
    runtime_finished_utc: null,
    terminal_utc: null,
    compiler: { outcome: "pending", exit_code: null, signal: null, stdout: "", stderr: "" },
    result: null,
    exception: null,
    error: null,
    cleanup_state: "not_run",
    evidence_events: [],
    evidence_selected: input.evidence_events.length > 0,
    evidence_exhaustive: false,
  };
}

function compilerLedgerOutcome(outcome) {
  return {
    outcome: outcome.code === 0 && !outcome.timed_out && !outcome.output_overflow ? "succeeded" : "failed",
    exit_code: outcome.code,
    signal: outcome.signal,
    stdout: outcome.stdout,
    stderr: outcome.stderr,
    timed_out: outcome.timed_out,
    output_overflow: outcome.output_overflow,
  };
}

function terminalRecord(record, state, fields = {}) {
  return { ...record, ...fields, state, terminal: true, terminal_utc: isoNow() };
}

const TOOLS = Object.freeze([
  {
    name: "lab_status",
    description: "Read the current authorized Valheim Lab session and exact connected build identities.",
    inputSchema: { type: "object", properties: {}, additionalProperties: false },
  },
  {
    name: "apply_experiment",
    description: "Compile, load, and run one self-contained trusted C# experiment in the authorized Lab session.",
    inputSchema: {
      type: "object",
      properties: {
        source: { type: "string", description: "Exact C# source defining public static ValheimDevExperiment.Run(), returning string; Cleanup() is optional." },
        targets: { type: ["object", "array"], description: "JSON identities or selectors recorded with this operation." },
        inputs: { type: ["object", "array"], description: "JSON experiment inputs recorded with this operation." },
        evidence_events: { type: "array", maxItems: MAX_EVIDENCE_EVENTS, items: { type: "string", maxLength: 128, pattern: "^[^:\\s]+:[^:\\s]+$" } },
        evidence_timeout_ms: { type: "integer", minimum: 0, maximum: MAX_EVIDENCE_TIMEOUT_MS, default: DEFAULT_EVIDENCE_TIMEOUT_MS },
      },
      required: ["source"],
      additionalProperties: false,
    },
  },
  {
    name: "read_ledger",
    description: "Read persistent Valheim Lab operation records, including after the session disconnects.",
    inputSchema: {
      type: "object",
      properties: {
        operation_id: { type: "string", pattern: "^[a-f0-9-]{36}$", description: "Read one exact operation record." },
        limit: { type: "integer", minimum: 1, maximum: MAX_LEDGER_LIST, default: 20, description: "Newest-first list bound when operation_id is omitted." },
      },
      additionalProperties: false,
    },
  },
]);

export function createService({ root, bridgeRequest = requestBridge, compilerRunner = runCompiler } = {}) {
  if (!root) throw new Error("VALHEIM_DEV_ROOT is required");
  if (!isAbsolute(root)) throw new Error("VALHEIM_DEV_ROOT must be absolute");

  async function labStatus() {
    try {
      const descriptor = await loadDescriptor(root);
      const response = await bridgeRequest(descriptor, { kind: "status" }, 5_000);
      validateBridgeIdentity(response, descriptor);
      if (response.ok !== true || response.authorized === false) {
        return {
          authorized: false,
          connected: true,
          session_id: descriptor.session_id,
          generation: descriptor.generation,
          ...buildIdentity(descriptor),
          error: response.error ?? "Lab authorization is unavailable",
        };
      }
      return {
        authorized: true,
        connected: true,
        session_id: descriptor.session_id,
        generation: descriptor.generation,
        authorized_at: descriptor.authorized_at,
        ...buildIdentity(descriptor),
      };
    } catch (error) {
      return unavailable(error);
    }
  }

  async function applyExperiment(rawArguments) {
    const input = validateApplyArguments(rawArguments);
    let descriptor;
    try {
      descriptor = await loadDescriptor(root);
      const status = await bridgeRequest(descriptor, { kind: "status" }, 5_000);
      validateBridgeIdentity(status, descriptor);
      if (status.ok !== true || status.authorized === false) {
        throw new Error(status.error ?? "Lab authorization is unavailable");
      }
    } catch (error) {
      throw new Error(`experiment refused: ${error.message}`);
    }

    const operationId = randomUUID();
    const sourceHash = sha256(input.source);
    let record = baseRecord(descriptor, operationId, input, sourceHash);
    await writeLedger(root, record);
    const workRoot = await mkdtemp(join(tmpdir(), "valheim-dev-"));
    const sourcePath = join(workRoot, `${operationId}-${sourceHash.slice(0, 16)}.cs`);
    const assemblyPath = join(workRoot, `${operationId}-${sourceHash.slice(0, 16)}.dll`);

    try {
      record = { ...record, compile_started_utc: isoNow() };
      await writeFile(sourcePath, input.source, { encoding: "utf8", mode: 0o600, flag: "wx" });
      let compilation;
      try {
        compilation = await compilerRunner({ descriptor, sourcePath, assemblyPath });
      } catch (error) {
        record = terminalRecord(record, "compile_failed", {
          artifact_sha256: await optionalArtifactHash(assemblyPath),
          compile_finished_utc: isoNow(),
          compiler: { outcome: "failed", exit_code: null, signal: null, stdout: "", stderr: "", error: error.message },
          error: `compiler failed: ${error.message}`,
        });
        await writeLedger(root, record);
        return record;
      }
      const compiler = compilerLedgerOutcome(compilation);
      record = { ...record, compile_finished_utc: isoNow(), compiler };
      if (compiler.outcome !== "succeeded") {
        record = terminalRecord(record, "compile_failed", {
          artifact_sha256: await optionalArtifactHash(assemblyPath),
          error: "C# compilation failed",
        });
        await writeLedger(root, record);
        return record;
      }

      let artifact;
      try {
        artifact = await readFile(assemblyPath);
      } catch (error) {
        record = terminalRecord(record, "compile_failed", { error: `compiler emitted no readable assembly: ${error.message}` });
        await writeLedger(root, record);
        return record;
      }
      const assemblyHash = sha256(artifact);
      record = { ...record, artifact_sha256: assemblyHash };
      if (artifact.byteLength > MAX_ASSEMBLY_BYTES) {
        record = terminalRecord(record, "compile_failed", { error: "compiled assembly exceeds size limit" });
        await writeLedger(root, record);
        return record;
      }

      try {
        const current = await loadDescriptor(root);
        if (!sameAuthorization(descriptor, current)) throw new Error("Lab authorization changed during compilation");
      } catch (error) {
        record = terminalRecord(record, "runtime_failed", { error: `experiment revoked before load: ${error.message}` });
        await writeLedger(root, record);
        return record;
      }

      let response;
      try {
        response = await bridgeRequest(descriptor, {
          kind: "apply",
          operation_id: operationId,
          source: input.source,
          source_sha256: sourceHash,
          assembly_sha256: assemblyHash,
          assembly: artifact.toString("base64"),
          entry_type: "ValheimDevExperiment",
          evidence_events: input.evidence_events,
          evidence_timeout_ms: input.evidence_timeout_ms,
        }, input.evidence_timeout_ms + 15_000);
        validateBridgeIdentity(response, descriptor);
        if (response.error === "main_thread_timeout") {
          const error = new Error("runtime stopped waiting while the experiment outcome remained unknown");
          error.code = "RUNTIME_UNRESOLVED";
          throw error;
        }
        if (response.operation_id !== operationId) {
          if (response.ok === false && response.error) throw new Error(`runtime rejected experiment: ${response.error}`);
          throw new Error("bridge operation identity mismatch");
        }
        if (typeof response.evidence_selected !== "boolean") throw new Error("bridge must label whether evidence is selected");
        if (response.evidence_selected !== (input.evidence_events.length > 0)) {
          throw new Error("bridge evidence selection label does not match the operation request");
        }
        if (response.evidence_exhaustive !== false) throw new Error("bridge must label evidence as non-exhaustive");
        if (!Array.isArray(response.evidence_events) || response.evidence_events.some((item) => typeof item !== "string")) {
          throw new Error("bridge evidence_events must be JSON strings");
        }
        if (Buffer.byteLength(JSON.stringify(response.evidence_events), "utf8") > MAX_EVIDENCE_BYTES) {
          throw new Error("bridge evidence exceeded size limit");
        }
        for (const item of response.evidence_events) JSON.parse(item);
        if (!CLEANUP_STATES.has(response.cleanup_state)) throw new Error("bridge cleanup_state is invalid");
      } catch (error) {
        if (error.code === "BRIDGE_TIMEOUT" || error.code === "RUNTIME_UNRESOLVED") {
          record = {
            ...record,
            state: "runtime_unresolved",
            terminal: false,
            error: `${error.message}; the experiment final result is unknown`,
          };
          await writeLedger(root, record);
          return record;
        }
        record = terminalRecord(record, "runtime_failed", { error: `bridge apply failed: ${error.message}` });
        await writeLedger(root, record);
        return record;
      }

      record = terminalRecord(record, response.ok === true && !response.exception ? "succeeded" : "runtime_failed", {
        runtime_started_utc: response.started_utc ?? null,
        runtime_finished_utc: response.finished_utc ?? null,
        result: response.result ?? null,
        exception: response.exception ?? null,
        error: response.ok === true ? null : (response.error ?? "runtime rejected experiment"),
        cleanup_state: response.cleanup_state ?? "unknown",
        evidence_events: response.evidence_events,
        evidence_selected: response.evidence_selected === true,
        evidence_exhaustive: false,
      });
      await writeLedger(root, record);
      return record;
    } finally {
      await rm(workRoot, { recursive: true, force: true });
    }
  }

  return {
    tools: TOOLS,
    async call(name, args = {}) {
      if (name === "lab_status") {
        validateKeys(args, new Set());
        return labStatus();
      }
      if (name === "apply_experiment") return applyExperiment(args);
      if (name === "read_ledger") return readLedger(root, args);
      throw new Error(`unknown tool: ${name}`);
    },
  };
}
