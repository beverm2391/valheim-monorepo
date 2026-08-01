using UnityEngine;

namespace BenheimQoL.Farming;

internal static class PlantingState
{
    internal static Vector3 AnchorPosition { get; private set; }
    internal static Quaternion AnchorRotation { get; private set; }
    internal static Piece? AnchorPiece { get; private set; }
    internal static bool AnchorPlaced { get; set; }
    internal static int? Rotation { get; set; }
    internal static bool MassPlacementRunning { get; set; }

    internal static void CaptureAnchor(Player player, Piece piece)
    {
        GameObject? ghost = (GameObject?)FarmingReflection.PlacementGhostField.GetValue(player);
        if (!ghost)
        {
            AnchorPlaced = false;
            return;
        }

        AnchorPosition = ghost.transform.position;
        AnchorRotation = ghost.transform.rotation;
        AnchorPiece = piece;
        AnchorPlaced = true;
    }
}
