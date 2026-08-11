#!/usr/bin/env python3
"""Summarize the map embedded in a Valheim character without writing it."""

from __future__ import annotations

import argparse
import json
import math
import sys
import unicodedata
from pathlib import Path
from typing import Any, Iterable

from character_map_format import (
    DEFAULT_PIXEL_SIZE_METERS,
    DEFAULT_WORLD_SIZE_METERS,
    InspectionError,
    Reader,
    parse_character,
    read_collection_count,
)


SUPPORTED_WORLD_META_VERSION = 37
MAX_WORLD_META_BYTES = 1024 * 1024


def parse_world_meta(path: Path) -> dict[str, Any]:
    try:
        size = path.stat().st_size
    except OSError as error:
        raise InspectionError(
            f"world metadata: cannot stat input: {error.strerror or error.__class__.__name__}"
        ) from error
    if size > MAX_WORLD_META_BYTES:
        raise InspectionError(
            f"world metadata: file size {size} exceeds the inspection limit"
        )
    try:
        data = path.read_bytes()
    except OSError as error:
        raise InspectionError(
            f"world metadata: cannot read input: {error.strerror or error.__class__.__name__}"
        ) from error
    reader = Reader(data, "world metadata file")
    payload_length = reader.int32()
    if payload_length < 0:
        raise InspectionError(f"world metadata: negative payload length {payload_length}")
    payload = Reader(reader.take(payload_length), "world metadata payload")
    reader.require_end()
    version = payload.int32()
    if version != SUPPORTED_WORLD_META_VERSION:
        raise InspectionError(
            f"world metadata: unsupported version {version}; "
            f"supported version is {SUPPORTED_WORLD_META_VERSION}"
        )
    name = payload.string()
    payload.string()  # seed name
    payload.int32()  # seed
    uid = payload.int64()
    payload.int32()  # world generation version
    payload.boolean()  # requires DB
    starting_key_count = read_collection_count(payload, "starting global key")
    for _ in range(starting_key_count):
        payload.string()
    payload.require_end()
    return {"name": name, "uid": uid, "version": version}


def match_world_meta(
    report: dict[str, Any], world_uids: list[int], world_meta: dict[str, Any]
) -> None:
    matches = [index for index, uid in enumerate(world_uids) if uid == world_meta["uid"]]
    if len(matches) != 1:
        raise InspectionError(
            "world metadata: UID does not match exactly one character world entry"
        )
    world = report["worlds"][matches[0]]
    world["label"] = world_meta["name"]
    report["worldMetadata"] = {
        "matched": True,
        "name": world_meta["name"],
        "version": world_meta["version"],
    }


def format_distance(value: dict[str, Any]) -> str:
    if not value["present"]:
        return "not saved"
    return f'{value["radiusMeters"] / 1000:.3f} km ({value["worldRadiusPercent"]:.2f}%)'


def display_text(value: str) -> str:
    """Escape control characters before saved text reaches a terminal."""
    escaped = []
    for character in value:
        if unicodedata.category(character).startswith("C"):
            codepoint = ord(character)
            escaped.append(
                f"\\u{codepoint:04x}"
                if codepoint <= 0xFFFF
                else f"\\U{codepoint:08x}"
            )
        else:
            escaped.append(json.dumps(character, ensure_ascii=False)[1:-1])
    return "".join(escaped)


def print_summary(report: dict[str, Any]) -> None:
    calibration = report["calibration"]
    print(f'Character format: v{report["characterVersion"]}')
    print(f'World entries: {report["worldCount"]}')
    print(
        "Calibration: "
        f'{calibration["pixelSizeMeters"]:g} m/pixel, '
        f'{calibration["worldRadiusMeters"]:g} m world radius'
    )
    for world in report["worlds"]:
        print()
        print(f'World {world["index"]}: {display_text(world["label"])}')
        print(f'  Home: {format_distance(world["home"])}')
        print(f'  Logout: {format_distance(world["logout"])}')
        print(f'  Death: {format_distance(world["death"])}')
        if world["map"] is None:
            print("  Map: not saved")
            continue
        map_summary = world["map"]
        print(
            f'  Map: v{map_summary["version"]}, '
            f'{map_summary["textureSize"]}x{map_summary["textureSize"]}'
        )
        for label, key in (
            ("Personal frontier", "personal"),
            ("Shared frontier", "sharedAll"),
            ("Shared-only frontier", "sharedOnly"),
            ("Visible union frontier", "visibleUnion"),
        ):
            extent = map_summary["exploration"][key]
            print(
                f'  {label}: {extent["maxRadiusMeters"] / 1000:.3f} km '
                f'({extent["worldRadiusPercent"]:.2f}%), {extent["pixels"]} pixels'
            )
        local_count = sum(pin["source"] == "local-or-game" for pin in map_summary["pins"])
        shared_count = map_summary["savedPinCount"] - local_count
        print(
            f'  Saved pins: {map_summary["savedPinCount"]} '
            f'({local_count} local/game, {shared_count} shared-player)'
        )
        visited_pins = sorted(
            (pin for pin in map_summary["pins"] if pin["personallyExploredAtPin"]),
            key=lambda pin: pin["radiusMeters"],
            reverse=True,
        )[:10]
        if visited_pins:
            print("  Farthest pins inside personal exploration:")
            for pin in visited_pins:
                pin_name = display_text(pin["name"]) if pin["name"] else "(unnamed)"
                print(
                    f'    {pin["radiusMeters"] / 1000:.3f} km '
                    f'({pin["worldRadiusPercent"]:.2f}%)  '
                    f'{pin_name} [{pin["type"]}, {pin["source"]}]'
                )


def positive_float(value: str) -> float:
    parsed = float(value)
    if not math.isfinite(parsed) or parsed <= 0:
        raise argparse.ArgumentTypeError("must be a positive finite number")
    return parsed


def parse_args(arguments: Iterable[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Read a Valheim .fch map and print radial exploration facts. "
            "The command never writes the input file."
        )
    )
    parser.add_argument("character", type=Path, help="Explicit .fch character file to read.")
    parser.add_argument(
        "--world-meta",
        type=Path,
        help="Optional explicit .fwl file used only to match one world UID to its name.",
    )
    parser.add_argument(
        "--pixel-size-m",
        type=positive_float,
        default=DEFAULT_PIXEL_SIZE_METERS,
        help=f"Map pixel scale. Default: {DEFAULT_PIXEL_SIZE_METERS:g}.",
    )
    parser.add_argument(
        "--world-size-m",
        type=positive_float,
        default=DEFAULT_WORLD_SIZE_METERS,
        help=f"World radius used for percentages. Default: {DEFAULT_WORLD_SIZE_METERS:g}.",
    )
    parser.add_argument("--json", action="store_true", help="Write structured summary as JSON.")
    return parser.parse_args(arguments)


def run(arguments: Iterable[str]) -> int:
    args = parse_args(arguments)
    try:
        report, world_uids = parse_character(
            args.character,
            pixel_size_meters=args.pixel_size_m,
            world_size_meters=args.world_size_m,
        )
        if args.world_meta:
            match_world_meta(report, world_uids, parse_world_meta(args.world_meta))
    except InspectionError as error:
        print(f"inspect-character-map: {error}", file=sys.stderr)
        return 1
    if args.json:
        json.dump(report, sys.stdout, indent=2, sort_keys=True)
        print()
    else:
        print_summary(report)
    return 0


if __name__ == "__main__":
    raise SystemExit(run(sys.argv[1:]))
