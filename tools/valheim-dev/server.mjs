#!/usr/bin/env node

import { resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { McpServer } from "@modelcontextprotocol/server";
import { serveStdio } from "@modelcontextprotocol/server/stdio";
import { z } from "zod";

import {
  DEFAULT_EVIDENCE_TIMEOUT_MS,
  MAX_EVIDENCE_EVENTS,
  MAX_EVIDENCE_TIMEOUT_MS,
  MAX_LEDGER_LIST,
  SERVER_VERSION,
} from "./constants.mjs";
import { createService } from "./service.mjs";

export {
  discoverCompiler,
  loadDescriptor,
  requestBridge,
  runCompiler,
} from "./bridge-compiler.mjs";
export { createService } from "./service.mjs";

const jsonContainer = z.union([z.record(z.string(), z.json()), z.array(z.json())]);
const evidenceShape = {
  targets: jsonContainer.optional().describe("Target selectors or live handles recorded with this operation."),
  inputs: jsonContainer.optional().describe("Inputs recorded with this operation."),
  evidence_events: z.array(z.string().max(128).regex(/^[^:\s]+:[^:\s]+$/))
    .max(MAX_EVIDENCE_EVENTS).optional(),
  evidence_timeout_ms: z.int().min(0).max(MAX_EVIDENCE_TIMEOUT_MS)
    .default(DEFAULT_EVIDENCE_TIMEOUT_MS),
};
const changeId = z.string().max(128).regex(/^[A-Za-z0-9._-]+$/);

const TOOL_DEFINITIONS = Object.freeze([
  {
    name: "lab_status",
    description: "Read authorization, exact build identity, and every active managed change in the current Valheim Lab session.",
    inputSchema: z.strictObject({}),
  },
  {
    name: "inspect_runtime",
    description: "Compile and run one trusted C# inspection against the authorized live runtime for observation. The bridge does not enforce read-only behavior.",
    inputSchema: z.strictObject({
      source: z.string().min(1).describe(
        "Exact trusted C# source defining public static ValheimDevInspection.Run(): string for observation. The bridge does not enforce read-only behavior.",
      ),
      ...evidenceShape,
    }),
  },
  {
    name: "install_change",
    description: "Install or replace one managed live C# change. Failed-compile preservation is reported only after re-reading the same authorization.",
    inputSchema: z.strictObject({
      change_id: changeId,
      source: z.string().min(1).describe(
        "Exact trusted C# source defining public static ValheimDevChange.Run(): string and Cleanup(): void.",
      ),
      ...evidenceShape,
    }),
  },
  {
    name: "remove_change",
    description: "Run Cleanup for one active managed change and remove it only after cleanup succeeds.",
    inputSchema: z.strictObject({ change_id: changeId }),
  },
  {
    name: "read_ledger",
    description: "Read persistent Valheim Lab operation records, including after the session disconnects.",
    inputSchema: z.strictObject({
      operation_id: z.string().regex(/^[a-f0-9-]{36}$/).optional(),
      limit: z.int().min(1).max(MAX_LEDGER_LIST).default(20),
    }),
  },
]);

const OPERATION_TOOLS = new Set(["inspect_runtime", "install_change", "remove_change"]);

function toolResult(structuredContent, isError = false) {
  const result = {
    content: [{ type: "text", text: JSON.stringify(structuredContent) }],
    structuredContent,
  };
  if (isError) result.isError = true;
  return result;
}

async function callTool(service, name, args) {
  try {
    const result = await service.call(name, args);
    return toolResult(result, OPERATION_TOOLS.has(name) && result.state !== "succeeded");
  } catch (error) {
    return toolResult({ error: error instanceof Error ? error.message : String(error) }, true);
  }
}

export function createMcpServer({ service } = {}) {
  const selectedService = service ?? createService({ root: process.env.VALHEIM_DEV_ROOT });
  const server = new McpServer({ name: "valheim-dev", version: SERVER_VERSION });
  for (const tool of TOOL_DEFINITIONS) {
    server.registerTool(
      tool.name,
      { description: tool.description, inputSchema: tool.inputSchema },
      (args) => callTool(selectedService, tool.name, args),
    );
  }
  return server;
}

const invokedPath = process.argv[1] ? resolve(process.argv[1]) : "";
if (invokedPath === fileURLToPath(import.meta.url)) {
  serveStdio(() => createMcpServer(), {
    onerror(error) {
      process.stderr.write(`valheim-dev: ${error.message}\n`);
    },
  });
}
