#!/usr/bin/env python3
"""Route bounded Valheim and host failure records to their Axiom domains.

The journal remains the complete local record. This process sends game and mod
failures to Benheim, sends systemd and backup failures to Infrastructure, and
never participates in the game service lifecycle.
"""

from __future__ import annotations

import collections
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from typing import Iterable

from valheim_failure_events import (
    AxiomDestination,
    ForwarderConfig,
    MAXIMUM_MESSAGE_CHARACTERS,
    MONITORED_UNITS,
    classify_message,
    classify_operational_record,
    file_sha256,
    gameplay_event_record,
    journal_unit,
    operational_event_record,
    read_build_id,
    read_component_build_id,
)


MAXIMUM_EVENTS_PER_MINUTE = 120
DUPLICATE_WINDOW_SECONDS = 60.0
MAXIMUM_TRACKED_FINGERPRINTS = 512
REQUEST_TIMEOUT_SECONDS = 5
DEFAULT_AXIOM_ENDPOINT = "https://us-east-1.aws.edge.axiom.co"
DATASET_PATTERN = re.compile(r"^[A-Za-z0-9_.-]{1,200}$")
IDENTIFIER_PATTERN = re.compile(r"^[A-Za-z0-9_.-]{1,128}$")


class EventLimiter:
    """Bound remote volume without hiding distinct failures from the journal."""

    def __init__(self) -> None:
        self._minute: collections.deque[float] = collections.deque()
        self._fingerprints: collections.OrderedDict[str, float] = collections.OrderedDict()

    def allows(self, fingerprint: str, now: float) -> bool:
        while self._minute and now - self._minute[0] >= 60.0:
            self._minute.popleft()
        previous = self._fingerprints.get(fingerprint)
        if previous is not None and now - previous < DUPLICATE_WINDOW_SECONDS:
            return False
        if len(self._minute) >= MAXIMUM_EVENTS_PER_MINUTE:
            return False

        self._minute.append(now)
        self._fingerprints[fingerprint] = now
        self._fingerprints.move_to_end(fingerprint)
        while len(self._fingerprints) > MAXIMUM_TRACKED_FINGERPRINTS:
            self._fingerprints.popitem(last=False)
        return True


def validate_endpoint(endpoint: str) -> str:
    value = endpoint.rstrip("/")
    parsed = urllib.parse.urlsplit(value)
    if (
        parsed.scheme != "https"
        or not parsed.netloc
        or parsed.path not in ("", "/")
        or parsed.query
        or parsed.fragment
    ):
        raise ValueError("VALHEIM_AXIOM_ENDPOINT must be an HTTPS origin")
    return value


def destination(
    values: dict[str, str], endpoint: str, dataset_key: str, token_key: str
) -> AxiomDestination:
    dataset = values.get(dataset_key, "")
    token = values.get(token_key, "")
    if not DATASET_PATTERN.fullmatch(dataset):
        raise ValueError(f"{dataset_key} is invalid")
    if not token or len(token) > 4096:
        raise ValueError(f"{token_key} is missing or invalid")
    return AxiomDestination(endpoint, dataset, token)


def load_config(environment: dict[str, str] | None = None) -> ForwarderConfig:
    values = dict(os.environ if environment is None else environment)
    endpoint = validate_endpoint(values.get("VALHEIM_AXIOM_ENDPOINT", DEFAULT_AXIOM_ENDPOINT))
    server_id = values.get("VALHEIM_DIAGNOSTICS_SERVER_ID", "")
    environment_name = values.get("VALHEIM_DIAGNOSTICS_ENVIRONMENT", "production")
    if not IDENTIFIER_PATTERN.fullmatch(server_id):
        raise ValueError("VALHEIM_DIAGNOSTICS_SERVER_ID is invalid")
    if not IDENTIFIER_PATTERN.fullmatch(environment_name):
        raise ValueError("VALHEIM_DIAGNOSTICS_ENVIRONMENT is invalid")
    return ForwarderConfig(
        server_id=server_id,
        environment=environment_name,
        benheim=destination(
            values, endpoint, "BENHEIM_AXIOM_DATASET", "BENHEIM_AXIOM_INGEST_TOKEN"
        ),
        infrastructure=destination(
            values, endpoint, "INFRA_AXIOM_DATASET", "INFRA_AXIOM_INGEST_TOKEN"
        ),
    )


def send_record(destination_config: AxiomDestination, record: dict[str, object]) -> None:
    body = json.dumps([record], separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        destination_config.ingest_url,
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {destination_config.token}",
            "Content-Type": "application/json",
        },
    )
    with urllib.request.urlopen(request, timeout=REQUEST_TIMEOUT_SECONDS) as response:
        if response.status < 200 or response.status >= 300:
            raise OSError(f"Axiom returned HTTP {response.status}")


def journal_records(lines: Iterable[str]) -> Iterable[dict[str, object]]:
    for line in lines:
        try:
            value = json.loads(line)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict):
            yield value


def route_record(
    journal_record: dict[str, object], config: ForwarderConfig
) -> tuple[AxiomDestination, str, dict[str, object]] | None:
    operational_failure = classify_operational_record(journal_record)
    if operational_failure is not None:
        build_id = read_build_id() if operational_failure.unit == "valheim.service" else ""
        record = operational_event_record(
            journal_record, operational_failure, config, build_id
        )
        return config.infrastructure, "Infrastructure", record

    raw_message = journal_record.get("MESSAGE")
    if not isinstance(raw_message, str) or journal_unit(journal_record) == "valheim-backup.service":
        return None
    gameplay_failure = classify_message(raw_message)
    if gameplay_failure is None:
        return None
    record = gameplay_event_record(
        journal_record,
        gameplay_failure,
        config,
        read_build_id(),
        read_component_build_id(gameplay_failure),
    )
    if record is None:
        return None
    return config.benheim, "Benheim", record


def run() -> int:
    try:
        config = load_config()
    except ValueError as error:
        print(f"valheim diagnostics disabled: {error}", file=sys.stderr)
        return 2

    command = [os.environ.get("JOURNALCTL_BIN", "journalctl")]
    for unit in MONITORED_UNITS:
        command.extend(("--unit", unit))
    command.extend(("--lines", "0", "--follow", "--no-pager", "--output", "json"))

    limiter = EventLimiter()
    with subprocess.Popen(command, text=True, stdout=subprocess.PIPE) as journal:
        assert journal.stdout is not None
        for journal_record in journal_records(journal.stdout):
            routed = route_record(journal_record, config)
            if routed is None:
                continue
            target, target_name, record = routed
            fingerprint = str(record["fields"]["fingerprint"])  # type: ignore[index]
            if not limiter.allows(fingerprint, time.monotonic()):
                continue
            try:
                send_record(target, record)
            except (OSError, urllib.error.URLError) as error:
                print(
                    f"valheim diagnostics dropped one {target_name} failure after {type(error).__name__}; journal record remains",
                    file=sys.stderr,
                )
        return journal.wait()


if __name__ == "__main__":
    raise SystemExit(run())
