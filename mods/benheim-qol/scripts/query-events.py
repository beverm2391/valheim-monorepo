#!/usr/bin/env python3
"""Stream and filter Benheim newline-delimited JSON diagnostic events."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Iterable, Iterator


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Stream Benheim .ndjson event files without loading a session into memory."
    )
    parser.add_argument("paths", nargs="+", type=Path, help="event file or archive directory")
    parser.add_argument("--session")
    parser.add_argument("--domain")
    parser.add_argument("--event")
    parser.add_argument("--item")
    parser.add_argument("--station")
    parser.add_argument("--operation-id")
    parser.add_argument(
        "--incomplete",
        action="store_true",
        help="print matching start records that have no terminal record",
    )
    return parser.parse_args()


def event_files(paths: Iterable[Path]) -> Iterator[Path]:
    for path in paths:
        if path.is_dir():
            yield from sorted(path.glob("*.ndjson"))
        elif path.is_file():
            yield path
        else:
            raise FileNotFoundError(path)


def matches(record: dict[str, object], args: argparse.Namespace) -> bool:
    filters = (
        ("session", args.session),
        ("domain", args.domain),
        ("event", args.event),
        ("item", args.item),
        ("station", args.station),
        ("operation_id", args.operation_id),
    )
    return all(expected is None or str(record.get(field)) == expected for field, expected in filters)


def records(paths: Iterable[Path]) -> Iterator[tuple[dict[str, object], str]]:
    for path in event_files(paths):
        with path.open(encoding="utf-8") as stream:
            for line_number, line in enumerate(stream, 1):
                raw = line.rstrip("\n")
                if not raw:
                    continue
                try:
                    record = json.loads(raw)
                except json.JSONDecodeError as error:
                    raise ValueError(f"{path}:{line_number}: invalid JSON: {error.msg}") from error
                if not isinstance(record, dict):
                    raise ValueError(f"{path}:{line_number}: event must be a JSON object")
                yield record, raw


def run(args: argparse.Namespace) -> int:
    if not args.incomplete:
        for record, raw in records(args.paths):
            if matches(record, args):
                print(raw)
        return 0

    # Memory is proportional to matching open operations, not total records.
    open_operations: dict[str, str] = {}
    for record, raw in records(args.paths):
        operation_id = record.get("operation_id")
        if not isinstance(operation_id, str) or not operation_id:
            continue
        phase = record.get("operation_phase")
        if phase == "start" and matches(record, args):
            open_operations[operation_id] = raw
        elif phase == "terminal":
            open_operations.pop(operation_id, None)

    for operation_id in sorted(open_operations):
        print(open_operations[operation_id])
    return 0


def main() -> int:
    try:
        return run(parse_args())
    except (OSError, ValueError) as error:
        print(f"query-events: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
