using System;
using System.Collections.Generic;
using System.Text;

namespace ValheimDev;

internal sealed class ValheimDevAppSave
{
    internal string Name { get; set; } = string.Empty;
    internal string Filename { get; set; } = string.Empty;
    internal string Source { get; set; } = string.Empty;
    internal long Id { get; set; }
    internal bool Loadable { get; set; }
}

internal sealed class ValheimDevAppState
{
    internal string State { get; set; } = "loading";
    internal ValheimDevAppSave? World { get; set; }
    internal ValheimDevAppSave? Character { get; set; }
    internal object? Network { get; set; }
    internal object? Scene { get; set; }
    internal bool EligibleLocal { get; set; }
    internal bool LabAccess { get; set; }
    internal string? LabSessionId { get; set; }
}

internal interface IValheimDevAppNative
{
    ValheimDevAppState Snapshot();
    List<ValheimDevAppSave> Worlds();
    List<ValheimDevAppSave> Characters();
    void CreateWorld(string name, string? seed);
    void CreateCharacter(string name);
    void Open(ValheimDevAppSave world, ValheimDevAppSave character);
    void Logout();
    void QuitMenu();
}

internal sealed class ValheimDevAppController
{
    private readonly IValheimDevAppNative native;
    private readonly string appId;
    private readonly int pid;
    private readonly string launchId;
    private bool ownsLaunchMenu;
    private ValheimDevAppSave? selectedWorld;
    private ValheimDevAppSave? selectedCharacter;
    private object? ownedNetwork;
    private object? ownedScene;
    private DateTime openingDeadline;
    private DateTime closingDeadline;
    private bool opening;
    private bool closing;
    private string lastState = string.Empty;

    internal ValheimDevAppController(IValheimDevAppNative native, string appId, int pid, string launchId)
    {
        this.native = native;
        this.appId = appId;
        this.pid = pid;
        this.launchId = launchId;
        ownsLaunchMenu = launchId.Length > 0;
    }

    internal void Observe()
    {
        ValheimDevAppState state = native.Snapshot();
        Observe(state);
    }

    private void Observe(ValheimDevAppState state)
    {
        if (closing && state.State == "local_world" && !OwnsWorld(state))
        {
            closing = false;
            Emit("app_transition", "close_lab", string.Empty, "close_ownership_changed");
        }
        if (opening)
        {
            if (state.State == "local_world" && state.EligibleLocal && MatchesSelection(state))
            {
                ownedNetwork = state.Network;
                ownedScene = state.Scene;
                opening = false;
                ownsLaunchMenu = false;
            }
            else if (DateTime.UtcNow >= openingDeadline || state.State == "multiplayer" || state.State == "dedicated"
                || state.State == "local_world" && !MatchesSelection(state))
            {
                ForgetWorld();
                ownsLaunchMenu = false;
            }
        }
        else if (ownedNetwork != null && !OwnsWorld(state))
        {
            // A requested logout may cross a loading scene before reaching the
            // menu. Retain only the close continuation, never world ownership.
            ForgetWorld();
        }
        if (!opening && state.State != "menu" && state.State != "loading" && !OwnsWorld(state))
            ownsLaunchMenu = false;

        if (closing)
        {
            if (state.State == "menu")
            {
                closing = false;
                native.QuitMenu();
                Emit("app_transition", "close_lab", string.Empty, "quit_requested");
            }
            else if (DateTime.UtcNow >= closingDeadline || state.State == "multiplayer" || state.State == "dedicated")
            {
                closing = false;
                Emit("app_transition", "close_lab", string.Empty, "close_transition_not_completed");
            }
        }
        if (lastState != state.State)
        {
            lastState = state.State;
            Emit("app_transition", "status", string.Empty, state.State);
        }
    }

    internal string Process(ValheimDevAppRequest request)
    {
        if (!string.Equals(request.AppId, appId, StringComparison.Ordinal))
            return ValheimDevAppProtocol.Response(appId, request.RequestId, "stale_app");
        ValheimDevAppState state = native.Snapshot();
        Observe(state);
        bool mutation = request.Action != "status" && request.Action != "list_saves";
        if (mutation) Emit("app_operation_attempt", request.Action, request.RequestId, state.State);
        try
        {
            string result;
            switch (request.Action)
            {
                case "status": result = Status(state); break;
                case "list_saves": result = SaveList(); break;
                case "create_world":
                    RequireMenu(state);
                    RequireName(request.Name);
                    if (Collision(native.Worlds(), request.Name)) throw new InvalidOperationException("save_exists");
                    native.CreateWorld(request.Name, request.Seed);
                    result = Created(Find(native.Worlds(), request.Name));
                    break;
                case "create_character":
                    RequireMenu(state);
                    RequireName(request.Name);
                    if (Collision(native.Characters(), request.Name)) throw new InvalidOperationException("save_exists");
                    native.CreateCharacter(request.Name);
                    result = Created(Find(native.Characters(), request.Name));
                    break;
                case "open_lab":
                    RequireMenu(state);
                    RequireName(request.World); RequireName(request.Character);
                    ValheimDevAppSave world = Find(native.Worlds(), request.World);
                    ValheimDevAppSave character = Find(native.Characters(), request.Character);
                    native.Open(world, character);
                    selectedWorld = world; selectedCharacter = character;
                    ownedNetwork = null; ownedScene = null;
                    opening = true; openingDeadline = DateTime.UtcNow.AddMinutes(3);
                    result = "{\"accepted\":true}";
                    break;
                case "close_lab":
                    if (closing || opening) throw new InvalidOperationException("transition_in_progress");
                    if (state.State == "menu" && ownsLaunchMenu) { }
                    else if (OwnsWorld(state))
                    {
                        native.Logout();
                    }
                    else throw new InvalidOperationException("session_not_owned");
                    // The authorized request owns this save/quit continuation.
                    // Loss of its socket reply does not abandon a completed
                    // logout; the caller must still observe actual process exit.
                    closing = true; closingDeadline = DateTime.UtcNow.AddMinutes(1);
                    result = "{\"accepted\":true}";
                    break;
                default: throw new InvalidOperationException("unsupported_action");
            }
            if (mutation) Emit("app_operation_result", request.Action, request.RequestId, "accepted");
            return ValheimDevAppProtocol.Response(appId, request.RequestId, null, result);
        }
        catch (Exception exception)
        {
            string error = ValheimDevDiagnostics.Flatten(exception.GetBaseException().Message);
            if (error.Length > 256) error = error.Substring(0, 256);
            Emit("app_operation_result", request.Action, request.RequestId, error);
            return ValheimDevAppProtocol.Response(appId, request.RequestId, error);
        }
    }


    private string Status(ValheimDevAppState state)
    {
        StringBuilder json = new StringBuilder("{");
        ValheimDevJson.AppendProperty(json, "pid", pid);
        json.Append(','); ValheimDevJson.AppendProperty(json, "state", state.State);
        json.Append(','); ValheimDevJson.AppendNullableProperty(json, "world", state.World?.Name);
        json.Append(','); ValheimDevJson.AppendNullableProperty(json, "character", state.Character?.Name);
        json.Append(','); ValheimDevJson.AppendProperty(json, "lab_access", state.LabAccess);
        json.Append(','); ValheimDevJson.AppendNullableProperty(json, "lab_session_id", state.LabSessionId);
        json.Append(','); ValheimDevJson.AppendProperty(json, "owned", OwnsWorld(state) || state.State == "menu" && ownsLaunchMenu);
        json.Append(','); ValheimDevJson.AppendProperty(json, "launch_id", launchId);
        return json.Append('}').ToString();
    }

    private string SaveList()
    {
        StringBuilder json = new StringBuilder("{\"worlds\":");
        AppendSaves(json, native.Worlds());
        json.Append(",\"characters\":"); AppendSaves(json, native.Characters());
        return json.Append('}').ToString();
    }

    private static void AppendSaves(StringBuilder json, List<ValheimDevAppSave> saves)
    {
        if (saves.Count > 1000) throw new InvalidOperationException("too_many_saves");
        json.Append('[');
        for (int i = 0; i < saves.Count; i++)
        {
            if (i > 0) json.Append(',');
            json.Append(Created(saves[i]));
        }
        json.Append(']');
    }

    private static string Created(ValheimDevAppSave save)
    {
        StringBuilder json = new StringBuilder("{");
        ValheimDevJson.AppendProperty(json, "name", save.Name);
        json.Append(','); ValheimDevJson.AppendProperty(json, "source", save.Source);
        json.Append(','); ValheimDevJson.AppendProperty(json, "disposable", save.Source == "local" && ValheimDevAppProtocol.IsDisposableName(save.Name));
        return json.Append('}').ToString();
    }

    private void RequireMenu(ValheimDevAppState state)
    {
        if (opening || closing) throw new InvalidOperationException("transition_in_progress");
        if (state.State != "menu") throw new InvalidOperationException("menu_required");
    }

    private static void RequireName(string name)
    {
        if (!ValheimDevAppProtocol.IsDisposableName(name)) throw new InvalidOperationException("invalid_disposable_name");
    }

    private static bool Collision(List<ValheimDevAppSave> saves, string name) => saves.Exists(save =>
        string.Equals(save.Name, name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(save.Filename, name, StringComparison.OrdinalIgnoreCase));

    private static ValheimDevAppSave Find(List<ValheimDevAppSave> saves, string name)
    {
        List<ValheimDevAppSave> matches = saves.FindAll(save => save.Name == name && save.Source == "local");
        if (matches.Count != 1 || !matches[0].Loadable) throw new InvalidOperationException("local_save_unavailable");
        return matches[0];
    }

    private bool MatchesSelection(ValheimDevAppState state) => SameSave(selectedWorld, state.World) && SameSave(selectedCharacter, state.Character);
    private static bool SameSave(ValheimDevAppSave? expected, ValheimDevAppSave? actual) => expected != null && actual != null
        && expected.Source == "local" && actual.Source == "local" && expected.Id == actual.Id
        && expected.Filename == actual.Filename && expected.Name == actual.Name;
    private bool OwnsWorld(ValheimDevAppState state) => ownedNetwork != null && ownedScene != null && state.EligibleLocal
        && state.State == "local_world" && ReferenceEquals(ownedNetwork, state.Network) && ReferenceEquals(ownedScene, state.Scene)
        && MatchesSelection(state);
    private void ForgetWorld()
    {
        opening = false; selectedWorld = null; selectedCharacter = null; ownedNetwork = null; ownedScene = null;
    }

    private void Emit(string name, string action, string requestId, string outcome) => ValheimDevDiagnostics.Emit(name,
        new KeyValuePair<string, string>("app_id", appId),
        new KeyValuePair<string, string>("request_id", requestId),
        new KeyValuePair<string, string>("action", action),
        new KeyValuePair<string, string>("outcome", outcome));
}
