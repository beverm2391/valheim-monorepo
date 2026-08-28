using System;
using System.Collections.Generic;

namespace BenheimQoL.Infrastructure;

internal readonly struct RuntimePrimitiveCatalogAvailability
{
    internal RuntimePrimitiveCatalogAvailability(
        bool worldReady,
        bool objectDatabaseReady,
        bool objectDatabasePopulated,
        bool inventoryGuiReady,
        bool hudReady,
        bool messageHudReady,
        bool menuReady,
        bool menuSettingsReady,
        bool tmpReady)
    {
        WorldReady = worldReady;
        ObjectDatabaseReady = objectDatabaseReady;
        ObjectDatabasePopulated = objectDatabasePopulated;
        InventoryGuiReady = inventoryGuiReady;
        HudReady = hudReady;
        MessageHudReady = messageHudReady;
        MenuReady = menuReady;
        MenuSettingsReady = menuSettingsReady;
        TmpReady = tmpReady;
    }

    internal bool WorldReady { get; }
    internal bool ObjectDatabaseReady { get; }
    internal bool ObjectDatabasePopulated { get; }
    internal bool InventoryGuiReady { get; }
    internal bool HudReady { get; }
    internal bool MessageHudReady { get; }
    internal bool MenuReady { get; }
    internal bool MenuSettingsReady { get; }
    internal bool TmpReady { get; }
}

internal static class RuntimePrimitiveCatalogReadiness
{
    internal static bool TryValidate(
        RuntimePrimitiveCatalogCategory category,
        RuntimePrimitiveCatalogAvailability availability,
        out string failure)
    {
        if (!availability.WorldReady)
        {
            failure = "the playable world is not ready. Load a world, then try again.";
            return false;
        }

        if (category == RuntimePrimitiveCatalogCategory.Effects)
        {
            if (!availability.ObjectDatabaseReady)
            {
                failure = "ObjectDB is not ready. Load a world, then try again.";
                return false;
            }

            if (!availability.ObjectDatabasePopulated)
            {
                failure = "ObjectDB has not populated status effects and items yet. Try again after world loading finishes.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        List<string> missing = new List<string>();
        if (!availability.InventoryGuiReady)
        {
            missing.Add("InventoryGui");
        }
        if (!availability.HudReady)
        {
            missing.Add("Hud");
        }
        if (!availability.MessageHudReady)
        {
            missing.Add("MessageHud");
        }
        if (!availability.MenuReady)
        {
            missing.Add("Menu");
        }
        else if (!availability.MenuSettingsReady)
        {
            missing.Add("Menu settings prefab");
        }
        if (category == RuntimePrimitiveCatalogCategory.Text && !availability.TmpReady)
        {
            missing.Add("TMP default font");
        }

        if (missing.Count > 0)
        {
            failure = $"native {CategoryName(category)} systems are not ready ({string.Join(", ", missing)}). Load a world, then try again.";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    private static string CategoryName(RuntimePrimitiveCatalogCategory category)
    {
        return category switch
        {
            RuntimePrimitiveCatalogCategory.Effects => "effects",
            RuntimePrimitiveCatalogCategory.Text => "text",
            RuntimePrimitiveCatalogCategory.Ui => "ui",
            _ => throw new ArgumentOutOfRangeException(nameof(category))
        };
    }
}
