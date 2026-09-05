using System;
using System.Reflection;

namespace BenheimQoL.ValheimDev;

internal sealed class ValheimDevWorldState
{
    internal object? Network { get; set; }
    internal object? Scene { get; set; }
    internal long WorldId { get; set; }
    internal bool IsServer { get; set; }
    internal bool IsOpenServer { get; set; }
    internal bool IsDedicated { get; set; }
    internal int PeerCount { get; set; }
    internal bool HasServerRpc { get; set; }
    internal object? LocalPlayer { get; set; }
    internal bool LocalPlayerIsAlive { get; set; }
    internal bool LocalPlayerIsOwner { get; set; }
    internal bool GameplayHooksHealthy { get; set; }
}

internal sealed class ValheimDevWorldCapture
{
    internal object Network { get; set; } = null!;
    internal object Scene { get; set; } = null!;
    internal long WorldId { get; set; }
}

internal static class ValheimDevEligibility
{
    internal static string CheckAuthorization(ValheimDevWorldState state)
    {
        string sessionReason = CheckCommon(state);
        if (sessionReason != "eligible") return sessionReason;
        return CheckPlayer(state);
    }

    internal static string CheckOperation(
        ValheimDevWorldCapture capture,
        ValheimDevWorldState state)
    {
        string sessionReason = CheckCapturedSession(capture, state);
        if (sessionReason != "eligible") return sessionReason;
        return CheckPlayer(state);
    }

    // Player is deliberately not part of the captured world session. Valheim
    // replaces the local Player object during respawn without changing the
    // disposable world authorization.
    internal static string CheckCapturedSession(
        ValheimDevWorldCapture capture,
        ValheimDevWorldState state)
    {
        string commonReason = CheckCommon(state);
        if (commonReason != "eligible") return commonReason;
        if (!ReferenceEquals(capture.Network, state.Network)) return "network_changed";
        if (!ReferenceEquals(capture.Scene, state.Scene)) return "scene_changed";
        if (capture.WorldId != state.WorldId) return "world_changed";
        return "eligible";
    }

    private static string CheckCommon(ValheimDevWorldState state)
    {
        if (state.Network == null) return "no_network";
        if (state.Scene == null) return "no_world_scene";
        if (!state.GameplayHooksHealthy) return "gameplay_hooks_unhealthy";
        if (!state.IsServer) return "not_server";
        if (state.IsOpenServer) return "open_server";
        if (state.IsDedicated) return "dedicated_server";
        if (state.PeerCount != 0) return "peers_present";
        if (state.HasServerRpc) return "server_rpc_present";
        return "eligible";
    }

    private static string CheckPlayer(ValheimDevWorldState state)
    {
        if (state.LocalPlayer == null || !state.LocalPlayerIsAlive) return "no_local_player";
        if (!state.LocalPlayerIsOwner) return "local_player_not_owned";
        return "eligible";
    }
}

internal sealed class ValheimDevLoadedCode
{
    internal MethodInfo Run { get; set; } = null!;
    internal MethodInfo? Cleanup { get; set; }
}

internal sealed class ValheimDevExecutionResult
{
    internal bool Ok { get; set; }
    internal string? Result { get; set; }
    internal string? Exception { get; set; }
    internal string Error { get; set; } = string.Empty;
    internal ValheimDevLoadedCode? LoadedCode { get; set; }
}

internal static class ValheimDevCodeExecutor
{
    internal static ValheimDevExecutionResult Prepare(
        byte[] assemblyBytes,
        string entryType,
        bool requireCleanup)
    {
        ValheimDevExecutionResult result = new ValheimDevExecutionResult();
        try
        {
            Assembly assembly = Assembly.Load(assemblyBytes);
            Type? type = assembly.GetType(entryType, throwOnError: false, ignoreCase: false);
            if (type == null || !type.IsPublic || !type.IsAbstract || !type.IsSealed)
            {
                result.Error = "entry_type_not_public_static";
                return result;
            }

            MethodInfo? run = type.GetMethod(
                "Run",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (run == null || run.ReturnType != typeof(string))
            {
                result.Error = "run_entrypoint_invalid";
                return result;
            }

            MethodInfo? cleanup = type.GetMethod(
                "Cleanup",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (cleanup != null && cleanup.ReturnType != typeof(void))
            {
                result.Error = "cleanup_entrypoint_invalid";
                return result;
            }
            if (requireCleanup && cleanup == null)
            {
                result.Error = "cleanup_entrypoint_required";
                return result;
            }

            result.LoadedCode = new ValheimDevLoadedCode { Run = run, Cleanup = cleanup };
            result.Ok = true;
            return result;
        }
        catch (Exception exception)
        {
            result.Exception = BoundException(exception);
            result.Error = "assembly_or_entrypoint_error";
            return result;
        }
    }

    internal static ValheimDevExecutionResult Invoke(ValheimDevLoadedCode code)
    {
        ValheimDevExecutionResult result = new ValheimDevExecutionResult
        {
            LoadedCode = code
        };
        try
        {
            object? returnValue = code.Run.Invoke(null, null);
            result.Result = returnValue as string;
            result.Ok = true;
            return result;
        }
        catch (TargetInvocationException exception)
        {
            Exception cause = exception.InnerException ?? exception;
            result.Exception = BoundException(cause);
            result.Error = "entrypoint_exception";
            return result;
        }
        catch (Exception exception)
        {
            result.Exception = BoundException(exception);
            result.Error = "entrypoint_invocation_error";
            return result;
        }
    }

    internal static bool TryCleanup(ValheimDevLoadedCode? code, out string? exception)
    {
        exception = null;
        if (code?.Cleanup == null) return false;
        try
        {
            code.Cleanup.Invoke(null, null);
            return true;
        }
        catch (TargetInvocationException invocationException)
        {
            exception = BoundException(invocationException.InnerException ?? invocationException);
            return false;
        }
        catch (Exception cleanupException)
        {
            exception = BoundException(cleanupException);
            return false;
        }
    }

    private static string BoundException(Exception exception)
    {
        string value = exception.ToString();
        return value.Length <= 16384 ? value : value.Substring(0, 16384);
    }
}

// Experiments that perform bounded cooperative work can observe revocation.
// This cannot preempt C# already executing on Unity's main thread.
public static class ValheimDevCancellation
{
    public static bool IsCancellationRequested => ValheimDevRuntime.IsCancellationRequested;
}
