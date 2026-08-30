using UnityEngine;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Keeps Valheim's networked adrenaline activation effect while correcting its
/// shield-generator audio layer. In 0.221.12 that layer becomes fully 2D at
/// distance, so every peer that receives the effect can hear it without normal
/// spatial attenuation. The companion sound layer is already fully 3D at 14 m.
/// </summary>
internal static class EarnedStateActivationAudio
{
    internal const string NativeEffectPrefab = "fx_Adrenaline1";
    internal const string ShieldGeneratorAudioLayer = "sfx_shieldgenerator_startup";
    internal const float MaximumAudibleDistance = 14f;

    internal static bool IsShieldGeneratorLayer(
        string effectPrefab,
        string audioLayer)
    {
        return effectPrefab == NativeEffectPrefab
            && audioLayer == ShieldGeneratorAudioLayer;
    }

    internal static void Spatialize(AudioSource audioSource)
    {
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = MaximumAudibleDistance;
    }
}
