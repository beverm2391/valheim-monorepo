using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Repair;

internal static class BuildingRepair
{
    internal const float RepairRadius = 20f;

    private static readonly List<Piece> NearbyPieces = new List<Piece>();

    private static readonly MethodInfo NativeRepairMethod =
        AccessTools.DeclaredMethod(typeof(Player), "Repair", new[] { typeof(ItemDrop.ItemData), typeof(Piece) })
        ?? throw new MissingMethodException("Could not find Player.Repair(ItemData, Piece)");

    private static readonly FieldInfo HoveringPieceField =
        AccessTools.Field(typeof(Player), "m_hoveringPiece")
        ?? throw new MissingFieldException(typeof(Player).FullName, "m_hoveringPiece");

    internal static bool IsInvokingNativeRepair { get; private set; }

    private static bool nativeRepairSucceeded;
    private static WearNTear? nativeRepairTarget;
    private static int nativeMissingStationDenials;

    internal static int RepairNearby(Player player, ItemDrop.ItemData toolItem, Piece anchor)
    {
        NearbyPieces.Clear();
        nativeMissingStationDenials = 0;
        Piece.GetAllPiecesInRadius(anchor.transform.position, RepairRadius, NearbyPieces);
        NearbyPieces.Sort((left, right) =>
            Vector3.SqrMagnitude(left.transform.position - anchor.transform.position)
                .CompareTo(Vector3.SqrMagnitude(right.transform.position - anchor.transform.position)));

        int structures = 0;
        int attempted = 0;
        int repaired = 0;
        var repairedByDisplayName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        bool staminaExhausted = false;
        bool toolExhausted = false;

        foreach (Piece piece in NearbyPieces)
        {
            if (!piece)
            {
                continue;
            }

            WearNTear? wearNTear = piece.GetComponent<WearNTear>();
            if (!wearNTear)
            {
                continue;
            }

            structures++;
            if (toolItem.m_shared.m_useDurability && toolItem.m_durability <= 0f)
            {
                toolExhausted = true;
                break;
            }

            if (!player.HaveStamina(toolItem.m_shared.m_attack.m_attackStamina))
            {
                staminaExhausted = true;
                Hud.instance.StaminaBarEmptyFlash();
                break;
            }

            attempted++;
            if (InvokeNativeRepair(player, toolItem, piece, wearNTear))
            {
                repaired++;
                string displayName = GetDisplayName(piece);
                repairedByDisplayName.TryGetValue(displayName, out int previous);
                repairedByDisplayName[displayName] = previous + 1;
            }
        }

        Diagnostics.Event(
            "Repair",
            "building_repair_finished",
            $"anchor=\"{anchor.gameObject.name}\" candidates={NearbyPieces.Count} structures={structures} attempted={attempted} repaired={repaired} types={repairedByDisplayName.Count} missing_station_denials={nativeMissingStationDenials} stamina_exhausted={Diagnostics.Bool(staminaExhausted)} tool_exhausted={Diagnostics.Bool(toolExhausted)}");

        if (repaired > 0)
        {
            QuickStackReceiptHud.Show(FormatReceipt(repairedByDisplayName));
        }

        return repaired;
    }

    internal static int RecordNativeMissingStationDenial()
    {
        if (!IsInvokingNativeRepair)
        {
            return 0;
        }

        return ++nativeMissingStationDenials;
    }

    internal static void RecordNativeRepairResult(WearNTear repairTarget, bool repaired)
    {
        if (IsInvokingNativeRepair && ReferenceEquals(nativeRepairTarget, repairTarget))
        {
            nativeRepairSucceeded = repaired;
        }
    }

    private static bool InvokeNativeRepair(
        Player player,
        ItemDrop.ItemData toolItem,
        Piece piece,
        WearNTear repairTarget)
    {
        Piece? originalHover = HoveringPieceField.GetValue(player) as Piece;
        nativeRepairSucceeded = false;
        nativeRepairTarget = repairTarget;
        IsInvokingNativeRepair = true;

        try
        {
            // Player.Repair reads this field instead of its Piece parameter. Replacing it only
            // for the native call lets Valheim retain ownership, station, ward, and tool rules.
            HoveringPieceField.SetValue(player, piece);
            NativeRepairMethod.Invoke(player, new object[] { toolItem, piece });
            return nativeRepairSucceeded;
        }
        finally
        {
            IsInvokingNativeRepair = false;
            nativeRepairTarget = null;
            HoveringPieceField.SetValue(player, originalHover);
        }
    }

    private static string GetDisplayName(Piece piece)
    {
        string name = string.IsNullOrWhiteSpace(piece.m_name)
            ? piece.gameObject.name
            : piece.m_name;
        return Localization.instance != null
            ? Localization.instance.Localize(name)
            : name.TrimStart('$');
    }

    private static string FormatReceipt(IReadOnlyDictionary<string, int> repairedByDisplayName)
    {
        return string.Join(
            "\n",
            repairedByDisplayName
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"Repaired {pair.Value} {Pluralize(pair.Key, pair.Value)}"));
    }

    private static string Pluralize(string displayName, int count)
    {
        if (count == 1 || displayName.Length == 0)
        {
            return displayName;
        }

        char last = displayName[displayName.Length - 1];
        if (!char.IsLetter(last) || last == 's' || last == 'S')
        {
            return displayName;
        }

        if (displayName.EndsWith("ch", StringComparison.OrdinalIgnoreCase)
            || displayName.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
            || last == 'x'
            || last == 'X'
            || last == 'z'
            || last == 'Z')
        {
            return displayName + "es";
        }

        if ((last == 'y' || last == 'Y')
            && displayName.Length > 1
            && !"aeiouAEIOU".Contains(displayName[displayName.Length - 2]))
        {
            return displayName.Substring(0, displayName.Length - 1) + "ies";
        }

        return displayName + "s";
    }
}
