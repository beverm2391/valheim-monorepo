#!/usr/bin/env python3
from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "valheim_failure_forwarder", ROOT / "server" / "forward-valheim-failures.py"
)
assert SPEC is not None and SPEC.loader is not None
FORWARDER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = FORWARDER
SPEC.loader.exec_module(FORWARDER)


class ForwarderTests(unittest.TestCase):
    def setUp(self) -> None:
        self.config = FORWARDER.AxiomConfig(
            "https://example.axiom.co", "benheim", "test-token", "qa"
        )
        self.journal = {
            "MESSAGE": "ignored",
            "_SYSTEMD_INVOCATION_ID": "invocation-a",
            "__REALTIME_TIMESTAMP": "1788912000123456",
        }

    def test_ordinary_server_output_is_not_forwarded(self) -> None:
        self.assertIsNone(FORWARDER.classify_message("Game server connected"))
        self.assertIsNone(FORWARDER.classify_message("Benheim Eternal Fire 0.1.1 loaded after PatchAll."))
        self.assertIsNone(FORWARDER.classify_message("[Warning:Unity Log] saving world"))
        self.assertIsNone(FORWARDER.classify_message("[Info :Unity Log] player said error in chat"))

    def test_valheim_exception_is_structured_and_bounded(self) -> None:
        failure = FORWARDER.classify_message(
            "NullReferenceException: object missing\n  at ZNet.Update()"
        )
        self.assertIsNotNone(failure)
        assert failure is not None
        self.assertEqual("valheim", failure.source)
        self.assertEqual("exception", failure.kind)
        self.assertEqual("NullReferenceException", failure.exception_type)
        self.assertNotIn("\n", failure.message)

        record = FORWARDER.event_record(self.journal, failure, self.config, "12345")
        self.assertIsNotNone(record)
        assert record is not None
        self.assertEqual("invocation-a", record["session_id"])
        self.assertEqual("dedicated-server:qa", record["client_id"])
        self.assertEqual("12345", record["build_id"])
        self.assertEqual("valheim-failure", record["event"])
        self.assertEqual("NullReferenceException", record["fields"]["exception_type"])

    def test_bepinex_and_first_party_sources_are_distinct(self) -> None:
        bepinex = FORWARDER.classify_message(
            "[Error : BepInEx] Error loading plugin dependency"
        )
        first_party = FORWARDER.classify_message(
            "[Error : Benheim Server Support] Harmony patch failed: InvalidOperationException"
        )
        self.assertEqual("bepinex", bepinex.source)
        self.assertEqual("plugin_load", bepinex.kind)
        self.assertEqual("first-party-server-mod", first_party.source)
        self.assertEqual("benheim-server-support", first_party.plugin)
        self.assertEqual("harmony_patch", first_party.kind)

    def test_typed_failure_keeps_operation_correlation(self) -> None:
        failure = FORWARDER.classify_message(
            "[Info :Benheim Server Support] [diag][Inventory] "
            "put_away_lease_result_delivery_failed operation_id=op-7"
        )
        self.assertIsNotNone(failure)
        assert failure is not None
        record = FORWARDER.event_record(self.journal, failure, self.config, "12345")
        assert record is not None
        self.assertEqual("Inventory", record["fields"]["diagnostic_domain"])
        self.assertEqual("put_away_lease_result_delivery_failed", record["fields"]["diagnostic_event"])
        self.assertEqual("op-7", record["operation_id"])

    def test_secrets_are_removed_before_record_creation(self) -> None:
        failure = FORWARDER.classify_message(
            "ERROR token=abc123 password hunter2 Authorization: Bearer xyz -password worldpass"
        )
        self.assertIsNotNone(failure)
        assert failure is not None
        for secret in ("abc123", "hunter2", "xyz", "worldpass"):
            self.assertNotIn(secret, failure.message)

    def test_oversized_messages_are_truncated(self) -> None:
        failure = FORWARDER.classify_message("ERROR " + "x" * 5000)
        self.assertIsNotNone(failure)
        assert failure is not None
        self.assertLessEqual(len(failure.message), FORWARDER.MAXIMUM_MESSAGE_CHARACTERS)

    def test_missing_invocation_is_not_forwarded(self) -> None:
        failure = FORWARDER.classify_message("ERROR server failed")
        assert failure is not None
        self.assertIsNone(FORWARDER.event_record({}, failure, self.config, "12345"))

    def test_build_identity_comes_from_steam_manifest(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            manifest = Path(directory) / "appmanifest.acf"
            manifest.write_text('"AppState"\n{\n  "buildid"  "987654"\n}\n', encoding="utf-8")
            self.assertEqual("987654", FORWARDER.read_build_id(manifest))

    def test_component_build_identity_is_the_exact_binary_hash(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            component = Path(directory) / "component.dll"
            component.write_bytes(b"candidate-binary")
            self.assertEqual(
                "b75afc06019cb1f4b81851ad4d23fe7586c10564e26d06166ab124a9ff406233",
                FORWARDER.file_sha256(component),
            )

    def test_ingest_uses_existing_axiom_dataset_endpoint(self) -> None:
        response = mock.MagicMock()
        response.status = 200
        response.__enter__.return_value = response
        with mock.patch.object(FORWARDER.urllib.request, "urlopen", return_value=response) as open_url:
            FORWARDER.send_record(self.config, {"event": "valheim-failure"})
        request = open_url.call_args.args[0]
        self.assertEqual("https://example.axiom.co/v1/ingest/benheim", request.full_url)
        self.assertEqual("Bearer test-token", request.headers["Authorization"])
        self.assertEqual([{"event": "valheim-failure"}], json.loads(request.data))

    def test_rate_limiter_suppresses_bursts_and_short_duplicates(self) -> None:
        limiter = FORWARDER.EventLimiter()
        self.assertTrue(limiter.allows("same", 1.0))
        self.assertFalse(limiter.allows("same", 2.0))
        self.assertTrue(limiter.allows("same", 62.0))
        for index in range(FORWARDER.MAXIMUM_EVENTS_PER_MINUTE - 1):
            self.assertTrue(limiter.allows(f"unique-{index}", 62.0))
        self.assertFalse(limiter.allows("overflow", 62.0))

    def test_systemd_sidecar_cannot_control_the_game_service(self) -> None:
        unit = (ROOT / "systemd" / "valheim-diagnostics.service").read_text(encoding="utf-8")
        game_unit = (ROOT / "systemd" / "valheim.service").read_text(encoding="utf-8")
        self.assertIn("ConditionPathExists=/etc/valheim/diagnostics.env", unit)
        self.assertIn("DynamicUser=true", unit)
        self.assertIn("SupplementaryGroups=systemd-journal", unit)
        self.assertIn("ExecStart=/usr/local/bin/valheim-forward-failures", unit)
        self.assertIn("Before=valheim.service", unit)
        self.assertIn("Restart=always", unit)
        self.assertNotIn("Requires=valheim.service", unit)
        self.assertNotIn("PartOf=valheim.service", unit)
        self.assertNotIn("valheim-diagnostics", game_unit)


if __name__ == "__main__":
    unittest.main()
