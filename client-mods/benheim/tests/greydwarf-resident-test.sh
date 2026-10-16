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
python3 - "$native_chair" "$root/src/GreydwarfResident/GreydwarfResidentPatches.cs" \
    "$root/src/GreydwarfResident/ResidentClient.cs" "$root/src/Plugin.cs" <<'PY'
import pathlib
import re
import sys

native = pathlib.Path(sys.argv[1]).read_text()
patch = pathlib.Path(sys.argv[2]).read_text().split('class ResidentSeatInteractPatch', 1)[1]
client = pathlib.Path(sys.argv[3]).read_text()
plugin = pathlib.Path(sys.argv[4]).read_text()
# The fixture-backed tub test does not run the donor's Unity Update loop.
# Guard all cosmetic owners here so Ready-only action admission cannot remove
# or permanently block that same donor while another peer is authenticating.
feature = pathlib.Path(sys.argv[2]).parent
for name in ('GreydwarfResidentBehaviour.cs', 'GreydwarfResidentRuntime.cs', 'ResidentTubClient.cs'):
    source = (feature / name).read_text()
    assert 'ResidentClient.Available' not in source, f'{name} still uses action readiness for cosmetic lifetime'
    assert 'ResidentClient.CanRender' in source, f'{name} missing cosmetic readiness gate'
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

def method_body(source, signature):
    start = source.index(signature)
    opening = source.index('{', start)
    depth = 0
    for index in range(opening, len(source)):
        if source[index] == '{':
            depth += 1
        elif source[index] == '}':
            depth -= 1
            if depth == 0:
                return source[opening + 1:index]
    raise AssertionError(f'unclosed method body: {signature}')

update = method_body(client, 'internal static void Update()')
callback = method_body(client, 'private static void OnCapability(ZRpc rpc, int version, int state)')
request_notice = method_body(client, 'private static void RequestCapabilityUnavailableNotice()')
flush_notice = method_body(client, 'private static void FlushCapabilityNotice()')
reset = method_body(client, 'private static void ResetConnection()')
plugin_update = method_body(plugin, 'private void Update()')
assert 'now - connectedAt >= 5f' in update and 'RequestCapabilityUnavailableNotice();' in update, \
    'capability timeout no longer requests player feedback'
assert update.index('FlushCapabilityNotice();') < update.index('if (connection == null'), \
    'queued feedback must flush even when the RPC has since dropped'
assert re.search(
    r'capabilityNotice\.ObserveCapability\(\s*previous != readiness \|\| wasAvailable != Available,\s*'
    r'ResidentReadinessPolicy\.CanRender\(readiness\)\s*\)', callback
) and 'FlushCapabilityNotice();' in callback, \
    'capability RPC must feed actual readiness/availability into the tested notice state and flush it'
assert 'capabilityNotice.Request();' in request_notice and 'FlushCapabilityNotice();' in request_notice, \
    'timeout notice must use the deferred state and immediate display path'
assert 'bool canDisplay = player && player.IsOwner() && MessageHud.instance;' in flush_notice and \
    'capabilityNotice.TryTake(canDisplay)' in flush_notice and \
    'Message(CapabilityUnavailableMessage)' in flush_notice and \
    'ResidentDiagnostics.Emit("capability_notice_submitted", "support_unavailable")' in flush_notice, \
    'queued feedback must wait for Player and reach the existing message surface'
assert 'capabilityNotice.Reset();' in reset, 'new connection must reset response and notice state'
assert 'if (IsPatchGroupAvailable(typeof(GreydwarfResidentRuntime))) ResidentClient.Update();' in plugin_update, \
    'plugin frame loop must continue servicing deferred player feedback'
print('George capability feedback is wired to RPC, timeout, Player readiness, reset, and plugin Update')
PY
