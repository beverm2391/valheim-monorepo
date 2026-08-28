using System;
using System.Reflection;
using HarmonyLib;

namespace BenheimQoL.Farming;

internal static class FarmingReflection
{
    internal static readonly FieldInfo NoPlacementCostField =
        AccessTools.Field(typeof(Player), "m_noPlacementCost")
        ?? throw new MissingFieldException(typeof(Player).FullName, "m_noPlacementCost");

    internal static readonly FieldInfo PlacementGhostField =
        AccessTools.Field(typeof(Player), "m_placementGhost")
        ?? throw new MissingFieldException(typeof(Player).FullName, "m_placementGhost");

    internal static readonly FieldInfo BuildPiecesField =
        AccessTools.Field(typeof(Player), "m_buildPieces")
        ?? throw new MissingFieldException(typeof(Player).FullName, "m_buildPieces");

    internal static readonly FieldInfo BuildRemoveDebtField =
        AccessTools.Field(typeof(Player), "m_buildRemoveDebt")
        ?? throw new MissingFieldException(typeof(Player).FullName, "m_buildRemoveDebt");

    internal static readonly MethodInfo GetRightItemMethod =
        AccessTools.Method(typeof(Humanoid), "GetRightItem")
        ?? throw new MissingMethodException(typeof(Humanoid).FullName, "GetRightItem");

    internal static readonly MethodInfo GetBuildStaminaMethod =
        AccessTools.Method(typeof(Player), "GetBuildStamina")
        ?? throw new MissingMethodException(typeof(Player).FullName, "GetBuildStamina");

    internal static readonly MethodInfo GetPlaceDurabilityMethod =
        AccessTools.Method(typeof(Player), "GetPlaceDurability")
        ?? throw new MissingMethodException(typeof(Player).FullName, "GetPlaceDurability");

    internal static float GetBuildStamina(Player player)
    {
        return (float)(GetBuildStaminaMethod.Invoke(player, null) ?? 0f);
    }

    internal static float GetPlaceDurability(Player player, ItemDrop.ItemData tool)
    {
        return (float)(GetPlaceDurabilityMethod.Invoke(player, new object[] { tool }) ?? 0f);
    }

    internal static void ApplyBuildSkill(Player player, PieceTable? pieceTable)
    {
        if (pieceTable is null || pieceTable.m_skill == Skills.SkillType.None)
        {
            return;
        }

        int removeDebt = (int)(BuildRemoveDebtField.GetValue(player) ?? 0);
        if (removeDebt > 0)
        {
            BuildRemoveDebtField.SetValue(player, removeDebt - 1);
            return;
        }

        player.RaiseSkill(pieceTable.m_skill);
    }
}
