#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patches="$root/src/Adrenaline/AdrenalinePatches.cs"
feedback="$root/src/Adrenaline/AdrenalineFeedback.cs"

# Positive grants are doubled before Player.AddAdrenaline applies Valheim's rate,
# fill curve, status effects, cap, full-meter behavior, and decay-delay logic.
grep -Fq 'Prefix(Player __instance, ref float v' "$patches"
grep -Fq 'if (v > 0f)' "$patches"
grep -Fq 'v *= 2f;' "$patches"
grep -Fq 'positive_grant_doubled' "$patches"
if rg -n 'v \*= 2f|AddAdrenaline' "$patches" | grep -v -E 'v \*= 2f|nameof\(Player.AddAdrenaline\)'; then
  printf 'adrenaline grant multiplier must stay at the single native entry seam\n' >&2
  exit 1
fi

# Feedback observes the final value produced by Valheim's one native status-
# modifier pass, then caps the displayed amount to the meter's prior headroom.
grep -Fq '[HarmonyPatch(typeof(SEMan), nameof(SEMan.ModifyAdrenaline))]' "$patches"
grep -Fq 'AdrenalineFeedback.CaptureModifiedAmount(__instance, use)' "$patches"
grep -Fq 'award.NativeModifiedAmount.HasValue' "$feedback"
grep -Fq 'Mathf.Min(award.NativeModifiedAmount.Value, headroom)' "$feedback"
if rg -n 'm_adrenalineGainMultiplier|ModifyAdrenaline' "$feedback"; then
  printf 'feedback must not reimplement native adrenaline modifiers\n' >&2
  exit 1
fi

printf 'adrenaline native-gain and feedback checks passed\n'
