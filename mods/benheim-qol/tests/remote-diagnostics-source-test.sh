#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
event="$root/src/Infrastructure/DiagnosticEvent.cs"
diagnostics="$root/src/Infrastructure/Diagnostics.cs"
remote="$root/src/Infrastructure/RemoteDiagnostics.cs"
settings="$root/src/Infrastructure/DiagnosticsSharingSettings.cs"
overlay="$root/src/Shortcuts/ShortcutOverlayConfig.cs"
plugin="$root/src/Plugin.cs"
public_packages="$root/scripts/package-all.sh"

grep -Fq 'new DiagnosticEventRoute(SelectEveryTypedEvent, RemoteDiagnostics.TryEnqueue)' "$diagnostics"
grep -Fq 'OptionalDestinations.Route(diagnosticEvent);' "$diagnostics"
test "$(grep -Fc 'RemoteDiagnostics.TryEnqueue' "$diagnostics")" -eq 1
grep -Fq 'internal string ToRemoteJsonLine(' "$event"
grep -Fq 'AppendJsonStringProperty(builder, "client_id", clientId);' "$event"
grep -Fq 'AppendJsonStringProperty(builder, "player_name", playerName);' "$event"
grep -Fq 'AppendJsonStringProperty(builder, "peer_id", peerId);' "$event"
grep -Fq 'AppendJsonStringProperty(builder, "session_id", session);' "$event"
grep -Fq 'AppendJsonStringProperty(builder, "mod_version", benheimVersion);' "$event"
grep -Fq 'AppendJsonStringProperty(builder, "build_id", buildId);' "$event"
! grep -Fq 'RemoteFieldAllowed' "$event"
! grep -Fq 'RemoteInventoryFieldAllowed' "$event"
test "$(grep -Fc 'AppendJsonString(builder, field.Name);' "$event")" -eq 2

grep -Fq 'MaximumQueuedEvents = 512' "$remote"
grep -Fq 'MaximumBatchEvents = 100' "$remote"
grep -Fq 'MaximumEventCharacters = 16384' "$remote"
grep -Fq 'RequestTimeout = TimeSpan.FromSeconds(5)' "$remote"
grep -Fq 'queue.Clear();' "$remote"
grep -Fq 'toCancel?.Cancel();' "$remote"
grep -Fq 'Task.Run(() => Pump(cancellation.Token))' "$remote"
grep -Fq 'PlayerName = playerName;' "$remote"
grep -Fq 'ZNet.GetUID().ToString(CultureInfo.InvariantCulture)' "$remote"
! grep -Eq 'LogOutput|Chat\.instance|GetHostName|GetPlayerID' "$remote"

grep -Fq '"Share Diagnostics"' "$settings"
grep -Fq '"Legacy Private-Test Sharing Default Migrated"' "$settings"
grep -Fq 'ApplyLegacyPrivateTestDefault(bool privateTestConfigured)' "$settings"
grep -Fq '&& !legacyPrivateDefaultMigrated.Value' "$settings"
grep -Fq 'Guid.NewGuid().ToString("N")' "$settings"
grep -Fq 'RemoteDiagnostics.SetSharingEnabled(enabled);' "$settings"
grep -Fq '"Share Diagnostics"' "$overlay"
grep -Fq 'RemoteDiagnostics.Begin(Paths.ConfigPath);' "$plugin"
grep -Fq 'RemoteDiagnostics.IsConfigured' "$plugin"
grep -Fq 'DiagnosticsSharingSettings.ApplyLegacyPrivateTestDefault(' "$plugin"
begin_line="$(grep -n 'RemoteDiagnostics.Begin(Paths.ConfigPath);' "$plugin" | cut -d: -f1)"
migration_line="$(grep -n 'DiagnosticsSharingSettings.ApplyLegacyPrivateTestDefault(' "$plugin" | cut -d: -f1)"
if (( begin_line >= migration_line )); then
  printf 'legacy sharing migration must use the validated remote configuration state\n' >&2
  exit 1
fi
grep -Fq 'RemoteDiagnostics.Update();' "$plugin"
grep -Fq 'RemoteDiagnostics.Reset();' "$plugin"

test "$(grep -Fc 'env -u BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG' "$public_packages")" -eq 2
! grep -Eq 'AXIOM|private-test' "$public_packages"

dotnet run --project "$root/tests/diagnostics-sharing/DiagnosticsSharingTests.csproj"

echo "private typed diagnostics boundary checks passed"
