using System.Collections.Generic;

public sealed class PieceTable
{
}

public sealed class ItemDrop
{
    public sealed class ItemData
    {
        public UnityEngine.GameObject? m_dropPrefab;
        public SharedData m_shared = new SharedData();

        public sealed class SharedData
        {
            public PieceTable? m_buildPieces;
        }
    }
}

public sealed class Player
{
    public static Player? m_localPlayer;

    public ItemDrop.ItemData? RightItem;
    public bool PlaceMode;
    public string? LastMessage;

    public bool InPlaceMode() => PlaceMode;

    public void Message(MessageHud.MessageType type, string message)
    {
        if (type == MessageHud.MessageType.TopLeft)
        {
            LastMessage = message;
        }
    }
}

public sealed class Game
{
}

public static class Hud
{
    public static bool PickerVisible;

    public static bool IsPieceSelectionVisible() => PickerVisible;
}

public static class MessageHud
{
    public enum MessageType
    {
        TopLeft,
    }
}

public static class ZInput
{
    public static readonly HashSet<string> ButtonDown = new HashSet<string>();
    public static readonly HashSet<UnityEngine.KeyCode> Held = new HashSet<UnityEngine.KeyCode>();
    public static readonly HashSet<UnityEngine.KeyCode> KeyDown = new HashSet<UnityEngine.KeyCode>();

    public static bool GetButtonDown(string name) => ButtonDown.Contains(name);
    public static void Update(float deltaTime)
    {
    }

    public static bool GetKey(UnityEngine.KeyCode key)
    {
        if (Held.Contains(key) || KeyDown.Contains(key))
        {
            return true;
        }

        int number = (int)key - (int)UnityEngine.KeyCode.Alpha0;
        return number >= 1
            && number <= 8
            && ButtonDown.Contains($"Hotbar{number}");
    }
    public static bool GetKeyDown(UnityEngine.KeyCode key) => KeyDown.Contains(key);

    public static void ResetTransient()
    {
        ButtonDown.Clear();
        KeyDown.Clear();
    }
}

namespace UnityEngine
{
    public enum KeyCode
    {
        Alpha0 = 48,
        Alpha1,
        Alpha2,
        Alpha3,
        Alpha4,
        Alpha5,
        Alpha6,
        Alpha7,
        Alpha8,
        Alpha9,
        Keypad0 = 256,
        Keypad1,
        Keypad2,
        Keypad3,
        Keypad4,
        Keypad5,
        Keypad6,
        Keypad7,
        Keypad8,
        Keypad9,
        RightShift,
        LeftShift,
        LeftControl,
        RightControl,
        LeftAlt,
        RightAlt,
        LeftCommand,
        RightCommand,
        LeftWindows,
        RightWindows,
        AltGr,
        LeftMeta,
        RightMeta,
        JoystickButton4,
    }

    public sealed class GameObject
    {
        public GameObject(string name)
        {
            this.name = name;
        }

        public string name;

        public static implicit operator bool(GameObject? value) => value != null;
    }

    public static class Input
    {
        public static bool GetKey(KeyCode key) => false;
    }
}

namespace HarmonyLib
{
    using System;

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch()
        {
        }

        internal HarmonyPatch(Type type, string methodName)
        {
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPrefix : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyFinalizer : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPriority : Attribute
    {
        internal HarmonyPriority(int priority)
        {
        }
    }

    internal static class Priority
    {
        internal const int First = 800;
        internal const int Last = 0;
    }
}

namespace BenheimQoL.Infrastructure
{
    internal static class InputState
    {
        internal static bool TextEntryActive { get; set; }

        internal static bool IsTextEntryActive() => TextEntryActive;
        internal static bool IsShiftHeld() => ZInput.GetKey(UnityEngine.KeyCode.LeftShift);
    }

    internal static class Diagnostics
    {
        internal static int Events;
        internal static string? Last;

        internal static void Event(string domain, string name, string fields)
        {
            Events++;
            Last = $"{domain}.{name} {fields}";
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
}
