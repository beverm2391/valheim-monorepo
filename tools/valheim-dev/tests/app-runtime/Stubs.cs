using System;
using System.Collections.Generic;
using System.Threading;
using ValheimDev;

namespace ValheimDev
{
    internal static class Plugin { internal static TestLog Log { get; } = new TestLog(); }
    internal sealed class TestLog { internal void LogInfo(string line) { } internal void LogWarning(string line) { } }
}

internal sealed class Native : IValheimDevAppNative
{
    internal ValheimDevAppState State = new ValheimDevAppState { State = "menu" };
    internal readonly List<ValheimDevAppSave> WorldSaves = new List<ValheimDevAppSave>();
    internal readonly List<ValheimDevAppSave> CharacterSaves = new List<ValheimDevAppSave>();
    internal readonly List<string> Calls = new List<string>();
    internal bool FailSave;
    internal readonly int MainThread = Thread.CurrentThread.ManagedThreadId;
    private void Check(string method)
    {
        if (MainThread != Thread.CurrentThread.ManagedThreadId) throw new InvalidOperationException("native_off_main_thread");
        Calls.Add(method);
    }
    public ValheimDevAppState Snapshot() { Check("snapshot"); return State; }
    public List<ValheimDevAppSave> Worlds() { Check("worlds"); return new List<ValheimDevAppSave>(WorldSaves); }
    public List<ValheimDevAppSave> Characters() { Check("characters"); return new List<ValheimDevAppSave>(CharacterSaves); }
    public void CreateWorld(string name, string? seed) { Check("create_world"); if (!FailSave) WorldSaves.Add(Save(name, 11)); }
    public void CreateCharacter(string name) { Check("create_character"); if (!FailSave) CharacterSaves.Add(Save(name, 12)); }
    public void Open(ValheimDevAppSave world, ValheimDevAppSave character) { Check("open"); State.State = "loading"; }
    public void Logout() { Check("logout"); }
    public void QuitMenu() { Check("quit"); }
    internal void EnterWorld(ValheimDevAppSave world, ValheimDevAppSave character)
    {
        State = new ValheimDevAppState
        {
            State = "local_world", World = world, Character = character,
            Network = new object(), Scene = new object(), EligibleLocal = true,
            LabAccess = true, LabSessionId = "lab-session"
        };
    }
    internal static ValheimDevAppSave Save(string name, long id, string source = "local") => new ValheimDevAppSave
    { Name = name, Filename = name.ToLowerInvariant(), Id = id, Source = source, Loadable = true };
}
