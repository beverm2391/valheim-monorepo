#!/usr/bin/env python3
"""Forward bounded Valheim failure records from journald to Axiom.

The systemd journal remains the complete local record. This process observes the
Valheim unit, selects only actionable failures, and sends a small structured
copy. It owns no spool and never participates in the game service lifecycle.
"""

from __future__ import annotations

import collections
import datetime as dt
import hashlib
import json
import os
import re
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable


MAXIMUM_MESSAGE_CHARACTERS = 2048
MAXIMUM_EVENTS_PER_MINUTE = 120
DUPLICATE_WINDOW_SECONDS = 60.0
MAXIMUM_TRACKED_FINGERPRINTS = 512
REQUEST_TIMEOUT_SECONDS = 5
DEFAULT_AXIOM_ENDPOINT = "https://us-east-1.aws.edge.axiom.co"
DEFAULT_MANIFEST = Path("/opt/valheim/server/steamapps/appmanifest_896660.acf")
COMPONENT_PATHS = {
    "bepinex": Path("/opt/valheim/server/BepInEx/core/BepInEx.dll"),
    "benheim-eternal-fire": Path(
        "/opt/valheim/server/BepInEx/plugins/BenheimEternalFire/BenheimEternalFire.dll"
    ),
    "benheim-test-commands": Path(
        "/opt/valheim/server/BepInEx/plugins/BenheimTestCommands/BenheimTestCommands.dll"
    ),
    "benheim-server-support": Path(
        "/opt/valheim/server/BepInEx/plugins/BenheimServerSupport/BenheimServerSupport.dll"
    ),
}

DATASET_PATTERN = re.compile(r"^[A-Za-z0-9_.-]{1,200}$")
SERVER_ID_PATTERN = re.compile(r"^[A-Za-z0-9_.-]{1,128}$")
BEPINEX_PREFIX = re.compile(
    r"^\[(?P<severity>Fatal|Error|Warning|Message|Info|Debug)\s*:\s*(?P<logger>[^\]]+)\]\s*(?P<body>.*)$",
    re.IGNORECASE | re.DOTALL,
)
DIAGNOSTIC_LINE = re.compile(
    r"\[diag\]\[(?P<domain>[^\]]{1,80})\]\s+(?P<event>[A-Za-z0-9_.-]{1,120})",
    re.IGNORECASE,
)
OPERATION_ID = re.compile(r"(?:^|\s)operation_id=(?P<value>[A-Za-z0-9_.:-]{1,128})(?:\s|$)")
EXCEPTION_TYPE = re.compile(r"\b(?P<type>[A-Za-z_][A-Za-z0-9_.]*(?:Exception|Error))\b")
FAILURE_SIGNAL = re.compile(
    r"\b(?:fatal|crash(?:ed)?|exception|error|failed|failure|unavailable|assert(?:ion)?\s+failed)\b",
    re.IGNORECASE,
)
DIAGNOSTIC_FAILURE = re.compile(
    r"(?:^|[_.-])(?:failed|failure|rejected|unavailable)(?:$|[_.-])",
    re.IGNORECASE,
)
FIRST_PARTY_PLUGINS = (
    (
        "benheim-eternal-fire",
        re.compile(
            r"Benheim(?:\s+|\.)?Eternal\s*Fire|BenheimEternalFire|com\.benheim\.eternalfire",
            re.IGNORECASE,
        ),
    ),
    (
        "benheim-test-commands",
        re.compile(
            r"Benheim(?:\s+|\.)?Test\s*Commands|BenheimTestCommands|com\.benheim\.testcommands",
            re.IGNORECASE,
        ),
    ),
    (
        "benheim-server-support",
        re.compile(
            r"Benheim(?:\s+|\.)?Server\s*Support|BenheimServerSupport|com\.benheim\.serversupport",
            re.IGNORECASE,
        ),
    ),
)
SECRET_ASSIGNMENT = re.compile(
    r"(?i)\b(token|password|secret|authorization|api[_-]?key)\b(\s*(?:[:=]\s*|\s+))(\S+)"
)
BEARER_VALUE = re.compile(r"(?i)\bBearer\s+\S+")
PASSWORD_ARGUMENT = re.compile(r"(?i)(-password\s+)(\S+)")
CONTROL_CHARACTERS = re.compile(r"[\x00-\x1f\x7f]+")
WHITESPACE = re.compile(r"\s+")
BUILD_ID = re.compile(r'^\s*"buildid"\s+"(?P<value>[0-9]+)"\s*$')


@dataclass(frozen=True)
class Failure:
    source: str
    severity: str
    kind: str
    message: str
    plugin: str = ""
    diagnostic_domain: str = ""
    diagnostic_event: str = ""
    operation_id: str = ""
    exception_type: str = ""


@dataclass(frozen=True)
class AxiomConfig:
    endpoint: str
    dataset: str
    token: str
    server_id: str

    @property
    def ingest_url(self) -> str:
        return self.endpoint.rstrip("/") + "/v1/ingest/" + urllib.parse.quote(self.dataset, safe="")


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


def sanitize_message(message: str) -> str:
    message = BEARER_VALUE.sub("Bearer [redacted]", message)
    message = SECRET_ASSIGNMENT.sub(lambda match: f"{match.group(1)}{match.group(2)}[redacted]", message)
    message = PASSWORD_ARGUMENT.sub(r"\1[redacted]", message)
    message = CONTROL_CHARACTERS.sub(" ", message)
    message = WHITESPACE.sub(" ", message).strip()
    if len(message) > MAXIMUM_MESSAGE_CHARACTERS:
        message = message[: MAXIMUM_MESSAGE_CHARACTERS - 1] + "…"
    return message


def identify_plugin(message: str) -> str:
    for plugin, pattern in FIRST_PARTY_PLUGINS:
        if pattern.search(message):
            return plugin
    return ""


def failure_kind(message: str, diagnostic_event: str = "") -> str:
    lowered = f"{diagnostic_event} {message}".lower()
    if "harmony" in lowered or "patchall" in lowered or "patch failed" in lowered:
        return "harmony_patch"
    if "plugin" in lowered and any(word in lowered for word in ("load", "chainloader", "dependency")):
        return "plugin_load"
    if "exception" in lowered:
        return "exception"
    if "crash" in lowered or "fatal" in lowered:
        return "fatal"
    if "rejected" in lowered:
        return "rejected"
    if "unavailable" in lowered:
        return "unavailable"
    return "failure"


def classify_message(raw_message: str) -> Failure | None:
    message = sanitize_message(raw_message)
    if not message:
        return None

    prefix = BEPINEX_PREFIX.match(message)
    severity = "error"
    logger = ""
    body = message
    if prefix:
        severity = prefix.group("severity").lower()
        logger = prefix.group("logger").strip()
        body = prefix.group("body").strip()

    diagnostic = DIAGNOSTIC_LINE.search(message)
    diagnostic_domain = diagnostic.group("domain") if diagnostic else ""
    diagnostic_event = diagnostic.group("event") if diagnostic else ""
    is_diagnostic_failure = bool(
        diagnostic_event and DIAGNOSTIC_FAILURE.search(diagnostic_event)
    )
    has_failure_signal = bool(FAILURE_SIGNAL.search(message) or EXCEPTION_TYPE.search(message))
    if prefix and severity in ("message", "info", "debug") and not is_diagnostic_failure:
        return None
    if prefix and severity == "warning" and not has_failure_signal and not is_diagnostic_failure:
        return None
    if not prefix and not has_failure_signal and not is_diagnostic_failure:
        return None

    plugin = identify_plugin(f"{logger} {message}")
    logger_lower = logger.lower()
    if plugin:
        source = "first-party-server-mod"
    elif any(name in logger_lower for name in ("bepinex", "chainloader", "preloader", "harmony")):
        source = "bepinex"
    elif prefix and logger_lower not in ("unity log", "unity"):
        source = "bepinex"
    else:
        source = "valheim"

    exception = EXCEPTION_TYPE.search(message)
    operation = OPERATION_ID.search(message)
    return Failure(
        source=source,
        severity=severity,
        kind=failure_kind(message, diagnostic_event),
        message=message,
        plugin=plugin,
        diagnostic_domain=diagnostic_domain,
        diagnostic_event=diagnostic_event,
        operation_id=operation.group("value") if operation else "",
        exception_type=exception.group("type") if exception else "",
    )


def timestamp(record: dict[str, object]) -> str:
    raw = record.get("__REALTIME_TIMESTAMP")
    try:
        instant = dt.datetime.fromtimestamp(int(str(raw)) / 1_000_000, tz=dt.timezone.utc)
    except (TypeError, ValueError, OSError):
        instant = dt.datetime.now(tz=dt.timezone.utc)
    return instant.isoformat(timespec="microseconds").replace("+00:00", "Z")


def read_build_id(manifest: Path = DEFAULT_MANIFEST) -> str:
    try:
        with manifest.open(encoding="utf-8") as stream:
            for line in stream:
                match = BUILD_ID.match(line)
                if match:
                    return match.group("value")
    except OSError:
        pass
    return "unknown"


def file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    try:
        with path.open("rb") as stream:
            for chunk in iter(lambda: stream.read(128 * 1024), b""):
                digest.update(chunk)
    except OSError:
        return ""
    return digest.hexdigest()


def read_component_build_id(failure: Failure) -> str:
    component = failure.plugin or ("bepinex" if failure.source == "bepinex" else "")
    path = COMPONENT_PATHS.get(component)
    return file_sha256(path) if path else ""


def event_record(
    journal_record: dict[str, object],
    failure: Failure,
    config: AxiomConfig,
    build_id: str,
    component_build_id: str = "",
) -> dict[str, object] | None:
    invocation = journal_record.get("_SYSTEMD_INVOCATION_ID")
    if not isinstance(invocation, str) or not invocation:
        return None

    fingerprint_input = "\n".join(
        (invocation, build_id, component_build_id, failure.source, failure.kind, failure.plugin, failure.message)
    )
    fingerprint = hashlib.sha256(fingerprint_input.encode("utf-8")).hexdigest()[:20]
    fields: dict[str, object] = {
        "source": failure.source,
        "severity": failure.severity,
        "failure_kind": failure.kind,
        "message": failure.message,
        "fingerprint": fingerprint,
        "server_invocation_id": invocation,
    }
    if failure.plugin:
        fields["plugin"] = failure.plugin
    if component_build_id:
        fields["component_build_id"] = component_build_id
    if failure.diagnostic_domain:
        fields["diagnostic_domain"] = failure.diagnostic_domain
    if failure.diagnostic_event:
        fields["diagnostic_event"] = failure.diagnostic_event
    if failure.exception_type:
        fields["exception_type"] = failure.exception_type
    if failure.operation_id:
        fields["operation_id"] = failure.operation_id

    event: dict[str, object] = {
        "_time": timestamp(journal_record),
        "session_id": invocation,
        "client_id": f"dedicated-server:{config.server_id}",
        "player_name": "",
        "mod_version": "",
        "build_id": build_id,
        "domain": "ServerRuntime",
        "event": f"{failure.source}-failure",
        "schema": 2,
        "fields": fields,
    }
    if failure.operation_id:
        event["operation_id"] = failure.operation_id
    return event


def load_config(environment: dict[str, str] | None = None) -> AxiomConfig:
    values = os.environ if environment is None else environment
    endpoint = values.get("BENHEIM_AXIOM_ENDPOINT", DEFAULT_AXIOM_ENDPOINT).rstrip("/")
    dataset = values.get("BENHEIM_AXIOM_DATASET", "")
    token = values.get("BENHEIM_AXIOM_INGEST_TOKEN", "")
    server_id = values.get("VALHEIM_DIAGNOSTICS_SERVER_ID", "")
    parsed = urllib.parse.urlsplit(endpoint)
    if (
        parsed.scheme != "https"
        or not parsed.netloc
        or parsed.path not in ("", "/")
        or parsed.query
        or parsed.fragment
    ):
        raise ValueError("BENHEIM_AXIOM_ENDPOINT must be an HTTPS origin")
    if not DATASET_PATTERN.fullmatch(dataset):
        raise ValueError("BENHEIM_AXIOM_DATASET is invalid")
    if not token or len(token) > 4096:
        raise ValueError("BENHEIM_AXIOM_INGEST_TOKEN is missing or invalid")
    if not SERVER_ID_PATTERN.fullmatch(server_id):
        raise ValueError("VALHEIM_DIAGNOSTICS_SERVER_ID is invalid")
    return AxiomConfig(endpoint, dataset, token, server_id)


def send_record(config: AxiomConfig, record: dict[str, object]) -> None:
    body = json.dumps([record], separators=(",", ":"), ensure_ascii=False).encode("utf-8")
    request = urllib.request.Request(
        config.ingest_url,
        data=body,
        method="POST",
        headers={
            "Authorization": f"Bearer {config.token}",
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


def run() -> int:
    try:
        config = load_config()
    except ValueError as error:
        print(f"valheim diagnostics disabled: {error}", file=sys.stderr)
        return 2

    command = [
        os.environ.get("JOURNALCTL_BIN", "journalctl"),
        "--unit",
        "valheim.service",
        "--lines",
        "0",
        "--follow",
        "--no-pager",
        "--output",
        "json",
    ]
    limiter = EventLimiter()
    with subprocess.Popen(command, text=True, stdout=subprocess.PIPE) as journal:
        assert journal.stdout is not None
        for record in journal_records(journal.stdout):
            raw_message = record.get("MESSAGE")
            if not isinstance(raw_message, str):
                continue
            failure = classify_message(raw_message)
            if failure is None:
                continue
            event = event_record(
                record,
                failure,
                config,
                read_build_id(),
                read_component_build_id(failure),
            )
            if event is None:
                continue
            fingerprint = str(event["fields"]["fingerprint"])  # type: ignore[index]
            if not limiter.allows(fingerprint, time.monotonic()):
                continue
            try:
                send_record(config, event)
            except (OSError, urllib.error.URLError) as error:
                print(
                    f"valheim diagnostics dropped one remote failure after {type(error).__name__}; journal record remains",
                    file=sys.stderr,
                )
        return journal.wait()


if __name__ == "__main__":
    raise SystemExit(run())
