#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd "$root/../.." && pwd)"
release_branch="${BENHEIM_RELEASE_BRANCH:-main}"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
tag="benheim-v$version"

fail() {
  echo "$1" >&2
  exit 1
}

[[ -n "$version" ]] || fail "Could not determine the Benheim version."
command -v gh >/dev/null 2>&1 || fail "GitHub CLI is required to publish a release."

cd "$repo_root"
[[ -z "$(git status --porcelain)" ]] || fail "The worktree must be clean before a release."
[[ "$(git branch --show-current)" == "$release_branch" ]] ||
  fail "Releases must run from the $release_branch branch."

git fetch origin "$release_branch"
head="$(git rev-parse HEAD)"
remote_head="$(git rev-parse "origin/$release_branch")"
[[ "$head" == "$remote_head" ]] ||
  fail "Local $release_branch must exactly match origin/$release_branch."

if git show-ref --tags --verify --quiet "refs/tags/$tag" || gh release view "$tag" >/dev/null 2>&1; then
  fail "Release $tag already exists."
fi

for test_script in "$root"/tests/*-test.sh; do
  "$test_script"
done
dotnet run --project "$root/tests/quick-stack-summary/QuickStackSummaryTests.csproj"
dotnet run --project "$repo_root/tests/inventory-capabilities/InventoryCapabilityTests.csproj"

"$root/scripts/package-macos.sh"
"$root/scripts/package-windows.sh"

mac_package="$root/dist/Benheim-macOS-$version.zip"
windows_package="$root/dist/Benheim-Windows-$version.zip"
[[ -f "$mac_package" ]] || fail "Missing Mac package: $mac_package"
[[ -f "$windows_package" ]] || fail "Missing Windows package: $windows_package"

release_dir="$(mktemp -d)"
trap 'rm -rf "$release_dir"' EXIT
cp "$mac_package" "$release_dir/Benheim-macOS.zip"
cp "$windows_package" "$release_dir/Benheim-Windows.zip"

cat > "$release_dir/notes.md" <<EOF
Quit Valheim before installing or updating Benheim. Rerun the installer from
the new package to update an existing install.

- Mac: download \`Benheim-macOS.zip\`, unzip it, and open \`Install Benheim.command\`.
- Windows: download \`Benheim-Windows.zip\`, unzip it, and open \`Install Benheim.cmd\`.

The normal Steam Play button starts vanilla Valheim. Open \`Benheim\` to start
the modded game. Press F8 in game to confirm version $version and Put Away
compatibility.
EOF

gh release create "$tag" \
  "$release_dir/Benheim-macOS.zip" \
  "$release_dir/Benheim-Windows.zip" \
  --target "$head" \
  --title "Benheim v$version" \
  --notes-file "$release_dir/notes.md"

repo_slug="$(gh repo view --json nameWithOwner --jq .nameWithOwner)"
echo
echo "Published Benheim v$version:"
echo "  https://github.com/$repo_slug/releases/latest/download/Benheim-macOS.zip"
echo "  https://github.com/$repo_slug/releases/latest/download/Benheim-Windows.zip"
