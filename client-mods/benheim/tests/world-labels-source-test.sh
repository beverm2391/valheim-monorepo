#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
module="$root/src/WorldLabels"
feedback="$root/src/Infrastructure/WorldFeedback.cs"
plugin="$root/src/Plugin.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_version="$native_tree/Version.cs"
native_sign="$native_tree/Sign.cs"
native_portal="$native_tree/TeleportWorld.cs"

if ! grep -Fq 'CurrentVersion { get; } = new GameVersion(1, 0, 20);' "$native_version"; then
  printf 'Valheim source version mismatch: this native integration check is qualified for 1.0.20\n' >&2
  exit 1
fi

assert_native() {
  local source_file="$1"
  local expected="$2"
  if ! grep -Fq "$expected" "$source_file"; then
    printf 'Valheim 1.0.20 native contract changed: expected "%s" in %s\n' \
      "$expected" "${source_file##*/}" >&2
    exit 1
  fi
}

assert_native "$native_sign" 'public TextMeshProUGUI m_textWidget;'
assert_native "$native_sign" 'private void Awake()'
assert_native "$native_sign" 'UpdateText();'
assert_native "$native_sign" 'm_textWidget.text = m_currentText;'
assert_native "$native_portal" 'private void Awake()'
assert_native "$native_portal" 'InvokeRepeating("UpdatePortal", 0.5f, 0.5f);'
assert_native "$native_portal" 'public string GetText()'
assert_native "$native_portal" 'return GetTagInfo().text;'
assert_native "$native_portal" 'string text2 = zDO.GetString(ZDOVars.s_tag);'
assert_native "$native_portal" 'text = CensorShittyWords.FilterUGC(text2, UGCType.Text, userId, 0L),'

grep -Fq '[HarmonyPatch(typeof(Sign), "Awake")]' "$module/WorldLabelPatches.cs"
grep -Fq '[HarmonyPatch(typeof(TeleportWorld), "Awake")]' "$module/WorldLabelPatches.cs"
grep -Fq 'TryGetNativeWoodenSign(out Sign donor)' "$module/PortalLabelController.cs"
grep -Fq 'TryFindNativeWoodenSign(scene.m_prefabs, out sign)' "$module/WorldLabelRuntime.cs"
grep -Fq 'TryFindNativeWoodenSign(scene.m_nonNetViewPrefabs, out sign)' "$module/WorldLabelRuntime.cs"
grep -Fq 'sign.GetComponent<Piece>()?.m_name == "$piece_sign"' "$module/WorldLabelRuntime.cs"
if grep -Fq 'IsNativeWoodenSignName' "$module/WorldLabelRuntime.cs"; then
  printf 'portal sign donor must use the native Piece contract instead of the unstable prefab root name\n' >&2
  exit 1
fi
grep -Fq 'BoardClearanceMeters = 0.25f' "$module/PortalSignVisualFactory.cs"
grep -Fq 'CopyRenderHierarchy(donor.transform, root.transform)' "$module/PortalSignVisualFactory.cs"
grep -Fq 'target.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;' "$module/PortalSignVisualFactory.cs"
grep -Fq 'renderer.sharedMaterials = sourceRenderer.sharedMaterials;' "$module/PortalSignVisualFactory.cs"
grep -Fq 'portal.GetComponentsInChildren<MeshRenderer>(includeInactive: false)' "$module/PortalSignVisualFactory.cs"
grep -Fq 'renderer.bounds.max.y' "$module/PortalSignVisualFactory.cs"
grep -Fq 'typeof(CanvasRenderer)' "$module/PortalSignVisualFactory.cs"
grep -Fq 'canvas.renderMode = RenderMode.WorldSpace;' "$module/PortalSignVisualFactory.cs"
grep -Fq 'Quaternion.Euler(0f, 180f, 0f)' "$module/PortalSignVisualFactory.cs"
grep -Fq 'label.richText = false;' "$module/PortalSignVisualFactory.cs"
grep -Fq 'label.raycastTarget = false;' "$module/PortalSignVisualFactory.cs"
grep -Fq 'WorldLabelStyle.CreateSignLetterMaterial(label)' "$module/PortalSignVisualFactory.cs"
grep -Fq 'root.transform.SetParent(portal.transform, worldPositionStays: false);' "$module/PortalSignVisualFactory.cs"
grep -Fq 'hideFlags = HideFlags.DontSave' "$module/PortalSignVisualFactory.cs"
grep -Fq 'frontLabel.text = tag;' "$module/PortalLabelController.cs"
grep -Fq 'backLabel.text = tag;' "$module/PortalLabelController.cs"
grep -Fq 'DisposeVisual();' "$module/PortalLabelController.cs"
grep -Fq 'WorldLabelRuntime.Reset();' "$plugin"

grep -Fq 'internal static void ShowAbovePlayer(' "$feedback"
if rg -n 'TryCreatePersistentBonusText|PlacePersistentText' "$feedback"; then
  printf 'superseded persistent Bonus-text infrastructure remains\n' >&2
  exit 1
fi

if rg -n 'DamageText|Billboard|Physics\.|Linecast|Player\.m_localPlayer|Utils\.GetMainCamera|WorldToScreenPoint|PortalMaxDistance|ShouldShowPortalTag|Object\.Instantiate|AddComponent<Sign>|AddComponent<ZNetView>|AddComponent<Collider>' \
  "$module/PortalLabelController.cs" "$module/PortalSignVisualFactory.cs" "$module/WorldLabelRuntime.cs"; then
  printf 'portal sign boards must be fixed scene geometry without overlay, LOS, distance, persistence, or copied behavior\n' >&2
  exit 1
fi

if rg -n 'GetZDO\(\)\.Set|ClaimOwnership|InvokeRPC|ZDOMan|ConfigEntry|KeyCode|Input\.|AssetBundle|\bLight\b' "$module"; then
  printf 'World Labels must remain unsaved, client-visual, and native-asset-backed\n' >&2
  exit 1
fi

grep -Fq 'new(widget.fontSharedMaterial)' "$module/WorldLabelStyle.cs"
grep -Fq 'material.EnableKeyword("GLOW_ON")' "$module/WorldLabelStyle.cs"
grep -Fq 'widget.fontSharedMaterial = glowMaterial;' "$module/SignGlowController.cs"

dotnet run --project "$root/tests/world-labels/WorldLabelTests.csproj" -c Release

printf 'World Label native-source compatibility and runtime-shaped sign-board checks passed\n'
