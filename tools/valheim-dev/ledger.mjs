import { randomUUID } from "node:crypto";
import { mkdir, open, readFile, readdir, rename, rm, stat } from "node:fs/promises";
import { basename, dirname, join } from "node:path";

import { readSmallJson, sha256, validateKeys } from "./bridge-compiler.mjs";
import {
  MAX_LEDGER_LIST,
  MAX_LEDGER_RESPONSE_BYTES,
  MAX_LOG_READ_BYTES,
  OPERATION_PATTERN,
} from "./constants.mjs";

const LOG_LINE = /^\[(Warning|Error|Fatal)\s*:\s*([^\]]+)\]\s*(.*)$/i;

function parsedResult(result) {
  if (result === null || result === undefined) return null;
  try { return JSON.parse(result); }
  catch { return result; }
}

function durationMs(record) {
  if (Number.isInteger(record.duration_ms) && record.duration_ms >= 0) return record.duration_ms;
  const start = Date.parse(record.created_utc);
  const finish = Date.parse(record.terminal_utc ?? new Date().toISOString());
  return Number.isFinite(start) && Number.isFinite(finish) ? Math.max(0, finish - start) : null;
}

export function runSummary(record) {
  return {
    operation_id: record.operation_id,
    label: record.label ?? `${record.action}${record.change_id ? ` ${record.change_id}` : ""}`,
    time: record.created_utc,
    outcome: record.state,
    duration_ms: durationMs(record),
    action: record.action,
    change_id: record.change_id ?? null,
    result: parsedResult(record.result),
    error: record.error ?? null,
  };
}

export async function captureLogCursor(logPath) {
  try {
    const info = await stat(logPath);
    if (!info.isFile()) return null;
    return {
      offset: info.size,
      device: info.dev,
      inode: info.ino,
      captured_utc: new Date().toISOString(),
    };
  } catch (error) {
    if (error?.code === "ENOENT") return null;
    throw error;
  }
}

async function logsSinceRun(logPath, cursor) {
  const association = "Messages were observed after the run started; timing does not prove causation.";
  if (cursor === null || typeof cursor?.offset !== "number") {
    return { available: false, association, reason: "no_log_cursor", warning_count: 0, error_count: 0, entries: [] };
  }
  let info;
  try { info = await stat(logPath); }
  catch (error) {
    if (error?.code === "ENOENT") {
      return { available: false, association, reason: "log_unavailable", warning_count: 0, error_count: 0, entries: [] };
    }
    throw error;
  }
  if (!info.isFile() || info.size < cursor.offset
      || (cursor.inode && info.ino && cursor.inode !== info.ino)
      || (cursor.device && info.dev && cursor.device !== info.dev)) {
    return { available: false, association, reason: "log_replaced", warning_count: 0, error_count: 0, entries: [] };
  }

  const availableBytes = info.size - cursor.offset;
  const bytesToRead = Math.min(availableBytes, MAX_LOG_READ_BYTES);
  const start = info.size - bytesToRead;
  const handle = await open(logPath, "r");
  let text;
  try {
    const buffer = Buffer.alloc(bytesToRead);
    const { bytesRead } = await handle.read(buffer, 0, bytesToRead, start);
    text = buffer.subarray(0, bytesRead).toString("utf8");
  } finally {
    await handle.close();
  }
  if (start > cursor.offset) text = text.slice(Math.max(0, text.indexOf("\n") + 1));

  const grouped = new Map();
  let warningCount = 0;
  let errorCount = 0;
  for (const line of text.split(/\r?\n/)) {
    const match = LOG_LINE.exec(line);
    if (!match) continue;
    const level = match[1].toLowerCase() === "warning" ? "warning" : "error";
    if (level === "warning") warningCount += 1;
    else errorCount += 1;
    const source = match[2].trim();
    const message = match[3].trim();
    const key = `${level}\0${source}\0${message}`;
    const existing = grouped.get(key);
    if (existing) existing.count += 1;
    else grouped.set(key, { level, source, message, count: 1 });
  }
  return {
    available: true,
    association,
    through_utc: new Date().toISOString(),
    truncated: availableBytes > MAX_LOG_READ_BYTES,
    warning_count: warningCount,
    error_count: errorCount,
    entries: [...grouped.values()],
  };
}

async function atomicJsonWrite(path, value) {
  const directory = dirname(path);
  await mkdir(directory, { recursive: true, mode: 0o700 });
  const temporary = join(directory, `.${basename(path)}.${randomUUID()}.tmp`);
  let handle;
  try {
    handle = await open(temporary, "wx", 0o600);
    await handle.writeFile(`${JSON.stringify(value, null, 2)}\n`, "utf8");
    await handle.sync();
    await handle.close();
    handle = undefined;
    await rename(temporary, path);
  } finally {
    if (handle) await handle.close().catch(() => {});
    await rm(temporary, { force: true }).catch(() => {});
  }
}

function ledgerPath(root, operationId) {
  if (!OPERATION_PATTERN.test(operationId)) throw new Error("invalid operation_id");
  return join(root, "ledger", `${operationId}.json`);
}

export async function writeLedger(root, record) {
  await atomicJsonWrite(ledgerPath(root, record.operation_id), record);
}

export async function optionalArtifactHash(path) {
  try {
    const info = await stat(path);
    return info.isFile() ? sha256(await readFile(path)) : null;
  } catch (error) {
    if (error?.code === "ENOENT") return null;
    throw error;
  }
}

export async function readLedger(root, args, logPath = join(root, "..", "LogOutput.log")) {
  validateKeys(args, new Set(["operation_id", "limit"]));
  if (args.operation_id !== undefined) {
    if (typeof args.operation_id !== "string" || !OPERATION_PATTERN.test(args.operation_id)) {
      throw new Error("operation_id is invalid");
    }
    try {
      const record = await readSmallJson(ledgerPath(root, args.operation_id), MAX_LEDGER_RESPONSE_BYTES);
      return {
        run: runSummary(record),
        details: record,
        logs_since_run: await logsSinceRun(logPath, record.log_cursor),
      };
    } catch (error) {
      if (error?.code === "ENOENT") return { run: null, details: null, logs_since_run: null };
      throw error;
    }
  }
  const limit = args.limit ?? 20;
  if (!Number.isInteger(limit) || limit < 1 || limit > MAX_LEDGER_LIST) {
    throw new Error(`limit must be an integer from 1 through ${MAX_LEDGER_LIST}`);
  }
  let names;
  try {
    names = await readdir(join(root, "ledger"));
  } catch (error) {
    if (error?.code === "ENOENT") return { runs: [], truncated: false };
    throw error;
  }
  const records = [];
  for (const name of names) {
    if (!name.endsWith(".json") || !OPERATION_PATTERN.test(name.slice(0, -5))) continue;
    try {
      records.push(await readSmallJson(join(root, "ledger", name), MAX_LEDGER_RESPONSE_BYTES));
    } catch {
      // Atomic writes prevent partial records; ignore unrelated or externally corrupted files.
    }
  }
  records.sort((left, right) => String(right.created_utc).localeCompare(String(left.created_utc)));
  const selected = [];
  let selectedBytes = 0;
  for (const record of records.slice(0, limit)) {
    const size = Buffer.byteLength(JSON.stringify(record), "utf8");
    if (selected.length > 0 && selectedBytes + size > MAX_LEDGER_RESPONSE_BYTES) break;
    selected.push(record);
    selectedBytes += size;
  }
  return { runs: selected.map(runSummary), truncated: selected.length < records.length };
}
