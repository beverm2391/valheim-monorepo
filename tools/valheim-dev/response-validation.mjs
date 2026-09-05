import { plainObject, validateBridgeIdentity, validateKeys } from "./bridge-compiler.mjs";
import {
  CLEANUP_STATES, IDENTIFIER_PATTERN, MAX_EVIDENCE_BYTES, MAX_EVIDENCE_EVENTS,
  SHA256_PATTERN,
} from "./constants.mjs";

const ACTIVE_CHANGE_KEYS = new Set([
  "change_id", "operation_id", "source_sha256", "assembly_sha256",
  "installed_utc", "result", "cleanup_state",
]);
const STATUS_KEYS = new Set([
  "protocol", "ok", "error", "session_id", "generation", "valheim_version",
  "valheim_sha256", "benheim_version", "benheim_sha256", "authorized",
  "restart_required", "active_changes",
]);
const OPERATION_KEYS = new Set([
  ...STATUS_KEYS, "action", "operation_id", "change_id", "started_utc",
  "finished_utc", "result", "exception", "cleanup_state",
  "previous_change_preserved", "evidence_selected", "evidence_exhaustive",
  "evidence_truncated", "dropped_evidence_events", "evidence_events",
]);

function requireIdentifier(value, name) {
  if (typeof value !== "string" || !IDENTIFIER_PATTERN.test(value)) {
    throw new Error(`${name} is invalid`);
  }
}

function requireSha256(value, name) {
  if (typeof value !== "string" || !SHA256_PATTERN.test(value)) {
    throw new Error(`${name} must be a SHA-256 hex digest`);
  }
}

function requireNullableString(value, name) {
  if (value !== null && typeof value !== "string") throw new Error(`${name} must be a string or null`);
}

function requireTimestamp(value, name, allowEmpty = false) {
  if (typeof value !== "string" || (!allowEmpty && value.length === 0)
      || (value.length > 0 && !Number.isFinite(Date.parse(value)))) {
    throw new Error(`${name} is invalid`);
  }
}

function requireOutcome(response) {
  if (typeof response.ok !== "boolean" || typeof response.authorized !== "boolean"
      || typeof response.restart_required !== "boolean") {
    throw new Error("bridge outcome booleans are invalid");
  }
  requireNullableString(response.error, "bridge error");
  if (response.ok && response.error !== null) throw new Error("successful bridge response cannot contain an error");
  if (!response.ok && (typeof response.error !== "string" || response.error.length === 0)) {
    throw new Error("failed bridge response must contain an error");
  }
}

function validateActiveChanges(value) {
  if (!Array.isArray(value)) throw new Error("bridge active_changes must be an array");
  const ids = new Set();
  for (const change of value) {
    if (!plainObject(change)) throw new Error("bridge active change must be an object");
    validateKeys(change, ACTIVE_CHANGE_KEYS);
    requireIdentifier(change.change_id, "bridge change_id");
    requireIdentifier(change.operation_id, "bridge operation_id");
    if (ids.has(change.change_id)) throw new Error("bridge active change IDs must be unique");
    ids.add(change.change_id);
    requireSha256(change.source_sha256, "bridge source_sha256");
    requireSha256(change.assembly_sha256, "bridge assembly_sha256");
    requireTimestamp(change.installed_utc, "bridge installed_utc");
    requireNullableString(change.result, "bridge active change result");
    if (change.cleanup_state !== "active" && change.cleanup_state !== "restart_required") {
      throw new Error("bridge active change cleanup_state is invalid");
    }
  }
  return value;
}

export function validateStatusResponse(response, descriptor) {
  if (!plainObject(response)) throw new Error("bridge status must be an object");
  validateKeys(response, STATUS_KEYS);
  validateBridgeIdentity(response, descriptor);
  requireOutcome(response);
  validateActiveChanges(response.active_changes);
  if (!response.restart_required
      && response.active_changes.some((change) => change.cleanup_state === "restart_required")) {
    throw new Error("active restart-required state is missing from the top-level status");
  }
}

export function validateOperationResponse(response, descriptor, record, input) {
  if (!plainObject(response)) throw new Error("bridge operation must be an object");
  validateKeys(response, OPERATION_KEYS);
  validateBridgeIdentity(response, descriptor);
  requireOutcome(response);
  if (response.error === "main_thread_timeout") {
    const error = new Error("runtime stopped waiting while the operation outcome remained unknown");
    error.code = "RUNTIME_UNRESOLVED";
    throw error;
  }
  requireIdentifier(response.operation_id, "bridge operation_id");
  if (response.operation_id !== record.operation_id) throw new Error("bridge operation identity mismatch");
  const bridgeAction = record.action === "inspect_runtime" ? "inspect" : record.action;
  if (response.action !== bridgeAction) throw new Error("bridge action mismatch");
  if ((response.change_id ?? "") !== (record.change_id ?? "")) throw new Error("bridge change identity mismatch");
  if (typeof response.change_id !== "string") throw new Error("bridge change_id must be a string");
  requireTimestamp(response.started_utc, "bridge started_utc", true);
  requireTimestamp(response.finished_utc, "bridge finished_utc", true);
  if (response.ok && (!response.started_utc || !response.finished_utc)) {
    throw new Error("successful bridge response requires runtime timestamps");
  }
  if (response.started_utc && response.finished_utc
      && Date.parse(response.started_utc) > Date.parse(response.finished_utc)) {
    throw new Error("bridge runtime timestamps are out of order");
  }
  requireNullableString(response.result, "bridge result");
  requireNullableString(response.exception, "bridge exception");
  if (response.ok && response.exception !== null) throw new Error("successful bridge response cannot contain an exception");
  if (!CLEANUP_STATES.has(response.cleanup_state)) throw new Error("bridge cleanup_state is invalid");
  if (response.ok && ((bridgeAction === "inspect" && response.cleanup_state !== "not_applicable")
      || (bridgeAction === "install_change" && response.cleanup_state !== "active")
      || (bridgeAction === "remove_change" && response.cleanup_state !== "cleaned"))) {
    throw new Error("successful bridge cleanup_state does not match the action");
  }
  if (typeof response.previous_change_preserved !== "boolean") {
    throw new Error("bridge previous_change_preserved must be boolean");
  }
  if (bridgeAction !== "install_change" && response.previous_change_preserved) {
    throw new Error("only install_change can preserve a previous change");
  }
  if (typeof response.evidence_selected !== "boolean"
      || response.evidence_selected !== (input.evidence_events.length > 0)) {
    throw new Error("bridge evidence selection label does not match the request");
  }
  if (response.evidence_exhaustive !== false) throw new Error("bridge must label evidence as non-exhaustive");
  if (typeof response.evidence_truncated !== "boolean") throw new Error("bridge evidence_truncated must be boolean");
  if (!Number.isInteger(response.dropped_evidence_events) || response.dropped_evidence_events < 0
      || (response.dropped_evidence_events > 0) !== response.evidence_truncated) {
    throw new Error("bridge evidence truncation fields are inconsistent");
  }
  if (!Array.isArray(response.evidence_events) || response.evidence_events.length > MAX_EVIDENCE_EVENTS
      || response.evidence_events.some((item) => typeof item !== "string")) {
    throw new Error("bridge evidence_events must be bounded JSON strings");
  }
  if (Buffer.byteLength(JSON.stringify(response.evidence_events), "utf8") > MAX_EVIDENCE_BYTES) {
    throw new Error("bridge evidence exceeded the serialized-array byte limit");
  }
  const selectors = new Set(input.evidence_events);
  for (const item of response.evidence_events) {
    const event = JSON.parse(item);
    if (!plainObject(event) || typeof event.domain !== "string" || typeof event.event !== "string"
        || !selectors.has(`${event.domain}:${event.event}`)) {
      throw new Error("bridge evidence event does not match a requested selector");
    }
  }
  validateActiveChanges(response.active_changes);
  const currentChange = record.change_id === null
    ? null
    : response.active_changes.find((change) => change.change_id === record.change_id) ?? null;
  if (response.ok && bridgeAction === "install_change"
      && (currentChange === null || currentChange.operation_id !== record.operation_id
        || currentChange.source_sha256 !== record.source_sha256
        || currentChange.assembly_sha256 !== record.artifact_sha256
        || currentChange.cleanup_state !== "active")) {
    throw new Error("successful install is missing its exact active registry entry");
  }
  if (response.ok && bridgeAction === "remove_change" && currentChange !== null) {
    throw new Error("successful removal still reports the target as active");
  }
  if (response.previous_change_preserved
      && (record.previous_active_change === null
        || !response.active_changes.some((change) =>
          change.change_id === record.previous_active_change.change_id
          && change.operation_id === record.previous_active_change.operation_id))) {
    throw new Error("preserved previous change is missing from the active registry");
  }
  if (!response.restart_required
      && (response.cleanup_state === "restart_required"
        || response.active_changes.some((change) => change.cleanup_state === "restart_required"))) {
    throw new Error("restart-required state is missing from the top-level response");
  }
}
