using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal static class WorldLabelRuntime
{
    private static readonly List<SignGlowController> SignGlows = new();
    private static readonly List<PortalLabelController> PortalLabels = new();
    private static NativeTextDonor? nativeTextDonor;
    private static ZNetScene? scannedSceneWithoutDonor;
    private static bool nativeDonorWarningLogged;
    private static bool nativeDonorResolutionLogged;
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

        RememberNativeTextDonor(sign.m_textWidget, $"loaded Sign '{sign.gameObject.name}'");
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

        nativeDonorWarningLogged = false;
        nativeDonorResolutionLogged = false;
        portalLabelCreationLogged = false;
        nativeTextDonor = null;
        scannedSceneWithoutDonor = null;
    }

    internal static bool TryGetNativeTextDonor(out NativeTextDonor donor)
    {
        if (IsUsableDonor(nativeTextDonor))
        {
            donor = nativeTextDonor!.Value;
            return true;
        }

        nativeTextDonor = null;
        ZNetScene? scene = ZNetScene.instance;
        if (scene == null)
        {
            LogNativeDonorPending("ZNetScene is not ready");
            donor = default;
            return false;
        }

        if (scene == scannedSceneWithoutDonor)
        {
            LogNativeDonorPending("no registered Sign prefab has a ready text widget");
            donor = default;
            return false;
        }

        if (TryRememberPrefabDonor(scene.m_prefabs, out donor) ||
            TryRememberPrefabDonor(scene.m_nonNetViewPrefabs, out donor))
        {
            return true;
        }

        scannedSceneWithoutDonor = scene;
        LogNativeDonorPending("no registered Sign prefab has a ready text widget");
        donor = default;
        return false;
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

    private static bool TryRememberPrefabDonor(
        List<GameObject> prefabs,
        out NativeTextDonor donor)
    {
        foreach (GameObject prefab in prefabs)
        {
            Sign? sign = prefab != null ? prefab.GetComponent<Sign>() : null;
            if (sign == null || !IsUsableDonor(sign.m_textWidget))
            {
                continue;
            }

            TextMeshProUGUI widget = sign.m_textWidget!;
            RememberNativeTextDonor(
                widget,
                $"registered Sign prefab '{prefab!.name}'");
            donor = nativeTextDonor!.Value;
            return true;
        }

        donor = default;
        return false;
    }

    private static void RememberNativeTextDonor(TextMeshProUGUI donor, string source)
    {
        if (!IsUsableDonor(donor))
        {
            return;
        }

        nativeTextDonor = new NativeTextDonor(donor.font!, donor.fontSharedMaterial!);
        scannedSceneWithoutDonor = null;
        if (nativeDonorResolutionLogged)
        {
            return;
        }

        nativeDonorResolutionLogged = true;
        Plugin.Log.LogInfo($"Portal label text donor resolved from {source}.");
    }

    private static bool IsUsableDonor(TextMeshProUGUI? donor)
    {
        return donor != null &&
            donor.font != null &&
            donor.fontSharedMaterial != null;
    }

    private static bool IsUsableDonor(NativeTextDonor? donor)
    {
        return donor.HasValue &&
            donor.Value.Font != null &&
            donor.Value.Material != null;
    }

    private static void LogNativeDonorPending(string reason)
    {
        if (nativeDonorWarningLogged)
        {
            return;
        }

        nativeDonorWarningLogged = true;
        Plugin.Log.LogWarning(
            $"Portal labels are waiting for Valheim's native Sign text donor: {reason}.");
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

internal readonly struct NativeTextDonor
{
    internal NativeTextDonor(TMP_FontAsset font, Material material)
    {
        Font = font;
        Material = material;
    }

    internal TMP_FontAsset Font { get; }
    internal Material Material { get; }
}
