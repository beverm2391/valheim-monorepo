using System;
using System.Collections.Generic;
using BenheimQoL.DeveloperDiagnostics;
using BenheimQoL.EnemyTiers;
using BenheimQoL.Infrastructure;
using BenheimQoL.Interaction;

DeveloperDiagnosticsRuntime.InitializeConsole();
DeveloperDiagnosticsRuntime.InitializeConsole();

Expect(Terminal.Commands.Count == 3, "initialization registers each command once");
ExpectOptions("bhcatalog", "effects", "text", "ui");
ExpectOptions("bhrun", "comfort");
ExpectOptions("bhwatch", "colliders");

Terminal catalogOutput = Run("bhcatalog", "effects", " bronze ");
Expect(RuntimePrimitiveCatalogCommand.RunCount == 1, "production catalog registration runs");
Expect(
    RuntimePrimitiveCatalogCommand.LastCategory == RuntimePrimitiveCatalogCategory.Effects,
    "effects registration preserves its native catalog category");
Expect(
    RuntimePrimitiveCatalogCommand.LastArguments.Length == 1 &&
    RuntimePrimitiveCatalogCommand.LastArguments[0] == " bronze ",
    "registry passes only probe-owned arguments to the catalog");
ExpectLine(catalogOutput.Lines, "catalog:Effects");

Terminal snapshotOutput = Run("bhrun", "comfort");
Expect(ComfortDiagnosticCommand.RunCount == 1, "production snapshot registration runs");
ExpectLine(snapshotOutput.Lines, "comfort snapshot ran");

Terminal initialStatus = Run("bhwatch");
ExpectLine(
    initialStatus.Lines,
    "Benheim watcher colliders: shipped=off session=default effective=off");

Player.m_localPlayer = new Player();
DeveloperDiagnosticsRuntime.Update();
Terminal enabledStatus = Run("bhwatch", "colliders", "on");
Expect(CharacterColliderOverlay.Active, "watcher on activates the production watcher");
ExpectLine(
    enabledStatus.Lines,
    "Benheim watcher colliders: shipped=off session=on effective=on");

Player.m_localPlayer = null;
DeveloperDiagnosticsRuntime.Update();
Expect(!CharacterColliderOverlay.Active, "world exit cleans the active watcher");
Terminal exitedWorldStatus = Run("bhwatch", "colliders");
ExpectLine(
    exitedWorldStatus.Lines,
    "Benheim watcher colliders: shipped=off session=on effective=off");
int resetsAfterWorldExit = CharacterColliderOverlay.ResetCount;

Player.m_localPlayer = new Player();
DeveloperDiagnosticsRuntime.Update();
Expect(CharacterColliderOverlay.Active, "world entry restores the session's effective watcher state");
Expect(
    CharacterColliderOverlay.ResetCount == resetsAfterWorldExit,
    "world entry does not perform redundant cleanup");

Terminal offStatus = Run("bhwatch", "colliders", "off");
Expect(!CharacterColliderOverlay.Active, "off disables the watcher for the session");
ExpectLine(
    offStatus.Lines,
    "Benheim watcher colliders: shipped=off session=off effective=off");

_ = Run("bhwatch", "colliders", "on");
Terminal defaultStatus = Run("bhwatch", "colliders", "default");
Expect(!CharacterColliderOverlay.Active, "default restores the shipped off state");
ExpectLine(
    defaultStatus.Lines,
    "Benheim watcher colliders: shipped=off session=default effective=off");

ComfortDiagnosticCommand.ThrowOnRun = true;
Terminal failedSnapshot = Run("bhrun", "comfort");
ExpectLine(
    failedSnapshot.Lines,
    "Benheim bhrun comfort failed: comfort exploded without escaping");
Expect(Diagnostics.Events.Count == 1, "snapshot failure emits one typed diagnostic");

_ = Run("bhwatch", "colliders", "on");
CharacterColliderOverlay.ThrowOnUpdate = true;
DeveloperDiagnosticsRuntime.Update();
Expect(!CharacterColliderOverlay.Active, "watcher update failure cleans the watcher");
Expect(
    CharacterColliderOverlay.OwnedResourceCount == 0,
    "watcher update failure cleans resources allocated before the failure");
Expect(Diagnostics.Events.Count == 2, "watcher failure emits one typed diagnostic");

Terminal numericAlias = Run("bhwatch", "colliders", "1");
ExpectLine(numericAlias.Lines, "Usage: bhwatch [<watcher> [on|off|default]]");

DeveloperDiagnosticsRuntime.Reset();
Terminal resetStatus = Run("bhwatch", "colliders");
ExpectLine(
    resetStatus.Lines,
    "Benheim watcher colliders: shipped=off session=default effective=off");

Console.WriteLine("developer diagnostics production registry checks passed");

static Terminal Run(params string[] arguments)
{
    Terminal context = new Terminal();
    Terminal.Commands[arguments[0]].Run(arguments, context);
    return context;
}

static void ExpectOptions(string command, params string[] expected)
{
    List<string>? actual = Terminal.Commands[command].GetTabOptions();
    Expect(actual != null, $"{command} exposes native first-argument completion");
    Expect(actual!.Count == expected.Length, $"{command} completion count matches production registry");
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
