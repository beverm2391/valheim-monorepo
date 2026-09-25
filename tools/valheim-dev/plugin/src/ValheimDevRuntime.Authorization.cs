using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace ValheimDev;

internal static partial class ValheimDevRuntime
{
    private static bool TryAuthorize(out string result)
    {
        if (!initialized)
        {
            result = "not_initialized";
            return false;
        }
        if (Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            result = "not_main_thread";
            return false;
        }

        if (session != null)
        {
            result = "already_authorized";
            return true;
        }

        ValheimDevWorldState state = Snapshot();
        string eligibility = ValheimDevEligibility.CheckAuthorization(state);
        if (eligibility != "eligible")
        {
            result = eligibility;
            return false;
        }

        ValheimDevWorldCapture? candidateCapture = trackedWorld;
        if (restartRequired
            && (candidateCapture == null
                || ValheimDevEligibility.CheckCapturedSession(candidateCapture, state) != "eligible"))
        {
            result = ValheimDevCleanupState.RestartRequired;
            return false;
        }
        if (candidateCapture != null
            && ValheimDevEligibility.CheckCapturedSession(candidateCapture, state) != "eligible")
        {
            string cleanupState = Revoke("authorization_world_changed");
            if (cleanupState == ValheimDevCleanupState.RestartRequired)
            {
                result = cleanupState;
                return false;
            }
            candidateCapture = null;
        }
        bool reusesTrackedWorld = candidateCapture != null;

        TcpListener candidate = new TcpListener(IPAddress.Loopback, 0);
        try
        {
            candidate.Start(ValheimDevProtocol.MaximumQueueDepth);
            ValheimDevSessionIdentity candidateIdentity = CreateSessionIdentity();
            candidateIdentity.SessionId = Guid.NewGuid().ToString("N");
            candidateIdentity.AuthorizedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            int port = ((IPEndPoint)candidate.LocalEndpoint).Port;
            WriteDescriptor(candidateIdentity, port);

            candidateCapture ??= new ValheimDevWorldCapture
            {
                Network = state.Network!, Scene = state.Scene!, WorldId = state.WorldId
            };
            ValheimDevSession candidateSession = new ValheimDevSession(
                candidateIdentity,
                candidateCapture,
                candidate);
            trackedWorld = candidateCapture;
            session = candidateSession;
            Thread acceptThread = new Thread(() => AcceptLoop(candidateSession))
            {
                IsBackground = true,
                Name = "Valheim Dev listener"
            };
            acceptThread.Start();
            ValheimDevDiagnostics.Emit(
                "lab_authorized",
                new KeyValuePair<string, string>("lab_session_id", candidateIdentity.SessionId));
            result = "authorized";
            return true;
        }
        catch (Exception exception)
        {
            session = null;
            if (!reusesTrackedWorld) trackedWorld = null;
            candidate.Stop();
            DeleteDescriptor();
            result = "session_start_failed:" + ValheimDevDiagnostics.Flatten(exception.Message);
            return false;
        }
    }
}
