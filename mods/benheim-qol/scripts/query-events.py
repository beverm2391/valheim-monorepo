#!/usr/bin/env python3
"""Filter local Benheim NDJSON or query the private Axiom test dataset."""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path
from typing import Iterable, Iterator


QUERY_URL = "https://api.axiom.co/v1/datasets/_apl?format=tabular"
REMOTE_MAP_FIELD = "fields"
MAP_FILTER_FIELDS = frozenset(("item", "station"))
DATASET_PATTERN = re.compile(r"^[A-Za-z0-9_.-]{1,200}$")
DURATION_PATTERN = re.compile(r"^[1-9][0-9]*[mhd]$")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Filter local Benheim NDJSON or query private typed events from Axiom."
    )
    parser.add_argument("paths", nargs="*", type=Path, help="local event file or archive directory")
    parser.add_argument("--remote", action="store_true", help="query Axiom instead of local files")
    parser.add_argument("--dataset", help="Axiom dataset; defaults to BENHEIM_AXIOM_DATASET")
    parser.add_argument("--since", default="24h", help="remote lookback such as 30m, 12h, or 7d")
    parser.add_argument("--limit", type=int, default=100, help="remote result limit, 1-500")
    parser.add_argument("--session")
    parser.add_argument("--player")
    parser.add_argument("--client")
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


def field_value(record: dict[str, object], field: str) -> object | None:
    if field == "session":
        return record.get("session_id", record.get("session"))
    return record.get(field)


def filters(args: argparse.Namespace) -> tuple[tuple[str, str | None], ...]:
    return (
        ("session", args.session),
        ("player_name", args.player),
        ("client_id", args.client),
        ("domain", args.domain),
        ("event", args.event),
        ("item", args.item),
        ("station", args.station),
        ("operation_id", args.operation_id),
    )


def matches(record: dict[str, object], args: argparse.Namespace) -> bool:
    return all(
        expected is None or str(field_value(record, field)) == expected
        for field, expected in filters(args)
    )


def local_records(paths: Iterable[Path]) -> Iterator[tuple[dict[str, object], str]]:
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


def escape_apl(value: str) -> str:
    return value.replace("\\", "\\\\").replace('"', '\\"')


def remote_apl(dataset: str, args: argparse.Namespace) -> str:
    query = f"['{dataset}']"
    for field, expected in filters(args):
        if expected is None:
            continue
        # In incomplete mode these fields select the start record only. Keep
        # them local so the remote query still returns the terminal partner.
        if args.incomplete and field in ("event", "item", "station"):
            continue
        remote_field = "session_id" if field == "session" else field
        expected_apl = escape_apl(expected)
        if remote_field in MAP_FILTER_FIELDS:
            query += (
                " | where coalesce("
                f"tostring(ensure_field('{remote_field}', typeof(string))), "
                f"tostring(ensure_field('{REMOTE_MAP_FIELD}', typeof(dynamic))['{remote_field}'])"
                f') == "{expected_apl}"'
            )
        else:
            query += f' | where tostring([\'{remote_field}\']) == "{expected_apl}"'
    return f"{query} | order by _time desc | take {args.limit}"


def operation_transition(record: dict[str, object]) -> str | None:
    phase = record.get("operation_phase")
    if record.get("domain") != "InventoryTransaction":
        return phase if phase in ("start", "terminal") else None

    event = record.get("event")
    status = record.get("status")
    if event == "put_away_batch_started" and phase == "start" and status == "running":
        return "start"
    if (
        event == "put_away_batch_finished"
        and phase == "terminal"
        and status in ("completed", "cancelled")
    ):
        return "terminal"
    return None


def operation_key(record: dict[str, object], operation_id: str) -> tuple[str, str, str, str]:
    return (
        str(field_value(record, "session") or ""),
        str(record.get("client_id") or ""),
        str(record.get("domain") or ""),
        operation_id,
    )


def tabular_rows(data: object) -> list[dict[str, object]]:
    if not isinstance(data, dict):
        raise ValueError("Axiom response must be an object")
    tables = data.get("tables")
    if not isinstance(tables, list) or not tables:
        return []
    table = tables[0]
    if not isinstance(table, dict):
        raise ValueError("Axiom table must be an object")
    fields = table.get("fields")
    columns = table.get("columns")
    if not isinstance(fields, list) or not isinstance(columns, list) or not columns:
        return []
    names = [field.get("name") for field in fields if isinstance(field, dict)]
    if len(names) != len(fields) or any(not isinstance(name, str) for name in names):
        raise ValueError("Axiom response has invalid fields")
    if any(not isinstance(column, list) for column in columns):
        raise ValueError("Axiom response has invalid columns")
    row_count = len(columns[0])
    return [
        {
            str(name): columns[index][row] if row < len(columns[index]) else None
            for index, name in enumerate(names)
            if index < len(columns)
        }
        for row in range(row_count)
    ]


def normalize_remote_record(record: dict[str, object]) -> dict[str, object]:
    """Return old flat and new map-backed Axiom events in one flat shape."""
    payload = record.get(REMOTE_MAP_FIELD)
    if not isinstance(payload, dict):
        normalized = dict(record)
        if payload is None:
            normalized.pop(REMOTE_MAP_FIELD, None)
        return normalized

    normalized = dict(payload)
    for name, value in record.items():
        if name == REMOTE_MAP_FIELD:
            continue
        # Axiom's tabular format includes null placeholders for old flat
        # columns. They must not hide a real value from the new map payload.
        if value is not None or name not in normalized:
            normalized[name] = value
    return normalized


def remote_records(args: argparse.Namespace) -> Iterator[tuple[dict[str, object], str]]:
    dataset = args.dataset or os.environ.get("BENHEIM_AXIOM_DATASET", "")
    token = os.environ.get("BENHEIM_AXIOM_QUERY_TOKEN") or os.environ.get("AXIOM_TOKEN", "")
    if not DATASET_PATTERN.fullmatch(dataset):
        raise ValueError("set a valid --dataset or BENHEIM_AXIOM_DATASET")
    if not token:
        raise ValueError("BENHEIM_AXIOM_QUERY_TOKEN or AXIOM_TOKEN is not set")
    if not DURATION_PATTERN.fullmatch(args.since):
        raise ValueError("--since must look like 30m, 12h, or 7d")
    if args.limit < 1 or args.limit > 500:
        raise ValueError("--limit must be from 1 to 500")

    body = json.dumps(
        {
            "apl": remote_apl(dataset, args),
            "startTime": f"now-{args.since}",
            "endTime": "now",
        }
    ).encode("utf-8")
    request = urllib.request.Request(
        QUERY_URL,
        data=body,
        method="POST",
        headers={"Authorization": f"Bearer {token}", "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(request, timeout=10) as response:
            data = json.load(response)
    except urllib.error.HTTPError as error:
        raise ValueError(f"Axiom query failed with HTTP {error.code}") from error
    except urllib.error.URLError as error:
        raise ValueError(f"Axiom query failed: {error.reason}") from error
    rows = tabular_rows(data)
    if args.incomplete:
        # Axiom returns the newest bounded window. Process that window in time
        # order so terminal records close starts instead of reopening them.
        rows.reverse()
    for record in rows:
        normalized = normalize_remote_record(record)
        yield normalized, json.dumps(normalized, separators=(",", ":"), sort_keys=True)


def records(args: argparse.Namespace) -> Iterator[tuple[dict[str, object], str]]:
    if args.remote:
        if args.paths:
            raise ValueError("local paths cannot be combined with --remote")
        yield from remote_records(args)
        return
    if not args.paths:
        raise ValueError("provide a local event path or use --remote")
    yield from local_records(args.paths)


def run(args: argparse.Namespace) -> int:
    source = records(args)
    if not args.incomplete:
        for record, raw in source:
            if matches(record, args):
                print(raw)
        return 0

    # Memory is proportional to matching open operations, not total records.
    open_operations: dict[tuple[str, str, str, str], str] = {}
    for record, raw in source:
        operation_id = record.get("operation_id")
        if not isinstance(operation_id, str) or not operation_id:
            continue
        transition = operation_transition(record)
        key = operation_key(record, operation_id)
        if transition == "start" and matches(record, args):
            open_operations[key] = raw
        elif transition == "terminal":
            open_operations.pop(key, None)

    for key in sorted(open_operations):
        print(open_operations[key])
    return 0


def main() -> int:
    try:
        return run(parse_args())
    except (OSError, ValueError) as error:
        print(f"query-events: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
