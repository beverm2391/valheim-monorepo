import { spawn } from "node:child_process";
import { createHash } from "node:crypto";
import { constants as fsConstants } from "node:fs";
import { access, readFile, stat } from "node:fs/promises";
import { createConnection } from "node:net";
import { isAbsolute, join } from "node:path";

import {
  BRIDGE_PROTOCOL,
  COMPILE_TIMEOUT_MS,
  MAX_BRIDGE_RESPONSE_BYTES,
  SHA256_PATTERN,
} from "./constants.mjs";

export function sha256(value) {
  return createHash("sha256").update(value).digest("hex");
}

export function isoNow() {
  return new Date().toISOString();
}

export function plainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

export function validateKeys(value, allowed) {
  if (!plainObject(value)) throw new Error("arguments must be an object");
  const unexpected = Object.keys(value).find((key) => !allowed.has(key));
  if (unexpected) throw new Error(`unexpected argument: ${unexpected}`);
}

function assertString(value, name) {
  if (typeof value !== "string" || value.length === 0) {
    throw new Error(`${name} must be a non-empty string`);
  }
}

function validateBuildFields(value, label) {
  assertString(value[`${label}_version`], `${label}_version`);
  if (!SHA256_PATTERN.test(value[`${label}_sha256`] ?? "")) {
    throw new Error(`${label}_sha256 must be a SHA-256 hex digest`);
  }
}

export function buildIdentity(value) {
  return {
    valheim_version: value.valheim_version,
    valheim_sha256: value.valheim_sha256,
    benheim_version: value.benheim_version,
    benheim_sha256: value.benheim_sha256,
  };
}

export async function readSmallJson(path, maxBytes) {
  const info = await stat(path);
  if (!info.isFile() || info.size > maxBytes) {
    throw new Error(`invalid JSON file: ${path}`);
  }
  return JSON.parse(await readFile(path, "utf8"));
}

export async function loadDescriptor(root) {
  const descriptorPath = join(root, "session.json");
  const descriptor = await readSmallJson(descriptorPath, 256 * 1024);
  if (!plainObject(descriptor)) throw new Error("session descriptor must be an object");
  if (descriptor.protocol !== BRIDGE_PROTOCOL) throw new Error("unsupported bridge protocol");
  assertString(descriptor.session_id, "session_id");
  assertString(descriptor.generation, "generation");
  assertString(descriptor.token, "token");
  if (descriptor.host !== "127.0.0.1") throw new Error("bridge host must be 127.0.0.1");
  if (!Number.isInteger(descriptor.port) || descriptor.port < 1 || descriptor.port > 65535) {
    throw new Error("bridge port is invalid");
  }
  assertString(descriptor.authorized_at, "authorized_at");
  if (!Number.isFinite(Date.parse(descriptor.authorized_at))) throw new Error("authorized_at is invalid");
  if (descriptor.authorized === false || descriptor.closed_at || descriptor.revoked_at) {
    throw new Error("Lab authorization is closed");
  }
  validateBuildFields(descriptor, "valheim");
  validateBuildFields(descriptor, "benheim");
  if (!Array.isArray(descriptor.compiler_references) || descriptor.compiler_references.length === 0) {
    throw new Error("compiler_references must be a non-empty array");
  }
  for (const reference of descriptor.compiler_references) {
    if (typeof reference !== "string" || !isAbsolute(reference)) {
      throw new Error("compiler reference paths must be absolute");
    }
    const info = await stat(reference);
    if (!info.isFile()) throw new Error(`compiler reference is not a file: ${reference}`);
  }
  return Object.freeze({ ...descriptor, compiler_references: [...descriptor.compiler_references] });
}

export function sameAuthorization(left, right) {
  return left.session_id === right.session_id &&
    left.generation === right.generation &&
    left.token === right.token &&
    left.host === right.host &&
    left.port === right.port;
}

export function validateBridgeIdentity(response, descriptor) {
  if (!plainObject(response)) throw new Error("bridge response must be an object");
  if (response.protocol !== BRIDGE_PROTOCOL) throw new Error("bridge response protocol mismatch");
  if (response.session_id !== descriptor.session_id) throw new Error("bridge session identity mismatch");
  if (response.generation !== descriptor.generation) throw new Error("bridge generation mismatch");
  validateBuildFields(response, "valheim");
  validateBuildFields(response, "benheim");
  for (const [key, expected] of Object.entries(buildIdentity(descriptor))) {
    if (response[key] !== expected) throw new Error(`bridge ${key} mismatch`);
  }
}

export function requestBridge(descriptor, request, timeoutMs) {
  return new Promise((resolveRequest, rejectRequest) => {
    let settled = false;
    let response = "";
    const socket = createConnection({ host: descriptor.host, port: descriptor.port });
    const finish = (error, value) => {
      if (settled) return;
      settled = true;
      socket.destroy();
      if (error) rejectRequest(error);
      else resolveRequest(value);
    };
    socket.setEncoding("utf8");
    socket.setTimeout(timeoutMs, () => {
      const error = new Error("bridge request timed out");
      error.code = "BRIDGE_TIMEOUT";
      finish(error);
    });
    socket.on("connect", () => {
      socket.write(`${JSON.stringify({
        ...request,
        protocol: BRIDGE_PROTOCOL,
        token: descriptor.token,
        generation: descriptor.generation,
      })}\n`);
    });
    socket.on("data", (chunk) => {
      response += chunk;
      if (Buffer.byteLength(response, "utf8") > MAX_BRIDGE_RESPONSE_BYTES) {
        finish(new Error("bridge response exceeded size limit"));
        return;
      }
      const newline = response.indexOf("\n");
      if (newline === -1) return;
      const trailing = response.slice(newline + 1).trim();
      if (trailing) {
        finish(new Error("bridge returned more than one response"));
        return;
      }
      try {
        finish(null, JSON.parse(response.slice(0, newline)));
      } catch (error) {
        finish(new Error(`invalid bridge JSON: ${error.message}`));
      }
    });
    socket.on("end", () => {
      if (!settled) finish(new Error("bridge disconnected before a complete response"));
    });
    socket.on("error", (error) => finish(new Error(`bridge connection failed: ${error.message}`)));
  });
}

function parseSdkListing(output) {
  const candidates = [];
  for (const line of output.split(/\r?\n/)) {
    const match = line.match(/^([^\s]+)\s+\[([^\]]+)\]\s*$/);
    if (match) candidates.push(join(match[2], match[1], "Roslyn", "bincore", "csc.dll"));
  }
  return candidates.reverse();
}

function spawnCaptured(command, args, { timeoutMs, maxBytes = 4 * 1024 * 1024 } = {}) {
  return new Promise((resolveSpawn, rejectSpawn) => {
    const child = spawn(command, args, { shell: false, stdio: ["ignore", "pipe", "pipe"] });
    let stdout = "";
    let stderr = "";
    let overflow = false;
    let timedOut = false;
    const timer = setTimeout(() => {
      timedOut = true;
      child.kill("SIGKILL");
    }, timeoutMs);
    const append = (which, chunk) => {
      if (overflow) return;
      if (which === "stdout") stdout += chunk;
      else stderr += chunk;
      if (Buffer.byteLength(stdout) + Buffer.byteLength(stderr) > maxBytes) {
        overflow = true;
        child.kill("SIGKILL");
      }
    };
    child.stdout.setEncoding("utf8");
    child.stderr.setEncoding("utf8");
    child.stdout.on("data", (chunk) => append("stdout", chunk));
    child.stderr.on("data", (chunk) => append("stderr", chunk));
    child.on("error", (error) => {
      clearTimeout(timer);
      rejectSpawn(error);
    });
    child.on("close", (code, signal) => {
      clearTimeout(timer);
      resolveSpawn({ code, signal, stdout, stderr, timed_out: timedOut, output_overflow: overflow });
    });
  });
}

export async function discoverCompiler(dotnetPath = "dotnet", explicitCsc = process.env.VALHEIM_DEV_CSC_DLL) {
  if (explicitCsc) {
    if (!isAbsolute(explicitCsc)) throw new Error("VALHEIM_DEV_CSC_DLL must be absolute");
    await access(explicitCsc, fsConstants.R_OK);
    return { dotnetPath, cscDll: explicitCsc };
  }
  const listing = await spawnCaptured(dotnetPath, ["--list-sdks"], { timeoutMs: 10_000, maxBytes: 256 * 1024 });
  if (listing.code !== 0) throw new Error(`dotnet --list-sdks failed: ${listing.stderr.trim()}`);
  for (const candidate of parseSdkListing(listing.stdout)) {
    try {
      await access(candidate, fsConstants.R_OK);
      return { dotnetPath, cscDll: candidate };
    } catch {
      // Continue to older installed SDKs if the newest listing is incomplete.
    }
  }
  throw new Error("could not locate Roslyn csc.dll in an installed dotnet SDK");
}

export async function runCompiler({ descriptor, sourcePath, assemblyPath, compiler }) {
  const resolvedCompiler = compiler ?? await discoverCompiler();
  const args = [
    resolvedCompiler.cscDll,
    "-noconfig",
    "-nostdlib+",
    "-target:library",
    "-langversion:latest",
    `-out:${assemblyPath}`,
    ...descriptor.compiler_references.map((path) => `-reference:${path}`),
    sourcePath,
  ];
  const outcome = await spawnCaptured(resolvedCompiler.dotnetPath, args, {
    timeoutMs: COMPILE_TIMEOUT_MS,
    maxBytes: 512 * 1024,
  });
  return { ...outcome, command: resolvedCompiler.dotnetPath, csc_dll: resolvedCompiler.cscDll, arguments: args.slice(1) };
}
