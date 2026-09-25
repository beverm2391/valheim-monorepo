#!/usr/bin/env python3
"""Classify and format bounded Valheim and host failure records."""

from __future__ import annotations

import datetime as dt
import hashlib
import re
import urllib.parse
from dataclasses import dataclass
from pathlib import Path


MAXIMUM_MESSAGE_CHARACTERS = 2048
DEFAULT_MANIFEST = Path("/opt/valheim/server/steamapps/appmanifest_896660.acf")
SERVICE_NAME = "valheim"
MONITORED_UNITS = ("valheim.service", "valheim-backup.service")
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
SYSTEMD_FAILURE_SIGNAL = re.compile(
    r"(?:main process exited|failed with result|failed to start|dependency failed|"
    r"scheduled restart job|start operation timed out|stop-sigterm timed out|"
    r"watchdog timeout|out of memory|oom-kill)",
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
class GameplayFailure:
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
class OperationalFailure:
    unit: str
    severity: str
    kind: str
    message: str


@dataclass(frozen=True)
class AxiomDestination:
    endpoint: str
    dataset: str
    token: str

    @property
    def ingest_url(self) -> str:
        return self.endpoint.rstrip("/") + "/v1/ingest/" + urllib.parse.quote(self.dataset, safe="")


@dataclass(frozen=True)
class ForwarderConfig:
    server_id: str
    environment: str
    benheim: AxiomDestination
    infrastructure: AxiomDestination


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


def gameplay_failure_kind(message: str, diagnostic_event: str = "") -> str:
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


def classify_message(raw_message: str) -> GameplayFailure | None:
    """Classify game and mod output after operational records are excluded."""

    message = sanitize_message(raw_message)
    if not message:
        return None

    prefix = BEPINEX_PREFIX.match(message)
    severity = "error"
    logger = ""
    if prefix:
        severity = prefix.group("severity").lower()
        logger = prefix.group("logger").strip()

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
    return GameplayFailure(
        source=source,
        severity=severity,
        kind=gameplay_failure_kind(message, diagnostic_event),
        message=message,
        plugin=plugin,
        diagnostic_domain=diagnostic_domain,
        diagnostic_event=diagnostic_event,
        operation_id=operation.group("value") if operation else "",
        exception_type=exception.group("type") if exception else "",
    )


def journal_unit(record: dict[str, object]) -> str:
    for key in ("UNIT", "_SYSTEMD_UNIT"):
        unit = record.get(key)
        if isinstance(unit, str) and unit in MONITORED_UNITS:
            return unit
    return ""


def operational_failure_kind(message: str, unit: str) -> str:
    lowered = message.lower()
    if "out of memory" in lowered or "oom-kill" in lowered:
        return "out_of_memory"
    if "timed out" in lowered or "watchdog timeout" in lowered:
        return "timeout"
    if "main process exited" in lowered:
        return "process_exit"
    if "scheduled restart job" in lowered:
        return "restart_scheduled"
    if "failed with result" in lowered or "failed to start" in lowered or "dependency failed" in lowered:
        return "service_failed"
    if unit == "valheim-backup.service":
        return "backup_failure"
    return "operational_failure"


def classify_operational_record(record: dict[str, object]) -> OperationalFailure | None:
    unit = journal_unit(record)
    raw_message = record.get("MESSAGE")
    if not unit or not isinstance(raw_message, str):
        return None
    message = sanitize_message(raw_message)
    if not message:
        return None

    is_systemd = record.get("_COMM") == "systemd" or record.get("SYSLOG_IDENTIFIER") == "systemd"
    is_backup_failure = unit == "valheim-backup.service" and bool(
        FAILURE_SIGNAL.search(message) or SYSTEMD_FAILURE_SIGNAL.search(message)
    )
    if not is_backup_failure and not (is_systemd and SYSTEMD_FAILURE_SIGNAL.search(message)):
        return None

    priority = str(record.get("PRIORITY", ""))
    severity = "error" if priority in ("0", "1", "2", "3") else "warning"
    if "scheduled restart job" not in message.lower():
        severity = "error"
    return OperationalFailure(
        unit=unit,
        severity=severity,
        kind=operational_failure_kind(message, unit),
        message=message,
    )


def timestamp(record: dict[str, object]) -> str:
    raw = record.get("__REALTIME_TIMESTAMP")
    try:
        instant = dt.datetime.fromtimestamp(int(str(raw)) / 1_000_000, tz=dt.timezone.utc)
    except (TypeError, ValueError, OSError):
        instant = dt.datetime.now(tz=dt.timezone.utc)
    return instant.isoformat(timespec="microseconds").replace("+00:00", "Z")


def string_field(record: dict[str, object], *keys: str) -> str:
    for key in keys:
        value = record.get(key)
        if isinstance(value, (str, int)) and str(value):
            return str(value)
    return ""


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


def read_component_build_id(failure: GameplayFailure) -> str:
    component = failure.plugin or ("bepinex" if failure.source == "bepinex" else "")
    path = COMPONENT_PATHS.get(component)
    return file_sha256(path) if path else ""


def gameplay_event_record(
    journal_record: dict[str, object],
    failure: GameplayFailure,
    config: ForwarderConfig,
    build_id: str,
    component_build_id: str = "",
) -> dict[str, object] | None:
    invocation = string_field(journal_record, "_SYSTEMD_INVOCATION_ID")
    if not invocation:
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


def operational_event_record(
    journal_record: dict[str, object],
    failure: OperationalFailure,
    config: ForwarderConfig,
    build_id: str,
) -> dict[str, object]:
    invocation = string_field(journal_record, "_SYSTEMD_INVOCATION_ID", "INVOCATION_ID")
    boot_id = string_field(journal_record, "_BOOT_ID")
    fingerprint_input = "\n".join(
        (boot_id, invocation, failure.unit, failure.kind, failure.message)
    )
    fingerprint = hashlib.sha256(fingerprint_input.encode("utf-8")).hexdigest()[:20]
    fields: dict[str, object] = {
        "unit": failure.unit,
        "failure_kind": failure.kind,
        "message": failure.message,
        "fingerprint": fingerprint,
    }
    optional_fields = {
        "boot_id": boot_id,
        "service_invocation_id": invocation,
        "systemd_message_id": string_field(journal_record, "MESSAGE_ID"),
        "result": string_field(journal_record, "RESULT", "JOB_RESULT"),
        "restart_count": string_field(journal_record, "N_RESTARTS"),
    }
    if failure.unit == "valheim.service":
        optional_fields["game_build_id"] = build_id
    fields.update({key: value for key, value in optional_fields.items() if value})

    event: dict[str, object] = {
        "_time": timestamp(journal_record),
        "service": SERVICE_NAME,
        "host": config.server_id,
        "environment": config.environment,
        "level": failure.severity,
        "event": "host_failure",
        "fields": fields,
    }
    if invocation:
        event["run_id"] = invocation
    return event
