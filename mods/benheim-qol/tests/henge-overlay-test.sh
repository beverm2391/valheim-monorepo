#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
client="$root/src/EnemyTiers/BenheimTestCommandClient.cs"
protocol="$root/src/EnemyTiers/HengeOverlayProtocol.cs"
project="$root/tests/henge-overlay-protocol/HengeOverlayProtocolTests.csproj"
source_tree="$($root/scripts/ensure-valheim-source.sh)"

rg -Fq 'HengeOverlayProtocol.TryParse(args.Args' "$client"
rg -Fq 'serverRpc.Invoke(HengeOverlayProtocol.RequestRpc, operationId);' "$client"
rg -Fq 'Minimap.PinType.Icon3' "$client"
rg -Fq 'save: false' "$client"
rg -Fq 'isChecked: false' "$client"
rg -Fq 'ownerID: 0L' "$client"
rg -Fq 'ClearHengeOverlay();' "$client"
rg -Fq 'HengeOverlayPins.Clear();' "$client"
rg -Fq 'HengeOverlayProtocol.Usage' "$client"
rg -Fq '"StoneHenge1"' "$protocol"
rg -Fq '"StoneHenge3"' "$protocol"
rg -Fq '"StoneHenge4"' "$protocol"
rg -Fq '"StoneHenge5"' "$protocol"
rg -Fq 'arguments.Length != 3' "$protocol"
rg -Fq 'public PinData AddPin(Vector3 pos, PinType type, string name, bool save, bool isChecked, long ownerID = 0L' "$source_tree/Minimap.cs"

if rg -n 'devcommands|onlyAdmin: true|ZRoutedRpc|InvokeRoutedRPC|GetPrefab\(|Object\.Instantiate|SetLevel\(' "$client" "$protocol"; then
  printf 'henge overlay client must only request the fixed server-authoritative operation\n' >&2
  exit 1
fi

dotnet run --project "$project"
printf 'henge overlay source and protocol checks passed\n'
