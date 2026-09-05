using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal static class WorldLabelRuntime
{
    private static readonly List<SignGlowController> SignGlows = new();
    private static readonly List<PortalLabelController> PortalLabels = new();
    private static Sign? nativeWoodenSign;
    private static bool nativeSignResolutionLogged;
    internal static string NativeSignPendingReason { get; private set; } = "native_sign_not_loaded";

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
        nativeSignResolutionLogged = false;
        NativeSignPendingReason = "native_sign_not_loaded";
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
        NativeSignPendingReason = scene == null ? "native_scene_missing" : "native_sign_not_loaded";
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

    private static bool TryFindNativeWoodenSign(
        List<GameObject> prefabs,
        out Sign sign)
    {
        foreach (GameObject prefab in prefabs)
        {
            Sign? candidate = prefab != null ? prefab.GetComponent<Sign>() : null;
            if (!IsUsableNativeWoodenSign(candidate))
            {
                if (prefab != null && prefab.GetComponent<Piece>()?.m_name == "$piece_sign")
                {
                    NativeSignPendingReason = "native_sign_" +
                        PortalSignVisualFactory.GetVisualFailureReason(candidate!);
                }
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
        PortalLabelDiagnostics.Emit(
            DiagnosticEvent.Create("WorldLabels", "portal_sign_donor_resolved")
                .Integer("donor_instance", sign.GetInstanceID())
                .String("font", sign.m_textWidget.font.name));
    }

    private static bool IsUsableNativeWoodenSign(Sign? sign) =>
        sign != null &&
        sign.GetComponent<Piece>()?.m_name == "$piece_sign" &&
        PortalSignVisualFactory.HasUsableVisual(sign);

    internal static void Forget(PortalLabelController controller)
    {
        PortalLabels.Remove(controller);
    }

    internal static void Forget(SignGlowController controller)
    {
        SignGlows.Remove(controller);
    }
}
