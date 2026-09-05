using System;
using BenheimQoL.PlayerCombat;
using UnityEngine;

Expect(EarnedStateActivationAudio.IsShieldGeneratorLayer(
        "fx_Adrenaline1",
        "sfx_shieldgenerator_startup"),
    "the exact networked native donor layer is selected");
Expect(!EarnedStateActivationAudio.IsShieldGeneratorLayer(
        "fx_Adrenaline1",
        "sfx_ui_player_firedamage_ignite"),
    "the donor's already-spatial companion layer remains unchanged");
Expect(!EarnedStateActivationAudio.IsShieldGeneratorLayer(
        "fx_shieldgenerator_startup",
        "sfx_shieldgenerator_startup"),
    "the reused shield-generator sound remains unchanged outside the donor");

AudioSource source = new AudioSource
{
    spatialBlend = 0f,
    maxDistance = 80f
};
EarnedStateActivationAudio.Spatialize(source);

Expect(source.spatialBlend == 1f,
    "the donor layer stays fully spatial at every distance");
Expect(source.maxDistance == 14f,
    "the donor layer matches the native companion layer's audible range");
Expect(source.SpatialBlendCurve?.StartValue == 1f
        && source.SpatialBlendCurve.EndValue == 1f,
    "the donor layer cannot return to 2D through its distance curve");
Expect(source.rolloffMode == AudioRolloffMode.Linear,
    "the donor layer fades to silence at its maximum distance");

Console.WriteLine("earned-state activation audio checks passed");

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
