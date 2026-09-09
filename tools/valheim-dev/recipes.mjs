import { readFile } from "node:fs/promises";
import { join } from "node:path";

import { plainObject, validateKeys } from "./bridge-compiler.mjs";
import { MAX_RUN_LABEL_BYTES, MAX_SOURCE_BYTES } from "./constants.mjs";

const ENTRYPOINTS = Object.freeze({
  run_once: /\bpublic\s+static\s+class\s+ValheimDevCommand\b/,
  install_change: /\bpublic\s+static\s+class\s+ValheimDevChange\b/,
});

function validateRecipeId(value) {
  if (typeof value !== "string" || !/^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$/.test(value)) {
    throw new Error("recipe id must contain 1-128 letters, digits, dots, underscores, or hyphens and start with a letter or digit");
  }
  return value;
}

function validateRecipeRequest(value) {
  validateKeys(value, new Set(["id", "preset_index", "inputs"]));
  const id = validateRecipeId(value.id);
  if (value.preset_index !== undefined && value.inputs !== undefined) {
    throw new Error(`${id}: choose preset_index or inputs, not both`);
  }
  if (value.preset_index !== undefined
      && (!Number.isInteger(value.preset_index) || value.preset_index < 0)) {
    throw new Error(`${id}: preset_index must be a non-negative integer`);
  }
  if (value.inputs !== undefined && !plainObject(value.inputs) && !Array.isArray(value.inputs)) {
    throw new Error(`${id}: inputs must be a JSON object or array`);
  }
  return { id, preset_index: value.preset_index, inputs: value.inputs };
}

function recipeAction(source, id) {
  const matches = Object.entries(ENTRYPOINTS)
    .filter(([, pattern]) => pattern.test(source))
    .map(([action]) => action);
  if (matches.length !== 1) {
    throw new Error(`${id}: code.cs must define exactly one supported public static entrypoint class`);
  }
  return matches[0];
}

async function readRecipe(recipesRoot, rawRequest) {
  const request = validateRecipeRequest(rawRequest);
  const directory = join(recipesRoot, request.id);
  const source = await readFile(join(directory, "code.cs"), "utf8");
  if (Buffer.byteLength(source, "utf8") > MAX_SOURCE_BYTES) {
    throw new Error(`${request.id}: code.cs exceeds size limit`);
  }
  let inputs = request.inputs;
  if (request.preset_index !== undefined) {
    const rawPresets = await readFile(join(directory, "presets.json"));
    if (rawPresets.byteLength > MAX_SOURCE_BYTES) {
      throw new Error(`${request.id}: presets.json exceeds size limit`);
    }
    let presets;
    try { presets = JSON.parse(rawPresets.toString("utf8")); }
    catch (error) { throw new Error(`${request.id}: presets.json is invalid JSON: ${error.message}`); }
    if (!Array.isArray(presets)
        || presets.some((preset) => !plainObject(preset) && !Array.isArray(preset))) {
      throw new Error(`${request.id}: presets.json must be an array of JSON objects or arrays`);
    }
    if (request.preset_index >= presets.length) {
      throw new Error(`${request.id}: preset_index ${request.preset_index} does not exist`);
    }
    inputs = presets[request.preset_index];
  }
  return { ...request, source, inputs, action: recipeAction(source, request.id) };
}

export function createRecipeRunner({ recipesRoot, runCodeOperation, readStatus }) {
  return async function runRecipes(args) {
    validateKeys(args, new Set(["recipes"]));
    if (!Array.isArray(args.recipes) || args.recipes.length === 0) {
      throw new Error("recipes must be a non-empty array");
    }

    const outcomes = [];
    let lastKnownActiveChanges = null;
    let stopReason = null;
    for (const rawRequest of args.recipes) {
      if (stopReason !== null) {
        outcomes.push({
          recipe_id: typeof rawRequest?.id === "string" ? rawRequest.id : null,
          preset_index: rawRequest?.preset_index ?? null,
          action: null,
          state: "not_attempted",
          error: stopReason,
        });
        continue;
      }
      let recipe;
      try {
        recipe = await readRecipe(recipesRoot, rawRequest);
        const record = await runCodeOperation({
          label: recipe.id.slice(0, MAX_RUN_LABEL_BYTES),
          source: recipe.source,
          inputs: recipe.inputs,
          ...(recipe.action === "install_change" ? { change_id: recipe.id } : {}),
        }, recipe.action);
        outcomes.push({
          recipe_id: recipe.id,
          preset_index: recipe.preset_index ?? null,
          action: recipe.action,
          record,
        });
        if (Array.isArray(record.active_changes)) lastKnownActiveChanges = record.active_changes;
        if (record.state === "runtime_unresolved" || record.restart_required === true) {
          stopReason = `${recipe.id} left runtime state uncertain; remaining recipes were not attempted`;
        }
      } catch (error) {
        outcomes.push({
          recipe_id: recipe?.id ?? (typeof rawRequest?.id === "string" ? rawRequest.id : null),
          preset_index: recipe?.preset_index ?? rawRequest?.preset_index ?? null,
          action: recipe?.action ?? null,
          state: "failed",
          error: error instanceof Error ? error.message : String(error),
        });
      }
    }

    const status = await readStatus();
    const activeChanges = status.connected === true ? status.active_changes : lastKnownActiveChanges;
    return {
      recipes: outcomes,
      active_changes: activeChanges,
      active_changes_error: activeChanges === null ? status.error : null,
      restart_required: status.restart_required === true
        || outcomes.some((outcome) => outcome.record?.restart_required === true),
    };
  };
}
