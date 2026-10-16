#!/usr/bin/env python3
"""Compose the existing native-server maintenance helpers; never schedule updates."""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import subprocess
import tarfile
import tempfile
import uuid
from datetime import datetime, timezone


class Update:
    def __init__(self, world, root=Path('/'), helpers=Path(__file__).resolve().parent):
        self.world, self.root, self.helpers = world, root, helpers
        self.id = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ-') + uuid.uuid4().hex[:8]
        self.server = self.path('opt/valheim/server')
        self.worlds = self.path('var/lib/valheim/worlds_local')
        self.backups = self.path('var/backups/valheim')
        self.record = None
        self.work = None
        self.phase = 'preflight'

    def path(self, name):
        return self.root / name.lstrip('/')

    def emit(self, phase, **fields):
        self.phase = phase
        line = json.dumps(dict(component='valheim-native-update', operation_id=self.id,
                               utc=datetime.now(timezone.utc).isoformat(), phase=phase, **fields))
        try:
            print(line, flush=True)
        except BrokenPipeError:
            pass  # An SSH disconnect must not prevent recovery or its ledger.
        if self.record:
            with self.record.open('a') as stream:
                stream.write(line + '\n')

    def run(self, *args, env=None):
        # Own the CLI process group. Interruptions must finish SteamCMD before
        # recovery can replace its installation; the game is owned by systemd.
        process = subprocess.Popen([str(a) for a in args], stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, text=True, start_new_session=True,
                                   env=dict(os.environ, **(env or {})))
        try:
            output, _ = process.communicate()
        except BaseException:
            os.killpg(process.pid, signal.SIGTERM)
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait()
            raise
        if self.work:
            with (self.work / 'commands.log').open('a') as stream:
                stream.write(output)
        if process.returncode:
            raise RuntimeError('command_failed:' + Path(str(args[0])).name)
        return output.strip()

    def property(self, name):
        return self.run('systemctl', 'show', '--property=' + name, '--value', 'valheim.service')

    def active(self):
        return self.property('ActiveState') == 'active'

    @staticmethod
    def digest_stream(stream):
        digest = hashlib.sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
        return digest.hexdigest()

    @staticmethod
    def digest(path):
        with path.open('rb') as stream:
            return Update.digest_stream(stream)

    def snapshot(self, paths, base):
        return {str(p.relative_to(base)): [self.digest(p), p.stat().st_mode & 0o777,
                                          p.stat().st_uid, p.stat().st_gid]
                for p in sorted(paths) if p.is_file()}

    def preserved(self):
        paths = []
        for name in ['BepInEx/config', 'BepInEx/plugins', 'BepInEx/core', 'doorstop_libs']:
            paths.extend((self.server / name).rglob('*'))
        paths.extend(self.server / n for n in ['doorstop_config.ini', 'start_server_bepinex.sh',
                                              'start_game_bepinex.sh', '.doorstop_version'])
        paths.extend(self.path(n) for n in ['etc/valheim/server.env', 'etc/valheim/r2.env',
                     'etc/valheim/diagnostics.env', 'usr/local/bin/valheim-start',
                     'usr/local/bin/valheim-wait-ready', 'etc/systemd/system/valheim.service'])
        return self.snapshot(paths, self.root)

    def build(self):
        text = (self.server / 'steamapps/appmanifest_896660.acf').read_text()
        match = re.search(r'"buildid"\s+"(\d+)"', text)
        if not match:
            raise RuntimeError('missing_native_build')
        return match[1]

    def verify(self, invocation):
        self.run('bash', self.helpers / 'verify-benheim-server-plugins', invocation, self.world)
        logs = self.run('journalctl', '-u', 'valheim.service',
                        '_SYSTEMD_INVOCATION_ID=' + invocation, '--no-pager', '-o', 'cat')
        if re.search(r'\[(?:Error|Fatal)\s*:\s*(?:Harmony|BepInEx|Benheim)[^\]]*\]|'
                     r'HarmonyException|gameplay.disabled|patch_group_disabled', logs, re.I):
            raise RuntimeError('mod_startup_failure')
        version = re.search(r'Valheim version:\s*([\w.-]+) \(network version (\d+)\)', logs, re.I)
        if not version:
            raise RuntimeError('native_version_unconfirmed')
        sockets = self.run('ss', '-H', '-lunp')
        pid = self.property('MainPID')
        for port in [self.port, self.port + 1]:
            if not any(re.search(r':' + str(port) + r'\s', line) and 'pid=' + pid + ',' in line
                       for line in sockets.splitlines()):
                raise RuntimeError('udp_listener_missing')
        if not self.active() or self.property('InvocationID') != invocation:
            raise RuntimeError('invocation_changed')
        return dict(invocation_id=invocation, game_version=version[1], network_version=int(version[2]))

    def preflight(self):
        if not self.world or self.world in ['.', '..'] or '/' in self.world or '\\' in self.world:
            raise RuntimeError('invalid_world_name')
        for name in ['valheim-backup', 'valheim-r2-upload', 'valheim-update', 'valheim-start']:
            if not os.access(self.path('usr/local/bin/' + name), os.X_OK):
                raise RuntimeError('missing_operator_helper:' + name)
        if not self.path('etc/valheim/r2.env').is_file():
            raise RuntimeError('offbox_backup_unconfigured')
        # Query only non-secret runtime settings. Do not emit/source credentials
        # into the caller's environment or reconstruct the generated config.
        settings = self.run('bash', '-c', 'source "$1"; printf "%s\n%s\n%s\n" '
                            '"$VALHEIM_WORLD_NAME" "$VALHEIM_MODDED" "$VALHEIM_PORT"',
                            'settings', self.path('etc/valheim/server.env')).splitlines()
        if len(settings) != 3 or settings[:2] != [self.world, '1']:
            raise RuntimeError('world_or_modded_config_mismatch')
        self.port = int(settings[2])
        native = self.worlds / self.world
        chunked = (native.is_dir() and any(native.glob('*.chunk')) and
                   any(p.stat().st_size for p in native.glob('_main.*.fwl2')))
        legacy = all((self.worlds / (self.world + suffix)).is_file() for suffix in ['.db', '.fwl'])
        if not (chunked or legacy):
            raise RuntimeError('world_storage_missing')
        size = sum(p.stat().st_size for parent in [self.server, self.worlds]
                   for p in parent.rglob('*') if p.is_file())
        if shutil.disk_usage(self.backups).free < size * 2:
            raise RuntimeError('recovery_disk_space_insufficient')
        result = dict(build_id=self.build(), service_active=self.active())
        if result['service_active']:
            result.update(self.verify(self.property('InvocationID')))
        return result

    def stop(self, graceful=True):
        try:
            self.run('systemctl', 'stop', 'valheim.service')
        except RuntimeError:
            if graceful:
                raise
        # Updating requires a confirmed graceful save. Recovery only requires
        # quiescence: a timed-out stop can still have terminated the process,
        # and the stopped-world archive protects against a failed new startup.
        if self.active() or self.property('MainPID') != '0' or (
                graceful and self.property('Result') != 'success'):
            raise RuntimeError('graceful_stop_unconfirmed')

    def start(self):
        self.run('systemctl', 'start', 'valheim.service')
        return self.verify(self.property('InvocationID'))

    def world_backup(self):
        before = self.snapshot(self.worlds.rglob('*'), self.worlds)
        archive = Path(self.run(self.path('usr/local/bin/valheim-backup')).splitlines()[-1])
        if archive.parent != self.backups or not archive.name.startswith('worlds-'):
            raise RuntimeError('unexpected_backup_path')
        archive.chmod(0o600)
        self.run('bash', self.helpers / 'inspect-world-archive.sh', '--validate-only', archive)
        copied = {}
        # Compare every stopped-world file, including native .chunk/.fwl2 and
        # other saved worlds/backups. A readable tar alone is insufficient.
        with tarfile.open(archive, 'r|gz') as stream:
            for member in stream:
                if member.isfile():
                    name = member.name.removeprefix('./')
                    copied[name] = [self.digest_stream(stream.extractfile(member)),
                                    member.mode & 0o777, member.uid, member.gid]
        if copied != before:
            raise RuntimeError('world_backup_contents_mismatch')
        self.run(self.path('usr/local/bin/valheim-r2-upload'), archive)
        self.emit('world_backup_verified', archive=str(archive), sha256=self.digest(archive), files=len(copied))
        return archive

    def restore_installation(self, archive):
        stage = Path(tempfile.mkdtemp(prefix='.native-update-restore-', dir=self.server.parent))
        self.run('tar', '-xzf', archive, '-C', stage, 'opt/valheim/server')
        # Keep the failed installation for diagnosis; do not overlay stale
        # files from a newer build into the restored native installation. A
        # failed rename leaves the extracted recovery copy available too.
        self.server.rename(self.server.with_name('server.failed-' + self.id))
        (stage / 'opt/valheim/server').rename(self.server)
        shutil.rmtree(stage)

    def execute(self):
        old = self.preflight()
        self.work = self.backups / ('native-update-' + self.id)
        self.work.mkdir(mode=0o700)
        self.record = self.work / 'events.jsonl'
        self.emit('preflight_passed', **old)
        stop_attempted = updating = started = False
        installation = world_archive = None
        try:
            # Arm recovery before systemd can stop the process. A timeout or
            # signal after that side effect must not leave the old game down.
            stop_attempted = True
            self.stop()
            self.emit('stopped')
            preserved = self.preserved()
            (self.work / 'preserved.json').write_text(json.dumps(preserved))
            # External generated runtime files are private rollback inputs, not
            # a credential source. SteamCMD should never modify them.
            for name in preserved:
                if not name.startswith('opt/valheim/server/'):
                    target = self.work / 'runtime' / name
                    target.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copy2(self.path(name), target)
            self.emit('world_backup_started')
            world_archive = self.world_backup()
            self.emit('installation_backup_started')
            output = self.run('bash', self.helpers / 'archive-valheim-installation',
                              env={'VALHEIM_ARCHIVE_ROOT': str(self.root).rstrip('/')})
            installation = Path(re.search(r'^Server installation archive: (.+)$', output, re.M)[1])
            digest = re.search(r'^SHA-256: ([0-9a-f]{64})$', output, re.M)[1]
            if installation.parent != self.backups or self.digest(installation) != digest:
                raise RuntimeError('installation_backup_mismatch')
            self.emit('installation_backup_verified', archive=str(installation), sha256=digest)
            updating = True
            self.emit('updating')
            self.run(self.path('usr/local/bin/valheim-update'))
            if self.preserved() != preserved:
                raise RuntimeError('private_or_mod_state_changed')
            self.emit('preservation_verified', files=len(preserved), build_id=self.build())
            started = True
            self.emit('starting')
            ready = self.start()
            self.emit('ready', build_id=self.build(), **ready)
            return 0
        except BaseException as error:
            self.emit('failed', failed_phase=self.phase, reason=str(error) if isinstance(error, RuntimeError)
                      else type(error).__name__)
            if stop_attempted:
                try:
                    if not updating and self.active() and self.property('InvocationID') == old.get('invocation_id'):
                        self.emit('recovery_ready', build_id=self.build(), **self.verify(old['invocation_id']))
                        return 1
                    self.stop(graceful=False)
                    if updating:
                        self.restore_installation(installation)
                        for path in (self.work / 'runtime').rglob('*'):
                            if path.is_file():
                                shutil.copy2(path, self.path(str(path.relative_to(self.work / 'runtime'))))
                                if os.geteuid() == 0:
                                    name = str(path.relative_to(self.work / 'runtime'))
                                    os.chown(self.path(name), preserved[name][2], preserved[name][3])
                        if started:
                            self.run('bash', self.helpers / 'restore-world-archive.sh', '--local',
                                     world_archive, self.worlds, '--expected-sha256', self.digest(world_archive))
                        if self.preserved() != preserved:
                            raise RuntimeError('recovered_state_mismatch')
                    if old['service_active']:
                        self.emit('recovery_ready', build_id=self.build(), **self.start())
                    else:
                        self.emit('recovery_stopped', build_id=self.build())
                except BaseException as recovery_error:
                    try:
                        self.run('systemctl', 'stop', 'valheim.service')
                    except BaseException:
                        pass
                    self.emit('recovery_failed', reason=type(recovery_error).__name__)
            return 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--world', required=True)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--check', action='store_true', help='read-only native/mod/backup preflight')
    mode.add_argument('--restart-approved', action='store_true', help='operator has approved this downtime')
    args = parser.parse_args()
    os.umask(0o077)
    update = Update(args.world)
    try:
        if args.check:
            update.emit('check_passed', **update.preflight())
            return 0
        lock_path = update.path('var/lock/valheim-native-update.lock')
        with lock_path.open('a') as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            def interrupted(*_):
                raise InterruptedError()
            for signum in [signal.SIGTERM, signal.SIGHUP]:
                signal.signal(signum, interrupted)
            return update.execute()
    except Exception as error:
        update.emit('failed', reason=str(error) if isinstance(error, RuntimeError) else type(error).__name__)
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
