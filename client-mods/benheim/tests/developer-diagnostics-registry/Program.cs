using System;
using System.Collections.Generic;
using BenheimQoL.DeveloperDiagnostics;
using BenheimQoL.EnemyTiers;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using BenheimQoL.Interaction;
using BenheimQoL.Spawning;

DeveloperDiagnosticsRuntime.InitializeConsole();
DeveloperDiagnosticsRuntime.InitializeConsole();

Expect(Terminal.Commands.Count == 3, "initialization registers each command once");
ExpectOptions("bhcatalog", "effects", "text", "ui");
ExpectOptions("bhrun", "comfort", "wispecho");
ExpectOptions("bhwatch", "colliders", "gridinput", "spawns");

Terminal catalogOutput = Run("bhcatalog", "effects", " bronze ");
Expect(RuntimePrimitiveCatalogCommand.RunCount == 1, "production catalog registration runs");
Expect(
    RuntimePrimitiveCatalogCommand.LastCategory == RuntimePrimitiveCatalogCategory.Effects,
    "effects registration preserves its native catalog category");
Expect(
    RuntimePrimitiveCatalogCommand.LastArguments.Length == 1 &&
    RuntimePrimitiveCatalogCommand.LastArguments[0] == " bronze ",
    "registry passes only snapshot-owned arguments to the catalog");
ExpectLine(catalogOutput.Lines, "catalog:Effects");

Terminal snapshotOutput = Run("bhrun", "comfort");
Expect(ComfortDiagnosticCommand.RunCount == 1, "production snapshot registration runs once");
ExpectLine(snapshotOutput.Lines, "comfort snapshot ran");

Terminal initialStatus = Run("bhwatch");
ExpectLine(
    initialStatus.Lines,
    "Benheim probe colliders: kind=visual default=off override=default effective=off");
ExpectLine(
    initialStatus.Lines,
    "Benheim probe spawns: kind=event default=on override=default effective=off");

ZNetScene.instance = new ZNetScene();
Player.m_localPlayer = new Player();
DeveloperDiagnosticsRuntime.Update();
Expect(TestSpawnProbe.Active, "the production registration path activates a shipped-on event probe on world entry");
Expect(TestSpawnProbe.EnableCount == 1, "world entry activates the event probe once");
Terminal worldStatus = Run("bhwatch");
ExpectLine(worldStatus.Lines,
    "Benheim probe gridinput: kind=event default=off override=default effective=off");
Run("bhwatch", "gridinput", "on");
Expect(FarmingInputProbe.Active, "the capture command activates the default-off input probe");
DeveloperDiagnosticsRuntime.DisableEventProbe("gridinput");
Expect(!FarmingInputProbe.Active && FarmingInputProbe.CleanupCount == 1,
    "capture completion uses the real registry cleanup transition");
ExpectLine(Run("bhwatch", "gridinput").Lines,
    "Benheim probe gridinput: kind=event default=off override=off effective=off");
ExpectLine(
    worldStatus.Lines,
    "Benheim probe spawns: kind=event default=on override=default effective=on");

Terminal enabledStatus = Run("bhwatch", "colliders", "on");
Expect(CharacterColliderOverlay.Active, "visual probe on activates the production overlay");
ExpectLine(
    enabledStatus.Lines,
    "Benheim probe colliders: kind=visual default=off override=on effective=on");

Terminal spawnOffStatus = Run("bhwatch", "spawns", "off");
Expect(!TestSpawnProbe.Active, "event probe off deactivates it for the session");
ExpectLine(
    spawnOffStatus.Lines,
    "Benheim probe spawns: kind=event default=on override=off effective=off");
int cleanupBeforeRejectedActivation = TestSpawnProbe.CleanupCount;
TestSpawnProbe.RejectActivation = true;
Terminal rejectedActivation = Run("bhwatch", "spawns", "on");
Expect(!TestSpawnProbe.Active, "rejected activation cleans resources allocated before returning false");
Expect(
    TestSpawnProbe.CleanupCount == cleanupBeforeRejectedActivation + 1,
    "rejected activation invokes generic failure cleanup exactly once");
ExpectLine(
    rejectedActivation.Lines,
    "Benheim probe spawns: kind=event default=on override=on effective=off");
Expect(Diagnostics.Events.Count == 1, "rejected activation emits one typed failure");
TestSpawnProbe.RejectActivation = false;
Terminal spawnDefaultStatus = Run("bhwatch", "spawns", "default");
Expect(TestSpawnProbe.Active, "default restores the event probe's shipped-on state");
ExpectLine(
    spawnDefaultStatus.Lines,
    "Benheim probe spawns: kind=event default=on override=default effective=on");
Diagnostics.Events.Clear();

int respawnCleanupCount = TestSpawnProbe.CleanupCount;
int respawnEnableCount = TestSpawnProbe.EnableCount;
Player.m_localPlayer = null;
DeveloperDiagnosticsRuntime.Update();
Expect(CharacterColliderOverlay.Active, "temporary player absence keeps the visual probe session active");
Expect(TestSpawnProbe.Active, "temporary player absence during respawn keeps the event probe active");
Expect(
    TestSpawnProbe.CleanupCount == respawnCleanupCount,
    "temporary player absence does not run world cleanup or discard registered rules");

Player.m_localPlayer = new Player();
DeveloperDiagnosticsRuntime.Update();
Expect(
    TestSpawnProbe.EnableCount == respawnEnableCount,
    "respawn does not reactivate an event probe that never left its world");

Run("bhwatch", "gridinput", "on");
FarmingInputProbe.CompleteSessionOnCleanup = true;
int inputCleanupsBeforeExit = FarmingInputProbe.CleanupCount;
ZNetScene.instance = null;
Player.m_localPlayer = null;
DeveloperDiagnosticsRuntime.Update();
Expect(!CharacterColliderOverlay.Active, "world exit cleans the active visual probe");
Expect(!TestSpawnProbe.Active, "world exit cleans the active event probe");
Expect(TestSpawnProbe.CleanupCount > 0, "world exit invokes event-probe cleanup");
Expect(!FarmingInputProbe.Active &&
    FarmingInputProbe.CleanupCount == inputCleanupsBeforeExit + 1,
    "early exit ends a bounded input capture without reentering its cleanup callback");
ExpectLine(Run("bhwatch", "gridinput").Lines,
    "Benheim probe gridinput: kind=event default=off override=off effective=off");

ZNetScene.instance = new ZNetScene();
Player.m_localPlayer = new Player();
DeveloperDiagnosticsRuntime.Update();
Expect(CharacterColliderOverlay.Active, "world entry restores the visual session override");
Expect(TestSpawnProbe.Active, "world entry restores the event shipped default");
Expect(!FarmingInputProbe.Active, "a completed bounded capture does not restart on world entry");
Expect(FarmingInputProbe.CleanupCount == inputCleanupsBeforeExit + 1,
    "world reentry leaves the completed capture untouched until another on command");

Terminal colliderDefaultStatus = Run("bhwatch", "colliders", "default");
Expect(!CharacterColliderOverlay.Active, "default restores the visual probe's shipped-off state");
ExpectLine(
    colliderDefaultStatus.Lines,
    "Benheim probe colliders: kind=visual default=off override=default effective=off");

ComfortDiagnosticCommand.ThrowOnRun = true;
Terminal failedSnapshot = Run("bhrun", "comfort");
ExpectLine(
    failedSnapshot.Lines,
    "Benheim bhrun comfort failed: comfort exploded without escaping");
Expect(Diagnostics.Events.Count == 1, "snapshot failure emits one typed diagnostic");

TestSpawnProbe.ThrowOnUpdate = true;
_ = Run("bhwatch", "colliders", "on");
int colliderUpdatesBeforeSiblingFailure = CharacterColliderOverlay.UpdateCount;
DeveloperDiagnosticsRuntime.Update();
Expect(!TestSpawnProbe.Active, "event-probe update failure deactivates and cleans the probe");
Expect(Diagnostics.Events.Count == 2, "event-probe failure emits one typed diagnostic");
Expect(
    CharacterColliderOverlay.UpdateCount == colliderUpdatesBeforeSiblingFailure + 1,
    "one probe failure does not stop a healthy active sibling");

CharacterColliderOverlay.ThrowOnUpdate = true;
DeveloperDiagnosticsRuntime.Update();
Expect(!CharacterColliderOverlay.Active, "visual-probe update failure cleans the overlay");
Expect(
    CharacterColliderOverlay.OwnedResourceCount == 0,
    "visual-probe failure cleans resources allocated before the failure");
Expect(Diagnostics.Events.Count == 3, "visual-probe failure emits one typed diagnostic");

Terminal numericAlias = Run("bhwatch", "colliders", "1");
ExpectLine(numericAlias.Lines, "Usage: bhwatch [<probe> [on|off|default]]");

Player.m_localPlayer = null;
ZNetScene.instance = null;
DeveloperDiagnosticsRuntime.Reset();
Terminal resetStatus = Run("bhwatch");
ExpectLine(
    resetStatus.Lines,
    "Benheim probe colliders: kind=visual default=off override=default effective=off");
ExpectLine(
    resetStatus.Lines,
    "Benheim probe spawns: kind=event default=on override=default effective=off");
Expect(TestSpawnProbe.DisableCount > 0, "event probe receives an explicit disable transition");

Console.WriteLine("developer diagnostics registry behavior checks passed");

static Terminal Run(params string[] arguments)
{
    Terminal context = new();
    Terminal.Commands[arguments[0]].Run(arguments, context);
    return context;
}

static void ExpectOptions(string command, params string[] expected)
{
    List<string>? actual = Terminal.Commands[command].GetTabOptions();
    Expect(actual != null, $"{command} exposes native first-argument completion");
    Expect(actual!.Count == expected.Length, $"{command} completion count matches registry");
    for (int index = 0; index < expected.Length; index++)
    {
        Expect(actual[index] == expected[index], $"{command} completion includes {expected[index]}");
    }
}

static void ExpectLine(IReadOnlyList<string> lines, string expected)
{
    for (int index = 0; index < lines.Count; index++)
    {
        if (string.Equals(lines[index], expected, StringComparison.Ordinal))
        {
            return;
        }
    }
    throw new InvalidOperationException($"Missing line: {expected}");
}

static void Expect(bool condition, string description)
{
    if (!condition)
    {
        throw new InvalidOperationException($"Expectation failed: {description}");
    }
}
