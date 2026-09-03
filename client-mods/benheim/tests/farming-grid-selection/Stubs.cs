using System;
using System.Collections.Generic;

public sealed class PieceTable { }
public sealed class ItemDrop
{
    public sealed class ItemData
    {
        public UnityEngine.GameObject? m_dropPrefab;
        public SharedData m_shared = new();
        public sealed class SharedData { public PieceTable? m_buildPieces; }
    }
}
public sealed class Player
{
    public static Player? m_localPlayer;
    public ItemDrop.ItemData? RightItem;
    public bool PlaceMode;
    public bool InPlaceMode() => PlaceMode;
}
public sealed class Hud
{
    public static Hud? instance;
    public static bool PickerVisible;
    public static bool IsPieceSelectionVisible() => instance != null && PickerVisible;
}
public static class ZInput
{
    public static readonly HashSet<UnityEngine.KeyCode> Held = new();
    public static bool GetKey(UnityEngine.KeyCode key) => Held.Contains(key);
}
namespace UnityEngine
{
    public static class Time { public static float realtimeSinceStartup; }
    public enum KeyCode { JoystickButton4, LeftShift }
    public sealed class GameObject
    {
        public GameObject(string name) { this.name = name; }
        public string name;
    }
    public static class Input
    {
        public static readonly HashSet<KeyCode> Held = new();
        public static bool GetKey(KeyCode key) => Held.Contains(key);
    }
}
namespace BenheimQoL.Infrastructure
{
    internal static class HealthReporting { internal static bool GameplayActionsEnabled = true; }
    internal static class InputState
    {
        internal static bool TextEntryActive;
        internal static bool IsTextEntryActive() => TextEntryActive;
        internal static bool IsShiftHeld() => ZInput.GetKey(UnityEngine.KeyCode.LeftShift);
    }
    internal static class Diagnostics
    {
        internal static readonly List<DiagnosticEvent> CoreEvents = new();
        internal static bool ThrowOnEmit;
        internal static void Emit(DiagnosticEvent value)
        {
            if (ThrowOnEmit) throw new InvalidOperationException("diagnostic sink failed");
            value.Prepare(DateTime.UtcNow, "test", "test");
            CoreEvents.Add(value);
        }
    }
}
namespace BenheimQoL.Farming
{
    internal static class PlantingPreview
    {
        internal static int DestroyCalls;
        internal static void DestroyGhosts() => DestroyCalls++;
    }

    // This harness tests the production controller and its real callback,
    // including lifecycle and diagnostics. It does not simulate Unity rendering
    // or claim proof of native donor resolution, visual fit, or pointer routing.
    internal sealed class FarmingGridPickerView
    {
        internal static FarmingGridPickerView? Last;
        internal static int CreateCount;
        internal static string MissingReason = string.Empty;
        internal static bool ThrowOnCreate;
        internal bool ThrowOnHighlight;
        internal bool IsAlive { get; private set; } = true;
        internal int HighlightedSize { get; private set; }
        internal readonly Action<int> Click;
        private FarmingGridPickerView(Action<int> click) { Click = click; }
        internal static FarmingGridPickerView? TryCreate(Hud hud, Action<int> select, out string failure)
        {
            CreateCount++;
            if (ThrowOnCreate) throw new InvalidOperationException("create failed");
            failure = MissingReason;
            if (failure.Length > 0) return null;
            return Last = new FarmingGridPickerView(select);
        }
        internal void Highlight(int size)
        {
            if (ThrowOnHighlight) throw new InvalidOperationException("highlight failed");
            HighlightedSize = size;
        }
        internal void Destroy() { IsAlive = false; }
    }
}
