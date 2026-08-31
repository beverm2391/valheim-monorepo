using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.Farming;

internal readonly struct FarmingGridPoint
{
    internal FarmingGridPoint(int index, int row, int column, Vector3 position, bool isAnchor)
    {
        Index = index;
        Row = row;
        Column = column;
        Position = position;
        IsAnchor = isAnchor;
    }

    internal int Index { get; }
    internal int Row { get; }
    internal int Column { get; }
    internal Vector3 Position { get; }
    internal bool IsAnchor { get; }
}

internal static class FarmingGrid
{
    internal static List<FarmingGridPoint> Build(Vector3 origin, float spacing, Quaternion rotation)
    {
        Vector3 left = rotation * Vector3.left * spacing;
        Vector3 forward = rotation * Vector3.forward * spacing;
        Vector3 rowOrigin = origin
            - forward * (FarmingSettings.GridLength / 2)
            - left * (FarmingSettings.GridWidth / 2);

        var points = new List<FarmingGridPoint>(FarmingSettings.GridWidth * FarmingSettings.GridLength);
        int index = 0;
        for (int row = 0; row < FarmingSettings.GridLength; row++)
        {
            Vector3 position = rowOrigin;
            for (int column = 0; column < FarmingSettings.GridWidth; column++)
            {
                position.y = ZoneSystem.instance.GetGroundHeight(position);
                bool isAnchor = row == FarmingSettings.GridLength / 2
                    && column == FarmingSettings.GridWidth / 2;
                points.Add(new FarmingGridPoint(index++, row, column, position, isAnchor));
                position += left;
            }

            rowOrigin += forward;
        }

        return points;
    }
}
