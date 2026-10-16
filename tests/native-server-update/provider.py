#!/usr/bin/env python3
"""Fixture systemd/SteamCMD/uploader boundary; files and backup helpers are real."""
import json
import os
from pathlib import Path
import re
import sys
import tarfile
import subprocess

root = Path(os.environ['UPDATE_TEST_ROOT'])
state_file = root / 'state.json'
state = json.loads(state_file.read_text())
name, args = Path(sys.argv[0]).name, sys.argv[1:]
server = root / 'opt/valheim/server'
manifest = server / 'steamapps/appmanifest_896660.acf'
new = '"456"' in manifest.read_text()
mode = state['mode']
state['calls'].append([name, *args])

def save():
    state_file.write_text(json.dumps(state))

if name == 'systemctl':
    if args[0] == 'show':
        prop = next(x.split('=', 1)[1] for x in args if x.startswith('--property='))
        print({'ActiveState': 'active' if state['active'] else 'inactive',
               'MainPID': state['pid'], 'InvocationID': state['invocation'],
               'Result': state.get('result', 'success')}[prop])
    elif args[0] == 'is-active':
        save()
        raise SystemExit(0 if state['active'] else 3)
    elif args[0] == 'stop':
        if mode == 'stop-failure':
            save()
            raise SystemExit(8)
        state.update(active=False, pid='0')
        if mode in ['stop-timeout', 'stop-timeout-result']:
            state['result'] = 'timeout'
            if mode == 'stop-timeout':
                save()
                raise SystemExit(8)
    elif args[0] == 'start':
        state['starts'] += 1
        state.update(active=True, pid=str(200 + state['starts']), invocation='start-' + str(state['starts']), result='success')
        if new:
            # A new native version can write/migrate a world before the final
            # startup gate fails. Recovery must restore the stopped snapshot.
            (root / 'var/lib/valheim/worlds_local/FriendWorld/22_1e__1_10.chunk').write_bytes(b'migrated')
    else:
        raise AssertionError(args)
elif name == 'journalctl':
    if new and mode == 'delayed-startup':
        state['startup_reads'] = state.get('startup_reads', 0) + 1
        if state['startup_reads'] < 3:
            print('Valheim version: l-1.0.20 (network version 41)')
            save()
            raise SystemExit(0)
    print('ZNet.LoadWorld: FriendWorld (FriendWorld)\nGame server connected')
    print('Benheim Eternal Fire 0.1.1 loaded after PatchAll.')
    print('Benheim Test Commands 0.1.4 loaded with direct peer RPC authorization.')
    print('Benheim Server Support 0.1.9 loaded with Put Away, confirmed-kill, and George resident coordination.')
    print('Valheim version: l-1.0.20 (network version 41)' if new else
          'Valheim version: l-1.0.12 (network version 40)')
    if new and mode in ['harmony-failure', 'recovery-failure'] or mode == 'recovery-failure' and state['starts'] > 1:
        print('[Error : HarmonyX] fixture patch failure')
    if new and mode == 'invocation-change':
        state['invocation'] = 'replacement'
elif name == 'ss':
    for port in [2456, 2457]:
        if new and mode == 'udp-missing' and port == 2456:
            continue
        print('UNCONN 0 0 0.0.0.0:' + str(port) + ' 0.0.0.0:* users(("game",pid=' + state['pid'] + ',fd=3))')
elif name == 'sudo':
    assert args == ['-u', 'valheim', '/usr/games/steamcmd', '+force_install_dir', '/opt/valheim/server',
                    '+login', 'anonymous', '+app_update', '896660', 'validate', '+quit'], args
    assert not state['active'], 'SteamCMD must never run before the server stops'
    (server / 'valheim_server.x86_64').write_bytes(b'new binary')
    manifest.write_text('"buildid" "456"\n')
    if mode == 'steamcmd-failure':
        (server / 'partial-download').write_bytes(b'partial update')
        save()
        raise SystemExit(9)
    if mode == 'config-changed':
        (server / 'BepInEx/config/private.cfg').write_text('changed')
elif name == 'valheim-backup':
    assert not state['active']
    helper = Path(__file__).resolve().parents[2] / 'server/backup-valheim-worlds'
    result = subprocess.run(['bash', str(helper)], check=True, capture_output=True, text=True,
                            env=dict(os.environ, VALHEIM_BACKUP_ROOT=str(root)))
    target = Path(result.stdout.strip())
    if mode == 'chunk-omitted':
        with tarfile.open(target, 'w:gz') as archive:
            archive.add(root / 'var/lib/valheim/worlds_local', arcname='.',
                        filter=lambda m: None if m.name.endswith('.chunk') else m)
    if mode == 'corrupt-archive':
        target.write_bytes(b'corrupt')
    print(target)
elif name == 'valheim-r2-upload':
    assert Path(args[0]).is_file()
    if mode == 'upload-failure':
        save()
        raise SystemExit(7)
else:
    raise AssertionError(name)
save()
