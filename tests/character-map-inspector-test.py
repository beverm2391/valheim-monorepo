#!/usr/bin/env python3
"""Behavioral tests for the read-only character map inspector."""

from __future__ import annotations

import gzip
import hashlib
import json
import os
import struct
import subprocess
import tempfile
import unittest
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
INSPECTOR = REPO_ROOT / "scripts" / "inspect-character-map.py"
TEXTURE_SIZE = 2048
PIXEL_COUNT = TEXTURE_SIZE * TEXTURE_SIZE
WORLD_UID = 4242


def boolean(value: bool) -> bytes:
    return struct.pack("<B", value)


def string(value: str) -> bytes:
    encoded = value.encode("utf-8")
    length = len(encoded)
    prefix = bytearray()
    while length >= 0x80:
        prefix.append((length & 0x7F) | 0x80)
        length >>= 7
    prefix.append(length)
    return bytes(prefix) + encoded


def vector3(x: float, y: float, z: float) -> bytes:
    return struct.pack("<fff", x, y, z)


def string_float_dictionary(entries: dict[str, float]) -> bytes:
    payload = bytearray(struct.pack("<i", len(entries)))
    for key, value in entries.items():
        payload.extend(string(key))
        payload.extend(struct.pack("<f", value))
    return bytes(payload)


def map_data(
    version: int = 8,
    first_pin_x: float = 1200.0,
    first_pin_name: str = "Personal route",
) -> bytes:
    personal = bytearray(PIXEL_COUNT)
    shared = bytearray(PIXEL_COUNT)
    center = TEXTURE_SIZE // 2
    personal[center * TEXTURE_SIZE + center + 100] = 1
    shared[center * TEXTURE_SIZE + center + 50] = 1

    inner = bytearray(struct.pack("<i", TEXTURE_SIZE))
    inner.extend(personal)
    inner.extend(shared)
    inner.extend(struct.pack("<i", 2))
    inner.extend(string(first_pin_name))
    inner.extend(vector3(first_pin_x, 0.0, 0.0))
    inner.extend(struct.pack("<i", 6))
    inner.extend(boolean(False))
    inner.extend(struct.pack("<q", 0))
    inner.extend(string(""))
    inner.extend(string("Shared route"))
    inner.extend(vector3(600.0, 0.0, 0.0))
    inner.extend(struct.pack("<i", 0))
    inner.extend(boolean(True))
    inner.extend(struct.pack("<q", 999))
    inner.extend(string("private-author"))
    inner.extend(boolean(True))

    compressed = gzip.compress(bytes(inner), mtime=0)
    return struct.pack("<ii", version, len(compressed)) + compressed


def character_file(
    character_version: int = 43,
    map_version: int = 8,
    custom_spawn_x: float = 300.0,
    first_pin_x: float = 1200.0,
    first_pin_name: str = "Personal route",
) -> bytes:
    payload = bytearray(struct.pack("<II", character_version, 105))
    payload.extend(bytes(105 * 4))
    payload.extend(boolean(False))
    payload.extend(struct.pack("<i", 1))
    payload.extend(struct.pack("<q", WORLD_UID))
    payload.extend(boolean(True))
    payload.extend(vector3(custom_spawn_x, 20.0, 400.0))
    payload.extend(boolean(True))
    payload.extend(vector3(30.0, 10.0, 40.0))
    payload.extend(boolean(True))
    payload.extend(vector3(600.0, 10.0, 800.0))
    payload.extend(vector3(300.0, 20.0, 400.0))
    payload.extend(boolean(True))
    encoded_map = map_data(
        map_version,
        first_pin_x=first_pin_x,
        first_pin_name=first_pin_name,
    )
    payload.extend(struct.pack("<i", len(encoded_map)))
    payload.extend(encoded_map)
    payload.extend(string("PrivatePlayer"))
    payload.extend(struct.pack("<q", 98765))
    payload.extend(string("private-seed"))
    payload.extend(boolean(False))
    payload.extend(struct.pack("<q", 0))
    payload.extend(string_float_dictionary({"first": 12.0}))
    for _ in range(5):
        payload.extend(string_float_dictionary({}))
    payload.extend(boolean(False))
    digest = hashlib.sha512(payload).digest()
    return struct.pack("<I", len(payload)) + payload + struct.pack("<I", len(digest)) + digest


def world_meta(uid: int = WORLD_UID, version: int = 37) -> bytes:
    payload = bytearray(struct.pack("<i", version))
    payload.extend(string("first"))
    payload.extend(string("private-world-seed"))
    payload.extend(struct.pack("<i", 123))
    payload.extend(struct.pack("<q", uid))
    payload.extend(struct.pack("<i", 2))
    payload.extend(boolean(True))
    payload.extend(struct.pack("<i", 0))
    return struct.pack("<i", len(payload)) + payload


class CharacterMapInspectorTest(unittest.TestCase):
    def setUp(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(prefix="character-map-inspector-test.")
        self.root = Path(self.temporary.name)
        self.character = self.root / "character.fch"
        self.metadata = self.root / "first.fwl"
        self.character.write_bytes(character_file())
        self.metadata.write_bytes(world_meta())

    def tearDown(self) -> None:
        self.temporary.cleanup()

    def run_inspector(self, *arguments: object) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [str(INSPECTOR), *(str(argument) for argument in arguments)],
            cwd=REPO_ROOT,
            text=True,
            capture_output=True,
            check=False,
        )

    def test_json_reports_distance_boundaries_without_private_payloads(self) -> None:
        before = hashlib.sha256(self.character.read_bytes()).hexdigest()
        os.chmod(self.character, 0o444)

        result = self.run_inspector(self.character, "--world-meta", self.metadata, "--json")

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(hashlib.sha256(self.character.read_bytes()).hexdigest(), before)
        report = json.loads(result.stdout)
        self.assertEqual(report["characterVersion"], 43)
        self.assertEqual(report["provenance"]["scope"], "local-files-only")
        self.assertFalse(report["provenance"]["dedicatedServerWorldMatched"])
        self.assertIn(
            "do not identify the active dedicated-server world",
            report["provenance"]["notice"],
        )
        self.assertEqual(report["worldMetadata"]["name"], "first")
        world = report["worlds"][0]
        self.assertEqual(world["label"], "first")
        self.assertEqual(world["home"]["radiusMeters"], 500.0)
        self.assertEqual(world["death"]["radiusMeters"], 1000.0)
        exploration = world["map"]["exploration"]
        self.assertEqual(exploration["personal"]["maxRadiusMeters"], 1200.0)
        self.assertEqual(exploration["personal"]["worldRadiusPercent"], 12.0)
        self.assertEqual(exploration["sharedOnly"]["maxRadiusMeters"], 600.0)
        self.assertEqual(exploration["visibleUnion"]["maxRadiusMeters"], 1200.0)
        self.assertEqual(world["map"]["pins"][0]["source"], "local-or-game")
        self.assertEqual(world["map"]["pins"][1]["source"], "shared-player")
        encoded_report = result.stdout
        for private_value in (
            "PrivatePlayer",
            "private-seed",
            "private-world-seed",
            "private-author",
            "98765",
            "999",
            str(self.character),
        ):
            self.assertNotIn(private_value, encoded_report)

    def test_text_summary_is_concise_and_names_the_calibration(self) -> None:
        result = self.run_inspector(self.character, "--world-meta", self.metadata)

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("Calibration: 12 m/pixel, 10000 m world radius", result.stdout)
        self.assertIn(
            "Provenance: Local character and world files identify only the inspected "
            "local world.",
            result.stdout,
        )
        self.assertIn(
            "do not identify the active dedicated-server world",
            result.stdout,
        )
        self.assertIn("Personal frontier: 1.200 km (12.00%)", result.stdout)
        self.assertIn("Saved pins: 2 (1 local/game, 1 shared-player)", result.stdout)
        self.assertIn("Personal route", result.stdout)
        self.assertNotIn(str(self.character), result.stdout)

    def test_hash_mismatch_fails_visibly(self) -> None:
        corrupted = bytearray(self.character.read_bytes())
        corrupted[20] ^= 1
        self.character.write_bytes(corrupted)

        result = self.run_inspector(self.character)

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("trailer hash does not match", result.stderr)

    def test_unsupported_character_map_and_world_versions_fail_visibly(self) -> None:
        cases = (
            (character_file(character_version=42), world_meta(), "character: unsupported version 42"),
            (character_file(map_version=7), world_meta(), "map: unsupported version 7"),
            (character_file(), world_meta(version=36), "world metadata: unsupported version 36"),
        )
        for character, metadata, expected in cases:
            with self.subTest(expected=expected):
                self.character.write_bytes(character)
                self.metadata.write_bytes(metadata)
                result = self.run_inspector(
                    self.character, "--world-meta", self.metadata
                )
                self.assertNotEqual(result.returncode, 0)
                self.assertIn(expected, result.stderr)

    def test_malformed_input_and_wrong_world_metadata_fail_visibly(self) -> None:
        self.character.write_bytes(b"not a character")
        result = self.run_inspector(self.character)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("file is too short", result.stderr)

        self.character.write_bytes(character_file())
        self.metadata.write_bytes(world_meta(uid=WORLD_UID + 1))
        result = self.run_inspector(self.character, "--world-meta", self.metadata)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("UID does not match exactly one", result.stderr)

    def test_missing_input_error_does_not_echo_the_private_path(self) -> None:
        missing = self.root / "private-character-name.fch"

        result = self.run_inspector(missing)

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("cannot stat input", result.stderr)
        self.assertNotIn(str(missing), result.stderr)

    def test_oversized_world_metadata_fails_before_reading_it(self) -> None:
        with self.metadata.open("wb") as metadata:
            metadata.truncate(2 * 1024 * 1024)

        result = self.run_inspector(
            self.character, "--world-meta", self.metadata
        )

        self.assertNotEqual(result.returncode, 0)
        self.assertIn("world metadata: file size", result.stderr)
        self.assertIn("exceeds the inspection limit", result.stderr)

    def test_non_finite_world_and_pin_coordinates_fail_visibly(self) -> None:
        cases = (
            (
                character_file(custom_spawn_x=float("nan")),
                "non-finite vector component",
            ),
            (character_file(first_pin_x=float("inf")), "non-finite vector component"),
        )
        for character, expected in cases:
            with self.subTest(expected=expected):
                self.character.write_bytes(character)
                result = self.run_inspector(self.character)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn(expected, result.stderr)
                self.assertNotIn("Traceback", result.stderr)

    def test_text_summary_escapes_saved_control_characters(self) -> None:
        self.character.write_bytes(
            character_file(
                first_pin_name=(
                    "Personal\nroute\x1b[31m\x7f\u0085\u202e\U000e0001"
                )
            )
        )

        result = self.run_inspector(self.character)

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn(
            (
                "Personal\\u000aroute\\u001b[31m\\u007f\\u0085\\u202e"
                "\\U000e0001"
            ),
            result.stdout,
        )
        pin_line = next(line for line in result.stdout.splitlines() if "Personal" in line)
        for control in ("\x1b", "\x7f", "\u0085", "\u202e", "\U000e0001"):
            self.assertNotIn(control, pin_line)


if __name__ == "__main__":
    unittest.main(verbosity=2)
