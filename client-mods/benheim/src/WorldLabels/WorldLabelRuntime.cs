using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal static class WorldLabelRuntime
{
    private static readonly List<SignGlowController> SignGlows = new();
    private static readonly List<PortalLabelController> PortalLabels = new();
    private static bool portalPresentationWarningLogged;
    private static bool portalLabelCreationLogged;

    internal static void Attach(Sign sign)
    {
        if (sign.m_textWidget == null ||
            sign.m_textWidget.fontSharedMaterial == null)
        {
            return;
        }

        if (sign.GetComponent<SignGlowController>() != null)
        {
            return;
        }

        SignGlowController controller = sign.gameObject.AddComponent<SignGlowController>();
        controller.Initialize(sign);
        SignGlows.Add(controller);
    }

    internal static void Attach(TeleportWorld portal)
    {
        if (portal.GetComponent<PortalLabelController>() != null)
        {
            return;
        }

        PortalLabelController controller = portal.gameObject.AddComponent<PortalLabelController>();
        controller.Initialize(portal);
        PortalLabels.Add(controller);
    }

    internal static void Reset()
    {
        while (PortalLabels.Count > 0)
        {
            int last = PortalLabels.Count - 1;
            PortalLabelController controller = PortalLabels[last];
            PortalLabels.RemoveAt(last);
            if (controller != null)
            {
                controller.DisposeAndRemove();
            }
        }

        while (SignGlows.Count > 0)
        {
            int last = SignGlows.Count - 1;
            SignGlowController controller = SignGlows[last];
            SignGlows.RemoveAt(last);
            if (controller != null)
            {
                controller.RestoreAndRemove();
            }
        }

        portalPresentationWarningLogged = false;
        portalLabelCreationLogged = false;
    }

    internal static void LogPortalPresentationPending()
    {
        if (portalPresentationWarningLogged)
        {
            return;
        }

        portalPresentationWarningLogged = true;
        Plugin.Log.LogWarning(
            "Portal labels are waiting for Valheim's native Bonus world-text presentation.");
    }

    internal static void LogPortalLabelCreated(TeleportWorld portal)
    {
        if (portalLabelCreationLogged)
        {
            return;
        }

        portalLabelCreationLogged = true;
        Plugin.Log.LogInfo(
            $"Portal label created for native portal '{portal.gameObject.name}'.");
    }

    internal static void Forget(PortalLabelController controller)
    {
        PortalLabels.Remove(controller);
    }

    internal static void Forget(SignGlowController controller)
    {
        SignGlows.Remove(controller);
    }
}
