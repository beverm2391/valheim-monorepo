#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
oven="$root/src/Production/StoneOven.cs"
patches="$root/src/Production/StoneOvenPatches.cs"

grep -Fq 'PrefabName = "piece_oven"' "$oven"
grep -Fq 'BakeTimeMultiplier = 0.5f' "$oven"
grep -Fq 'BurnThresholdMultiplier = 2f' "$oven"
grep -Fq 'conversion.m_cookTime *= BakeTimeMultiplier' "$oven"
grep -Fq 'bake_time_halved' "$oven"
grep -Fq 'native_owner_observed' "$oven"
grep -Fq 'AppliedTimings.TryGetValue(station, out _)' "$oven"
grep -Fq 'native_bake=' "$oven"
grep -Fq 'effective_bake=' "$oven"
grep -Fq 'native_done_to_burn=' "$oven"
grep -Fq 'effective_done_to_burn=' "$oven"
grep -Fq 'native_burn_threshold=' "$oven"
grep -Fq 'effective_burn_threshold=' "$oven"
grep -Fq 'burn_rule=cook_time_x' "$oven"
grep -Fq 'Utils.GetPrefabName(station.gameObject) == PrefabName' "$oven"
grep -Fq 'netView.GetZDO() == null' "$oven"

grep -Fq 'HarmonyPatch(typeof(CookingStation), "Awake")' "$patches"
grep -Fq 'HarmonyPatch(typeof(CookingStation), "UpdateCooking")' "$patches"

if rg -n 'm_secPerFuel|SetFuel|InvokeRPC|Set\(' "$oven" "$patches"; then
  printf 'Stone Oven must not alter fuel or write synchronized station state\n' >&2
  exit 1
fi

printf 'stone oven source checks passed\n'
