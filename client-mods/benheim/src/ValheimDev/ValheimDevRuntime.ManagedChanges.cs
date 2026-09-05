using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.ValheimDev;

internal static partial class ValheimDevRuntime
{
    private static void InstallChange(ValheimDevLoadedCode newCode, ValheimDevRequest request)
    {
        ValheimDevManagedChange? previous = GetManagedChange(request.ChangeId);
        if (previous != null && !TryCleanup(previous.Code, out string? cleanupException))
        {
            restartRequired = true;
            MarkRestartRequired(request.ChangeId, previous);
            activeOperation!.Response.Exception = cleanupException;
            FinishActiveOperation("previous_change_cleanup_failed", ok: false, ValheimDevCleanupState.RestartRequired);
            return;
        }

        ValheimDevExecutionResult execution = ValheimDevCodeExecutor.Invoke(newCode);
        activeOperation!.Response.Result = execution.Result;
        activeOperation.Response.Exception = execution.Exception;
        if (!execution.Ok)
        {
            HandleFailedReplacement(newCode, previous, execution.Error, request);
            return;
        }

        SetManagedChange(request.ChangeId, new ValheimDevManagedChange
        {
            Code = newCode,
            Summary = SummaryFor(request, execution.Result, ValheimDevCleanupState.Active)
        });
        FinishOrObserve(request, ValheimDevCleanupState.Active);
    }

    private static void HandleFailedReplacement(
        ValheimDevLoadedCode newCode,
        ValheimDevManagedChange? previous,
        string executionError,
        ValheimDevRequest request)
    {
        if (!TryCleanup(newCode, out string? newCleanupException))
        {
            restartRequired = true;
            ValheimDevManagedChange uncertainCandidate = new ValheimDevManagedChange
            {
                Code = newCode,
                Summary = SummaryFor(
                    request,
                    activeOperation!.Response.Result,
                    ValheimDevCleanupState.RestartRequired)
            };
            SetManagedChange(request.ChangeId, uncertainCandidate);
            MarkRestartRequired(request.ChangeId, uncertainCandidate);
            activeOperation.Response.Exception = JoinExceptions(activeOperation.Response.Exception, newCleanupException);
            FinishActiveOperation(executionError, ok: false, ValheimDevCleanupState.RestartRequired);
            return;
        }
        if (previous == null)
        {
            FinishActiveOperation(executionError, ok: false, ValheimDevCleanupState.Cleaned);
            return;
        }

        ValheimDevExecutionResult restoration = ValheimDevCodeExecutor.Invoke(previous.Code);
        if (!restoration.Ok)
        {
            restartRequired = true;
            MarkRestartRequired(request.ChangeId, previous);
            activeOperation!.Response.Exception = JoinExceptions(activeOperation.Response.Exception, restoration.Exception);
            FinishActiveOperation("previous_change_restore_failed", ok: false, ValheimDevCleanupState.RestartRequired);
            return;
        }
        lock (Gate)
        {
            previous.Summary.Result = restoration.Result;
            previous.Summary.CleanupState = ValheimDevCleanupState.Active;
        }
        activeOperation!.Response.PreviousChangePreserved = true;
        FinishActiveOperation(executionError, ok: false, ValheimDevCleanupState.Restored);
    }

    private static void RemoveChange(
        ValheimDevPendingRequest pending,
        ValheimDevResponse response,
        string changeId)
    {
        ValheimDevManagedChange? change = GetManagedChange(changeId);
        if (change == null)
        {
            Complete(pending, response, "change_not_active");
            return;
        }
        if (!TryCleanup(change.Code, out string? cleanupException))
        {
            restartRequired = true;
            MarkRestartRequired(changeId, change);
            response.Exception = cleanupException;
            response.CleanupState = ValheimDevCleanupState.RestartRequired;
            Complete(pending, response, "change_cleanup_failed");
            return;
        }
        RemoveManagedChangeIfSame(changeId, change);
        response.Ok = true;
        response.CleanupState = ValheimDevCleanupState.Cleaned;
        response.FinishedUtc = UtcNow();
        SnapshotActiveChanges(response);
        pending.Complete(response.ToJson(includeOperation: true));
    }

    private static Dictionary<string, string> CleanupManagedChanges()
    {
        List<KeyValuePair<string, ValheimDevManagedChange>> changes;
        lock (Gate) changes = new List<KeyValuePair<string, ValheimDevManagedChange>>(ManagedChanges);
        Dictionary<string, string> results = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, ValheimDevManagedChange> item in changes)
        {
            string changeId = item.Key;
            ValheimDevManagedChange change = item.Value;
            if (TryCleanup(change.Code, out string? cleanupException))
            {
                RemoveManagedChangeIfSame(changeId, change);
                results[changeId] = ValheimDevCleanupState.Cleaned;
                continue;
            }
            restartRequired = true;
            results[changeId] = ValheimDevCleanupState.RestartRequired;
            MarkRestartRequired(changeId, change);
            Plugin.Log.LogWarning("Benheim Lab cleanup failed: " + Diagnostics.Flatten(cleanupException ?? "unknown"));
        }
        return results;
    }

    private static string AggregateCleanupState(Dictionary<string, string> results)
    {
        if (results.Count == 0) return ValheimDevCleanupState.NotApplicable;
        foreach (string state in results.Values)
        {
            if (state == ValheimDevCleanupState.RestartRequired) return state;
        }
        return ValheimDevCleanupState.Cleaned;
    }

    private static string CleanupStateForActiveOperation(Dictionary<string, string> results)
    {
        ValheimDevActiveOperation? operation = activeOperation;
        if (operation == null || operation.Response.Action != "install_change")
        {
            return ValheimDevCleanupState.NotApplicable;
        }
        return results.TryGetValue(operation.Response.ChangeId, out string? state) && state != null
            ? state
            : ValheimDevCleanupState.NotApplicable;
    }

    private static bool TryCleanup(ValheimDevLoadedCode code, out string? exception)
    {
        return ValheimDevCodeExecutor.TryCleanup(code, out exception);
    }

    private static ValheimDevChangeSummary SummaryFor(
        ValheimDevRequest request,
        string? result,
        string cleanupState)
    {
        return new ValheimDevChangeSummary
        {
            ChangeId = request.ChangeId,
            OperationId = request.OperationId,
            SourceSha256 = request.SourceSha256,
            AssemblySha256 = request.AssemblySha256,
            InstalledUtc = UtcNow(),
            Result = result,
            CleanupState = cleanupState
        };
    }

    private static void SnapshotActiveChanges(ValheimDevResponse response)
    {
        response.ActiveChanges.Clear();
        lock (Gate)
        {
            List<string> ids = new List<string>(ManagedChanges.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                ValheimDevChangeSummary source = ManagedChanges[id].Summary;
                response.ActiveChanges.Add(new ValheimDevChangeSummary
                {
                    ChangeId = source.ChangeId,
                    OperationId = source.OperationId,
                    SourceSha256 = source.SourceSha256,
                    AssemblySha256 = source.AssemblySha256,
                    InstalledUtc = source.InstalledUtc,
                    Result = source.Result,
                    CleanupState = source.CleanupState
                });
            }
        }
    }

    private static ValheimDevManagedChange? GetManagedChange(string changeId)
    {
        lock (Gate)
        {
            ManagedChanges.TryGetValue(changeId, out ValheimDevManagedChange? change);
            return change;
        }
    }

    private static bool HasManagedChange(string changeId)
    {
        lock (Gate) return ManagedChanges.ContainsKey(changeId);
    }

    private static bool MatchesExpectedChangeState(string changeId, string? expectedOperationId)
    {
        ValheimDevManagedChange? current = GetManagedChange(changeId);
        return expectedOperationId == null
            ? current == null
            : current != null
                && string.Equals(current.Summary.OperationId, expectedOperationId, StringComparison.Ordinal);
    }

    private static int ManagedChangeCount()
    {
        lock (Gate) return ManagedChanges.Count;
    }

    private static void SetManagedChange(string changeId, ValheimDevManagedChange change)
    {
        lock (Gate) ManagedChanges[changeId] = change;
    }

    private static void MarkRestartRequired(string changeId, ValheimDevManagedChange change)
    {
        lock (Gate)
        {
            change.Summary.CleanupState = ValheimDevCleanupState.RestartRequired;
            RestartRequiredChanges.Add(changeId);
        }
    }

    private static void RemoveManagedChangeIfSame(string changeId, ValheimDevManagedChange expected)
    {
        lock (Gate)
        {
            if (ManagedChanges.TryGetValue(changeId, out ValheimDevManagedChange? current)
                && ReferenceEquals(current, expected))
            {
                ManagedChanges.Remove(changeId);
            }
        }
    }

    private static string UncertainChangeIds()
    {
        List<string> ids = new List<string>();
        lock (Gate)
        {
            ids.AddRange(RestartRequiredChanges);
        }
        ids.Sort(StringComparer.Ordinal);
        return string.Join(", ", ids);
    }

    private static string RestartRequiredMessage(string prefix)
    {
        string ids = UncertainChangeIds();
        return string.IsNullOrEmpty(ids) ? prefix : prefix + " Uncertain change(s): " + ids + ".";
    }

    private static string JoinExceptions(string? first, string? second)
    {
        if (string.IsNullOrEmpty(first)) return second ?? string.Empty;
        if (string.IsNullOrEmpty(second)) return first;
        return first + "\nRESTORE/CLEANUP: " + second;
    }
}
