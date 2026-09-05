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
    internal static List<FarmingGridPoint> Build(
        Vector3 origin,
        float spacing,
        Quaternion rotation,
        int size)
    {
        if (!FarmingGridSelection.IsAllowed(size))
        {
            throw new System.ArgumentOutOfRangeException(nameof(size), size, "Planting grids must use an allowed odd size.");
        }

        Vector3 left = rotation * Vector3.left * spacing;
        Vector3 forward = rotation * Vector3.forward * spacing;
        Vector3 rowOrigin = origin
            - forward * (size / 2)
            - left * (size / 2);

        var points = new List<FarmingGridPoint>(size * size);
        int index = 0;
        for (int row = 0; row < size; row++)
        {
            Vector3 position = rowOrigin;
            for (int column = 0; column < size; column++)
            {
                position.y = ZoneSystem.instance.GetGroundHeight(position);
                bool isAnchor = row == size / 2 && column == size / 2;
                points.Add(new FarmingGridPoint(index++, row, column, position, isAnchor));
                position += left;
            }

            rowOrigin += forward;
        }

        return points;
    }
}
