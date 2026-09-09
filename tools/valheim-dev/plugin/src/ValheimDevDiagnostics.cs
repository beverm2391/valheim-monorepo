using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace ValheimDev;

internal sealed class ValheimDevEvidenceRecord
{
    internal string Domain { get; set; } = string.Empty;
    internal string Name { get; set; } = string.Empty;
    internal string Json { get; set; } = string.Empty;
}

internal static class ValheimDevDiagnostics
{
    private const string OptionalProviderType =
        "BenheimQoL.Infrastructure.ValheimDevEvidenceAdapter, BenheimQoL";
    private const string OptionalProviderMethod = "SetExternalObserver";
    private static MethodInfo? unsubscribeMethod;
    private static Action<ValheimDevEvidenceRecord>? activeObserver;

    internal static string Flatten(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ').Replace(' ', '_');
    }

    internal static void Emit(string name, params KeyValuePair<string, string>[] fields)
    {
        StringBuilder line = new StringBuilder("[diag][ValheimDev] ").Append(name);
        StringBuilder json = new StringBuilder(256).Append('{');
        ValheimDevJson.AppendProperty(json, "timestamp", DateTime.UtcNow.ToString("O"));
        json.Append(',');
        ValheimDevJson.AppendProperty(json, "domain", "ValheimDev");
        json.Append(',');
        ValheimDevJson.AppendProperty(json, "event", name);
        foreach (KeyValuePair<string, string> field in fields)
        {
            line.Append(' ').Append(field.Key).Append('=').Append(Flatten(field.Value));
            json.Append(',');
            ValheimDevJson.AppendProperty(json, field.Key, field.Value);
        }
        json.Append('}');
        Plugin.Log.LogInfo(line.ToString());
        ValheimDevEvidenceRecord record = new ValheimDevEvidenceRecord
        {
            Domain = "ValheimDev",
            Name = name,
            Json = json.ToString()
        };
#if VALHEIM_DEV_TESTS
        emittedForTests.Add(record);
#endif
        activeObserver?.Invoke(record);
    }

    internal static bool BeginObservation(
        Action<ValheimDevEvidenceRecord> observer,
        bool needsOptionalProvider,
        out string? unavailableReason)
    {
        activeObserver = observer;
        if (!needsOptionalProvider)
        {
            unavailableReason = null;
            return true;
        }
#if VALHEIM_DEV_TESTS
        if (testEvidenceSubscription != null)
        {
            string status = testEvidenceSubscription((domain, name, json) => observer(new ValheimDevEvidenceRecord
            {
                Domain = domain,
                Name = name,
                Json = json
            }));
            unavailableReason = status == "available" ? null : status;
            return unavailableReason == null;
        }
#endif
        Type? provider = Type.GetType(OptionalProviderType, throwOnError: false);
        MethodInfo? method = provider?.GetMethod(
            OptionalProviderMethod,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
        {
            unavailableReason = "optional_provider_absent";
            return false;
        }

        Action<string, string, string> adapter = (domain, name, json) => observer(new ValheimDevEvidenceRecord
        {
            Domain = domain,
            Name = name,
            Json = json
        });
        try
        {
            string status = method.Invoke(null, new object?[] { "ValheimDev", adapter }) as string
                ?? "optional_provider_invalid";
            if (status != "available")
            {
                unavailableReason = status;
                return false;
            }
            unsubscribeMethod = method;
            unavailableReason = null;
            return true;
        }
        catch (Exception exception)
        {
            unavailableReason = "optional_provider_error:" + Flatten(exception.Message);
            return false;
        }
    }

    internal static void UnsubscribeOptionalEvidence()
    {
        activeObserver = null;
        MethodInfo? method = unsubscribeMethod;
        unsubscribeMethod = null;
        if (method == null) return;
        try { method.Invoke(null, new object?[] { "ValheimDev", null }); }
        catch { }
    }

#if VALHEIM_DEV_TESTS
    private static Func<Action<string, string, string>?, string>? testEvidenceSubscription;
    private static readonly List<ValheimDevEvidenceRecord> emittedForTests = new List<ValheimDevEvidenceRecord>();

    internal static IReadOnlyList<ValheimDevEvidenceRecord> EmittedForTests => emittedForTests;
    internal static ValheimDevEvidenceRecord? LastEmittedForTests =>
        emittedForTests.Count == 0 ? null : emittedForTests[emittedForTests.Count - 1];
    internal static Action<ValheimDevEvidenceRecord>? CaptureObserverForTests() => activeObserver;

    internal static void PublishEvidenceForTests(string domain, string name, string json)
    {
        activeObserver?.Invoke(new ValheimDevEvidenceRecord { Domain = domain, Name = name, Json = json });
    }

    internal static void ClearEmittedForTests()
    {
        emittedForTests.Clear();
    }

    internal static void SetEvidenceSubscriptionForTests(
        Func<Action<string, string, string>?, string>? subscription)
    {
        testEvidenceSubscription = subscription;
    }
#endif
}
