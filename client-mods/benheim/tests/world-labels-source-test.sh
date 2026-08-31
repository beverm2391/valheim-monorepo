#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
module="$root/src/WorldLabels"
plugin="$root/src/Plugin.cs"
catalog="$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_version="$native_tree/Version.cs"
native_sign="$native_tree/Sign.cs"
native_portal="$native_tree/TeleportWorld.cs"
native_billboard="$native_tree/Billboard.cs"

grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$native_version"
grep -Fq 'public TextMeshProUGUI m_textWidget;' "$native_sign"
grep -Fq 'm_textWidget.text = m_currentText;' "$native_sign"
grep -Fq 'public string GetText()' "$native_portal"
grep -Fq 'return CensorShittyWords.FilterUGC(zDO.GetString(ZDOVars.s_tag)' "$native_portal"
grep -Fq 'InvokeRepeating("UpdatePortal", 0.5f, 0.5f);' "$native_portal"
grep -Fq 'public class Billboard : MonoBehaviour' "$native_billboard"
grep -Fq 'Camera mainCamera = Utils.GetMainCamera();' "$native_billboard"

grep -Fq '[HarmonyPatch(typeof(Sign), "Awake")]' "$module/WorldLabelPatches.cs"
grep -Fq '[HarmonyPatch(typeof(TeleportWorld), "Awake")]' "$module/WorldLabelPatches.cs"
grep -Fq 'scene.GetPrefab(NativeSignPrefab)' "$module/PortalLabelController.cs"
grep -Fq 'private const string NativeSignPrefab = "piece_sign";' "$module/PortalLabelController.cs"
grep -Fq 'label.font = donor.font;' "$module/PortalLabelController.cs"
grep -Fq 'label.fontSharedMaterial = donor.fontSharedMaterial;' "$module/PortalLabelController.cs"
grep -Fq 'typeof(Billboard)' "$module/PortalLabelController.cs"
grep -Fq 'label.richText = false;' "$module/PortalLabelController.cs"
grep -Fq 'label!.text = tag;' "$module/PortalLabelController.cs"
grep -Fq 'Physics.Linecast(' "$module/PortalLabelController.cs"
grep -Fq 'PortalMaxDistanceMeters = 30f' "$module/WorldLabelVisibility.cs"
grep -Fq 'PortalRefreshIntervalSeconds = 0.5f' "$module/WorldLabelVisibility.cs"

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
