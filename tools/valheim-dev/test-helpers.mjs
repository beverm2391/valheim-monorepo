import { execFile } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { once } from "node:events";
import { mkdir, mkdtemp, readFile, readdir, writeFile } from "node:fs/promises";
import { createServer } from "node:net";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { promisify } from "node:util";

const VALHEIM_HASH = "a".repeat(64);
const VALHEIM_DEV_HASH = "b".repeat(64);
const execFileAsync = promisify(execFile);

export const SOURCE = "public static class ValheimDevCommand { public static string Run(string inputJson) => \"{\\\"ok\\\":true}\"; }";
export const CHANGE_SOURCE = "public static class ValheimDevChange { public static string Run(string inputJson) => \"{\\\"active\\\":true}\"; public static void Cleanup() { } }";

export function iconVariantSource(variant) {
  return `using System;
using System.Reflection;

public static class ValheimDevChange
{
    private static MethodInfo setter;
    private static string previousVariant;

    public static string Run(string inputJson)
    {
        Type runtime = Type.GetType("BenheimQoL.Affinities.IconRuntime, BenheimQoL", true);
        PropertyInfo variantProperty = runtime.GetProperty("Variant", BindingFlags.Static | BindingFlags.NonPublic);
        setter = runtime.GetMethod("SetVariant", BindingFlags.Static | BindingFlags.NonPublic);
        if (variantProperty == null || setter == null) return "{\\\"error\\\":\\\"missing_icon_runtime\\\"}";
        previousVariant = (string)variantProperty.GetValue(null);
        setter.Invoke(null, new object[] { "${variant}" });
        return "{\\\"previous\\\":\\\"" + previousVariant + "\\\",\\\"variant\\\":\\\"${variant}\\\"}";
    }

    public static void Cleanup()
    {
        if (setter != null) setter.Invoke(null, new object[] { previousVariant });
    }
}`;
}

export function digest(value) {
  return createHash("sha256").update(value).digest("hex");
}

export async function temporaryRoot() {
  const root = await mkdtemp(join(tmpdir(), "valheim-dev-test-"));
  const reference = join(root, "reference.dll");
  await writeFile(reference, "reference");
  return { root, reference };
}

export async function installedDotnetReferenceSet(cscDll) {
  let dotnetRoot = cscDll;
  for (let level = 0; level < 5; level += 1) dotnetRoot = dirname(dotnetRoot);
  const packRoot = join(dotnetRoot, "packs", "Microsoft.NETCore.App.Ref");
  const packVersions = (await readdir(packRoot, { withFileTypes: true }))
    .filter((entry) => entry.isDirectory())
    .map((entry) => entry.name)
    .sort((left, right) => right.localeCompare(left, undefined, { numeric: true }));
  for (const packVersion of packVersions) {
    const refRoot = join(packRoot, packVersion, "ref");
    const frameworks = (await readdir(refRoot, { withFileTypes: true }))
      .filter((entry) => entry.isDirectory())
      .map((entry) => entry.name)
      .sort((left, right) => right.localeCompare(left, undefined, { numeric: true }));
    for (const framework of frameworks) {
      const directory = join(refRoot, framework);
      const available = new Set(await readdir(directory));
      const fixtureNames = ["mscorlib.dll", "System.Runtime.dll", "System.Reflection.dll"];
      if (fixtureNames.every((name) => available.has(name))) {
        return fixtureNames.map((name) => join(directory, name));
      }
    }
  }
  throw new Error("installed dotnet SDK has no reference pack for the offline compile proof");
}

export async function buildOfflineIconHarness(root) {
  const fixtureDirectory = join(root, "offline-benheim-fixture");
  const runnerDirectory = join(root, "offline-experiment-runner");
  await mkdir(fixtureDirectory);
  await mkdir(runnerDirectory);
  await writeFile(join(fixtureDirectory, "BenheimQoL.csproj"), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AssemblyName>BenheimQoL</AssemblyName>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
`);
  await writeFile(join(fixtureDirectory, "IconRuntime.cs"), `namespace BenheimQoL.Affinities;

internal static class IconRuntime
{
    internal static string Variant { get; private set; } = "baseline";

    internal static void SetVariant(string variant)
    {
        Variant = variant;
    }
}
`);
  await writeFile(join(runnerDirectory, "OfflineExperimentRunner.csproj"), `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
</Project>
`);
  await writeFile(join(runnerDirectory, "Program.cs"), `using System;
using System.IO;
using System.Reflection;

internal static class Program
{
    private static int Main(string[] args)
    {
        Assembly fixture = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        Type icon = fixture.GetType("BenheimQoL.Affinities.IconRuntime", true);
        PropertyInfo variant = icon.GetProperty("Variant", BindingFlags.Static | BindingFlags.NonPublic);
        Assembly experiment = Assembly.LoadFrom(Path.GetFullPath(args[1]));
        Type entry = experiment.GetType("ValheimDevChange", true);
        MethodInfo run = entry.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
        MethodInfo cleanup = entry.GetMethod("Cleanup", BindingFlags.Public | BindingFlags.Static);
        string before = (string)variant.GetValue(null);
        string result = (string)run.Invoke(null, new object[] { "{}" });
        string afterRun = (string)variant.GetValue(null);
        cleanup.Invoke(null, null);
        string afterCleanup = (string)variant.GetValue(null);
        Console.WriteLine(fixture.GetName().Name + "|" + before + "|" + afterRun + "|" + afterCleanup + "|" + result);
        return 0;
    }
}
`);
  const buildOptions = { timeout: 60_000, maxBuffer: 4 * 1024 * 1024 };
  await execFileAsync("dotnet", ["build", join(fixtureDirectory, "BenheimQoL.csproj"), "--configuration", "Release", "--nologo", "--verbosity", "quiet"], buildOptions);
  await execFileAsync("dotnet", ["build", join(runnerDirectory, "OfflineExperimentRunner.csproj"), "--configuration", "Release", "--nologo", "--verbosity", "quiet"], buildOptions);
  return {
    fixtureAssembly: join(fixtureDirectory, "bin", "Release", "net8.0", "BenheimQoL.dll"),
    runnerAssembly: join(runnerDirectory, "bin", "Release", "net8.0", "OfflineExperimentRunner.dll"),
  };
}

export async function executeOfflineVariant(harness, experimentAssembly) {
  const { stdout } = await execFileAsync(
    "dotnet",
    [harness.runnerAssembly, harness.fixtureAssembly, experimentAssembly],
    { timeout: 30_000, maxBuffer: 1024 * 1024 },
  );
  const [fixtureName, before, afterRun, afterCleanup, result] = stdout.trim().split("|");
  return {
    fixtureName,
    before,
    afterRun,
    afterCleanup,
    result,
  };
}

export async function writeDescriptor(root, reference, port, overrides = {}) {
  const descriptor = {
    protocol: 5,
    session_id: randomUUID(),
    host: "127.0.0.1",
    port,
    authorized_at: new Date().toISOString(),
    valheim_version: "1.0.0",
    valheim_sha256: VALHEIM_HASH,
    valheim_dev_version: "0.3.0",
    valheim_dev_sha256: VALHEIM_DEV_HASH,
    data_root: root,
    log_path: join(root, "LogOutput.log"),
    compiler_references: [reference],
    ...overrides,
  };
  await writeFile(join(root, "session.json"), `${JSON.stringify(descriptor)}\n`);
  return descriptor;
}

export async function startBridge(handler) {
  const requests = [];
  const server = createServer((socket) => {
    socket.setEncoding("utf8");
    let input = "";
    socket.on("data", async (chunk) => {
      input += chunk;
      const newline = input.indexOf("\n");
      if (newline === -1) return;
      const request = JSON.parse(input.slice(0, newline));
      requests.push(request);
      try {
        const response = await handler(request);
        socket.end(`${JSON.stringify(response)}\n`);
      } catch {
        socket.destroy();
      }
    });
  });
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  return {
    server,
    requests,
    port: server.address().port,
    async close() {
      server.close();
      await once(server, "close");
    },
  };
}

export function bridgeIdentity(descriptor, extra = {}) {
  return {
    protocol: 5,
    ok: true,
    error: null,
    session_id: descriptor.session_id,
    valheim_version: descriptor.valheim_version,
    valheim_sha256: descriptor.valheim_sha256,
    valheim_dev_version: descriptor.valheim_dev_version,
    valheim_dev_sha256: descriptor.valheim_dev_sha256,
    authorized: true,
    restart_required: false,
    active_changes: [],
    ...extra,
  };
}

export function operationResponse(descriptor, request, extra = {}) {
  return bridgeIdentity(descriptor, {
    authorized: true,
    action: request.kind,
    operation_id: request.operation_id,
    change_id: request.change_id ?? "",
    started_utc: "2026-09-04T00:00:00.000Z",
    finished_utc: "2026-09-04T00:00:00.010Z",
    result: "{\"ok\":true}",
    exception: null,
    cleanup_state: request.kind === "install_change" ? "active" : "not_applicable",
    previous_change_preserved: false,
    evidence_selected: request.evidence_events?.length > 0,
    evidence_available: true,
    evidence_unavailable_reason: null,
    evidence_exhaustive: false,
    evidence_truncated: false,
    dropped_evidence_events: 0,
    evidence_events: [],
    active_changes: [],
    ...extra,
  });
}

export function managedChange(changeId = "affinity.weapon-icon", operationId = "working", extra = {}) {
  return {
    change_id: changeId,
    operation_id: operationId,
    source_sha256: "c".repeat(64),
    assembly_sha256: "d".repeat(64),
    installed_utc: "2026-09-04T00:00:00.000Z",
    result: "{\"active\":true}",
    cleanup_state: "active",
    ...extra,
  };
}
