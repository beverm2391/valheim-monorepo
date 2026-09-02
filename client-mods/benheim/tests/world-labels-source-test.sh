#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
module="$root/src/WorldLabels"
feedback="$root/src/Infrastructure/WorldFeedback.cs"
plugin="$root/src/Plugin.cs"
catalog="$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_version="$native_tree/Version.cs"
native_sign="$native_tree/Sign.cs"
native_portal="$native_tree/TeleportWorld.cs"
native_damage_text="$native_tree/DamageText.cs"

grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$native_version"
grep -Fq 'public TextMeshProUGUI m_textWidget;' "$native_sign"
grep -Fq 'm_textWidget.text = m_currentText;' "$native_sign"
grep -Fq 'public string GetText()' "$native_portal"
grep -Fq 'return CensorShittyWords.FilterUGC(zDO.GetString(ZDOVars.s_tag)' "$native_portal"
grep -Fq 'InvokeRepeating("UpdatePortal", 0.5f, 0.5f);' "$native_portal"
grep -Fq 'public GameObject m_worldTextBase;' "$native_damage_text"
grep -Fq 'TextType.Bonus => new Color(1f, 0.63f, 0.24f, 1f)' "$native_damage_text"
grep -Fq 'worldTextInstance.m_textField.fontSize *= 1.5f;' "$native_damage_text"
grep -Fq 'worldText.m_worldPos.y += dt;' "$native_damage_text"
grep -Fq 'worldText.m_textField.color = color;' "$native_damage_text"
grep -Fq 'Object.Destroy(worldTextInstance.m_gui);' "$native_damage_text"

grep -Fq '[HarmonyPatch(typeof(Sign), "Awake")]' "$module/WorldLabelPatches.cs"
grep -Fq '[HarmonyPatch(typeof(TeleportWorld), "Awake")]' "$module/WorldLabelPatches.cs"
grep -Fq 'WorldFeedback.TryCreatePersistentBonusText(' "$module/PortalLabelController.cs"
grep -Fq 'WorldFeedback.PlacePersistentText(' "$module/PortalLabelController.cs"
grep -Fq 'private void LateUpdate()' "$module/PortalLabelController.cs"
grep -Fq 'Portal label created for native portal' "$module/WorldLabelRuntime.cs"
grep -Fq 'internal static bool TryCreatePersistentBonusText(' "$feedback"
grep -Fq 'object? instance = AddBonusText(' "$feedback"
grep -Fq 'UnityEngine.Random.State randomState = UnityEngine.Random.state;' "$feedback"
grep -Fq 'UnityEngine.Random.state = randomState;' "$feedback"
grep -Fq 'worldTexts.Remove(instance);' "$feedback"
grep -Fq 'createdText.richText = false;' "$feedback"
grep -Fq 'createdText.raycastTarget = false;' "$feedback"
grep -Fq 'camera.WorldToScreenPointScaled(worldPosition)' "$feedback"
grep -Fq 'label!.text = tag;' "$module/PortalLabelController.cs"
grep -Fq 'Physics.Linecast(' "$module/PortalLabelController.cs"
grep -Fq 'PortalMaxDistanceMeters = 30f' "$module/WorldLabelVisibility.cs"
grep -Fq 'PortalRefreshIntervalSeconds = 0.5f' "$module/WorldLabelVisibility.cs"

if rg -n 'TryGetNativeTextDonor|NativeTextDonor|typeof\(Canvas\)|typeof\(Billboard\)' \
  "$module/PortalLabelController.cs" "$module/WorldLabelRuntime.cs"; then
  printf 'portal labels must use the existing Bonus overlay instead of sign text or parallel world-space UI\n' >&2
  exit 1
fi

grep -Fq 'new(widget.fontSharedMaterial)' "$module/WorldLabelStyle.cs"
grep -Fq 'material.EnableKeyword("GLOW_ON")' "$module/WorldLabelStyle.cs"
grep -Fq '"_GlowColor"' "$module/WorldLabelStyle.cs"
grep -Fq 'widget.fontSharedMaterial = glowMaterial;' "$module/SignGlowController.cs"
grep -Fq 'WorldLabelRuntime.Reset();' "$plugin"
grep -Fq '"Glowing signs"' "$catalog"
grep -Fq '"Portal labels"' "$catalog"

if rg -n 'GetZDO\(\)\.Set|ClaimOwnership|InvokeRPC|ZDOMan|ConfigEntry|KeyCode|Input\.|Font\.CreateDynamicFontFromOSFont|AssetBundle|\bLight\b' "$module"; then
  printf 'World Labels must remain unsaved, client-visual, and native-asset-backed\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/world-labels/WorldLabelTests.csproj" -c Release

printf 'World Label native-source and behavior checks passed\n'
