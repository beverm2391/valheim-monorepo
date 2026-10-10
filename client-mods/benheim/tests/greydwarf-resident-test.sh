#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$root/tests/greydwarf-resident/GreydwarfResidentTests.csproj"

# Harmony binds ordinary patch arguments by native parameter name. The C#
# compiler cannot detect a bad binding, so compare this interaction prefix
# with the exact installed game's signature before producing another package.
native_chair="$(mktemp)"
trap 'rm -f "$native_chair"' EXIT
"$root/scripts/decompile-valheim.sh" Chair > "$native_chair"
python3 - "$native_chair" "$root/src/GreydwarfResident/GreydwarfResidentPatches.cs" <<'PY'
import pathlib
import re
import sys

native = pathlib.Path(sys.argv[1]).read_text()
patch = pathlib.Path(sys.argv[2]).read_text().split('class ResidentSeatInteractPatch', 1)[1]
native_signature = re.search(r'public bool Interact\(([^)]*)\)', native)
patch_signature = re.search(r'private static bool Prefix\(([^)]*)\)', patch)
assert native_signature and patch_signature, 'Chair interaction signatures unavailable'
native_parameters = {
    parameter.strip().split()[-1]: parameter.strip().split()[0]
    for parameter in native_signature.group(1).split(',')
}
for parameter in patch_signature.group(1).split(','):
    parts = parameter.strip().split()
    name = parts[-1]
    if name.startswith('__'):
        continue
    assert native_parameters.get(name) == parts[0], f'Harmony Chair.Interact binding is invalid: {name}'
print('George Chair.Interact parameter bindings match the installed native signature')
PY
