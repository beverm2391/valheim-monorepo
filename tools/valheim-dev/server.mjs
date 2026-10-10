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
  targets: jsonContainer.optional().describe("Target context recorded with this operation."),
  inputs: jsonContainer.optional().describe(
    "Structured input passed to Run as a serialized JSON object or array.",
  ),
  evidence_events: z.array(z.string().max(128).regex(/^[^:\s]+:[^:\s]+$/))
    .max(MAX_EVIDENCE_EVENTS).optional(),
  evidence_timeout_ms: z.int().min(0).max(MAX_EVIDENCE_TIMEOUT_MS)
    .default(DEFAULT_EVIDENCE_TIMEOUT_MS),
};
const changeId = z.string().max(128).regex(/^[A-Za-z0-9._-]+$/);
const runLabel = z.string().trim().min(1).max(120).describe("Short human label shown in run history.");
const recipeRequest = z.strictObject({
  id: z.string().max(128).regex(/^[A-Za-z0-9][A-Za-z0-9._-]*$/)
    .describe("Recipe folder name under the Valheim Dev registry."),
  preset_index: z.int().min(0).optional().describe("Zero-based index in the recipe's presets.json array."),
  inputs: jsonContainer.optional().describe("Ad hoc structured inputs for this recipe."),
}).refine((value) => value.preset_index === undefined || value.inputs === undefined, {
  message: "choose preset_index or inputs, not both",
});
const appId = z.string().regex(/^[a-f0-9]{32}$/).describe("Current app_id returned by session_status; binds this action to the observed game process.");
const labName = z.string().regex(/^Lab-[A-Za-z0-9_-]{1,44}$/).describe("Disposable native local save name beginning Lab-.");

const TOOL_DEFINITIONS = Object.freeze([
  {
    name: "lab_status",
    description: "Read authorization, exact build identity, and every active managed change in the current Valheim Lab session.",
    inputSchema: z.strictObject({}),
  },
  {
    name: "run_once",
    description: "Compile and run one trusted C# command against the authorized live runtime. The command may observe or change the disposable world.",
    inputSchema: z.strictObject({
      label: runLabel,
      source: z.string().min(1).describe(
        "Exact trusted C# source defining public static ValheimDevCommand.Run(string inputJson): string. Return a JSON object or array string.",
      ),
      ...evidenceShape,
    }),
  },
  {
    name: "install_change",
    description: "Install or replace one managed live C# change. Failed-compile preservation is reported only after re-reading the same authorization.",
    inputSchema: z.strictObject({
      label: runLabel,
      change_id: changeId,
      source: z.string().min(1).describe(
        "Exact trusted C# source defining public static ValheimDevChange.Run(string inputJson): string and Cleanup(): void. Return a JSON object or array string.",
      ),
      ...evidenceShape,
    }),
  },
  {
    name: "remove_change",
    description: "Run Cleanup for one active managed change and remove it only after cleanup succeeds.",
    inputSchema: z.strictObject({ label: runLabel, change_id: changeId }),
  },
  {
    name: "reset_lab",
    description: "Retry every managed Cleanup in reverse installation order, including after restart-required lockout, and clear the lockout only after every change is removed.",
    inputSchema: z.strictObject({}),
  },
  {
    name: "run_recipes",
    description: "Run one or more disposable registry recipes in order. Each recipe may select a preset index or use ad hoc inputs; managed recipes replace the active change with the same recipe ID.",
    inputSchema: z.strictObject({
      recipes: z.array(recipeRequest).min(1),
    }),
  },
  {
    name: "read_ledger",
    description: "Read persistent Valheim Lab operation records, including after the session disconnects.",
    inputSchema: z.strictObject({
      operation_id: z.string().regex(/^[a-f0-9-]{36}$/).optional(),
      limit: z.int().min(1).max(MAX_LEDGER_LIST).default(20),
    }),
  },
  {
    name: "session_status",
    description: "Inspect actual local Valheim processes, menu/world/character, exact loaded build and Lab access without launching or changing the game.",
    inputSchema: z.strictObject({}),
  },
  {
    name: "list_saves",
    description: "List native worlds and characters through the app bridge, including before in-world Lab access. Does not launch Valheim.",
    inputSchema: z.strictObject({}),
  },
  {
    name: "create_lab_world",
    description: "Create a disposable native local Lab- world at the main menu. Refuses existing names and never overwrites saves.",
    inputSchema: z.strictObject({ app_id: appId, name: labName, seed: z.string().max(10).regex(/^[A-Za-z0-9]*$/).optional() }),
  },
  {
    name: "create_lab_character",
    description: "Create a disposable native local Lab- character at the main menu. Refuses existing names and never overwrites saves.",
    inputSchema: z.strictObject({ app_id: appId, name: labName }),
  },
  {
    name: "open_lab",
    description: "Launch the existing managed profile if stopped, or select a disposable local world and character at its menu. Omitting both save names opens only the menu. An existing process requires its current app_id. Waits for the actual transition.",
    inputSchema: z.strictObject({ app_id: appId.optional(), world: labName.optional(), character: labName.optional() })
      .refine((value) => (value.world === undefined) === (value.character === undefined), { message: "choose both world and character" }),
  },
  {
    name: "close_lab",
    description: "Request native clean quit only for the tools' owned Lab session and confirm the process exits. Refuses occupied normal or multiplayer sessions.",
    inputSchema: z.strictObject({ app_id: appId }),
  },
]);

const OPERATION_TOOLS = new Set(["run_once", "install_change", "remove_change", "reset_lab"]);

export function operationSummary(record) {
  const summary = {
    state: record.state,
    operation_id: record.operation_id,
    result: record.result === null ? null : JSON.parse(record.result),
    error: record.error,
  };
  if (record.action === "install_change" || record.action === "remove_change") {
    summary.change_id = record.change_id;
    summary.cleanup_state = record.cleanup_state;
    summary.previous_change_preserved = record.previous_change_preserved;
    summary.restart_required = record.restart_required;
    summary.active_changes = structuredChanges(record.active_changes);
  } else if (record.action === "reset_lab") {
    summary.cleanup_state = record.cleanup_state;
    summary.restart_required = record.restart_required;
    summary.active_changes = structuredChanges(record.active_changes);
  } else if (record.restart_required) {
    summary.restart_required = true;
  }
  if (record.evidence_selected) {
    summary.evidence_available = record.evidence_available;
    summary.evidence_unavailable_reason = record.evidence_unavailable_reason;
    summary.evidence_events = record.evidence_events;
    summary.evidence_truncated = record.evidence_truncated;
    summary.dropped_evidence_events = record.dropped_evidence_events;
  }
  return summary;
}

function structuredChanges(changes) {
  if (!Array.isArray(changes)) return changes;
  return changes.map((change) => ({
    ...change,
    result: change.result === null ? null : JSON.parse(change.result),
  }));
}

function structuredStatus(status) {
  return { ...status, active_changes: structuredChanges(status.active_changes) };
}

function toolResult(structuredContent, isError = false) {
  const result = {
    content: [{ type: "text", text: JSON.stringify(structuredContent) }],
    structuredContent,
  };
  if (isError) result.isError = true;
  return result;
}

function recipeBatchSummary(batch) {
  return {
    recipes: batch.recipes.map((outcome) => {
      if (!outcome.record) {
        return {
          recipe_id: outcome.recipe_id,
          preset_index: outcome.preset_index,
          action: outcome.action,
          state: outcome.state,
          operation_id: null,
          error: outcome.error,
        };
      }
      const { active_changes: _activeChanges, ...summary } = operationSummary(outcome.record);
      return {
        recipe_id: outcome.recipe_id,
        preset_index: outcome.preset_index,
        action: outcome.action,
        ...summary,
      };
    }),
    active_changes: structuredChanges(batch.active_changes),
    active_changes_error: batch.active_changes_error,
    restart_required: batch.restart_required,
  };
}

async function callTool(service, name, args) {
  try {
    const result = await service.call(name, args);
    const output = OPERATION_TOOLS.has(name)
      ? operationSummary(result)
      : name === "lab_status" ? structuredStatus(result)
        : name === "run_recipes" ? recipeBatchSummary(result) : result;
    const isError = OPERATION_TOOLS.has(name)
      ? result.state !== "succeeded"
      : name === "run_recipes"
        && result.recipes.some((outcome) => outcome.record?.state !== "succeeded")
        || ["open_lab", "close_lab", "create_lab_world", "create_lab_character"].includes(name) && result.state !== "succeeded";
    return toolResult(output, isError);
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
