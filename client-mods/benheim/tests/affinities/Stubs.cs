using System;
using System.Collections.Generic;

// Only native object/diagnostic boundaries are stubbed. Affinity identity,
// eligibility, state mutation, and projectile/draw behavior use production code.
namespace UnityEngine
{
    internal sealed class GameObject
    {
        internal GameObject(string name) { this.name = name; }
        internal string name;
        internal ItemDrop? Drop;
        internal T? GetComponent<T>() where T : class => Drop as T;
    }

    internal static class Mathf
    {
        internal static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
    }
}

internal sealed class ObjectDB
{
    internal static ObjectDB? instance;
    internal readonly Dictionary<string, UnityEngine.GameObject> Prefabs = new();
    internal UnityEngine.GameObject? GetItemPrefab(string name) =>
        Prefabs.TryGetValue(name, out UnityEngine.GameObject? prefab) ? prefab : null;
}

internal sealed class ItemDrop
{
    internal ItemData m_itemData = new();
    internal sealed class SharedData
    {
        internal int m_maxQuality = 4;
        internal string m_name = string.Empty;
    }

    internal sealed class ItemData
    {
        internal UnityEngine.GameObject? m_dropPrefab;
        internal int m_quality = 4;
        internal SharedData m_shared = new();
        internal Dictionary<string, string> m_customData = new();
    }
}

internal class Character { }
internal class Humanoid : Character { }
internal sealed class Player : Humanoid
{
    internal static Player? m_localPlayer;
    internal ItemDrop.ItemData? Weapon;
    internal ItemDrop.ItemData? GetCurrentWeapon() => Weapon;
    internal readonly Inventory Inventory = new();
    internal readonly HashSet<string> KnownRecipes = new();
    internal readonly HashSet<string> KnownMaterials = new();
    internal CraftingStation? Station;
    internal Inventory GetInventory() => Inventory;
    internal CraftingStation? GetCurrentCraftingStation() => Station;
    internal bool IsRecipeKnown(string name) => KnownRecipes.Contains(name);
    internal bool IsKnownMaterial(string name) => KnownMaterials.Contains(name);
}
internal sealed class Projectile { }

namespace BenheimQoL.Infrastructure
{
    internal static class HealthReporting
    {
        internal static bool GameplayActionsEnabled = true;
    }

    internal sealed class DiagnosticEvent
    {
        internal static DiagnosticEvent Create(string domain, string name) => new();
        internal DiagnosticEvent String(string name, string value) => this;
        internal DiagnosticEvent Integer(string name, int value) => this;
        internal DiagnosticEvent Boolean(string name, bool value) => this;
    }
}

namespace BenheimQoL.Affinities
{
    internal static class AffinityDiagnostics
    {
        internal static void Emit(BenheimQoL.Infrastructure.DiagnosticEvent value) { }
    }
}

internal sealed class CraftingStation
{
    internal string m_name = "$piece_forge";
    internal bool Usable = true;
    internal bool CheckUsable(Player player, bool message) => Usable;
}

internal sealed class Inventory
{
    internal readonly List<ItemDrop.ItemData> Items = new();
    internal int Wood;
    internal int RemoveCalls;
    internal Action? m_onChanged = null;
    internal bool ContainsItem(ItemDrop.ItemData item) => Items.Contains(item);
    internal List<ItemDrop.ItemData> GetAllItems() => Items;
    internal int CountItems(string name) => name == "$item_wood" ? Wood : 0;
    internal void RemoveItem(string name, int amount)
    {
        RemoveCalls++;
        if (name == "$item_wood") Wood = Math.Max(0, Wood - amount);
    }
    internal ItemDrop.ItemData? AddItem(string name, int amount, int quality, int variant, long crafter, string crafterName)
    {
        if (name != "Wood") return null;
        Wood += amount;
        return new ItemDrop.ItemData();
    }
}

namespace BenheimQoL
{
    internal static class Plugin
    {
        internal static readonly TestLog Log = new();
    }
    internal sealed class TestLog
    {
        internal void LogWarning(string message) { }
        internal void LogError(string message) { }
    }
}
namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static string Flatten(string value) => value;
    }
}
