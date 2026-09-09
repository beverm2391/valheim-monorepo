using System;
using System.Collections;
using System.Reflection;

namespace ValheimDev;

internal static class ValheimDevConsole
{
    private static Terminal.ConsoleCommand? priorBhCommand;
    private static Terminal.ConsoleCommand? ownedBhCommand;
    private static bool initialized;

    internal static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        priorBhCommand = FindCommand("bh");
        ownedBhCommand = new Terminal.ConsoleCommand(
            "bh",
            "Benheim and local Valheim Dev commands; run 'bh help'",
            Execute,
            isCheat: false,
            isNetwork: false);
    }

    internal static void Reset()
    {
        ValheimDevDiagnostics.UnsubscribeOptionalEvidence();
        IDictionary? commands = CommandDictionary();
        if (commands != null && ReferenceEquals(commands["bh"], ownedBhCommand))
        {
            if (priorBhCommand == null) commands.Remove("bh");
            else commands["bh"] = priorBhCommand;
        }
        initialized = false;
        priorBhCommand = null;
        ownedBhCommand = null;
    }

    private static object Execute(Terminal.ConsoleEventArgs args)
    {
        if (ValheimDevRuntime.TryHandleConsole(args.Args, args.Context)) return true;
        if (priorBhCommand != null)
        {
            if (!priorBhCommand.IsValid(args.Context)) return false;
            priorBhCommand.RunAction(args);
            return true;
        }

        args.Context.AddString("Valheim Dev commands:");
        ValheimDevRuntime.PrintUsage(args.Context);
        return true;
    }

    private static Terminal.ConsoleCommand? FindCommand(string name)
    {
        IDictionary? commands = CommandDictionary();
        if (commands == null) return null;
        return commands[name.ToLowerInvariant()] as Terminal.ConsoleCommand;
    }

    private static IDictionary? CommandDictionary()
    {
        FieldInfo? field = typeof(Terminal).GetField("commands", BindingFlags.Static | BindingFlags.NonPublic);
        return field?.GetValue(null) as IDictionary;
    }
}
