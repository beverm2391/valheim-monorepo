using System;
using BenheimQoL.EnemyTiers;

Expect(
    HengeOverlayProtocol.TryParse(new[] { "bh", "henge", "on" }, out bool enabled) && enabled,
    "henge on command is accepted");
Expect(
    HengeOverlayProtocol.TryParse(new[] { "BH", "HENGE", "OFF" }, out enabled) && !enabled,
    "henge off command is case-insensitive");
Expect(!HengeOverlayProtocol.TryParse(new[] { "bh", "henge", "mark" }, out _), "retired henge mark command is rejected");
Expect(!HengeOverlayProtocol.TryParse(new[] { "bh", "henge", "on", "extra" }, out _), "extra henge arguments are rejected");
Expect(HengeOverlayProtocol.IsHengeLocation("StoneHenge1"), "StoneHenge1 is selected");
Expect(HengeOverlayProtocol.IsHengeLocation("StoneHenge3"), "StoneHenge3 is selected");
Expect(HengeOverlayProtocol.IsHengeLocation("StoneHenge4"), "StoneHenge4 is selected");
Expect(HengeOverlayProtocol.IsHengeLocation("StoneHenge5"), "StoneHenge5 is selected");
Expect(!HengeOverlayProtocol.IsHengeLocation("StoneHenge2"), "StoneHenge2 is excluded");
Expect(!HengeOverlayProtocol.IsHengeLocation("stonehenge1"), "location selection uses exact native names");

Console.WriteLine("henge overlay protocol checks passed");

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
