"""Exercise real tar/validation/restore helpers with fixture systemd, SteamCMD
and R2 boundaries. These tests prove sequencing and recovery, not a live game
update or successful provider transfer; --check supplies live readiness proof.
"""
import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import tarfile
import unittest
from unittest.mock import patch

REPO = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('native_update', REPO / 'server/update-valheim-server.py')
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class UpdateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.umask = os.umask(0o077)
        self.helpers = self.root / 'helpers'
        self.bin = self.root / 'bin'
        self.server = self.root / 'opt/valheim/server'
        self.world = self.root / 'var/lib/valheim/worlds_local/FriendWorld'
        for path in [self.helpers, self.bin, self.server / 'steamapps', self.world,
                     self.root / 'var/backups/valheim', self.root / 'usr/local/bin',
                     self.root / 'etc/valheim', self.root / 'etc/systemd/system']:
            path.mkdir(parents=True, exist_ok=True)
        for source in ['server/archive-valheim-installation', 'server/verify-benheim-server-plugins',
                       'scripts/inspect-world-archive.sh', 'scripts/restore-world-archive.sh']:
            shutil.copy2(REPO / source, self.helpers / Path(source).name)
        provider = REPO / 'tests/native-server-update/provider.py'
        for command in ['systemctl', 'journalctl', 'ss', 'sudo', 'sleep']:
            if command == 'sleep':
                (self.bin / command).write_text('#!/usr/bin/env bash\nexit 0\n')
                (self.bin / command).chmod(0o700)
            else:
                (self.bin / command).symlink_to(provider)
        for command in ['valheim-backup', 'valheim-r2-upload']:
            (self.root / 'usr/local/bin' / command).symlink_to(provider)
        shutil.copy2(REPO / 'server/update-valheim-installation', self.root / 'usr/local/bin/valheim-update')
        for name, contents in {
            'opt/valheim/server/valheim_server.x86_64': 'old binary',
            'opt/valheim/server/steamapps/appmanifest_896660.acf': '"buildid" "123"\n',
            'opt/valheim/server/BepInEx/plugins/BenheimServerSupport.dll': 'old mod bytes',
            'opt/valheim/server/BepInEx/config/private.cfg': 'private profile sentinel',
            'opt/valheim/server/BepInEx/config/conversation-state.json': 'private history sentinel',
            'usr/local/bin/valheim-start': '#!/usr/bin/env bash\nexit 0\n',
            'usr/local/bin/valheim-wait-ready': 'waiter',
            'etc/valheim/r2.env': 'private storage sentinel',
            'etc/systemd/system/valheim.service': 'unit',
            'etc/valheim/server.env': 'VALHEIM_WORLD_NAME=FriendWorld\nVALHEIM_MODDED=1\nVALHEIM_PORT=2456\n',
        }.items():
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(contents)
        (self.root / 'usr/local/bin/valheim-start').chmod(0o700)
        (self.world / '_main.1761.fwl2').write_bytes(b'current native metadata')
        (self.world / '22_1e__1_10.chunk').write_bytes(b'original native chunk')
        self.state_file = self.root / 'state.json'
        self.set_mode('success')
        # Exercise Linux tar payloads on macOS without its synthetic AppleDouble
        # resource-fork entries, which the dedicated-server filesystem lacks.
        self.environment = patch.dict(os.environ, UPDATE_TEST_ROOT=str(self.root), COPYFILE_DISABLE='1',
                                      PATH=str(self.bin) + os.pathsep + os.environ['PATH'])
        self.environment.start()
        self.update = module.Update('FriendWorld', self.root, self.helpers)
        self.before_world = self.update.snapshot(self.update.worlds.rglob('*'), self.update.worlds)
        self.before_preserved = self.update.preserved()

    def tearDown(self):
        self.environment.stop()
        os.umask(self.umask)
        self.temp.cleanup()

    def set_mode(self, mode):
        self.state_file.write_text(json.dumps(dict(active=True, pid='101', invocation='old', starts=0, calls=[], mode=mode)))

    def state(self):
        return json.loads(self.state_file.read_text())

    def execute(self):
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            result = self.update.execute()
        self.events = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertNotIn('private profile sentinel', output.getvalue())
        self.assertNotIn('private history sentinel', output.getvalue())
        return result

    def assert_old_recovered(self):
        self.assertTrue(self.state()['active'])
        self.assertEqual(self.update.build(), '123')
        self.assertEqual(self.update.preserved(), self.before_preserved)
        self.assertEqual(self.update.snapshot(self.update.worlds.rglob('*'), self.update.worlds), self.before_world)
        self.assertEqual(self.events[-1]['phase'], 'recovery_ready')

    def test_readonly_preflight_and_explicit_downtime_flag(self):
        self.assertEqual(self.update.preflight()['network_version'], 40)
        self.assertFalse(any(c[:2] in [['systemctl', 'stop'], ['systemctl', 'start']] or c[0] == 'sudo'
                             for c in self.state()['calls']))
        with patch.object(module, 'Update') as factory, patch('sys.argv', ['update', '--world', 'FriendWorld']), \
                contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                module.main()
            factory.assert_not_called()

    def test_success_preserves_private_mods_and_orders_backups_before_steamcmd(self):
        self.assertEqual(self.execute(), 0)
        self.assertEqual(self.update.build(), '456')
        self.assertEqual(self.update.preserved(), self.before_preserved)
        names = [c[0] for c in self.state()['calls']]
        self.assertEqual(names.count('sudo'), 1)
        uploads = [i for i, name in enumerate(names) if name == 'valheim-r2-upload']
        self.assertEqual(len(uploads), 2)
        self.assertLess(max(uploads), names.index('sudo'))
        self.assertEqual(self.events[-1]['phase'], 'ready')
        self.assertEqual(self.events[-1]['network_version'], 41)
        self.assertEqual(self.events[-1]['invocation_id'], self.state()['invocation'])
        for path in self.update.work.rglob('*'):
            if path.is_file():
                self.assertEqual(path.stat().st_mode & 0o077, 0)

    def test_invalid_or_incomplete_backup_never_enters_steamcmd(self):
        for mode in ['corrupt-archive', 'chunk-omitted', 'upload-failure']:
            with self.subTest(mode=mode):
                self.set_mode(mode)
                self.update = module.Update('FriendWorld', self.root, self.helpers)
                self.assertEqual(self.execute(), 1)
                self.assertFalse(any(c[0] == 'sudo' for c in self.state()['calls']))
                self.assert_old_recovered()

    def test_start_waits_for_world_and_plugins_on_the_new_invocation(self):
        self.set_mode('delayed-startup')
        self.assertEqual(self.execute(), 0)
        self.assertGreaterEqual(self.state()['startup_reads'], 3)
        self.assertEqual(self.events[-1]['phase'], 'ready')

    def test_partial_steamcmd_update_restores_clean_old_installation(self):
        self.set_mode('steamcmd-failure')
        self.assertEqual(self.execute(), 1)
        self.assert_old_recovered()
        self.assertFalse((self.server / 'partial-download').exists())
        self.assertTrue((self.server.with_name('server.failed-' + self.update.id) / 'partial-download').exists())

    def test_mod_mutation_and_failed_startup_restore_installation_and_native_world(self):
        for mode in ['config-changed', 'harmony-failure', 'invocation-change', 'udp-missing']:
            with self.subTest(mode=mode):
                self.set_mode(mode)
                self.update = module.Update('FriendWorld', self.root, self.helpers)
                self.assertEqual(self.execute(), 1)
                self.assert_old_recovered()

    def test_failed_recovery_remains_stopped_and_retains_backups(self):
        self.set_mode('recovery-failure')
        self.assertEqual(self.execute(), 1)
        self.assertFalse(self.state()['active'])
        self.assertEqual(self.events[-1]['phase'], 'recovery_failed')
        self.assertTrue(list(self.update.backups.glob('worlds-*.tar.gz')))
        self.assertTrue(list(self.update.backups.glob('server-installation-*.tar.gz')))

    def test_failed_graceful_stop_never_enters_backup_or_update(self):
        self.set_mode('stop-failure')
        self.assertEqual(self.execute(), 1)
        self.assertFalse(any(c[0] in ['sudo', 'valheim-backup'] for c in self.state()['calls']))
        self.assertTrue(self.state()['active'])

    def test_stopped_game_is_restarted_after_failed_graceful_confirmation(self):
        for mode in ['stop-timeout', 'stop-timeout-result']:
            with self.subTest(mode=mode):
                self.set_mode(mode)
                self.update = module.Update('FriendWorld', self.root, self.helpers)
                self.assertEqual(self.execute(), 1)
                self.assertFalse(any(c[0] in ['sudo', 'valheim-backup'] for c in self.state()['calls']))
                self.assert_old_recovered()

    def test_interruption_after_stop_side_effect_still_recovers(self):
        original_stop = self.update.stop
        def interrupted_stop(graceful=True):
            original_stop(graceful)
            if graceful:
                raise InterruptedError()
        with patch.object(self.update, 'stop', interrupted_stop):
            self.assertEqual(self.execute(), 1)
        self.assert_old_recovered()
        self.assertFalse(any(c[0] == 'sudo' for c in self.state()['calls']))

    def test_concurrent_timer_and_operator_archives_are_distinct_and_complete(self):
        env = dict(os.environ, VALHEIM_BACKUP_ROOT=str(self.root))
        processes = [subprocess.Popen(['bash', str(REPO / 'server/backup-valheim-worlds')],
                                     env=env, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
                     for _ in range(2)]
        archives = []
        for process in processes:
            output, error = process.communicate(timeout=10)
            self.assertEqual(process.returncode, 0, error)
            archive = Path(output.strip())
            archives.append(archive)
            self.assertEqual(archive.stat().st_mode & 0o777, 0o600)
            with tarfile.open(archive) as stream:
                chunk = stream.extractfile('./FriendWorld/22_1e__1_10.chunk')
                self.assertEqual(chunk.read(), b'original native chunk')
        self.assertEqual(len(set(archives)), 2)
        self.assertFalse(list(self.update.backups.glob('.worlds-*')))

    def test_bootstrap_active_service_refusal_precedes_remote_mutation(self):
        config = self.root / 'operator.env'
        config.write_text('HETZNER_SERVER_NAME=fixture\nVALHEIM_SERVER_NAME=Fixture\n'
                          'VALHEIM_WORLD_NAME=FriendWorld\nSSH_HOST=fixture\n')
        ssh = self.bin / 'ssh'
        ssh.write_text('#!/usr/bin/env bash\nexec bash -c "${@: -1}"\n')
        ssh.chmod(0o700)
        scp = self.bin / 'scp'
        scp.write_text('#!/usr/bin/env bash\necho remote-mutation >&2\nexit 99\n')
        scp.chmod(0o700)
        result = subprocess.run(['bash', str(REPO / 'scripts/install-server.sh')],
                                env=dict(os.environ, VALHEIM_ENV_FILE=str(config),
                                         VALHEIM_PASSWORD='fixture-password'), capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Stop Valheim before bootstrap', result.stderr)
        self.assertNotIn('remote-mutation', result.stderr)
        self.assertTrue(self.state()['active'])

    def test_raw_steamcmd_entrypoint_refuses_running_game(self):
        result = subprocess.run([str(REPO / 'server/update-valheim-installation')], capture_output=True, text=True)
        self.assertNotEqual(result.returncode, 0)
        self.assertFalse(any(c[0] == 'sudo' for c in self.state()['calls']))


if __name__ == '__main__':
    unittest.main()
