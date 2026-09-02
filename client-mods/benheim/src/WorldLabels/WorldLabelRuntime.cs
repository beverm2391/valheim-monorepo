using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal static class WorldLabelRuntime
{
    private static readonly List<SignGlowController> SignGlows = new();
    private static readonly List<PortalLabelController> PortalLabels = new();
    private static Sign? nativeWoodenSign;
    private static bool nativeSignWarningLogged;
    private static bool nativeSignResolutionLogged;
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

        RememberNativeWoodenSign(sign);
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

        nativeWoodenSign = null;
        nativeSignWarningLogged = false;
        nativeSignResolutionLogged = false;
        portalLabelCreationLogged = false;
    }

    internal static bool TryGetNativeWoodenSign(out Sign sign)
    {
        if (IsUsableNativeWoodenSign(nativeWoodenSign))
        {
            sign = nativeWoodenSign!;
            return true;
        }

        nativeWoodenSign = null;
        ZNetScene? scene = ZNetScene.instance;
        if (scene != null &&
            (TryFindNativeWoodenSign(scene.m_prefabs, out sign) ||
             TryFindNativeWoodenSign(scene.m_nonNetViewPrefabs, out sign)))
        {
            RememberNativeWoodenSign(sign);
            return true;
        }

        sign = null!;
        return false;
    }

    internal static void LogNativeSignPending()
    {
        if (nativeSignWarningLogged)
        {
            return;
        }

        nativeSignWarningLogged = true;
        Plugin.Log.LogWarning(
            "Portal sign boards are waiting for Valheim's native piece_sign visual.");
    }

    internal static void LogPortalLabelCreated(TeleportWorld portal)
    {
        if (portalLabelCreationLogged)
        {
            return;
        }

        portalLabelCreationLogged = true;
        Plugin.Log.LogInfo(
            $"Portal sign board created for native portal '{portal.gameObject.name}'.");
    }

    private static bool TryFindNativeWoodenSign(
        List<GameObject> prefabs,
        out Sign sign)
    {
        foreach (GameObject prefab in prefabs)
        {
            Sign? candidate = prefab != null ? prefab.GetComponent<Sign>() : null;
            if (!IsUsableNativeWoodenSign(candidate))
            {
                continue;
            }

            sign = candidate!;
            return true;
        }

        sign = null!;
        return false;
    }

    private static void RememberNativeWoodenSign(Sign sign)
    {
        if (!IsUsableNativeWoodenSign(sign))
        {
            return;
        }

        nativeWoodenSign = sign;
        if (nativeSignResolutionLogged)
        {
            return;
        }

        nativeSignResolutionLogged = true;
        Plugin.Log.LogInfo("Portal sign-board donor resolved from Valheim's native piece_sign visual.");
    }

    private static bool IsUsableNativeWoodenSign(Sign? sign) =>
        sign != null &&
        IsNativeWoodenSignName(sign.gameObject.name) &&
        PortalSignVisualFactory.HasUsableVisual(sign);

    private static bool IsNativeWoodenSignName(string name) =>
        name == "piece_sign" || name.StartsWith("piece_sign(Clone)");

    internal static void Forget(PortalLabelController controller)
    {
        PortalLabels.Remove(controller);
    }

    internal static void Forget(SignGlowController controller)
    {
        SignGlows.Remove(controller);
    }
}
