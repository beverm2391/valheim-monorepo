import { randomUUID } from "node:crypto";
import { mkdir, open, readFile, readdir, rename, rm, stat } from "node:fs/promises";
import { basename, dirname, join } from "node:path";

import { readSmallJson, sha256, validateKeys } from "./bridge-compiler.mjs";
import {
  MAX_LEDGER_LIST,
  MAX_LEDGER_RESPONSE_BYTES,
  OPERATION_PATTERN,
} from "./constants.mjs";

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

export async function readLedger(root, args) {
  validateKeys(args, new Set(["operation_id", "limit"]));
  if (args.operation_id !== undefined) {
    if (typeof args.operation_id !== "string" || !OPERATION_PATTERN.test(args.operation_id)) {
      throw new Error("operation_id is invalid");
    }
    try {
      const record = await readSmallJson(ledgerPath(root, args.operation_id), MAX_LEDGER_RESPONSE_BYTES);
      return { record, records: [], truncated: false };
    } catch (error) {
      if (error?.code === "ENOENT") return { record: null, records: [], truncated: false };
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
    if (error?.code === "ENOENT") return { record: null, records: [], truncated: false };
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
  return { record: null, records: selected, truncated: selected.length < records.length };
}
