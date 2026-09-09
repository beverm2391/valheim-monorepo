using System;

namespace BenheimQoL.Infrastructure;

internal enum RuntimePrimitiveCatalogCategory
{
    Effects,
    Text,
    Ui
}

internal readonly struct RuntimePrimitiveCatalogRequest
{
    internal RuntimePrimitiveCatalogRequest(
        RuntimePrimitiveCatalogCategory category,
        string filter)
    {
        Category = category;
        Filter = filter;
    }

    internal RuntimePrimitiveCatalogCategory Category { get; }
    internal string Filter { get; }

    internal static bool TryCreate(
        RuntimePrimitiveCatalogCategory category,
        string[] arguments,
        out RuntimePrimitiveCatalogRequest request)
    {
        request = default;
        if (arguments.Length > 1)
        {
            return false;
        }

        string filter = arguments.Length == 1 ? arguments[0].Trim() : string.Empty;
        if (arguments.Length == 1 && filter.Length == 0)
        {
            return false;
        }

        request = new RuntimePrimitiveCatalogRequest(category, filter);
        return true;
    }

}
