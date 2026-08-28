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
    internal const string Usage = "bh debug catalog effects|text|ui [filter]";

    internal RuntimePrimitiveCatalogRequest(
        RuntimePrimitiveCatalogCategory category,
        string filter)
    {
        Category = category;
        Filter = filter;
    }

    internal RuntimePrimitiveCatalogCategory Category { get; }
    internal string Filter { get; }

    internal static bool HasCatalogPrefix(string[] arguments)
    {
        return arguments.Length >= 3
            && Equals(arguments[0], "bh")
            && Equals(arguments[1], "debug")
            && Equals(arguments[2], "catalog");
    }

    internal static bool TryParse(
        string[] arguments,
        out RuntimePrimitiveCatalogRequest request)
    {
        request = default;
        if ((arguments.Length != 4 && arguments.Length != 5)
            || !HasCatalogPrefix(arguments)
            || !TryParseCategory(arguments[3], out RuntimePrimitiveCatalogCategory category))
        {
            return false;
        }

        string filter = arguments.Length == 5 ? arguments[4].Trim() : string.Empty;
        if (arguments.Length == 5 && filter.Length == 0)
        {
            return false;
        }

        request = new RuntimePrimitiveCatalogRequest(category, filter);
        return true;
    }

    private static bool TryParseCategory(
        string value,
        out RuntimePrimitiveCatalogCategory category)
    {
        if (Equals(value, "effects"))
        {
            category = RuntimePrimitiveCatalogCategory.Effects;
            return true;
        }

        if (Equals(value, "text"))
        {
            category = RuntimePrimitiveCatalogCategory.Text;
            return true;
        }

        if (Equals(value, "ui"))
        {
            category = RuntimePrimitiveCatalogCategory.Ui;
            return true;
        }

        category = default;
        return false;
    }

    private static bool Equals(string actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
