namespace BenheimQoL.Farming;

internal static class PlantingStamina
{
    internal const float CostMultiplier = 0.5f;

    internal static float Cost(float nativeCost)
    {
        return nativeCost * CostMultiplier;
    }

    internal static void ApplyResolvedCost(PieceTable? buildPieces, ref float resolvedCost)
    {
        Piece? selectedPiece = buildPieces?.GetSelectedPiece();
        if (selectedPiece is not null && selectedPiece && selectedPiece.GetComponent<Plant>())
        {
            resolvedCost = Cost(resolvedCost);
        }
    }

    internal static bool HasPlacementStamina(Player player, float nativeThreshold, Piece selectedPiece)
    {
        if (!selectedPiece || !selectedPiece.GetComponent<Plant>())
        {
            return player.HaveStamina(nativeThreshold);
        }

        return player.HaveStamina(FarmingReflection.GetBuildStamina(player));
    }
}
