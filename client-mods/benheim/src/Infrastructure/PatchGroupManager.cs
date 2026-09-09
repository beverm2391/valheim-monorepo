using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;

namespace BenheimQoL.Infrastructure;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class PatchGroupAttribute : Attribute
{
    internal PatchGroupAttribute(string owner)
    {
        Owner = owner;
    }

    internal string Owner { get; }
}

/// <summary>
/// Installs Harmony patch classes under feature-owned Harmony IDs. Patch types
/// are still visited in assembly order so successful startup preserves the old
/// PatchAll ordering, while a failed feature can remove only its own patches.
/// </summary>
internal sealed class PatchGroupManager
{
    private const string BenheimNamespacePrefix = "BenheimQoL.";

    private readonly Dictionary<string, PatchGroupState> groups =
        new Dictionary<string, PatchGroupState>(StringComparer.Ordinal);
    private readonly string harmonyIdPrefix;
    private readonly Action<string, string, Exception> reportPatchFailure;
    private readonly Action<string, Exception> reportCleanupFailure;
    private readonly Action<string> reportCleanupSucceeded;

    private PatchGroupManager(
        string harmonyIdPrefix,
        Action<string, string, Exception> reportPatchFailure,
        Action<string, Exception> reportCleanupFailure,
        Action<string> reportCleanupSucceeded)
    {
        this.harmonyIdPrefix = harmonyIdPrefix;
        this.reportPatchFailure = reportPatchFailure;
        this.reportCleanupFailure = reportCleanupFailure;
        this.reportCleanupSucceeded = reportCleanupSucceeded;
    }

    internal static PatchGroupManager Apply(
        IEnumerable<Type> assemblyTypes,
        string harmonyIdPrefix,
        Action<string, string, Exception> reportPatchFailure,
        Action<string, Exception> reportCleanupFailure,
        Action<string> reportCleanupSucceeded)
    {
        PatchGroupManager manager = new PatchGroupManager(
            harmonyIdPrefix,
            reportPatchFailure,
            reportCleanupFailure,
            reportCleanupSucceeded);
        manager.Apply(assemblyTypes);
        return manager;
    }

    internal bool IsAvailable(Type featureType)
    {
        string owner = ResolveOwner(featureType);
        return groups.TryGetValue(owner, out PatchGroupState? group)
            && group.IsAvailable;
    }

    internal void RetryFailedCleanup()
    {
        foreach (PatchGroupState group in groups.Values)
        {
            TryCleanup(group);
        }
    }

    internal void UnpatchAll()
    {
        foreach (PatchGroupState group in groups.Values)
        {
            group.RequestCleanup();
            TryCleanup(group);
        }
    }

    private void Apply(IEnumerable<Type> assemblyTypes)
    {
        foreach (Type patchType in assemblyTypes)
        {
            if (!IsHarmonyPatchType(patchType))
            {
                continue;
            }

            string owner = ResolveOwner(patchType);
            PatchGroupState group = GetOrCreateGroup(owner);
            if (!group.IsAvailable)
            {
                continue;
            }

            try
            {
                group.Apply(patchType);
            }
            catch (Exception ex)
            {
                group.MarkFailed();
                reportPatchFailure(owner, patchType.FullName ?? patchType.Name, ex);
                TryCleanup(group);
            }
        }
    }

    private PatchGroupState GetOrCreateGroup(string owner)
    {
        if (groups.TryGetValue(owner, out PatchGroupState? group))
        {
            return group;
        }

        group = new PatchGroupState(
            owner,
            new Harmony($"{harmonyIdPrefix}.{NormalizeHarmonyId(owner)}"));
        groups.Add(owner, group);
        return group;
    }

    private void TryCleanup(PatchGroupState group)
    {
        if (!group.CleanupRequested)
        {
            return;
        }

        try
        {
            bool recoveredFromFailure = group.CleanupFailed;
            group.Unpatch();
            if (recoveredFromFailure)
            {
                reportCleanupSucceeded(group.Owner);
            }
        }
        catch (Exception ex)
        {
            if (!group.CleanupFailed)
            {
                reportCleanupFailure(group.Owner, ex);
            }

            group.MarkCleanupFailed();
        }
    }

    private static bool IsHarmonyPatchType(Type type)
    {
        return type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length > 0;
    }

    private static string ResolveOwner(Type featureType)
    {
        PatchGroupAttribute? explicitGroup = (PatchGroupAttribute?)Attribute.GetCustomAttribute(
            featureType,
            typeof(PatchGroupAttribute),
            inherit: false);
        if (explicitGroup?.Owner is { Length: > 0 })
        {
            return explicitGroup.Owner;
        }

        string patchNamespace = featureType.Namespace ?? string.Empty;
        if (!patchNamespace.StartsWith(BenheimNamespacePrefix, StringComparison.Ordinal))
        {
            return "Core";
        }

        string relativeNamespace = patchNamespace.Substring(BenheimNamespacePrefix.Length);
        int nestedNamespace = relativeNamespace.IndexOf('.');
        string owner = nestedNamespace >= 0
            ? relativeNamespace.Substring(0, nestedNamespace)
            : relativeNamespace;

        const string featureSuffix = "Feature";
        if (owner.EndsWith(featureSuffix, StringComparison.Ordinal))
        {
            owner = owner.Substring(0, owner.Length - featureSuffix.Length);
        }

        return owner.Length > 0 ? owner : "Core";
    }

    private static string NormalizeHarmonyId(string owner)
    {
        StringBuilder normalized = new StringBuilder(owner.Length);
        foreach (char character in owner)
        {
            normalized.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '-');
        }

        return normalized.ToString();
    }

    private sealed class PatchGroupState
    {
        private Harmony? harmony;

        internal PatchGroupState(string owner, Harmony harmony)
        {
            Owner = owner;
            this.harmony = harmony;
            IsAvailable = true;
        }

        internal string Owner { get; }
        internal bool IsAvailable { get; private set; }
        internal bool CleanupRequested { get; private set; }
        internal bool CleanupFailed { get; private set; }

        internal void Apply(Type patchType)
        {
            harmony!.CreateClassProcessor(patchType).Patch();
        }

        internal void MarkFailed()
        {
            IsAvailable = false;
            CleanupRequested = true;
        }

        internal void RequestCleanup()
        {
            CleanupRequested = harmony != null;
        }

        internal void MarkCleanupFailed()
        {
            CleanupFailed = true;
        }

        internal void Unpatch()
        {
            harmony?.UnpatchSelf();
            harmony = null;
            CleanupRequested = false;
            CleanupFailed = false;
        }
    }
}
