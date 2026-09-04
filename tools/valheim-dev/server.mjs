#!/usr/bin/env node

import readline from "node:readline";
import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { plainObject } from "./bridge-compiler.mjs";
import { MCP_PROTOCOL, MAX_MCP_LINE_BYTES, SERVER_VERSION } from "./constants.mjs";
import { createService } from "./service.mjs";

export {
  discoverCompiler,
  loadDescriptor,
  requestBridge,
  runCompiler,
} from "./bridge-compiler.mjs";
export { createService } from "./service.mjs";

function toolResult(structuredContent, isError = false) {
  const result = {
    content: [{ type: "text", text: JSON.stringify(structuredContent) }],
    structuredContent,
  };
  if (isError) result.isError = true;
  return result;
}

function jsonRpcError(id, code, message, data) {
  const error = { code, message };
  if (data !== undefined) error.data = data;
  return { jsonrpc: "2.0", id: id ?? null, error };
}

export function createMcpHandler(service) {
  let initializeAccepted = false;
  let initialized = false;
  return async function handle(message) {
    if (!plainObject(message) || message.jsonrpc !== "2.0" || typeof message.method !== "string") {
      return jsonRpcError(message?.id, -32600, "Invalid Request");
    }
    const notification = message.id === undefined;
    if (message.method === "notifications/initialized") {
      if (initializeAccepted) initialized = true;
      return null;
    }
    if (message.method === "initialize") {
      if (notification) return null;
      const params = message.params;
      const validProtocolVersion = typeof params?.protocolVersion === "string" &&
        /^\d{4}-\d{2}-\d{2}$/.test(params.protocolVersion) &&
        Number.isFinite(Date.parse(`${params.protocolVersion}T00:00:00Z`));
      const validClientInfo = plainObject(params?.clientInfo) &&
        typeof params.clientInfo.name === "string" && params.clientInfo.name.length > 0 &&
        typeof params.clientInfo.version === "string" && params.clientInfo.version.length > 0 &&
        (params.clientInfo.title === undefined || typeof params.clientInfo.title === "string");
      if (!plainObject(params) || !validProtocolVersion || !plainObject(params.capabilities) || !validClientInfo) {
        return jsonRpcError(message.id, -32602, "Malformed initialize parameters");
      }
      initializeAccepted = true;
      return {
        jsonrpc: "2.0",
        id: message.id,
        result: {
          protocolVersion: MCP_PROTOCOL,
          capabilities: { tools: { listChanged: false } },
          serverInfo: { name: "valheim-dev", version: SERVER_VERSION },
        },
      };
    }
    if (message.method === "ping") {
      return notification ? null : { jsonrpc: "2.0", id: message.id, result: {} };
    }
    if (!initialized) {
      return notification ? null : jsonRpcError(message.id, -32002, "Server is not initialized");
    }
    if (message.method === "tools/list") {
      return notification ? null : { jsonrpc: "2.0", id: message.id, result: { tools: service.tools } };
    }
    if (message.method === "tools/call") {
      if (notification) return null;
      try {
        const result = await service.call(message.params?.name, message.params?.arguments ?? {});
        const failed = message.params?.name === "apply_experiment" && result.state !== "succeeded";
        return { jsonrpc: "2.0", id: message.id, result: toolResult(result, failed) };
      } catch (error) {
        return { jsonrpc: "2.0", id: message.id, result: toolResult({ error: error.message }, true) };
      }
    }
    return notification ? null : jsonRpcError(message.id, -32601, "Method not found");
  };
}

export async function serve({ input = process.stdin, output = process.stdout, service } = {}) {
  const selectedService = service ?? createService({ root: process.env.VALHEIM_DEV_ROOT });
  const handle = createMcpHandler(selectedService);
  const lines = readline.createInterface({ input, crlfDelay: Infinity });
  for await (const line of lines) {
    let response;
    if (Buffer.byteLength(line, "utf8") > MAX_MCP_LINE_BYTES) {
      response = jsonRpcError(null, -32700, "JSON-RPC line exceeded size limit");
    } else {
      try {
        response = await handle(JSON.parse(line));
      } catch (error) {
        response = jsonRpcError(null, -32700, `Parse error: ${error.message}`);
      }
    }
    if (response) output.write(`${JSON.stringify(response)}\n`);
  }
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === fileURLToPath(import.meta.url)) {
  try {
    await serve();
  } catch (error) {
    process.stderr.write(`valheim-dev: ${error.message}\n`);
    process.exitCode = 1;
  }
}
