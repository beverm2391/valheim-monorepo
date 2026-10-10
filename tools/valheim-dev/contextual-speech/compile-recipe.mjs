import { mkdtemp, readdir, rm, stat } from "node:fs/promises";
import { tmpdir } from "node:os";
import { dirname, isAbsolute, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { discoverCompiler, runCompiler } from "../bridge-compiler.mjs";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const recipeSource = resolve(
  scriptDirectory,
  "../recipe-templates/greydwarf-resident-contextual-speech/code.cs",
);

async function dllFiles(directory) {
  return (await readdir(directory, { withFileTypes: true }))
    .filter((entry) => entry.isFile() && entry.name.toLowerCase().endsWith(".dll"))
    .map((entry) => join(directory, entry.name))
    .sort();
}

async function requireDirectory(path, name) {
  let info;
  try {
    info = await stat(path);
  } catch {
    throw new Error(`${name} directory is missing: ${path}`);
  }
  if (!info.isDirectory()) throw new Error(`${name} is not a directory: ${path}`);
}

async function compileRecipe() {
  const gameDirectory = process.env.VALHEIM_GAME_DIR;
  if (!gameDirectory || !isAbsolute(gameDirectory)) {
    throw new Error("VALHEIM_GAME_DIR must be set to the absolute active Valheim profile path.");
  }

  const managedDirectory = join(
    gameDirectory,
    "valheim.app/Contents/Resources/Data/Managed",
  );
  const coreDirectory = join(gameDirectory, "BepInEx/core");
  const pluginAssembly = join(gameDirectory, "BepInEx/plugins/ValheimDev/ValheimDev.dll");
  await requireDirectory(managedDirectory, "Valheim managed assemblies");
  await requireDirectory(coreDirectory, "BepInEx core assemblies");
  const compilerReferences = [
    ...(await dllFiles(managedDirectory)),
    ...(await dllFiles(coreDirectory)),
  ];
  try {
    if ((await stat(pluginAssembly)).isFile()) compilerReferences.push(pluginAssembly);
  } catch {
    // The recipe currently uses game and Unity APIs only; the plugin reference
    // is included when Valheim Dev is installed, as it is in the live profile.
  }
  if (compilerReferences.length === 0) throw new Error("No installed compiler references were found.");

  const compiler = await discoverCompiler();
  const temporaryDirectory = await mkdtemp(join(tmpdir(), "valheim-dev-contextual-speech-"));
  try {
    const outcome = await runCompiler({
      descriptor: { compiler_references: compilerReferences },
      sourcePath: recipeSource,
      assemblyPath: join(temporaryDirectory, "contextual-speech.dll"),
      compiler,
    });
    if (outcome.code !== 0) {
      const diagnostics = [outcome.stdout.trim(), outcome.stderr.trim()].filter(Boolean).join("\n");
      throw new Error(`Contextual speech recipe compilation failed${diagnostics ? `:\n${diagnostics}` : "."}`);
    }
    const assembly = await stat(join(temporaryDirectory, "contextual-speech.dll"));
    if (!assembly.isFile() || assembly.size === 0) {
      throw new Error("Roslyn reported success without producing a compiled recipe assembly.");
    }
    console.log(`Contextual speech recipe compiled against ${compilerReferences.length} installed assemblies.`);
  } finally {
    await rm(temporaryDirectory, { recursive: true, force: true });
  }
}

compileRecipe().catch((error) => {
  console.error(error instanceof Error ? error.message : String(error));
  process.exitCode = 1;
});
