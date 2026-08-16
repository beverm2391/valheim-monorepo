#!/usr/bin/env python3
"""Fail closed unless another client's first post-write open matches the writer."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Any


def load_events(path: Path) -> list[dict[str, Any]]:
    events: list[dict[str, Any]] = []
    with path.open(encoding="utf-8") as handle:
        for line_number, line in enumerate(handle, 1):
            if not line.strip():
                continue
            try:
                event = json.loads(line)
            except json.JSONDecodeError as error:
                raise ValueError(f"{path}:{line_number}: invalid JSON: {error.msg}") from error
            if isinstance(event, dict):
                events.append(event)
    return events


def contents(value: Any) -> dict[str, int]:
    if value in (None, ""):
        return {}
    if not isinstance(value, str):
        raise ValueError("contents must be a string")
    parsed: dict[str, int] = {}
    for entry in value.split(","):
        name, separator, count = entry.rpartition("=")
        if not separator or not name:
            raise ValueError(f"invalid contents entry: {entry}")
        parsed[name] = int(count)
    return parsed


def check(
    depositor_events: list[dict[str, Any]],
    observer_events: list[dict[str, Any]],
    requested_operation: str | None,
) -> str:
    writes = [event for event in depositor_events if event.get("event") == "quick_stack_write_snapshot"]
    if requested_operation is None:
        if not writes:
            raise ValueError("depositor log has no Put Away write snapshots")
        requested_operation = str(writes[-1].get("operation_id"))
    writes = [event for event in writes if event.get("operation_id") == requested_operation]
    if not writes:
        raise ValueError(f"depositor log has no writes for operation {requested_operation}")

    moved_events = [
        event
        for event in depositor_events
        if event.get("event") == "quick_stack_item" and event.get("operation_id") == requested_operation
    ]
    if not moved_events:
        raise ValueError("operation has no deposited item evidence")

    opens = [event for event in observer_events if event.get("event") == "container_open_snapshot"]
    checked = 0
    for write in writes:
        if not write.get("owner"):
            raise ValueError(f"writer did not own chest {write.get('zdo_id')} at completion")
        if int(write.get("moved", 0)) > 0 and not write.get("revision_advanced"):
            raise ValueError(f"write revision did not advance for chest {write.get('zdo_id')}")

        zdo_id = write.get("zdo_id")
        candidates = [
            event
            for event in opens
            if event.get("zdo_id") == zdo_id
        ]
        if not candidates:
            raise ValueError(f"observer has no first-open snapshot for chest {zdo_id}")
        if len(candidates) != 1:
            raise ValueError(f"observer log has ambiguous first-open snapshots for chest {zdo_id}")
        first_open = candidates[0]
        if not first_open.get("session"):
            raise ValueError("observer snapshot has no session identity")
        if first_open.get("session") == write.get("session"):
            raise ValueError(f"chest {zdo_id} was not observed in another client session")
        if first_open.get("peer") == write.get("peer"):
            raise ValueError(f"chest {zdo_id} was not observed by another peer")
        writer_player = int(write.get("player_id", 0))
        observer_player = int(first_open.get("player_id", 0))
        if writer_player == 0 or observer_player == 0:
            raise ValueError(f"chest {zdo_id} evidence has no stable player identity")
        if observer_player == writer_player:
            raise ValueError(f"chest {zdo_id} was not observed by another player")
        if int(first_open.get("revision", -1)) < int(write.get("revision_after", 0)):
            raise ValueError(f"observer saw an older revision for chest {zdo_id}")

        written_contents = contents(write.get("contents"))
        observed_contents = contents(first_open.get("contents"))
        if observed_contents != written_contents:
            raise ValueError(f"observer's first open saw stale contents for chest {zdo_id}")

        for item_event in (event for event in moved_events if event.get("zdo_id") == zdo_id):
            item_name = item_event.get("item")
            expected_count = int(item_event.get("resulting_count", -1))
            if observed_contents.get(item_name) != expected_count:
                raise ValueError(f"observer count mismatch for {item_name} in chest {zdo_id}")
        checked += 1

    return f"Put Away visibility proof passed: operation={requested_operation} chests={checked} manual_sequence=fresh_observer_session"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("depositor", type=Path, help="depositor BenheimEvents.ndjson")
    parser.add_argument("observer", type=Path, help="observer BenheimEvents.ndjson")
    parser.add_argument("--operation-id", help="specific Put Away operation; defaults to latest write")
    args = parser.parse_args()
    try:
        print(check(load_events(args.depositor), load_events(args.observer), args.operation_id))
    except (OSError, ValueError) as error:
        print(f"Put Away visibility proof failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
