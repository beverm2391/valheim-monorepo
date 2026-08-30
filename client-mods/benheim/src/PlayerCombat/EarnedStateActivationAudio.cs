using UnityEngine;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Keeps Valheim's networked adrenaline activation effect while correcting its
/// shield-generator audio layer. In 0.221.12 that layer becomes fully 2D at
/// distance, so every peer that receives the effect can hear it without normal
/// spatial attenuation. The corrected layer remains fully 3D and fades to
/// silence at the companion sound layer's 14 m maximum distance.
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
        audioSource.SetCustomCurve(
            AudioSourceCurveType.SpatialBlend,
            AnimationCurve.Linear(0f, 1f, 1f, 1f));
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }
}
