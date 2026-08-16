#!/usr/bin/env python3

import json
import subprocess
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
CHECKER = ROOT / "scripts" / "check-put-away-visibility.py"


class PutAwayVisibilityCheckerTests(unittest.TestCase):
    def run_case(
        self,
        observer_contents: str | None,
        observer_peer: int = 22,
        observer_player: int = 202,
    ) -> subprocess.CompletedProcess[str]:
        depositor = [
            event("quick_stack_item", "2026-08-15T20:00:00.1000000Z", session="writer-session", operation_id="op", zdo_id="1:2", item="$item_stone", moved=6, resulting_count=29),
            event("quick_stack_write_snapshot", "2026-08-15T20:00:00.2000000Z", session="writer-session", operation_id="op", zdo_id="1:2", peer=11, player_id=101, owner=True, revision_before=4, revision_after=6, revision_advanced=True, moved=6, contents="$item_stone=29"),
        ]
        observer = [] if observer_contents is None else [
            event("container_open_snapshot", "2026-08-15T19:59:59.0000000Z", operation_id=None, zdo_id="1:2", peer=observer_peer, player_id=observer_player, owner=False, revision=6, contents=observer_contents),
        ]
        with tempfile.TemporaryDirectory() as directory:
            directory_path = Path(directory)
            depositor_path = directory_path / "depositor.ndjson"
            observer_path = directory_path / "observer.ndjson"
            depositor_path.write_text("".join(json.dumps(item) + "\n" for item in depositor), encoding="utf-8")
            observer_path.write_text("".join(json.dumps(item) + "\n" for item in observer), encoding="utf-8")
            return subprocess.run(
                [str(CHECKER), str(depositor_path), str(observer_path), "--operation-id", "op"],
                text=True,
                capture_output=True,
                check=False,
            )

    def test_matching_other_client_first_open_passes(self) -> None:
        result = self.run_case("$item_stone=29")
        self.assertEqual(0, result.returncode, result.stderr)

    def test_stale_first_open_fails(self) -> None:
        result = self.run_case("$item_stone=23")
        self.assertNotEqual(0, result.returncode)
        self.assertIn("stale contents", result.stderr)

    def test_missing_first_open_fails(self) -> None:
        result = self.run_case(None)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("no first-open snapshot", result.stderr)

    def test_same_peer_is_not_multiplayer_proof(self) -> None:
        result = self.run_case("$item_stone=29", observer_peer=11)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("another peer", result.stderr)

    def test_same_player_after_reconnect_is_not_multiplayer_proof(self) -> None:
        result = self.run_case("$item_stone=29", observer_peer=22, observer_player=101)
        self.assertNotEqual(0, result.returncode)
        self.assertIn("another player", result.stderr)


def event(name: str, timestamp: str, **fields: object) -> dict[str, object]:
    return {"timestamp": timestamp, "session": "observer-session", "domain": "Inventory", "event": name, **fields}


if __name__ == "__main__":
    unittest.main()
