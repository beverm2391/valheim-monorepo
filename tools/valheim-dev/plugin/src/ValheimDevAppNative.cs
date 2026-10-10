using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace ValheimDev;

// These fixed bindings were inspected in the exact target assembly. No name,
// type, member, or invocation arguments are supplied by a bridge request.
internal sealed class ValheimDevAppNative : IValheimDevAppNative
{
    private static readonly FieldInfo Profiles = RequiredField("m_profiles");
    private static readonly FieldInfo ProfileIndex = RequiredField("m_profileIndex");
    private static readonly FieldInfo WorldsField = RequiredField("m_worlds");
    private static readonly FieldInfo WorldField = RequiredField("m_world");
    private static readonly FieldInfo StartingWorld = RequiredField("m_startingWorld");
    private static readonly MethodInfo SelectProfile = RequiredMethod("SetSelectedProfile", typeof(string));
    private static readonly MethodInfo SelectWorld = RequiredMethod("SetSelectedWorld", typeof(int), typeof(bool));

    public ValheimDevAppState Snapshot()
    {
        ValheimDevAppState state = new ValheimDevAppState
        {
            LabAccess = ValheimDevRuntime.HasAccess,
            LabSessionId = ValheimDevRuntime.LabSessionId
        };
        ZNet? network = ZNet.instance;
        Game? game = Game.instance;
        FejdStartup? menu = FejdStartup.instance;
        if (game != null && game.IsShuttingDown()) { state.State = "closing"; return state; }
        if (game == null && network == null && menu != null)
        {
            state.State = (bool)StartingWorld.GetValue(menu)! || menu.m_loading.activeInHierarchy ? "loading" : "menu";
            return state;
        }
        if (network == null) return state;
        state.Network = network;
        state.Scene = ZNetScene.instance;
        World? world = network.GetWorld();
        PlayerProfile? character = game?.GetPlayerProfile();
        if (world != null) state.World = Save(world);
        if (character != null) state.Character = Save(character);
        if (network.IsDedicated()) { state.State = "dedicated"; return state; }
        ValheimDevWorldState auth = ValheimDevRuntime.AppWorldState;
        // World ownership survives respawn. Player readiness is required by the
        // Lab execution bridge separately, not by this captured-world test.
        state.EligibleLocal = auth.Network != null && auth.Scene != null
            && ValheimDevEligibility.CheckCapturedSession(new ValheimDevWorldCapture
            {
                Network = auth.Network, Scene = auth.Scene, WorldId = auth.WorldId
            }, auth) == "eligible";
        if (!network.IsServer() || ZNet.IsOpenServer() || network.GetPeers().Count != 0 || network.GetServerRPC() != null)
            state.State = "multiplayer";
        else if (game != null && world != null && character != null && state.Scene != null)
            state.State = "local_world";
        return state;
    }

    public List<ValheimDevAppSave> Worlds()
    {
        SaveSystem.InvalidateCache(SaveDataType.World);
        SaveSystem.ClearWorldListCache(reload: false);
        return SaveSystem.GetWorldList().ConvertAll(Save);
    }

    public List<ValheimDevAppSave> Characters()
    {
        SaveSystem.InvalidateCache(SaveDataType.Character);
        return SaveSystem.GetAllPlayerProfiles().ConvertAll(Save);
    }

    public void CreateWorld(string name, string? seed)
    {
        FejdStartup menu = RequireMenu();
        // Native HaveWorld includes corrupt/deleted-name bookkeeping which a
        // normal loaded-save list cannot reliably expose. Never overwrite it.
        if (World.HaveWorld(name)) throw new InvalidOperationException("save_exists");
        menu.OnWorldNew();
        menu.m_newWorldName.text = name;
        if (seed != null) menu.m_newWorldSeed.text = seed;
        menu.OnNewWorldDone(forceLocal: true);
    }

    public void CreateCharacter(string name)
    {
        FejdStartup menu = RequireMenu();
        if (PlayerProfile.HaveProfile(name.ToLower())) throw new InvalidOperationException("save_exists");
        menu.OnSelelectCharacterBack();
        menu.OnStartGame();
        menu.OnCharacterNew();
        menu.m_csNewCharacterName.text = name;
        menu.OnNewCharacterDone(forceLocal: true);
    }

    public void Open(ValheimDevAppSave world, ValheimDevAppSave character)
    {
        FejdStartup menu = RequireMenu();
        List<PlayerProfile> profiles = SaveSystem.GetAllPlayerProfiles();
        List<World> worlds = SaveSystem.GetWorldList();
        int profileIndex = profiles.FindIndex(profile => Same(Save(profile), character));
        int worldIndex = worlds.FindIndex(candidate => Same(Save(candidate), world));
        if (profileIndex < 0 || worldIndex < 0) throw new InvalidOperationException("selection_changed");
        menu.OnSelelectCharacterBack(); // Clears queued server joins through the native path.
        menu.OnStartGame();
        Profiles.SetValue(menu, profiles);
        SelectProfile.Invoke(menu, new object[] { character.Filename });
        int selectedIndex = (int)ProfileIndex.GetValue(menu)!;
        if (selectedIndex != profileIndex || !Same(Save(profiles[selectedIndex]), character))
            throw new InvalidOperationException("character_selection_mismatch");
        menu.OnCharacterStart();
        // OnCharacterStart refreshes the list. Bind an explicit current list so
        // a clamped/default selection cannot accidentally start another world.
        WorldsField.SetValue(menu, worlds);
        SelectWorld.Invoke(menu, new object[] { worldIndex, true });
        if (WorldField.GetValue(menu) is not World selected || !Same(Save(selected), world))
            throw new InvalidOperationException("world_selection_mismatch");
        menu.m_openServerToggle.isOn = false;
        menu.m_publicServerToggle.isOn = false;
        menu.m_crossplayServerToggle.isOn = false;
        menu.OnWorldStart();
        if (!(bool)StartingWorld.GetValue(menu)!) throw new InvalidOperationException("world_start_not_accepted");
    }

    public void Logout()
    {
        Game game = Game.instance ?? throw new InvalidOperationException("no_game");
        // Logout performs native disk-space gates and synchronous save/shutdown.
        // Its prompt may suspend the transition; the controller waits for the
        // actual menu before requesting application quit.
        game.Logout(save: true, changeToStartScene: true);
    }

    public void QuitMenu() => RequireMenu().OnAbort();

    private FejdStartup RequireMenu()
    {
        if (Snapshot().State != "menu") throw new InvalidOperationException("menu_required");
        return FejdStartup.instance ?? throw new InvalidOperationException("menu_unavailable");
    }

    private static bool Same(ValheimDevAppSave left, ValheimDevAppSave right) => left.Source == "local" && right.Source == "local"
        && left.Name == right.Name && left.Filename == right.Filename && left.Id == right.Id && left.Loadable;

    private static ValheimDevAppSave Save(World world) => new ValheimDevAppSave
    {
        Name = world.m_name, Filename = world.m_worldName, Source = Source(world.m_fileSource), Id = world.m_uid,
        Loadable = world.m_dataError == World.SaveDataError.None
    };

    private static ValheimDevAppSave Save(PlayerProfile profile) => new ValheimDevAppSave
    {
        Name = profile.GetName(), Filename = profile.GetFilename(), Source = Source(profile.m_fileSource),
        Id = profile.GetPlayerID(), Loadable = true
    };

    private static string Source(FileHelpers.FileSource source) => source.ToString().ToLowerInvariant();
    private static FieldInfo RequiredField(string name) => AccessTools.Field(typeof(FejdStartup), name)
        ?? throw new MissingFieldException(typeof(FejdStartup).FullName, name);
    private static MethodInfo RequiredMethod(string name, params Type[] arguments) => AccessTools.Method(typeof(FejdStartup), name, arguments)
        ?? throw new MissingMethodException(typeof(FejdStartup).FullName, name);
}
