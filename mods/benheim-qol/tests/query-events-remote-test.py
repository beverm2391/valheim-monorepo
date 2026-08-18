#!/usr/bin/env python3
"""Offline contract checks for the stdlib Axiom query path."""

from __future__ import annotations

import importlib.util
import io
import json
import os
from argparse import Namespace
from contextlib import redirect_stdout
from pathlib import Path
from unittest.mock import patch


query_path = Path(__file__).parents[1] / "scripts" / "query-events.py"
spec = importlib.util.spec_from_file_location("benheim_query_events", query_path)
assert spec and spec.loader
query_events = importlib.util.module_from_spec(spec)
spec.loader.exec_module(query_events)


class Response(io.BytesIO):
    def __enter__(self) -> "Response":
        return self

    def __exit__(self, *args: object) -> None:
        self.close()


def tabular_response(records: list[dict[str, object]]) -> dict[str, object]:
    names = sorted({name for record in records for name in record})
    return {
        "tables": [
            {
                "fields": [{"name": name} for name in names],
                "columns": [[record.get(name) for record in records] for name in names],
            }
        ]
    }


args = Namespace(
    paths=[],
    remote=True,
    dataset="benheim-diagnostics",
    since="12h",
    limit=25,
    session="session-1",
    player="Johnny",
    client="client-1",
    domain="Inventory",
    event="put_away_finished",
    item=None,
    station='piece_oven#12"quoted',
    operation_id='op-"quoted',
    incomplete=False,
)
captured: dict[str, object] = {}
response = tabular_response(
    [{"_time": "2026-08-16T00:00:00Z", "client_id": "client-1", "moved": 13}]
)


def fake_urlopen(request: object, timeout: int) -> Response:
    captured["url"] = request.full_url
    captured["authorization"] = request.get_header("Authorization")
    captured["body"] = json.loads(request.data)
    captured["timeout"] = timeout
    return Response(json.dumps(response).encode("utf-8"))


with patch.dict(os.environ, {"BENHEIM_AXIOM_QUERY_TOKEN": "query-secret"}, clear=True):
    with patch.object(query_events.urllib.request, "urlopen", fake_urlopen):
        rows = list(query_events.remote_records(args))

assert captured["url"] == query_events.QUERY_URL
assert captured["authorization"] == "Bearer query-secret"
assert captured["timeout"] == 10
body = captured["body"]
assert body["startTime"] == "now-12h"
assert body["endTime"] == "now"
apl = body["apl"]
assert apl.startswith("['benheim-diagnostics']")
assert "['session_id']" in apl and '"session-1"' in apl
assert "['player_name']" in apl and '"Johnny"' in apl
assert "['client_id']" in apl and '"client-1"' in apl
assert "['station']" in apl and 'piece_oven#12\\"quoted' in apl
assert "['operation_id']" in apl and 'op-\\"quoted' in apl
assert apl.endswith("| order by _time desc | take 25")
assert rows[0][0] == {
    "_time": "2026-08-16T00:00:00Z",
    "client_id": "client-1",
    "moved": 13,
}
assert "query-secret" not in rows[0][1]

incomplete_args = Namespace(
    paths=[],
    remote=True,
    dataset="benheim-diagnostics",
    since="12h",
    limit=25,
    session="session-1",
    player="Johnny",
    client="client-1",
    domain=None,
    event="put_away_batch_started",
    item=None,
    station=None,
    operation_id=None,
    incomplete=True,
)


def lifecycle_record(
    second: str,
    domain: str,
    event: str,
    operation_id: str,
    phase: str,
    status: str,
) -> dict[str, object]:
    return {
        "_time": f"2026-08-16T00:00:{second}Z",
        "session_id": "session-1",
        "client_id": "client-1",
        "player_name": "Johnny",
        "domain": domain,
        "event": event,
        "operation_id": operation_id,
        "operation_phase": phase,
        "status": status,
    }


# Axiom returns newest first. The open batch includes lease, transaction-start,
# settlement, and receipt activity that must not replace or close its batch start.
transaction = "InventoryTransaction"
lifecycle_rows = [
    ("09", transaction, "put_away_batch_finished", "op-cancelled", "terminal", "cancelled"),
    ("08", transaction, "put_away_batch_started", "op-cancelled", "start", "running"),
    ("07", transaction, "put_away_batch_finished", "op-complete", "terminal", "completed"),
    ("06", transaction, "client_request_sent", "op-complete", "start", "sent"),
    ("05", transaction, "put_away_batch_started", "op-complete", "start", "running"),
    ("04", transaction, "client_result", "op-open", "settled", "settled"),
    ("03", transaction, "client_receipt_ack_sent", "op-open", "receipt_cleanup", "sent"),
    ("02", transaction, "client_request_sent", "op-open", "start", "sent"),
    ("01", "Inventory", "quick_stack_lease_result", "op-open", "lease_result", "granted"),
    ("00", transaction, "put_away_batch_started", "op-open", "start", "running"),
]
response = tabular_response([lifecycle_record(*values) for values in lifecycle_rows])
captured.clear()
printed = io.StringIO()
with patch.dict(os.environ, {"BENHEIM_AXIOM_QUERY_TOKEN": "query-secret"}, clear=True):
    with patch.object(query_events.urllib.request, "urlopen", fake_urlopen):
        with redirect_stdout(printed):
            assert query_events.run(incomplete_args) == 0

incomplete_apl = captured["body"]["apl"]
assert "['event']" not in incomplete_apl
assert "['session_id']" in incomplete_apl
incomplete_rows = [json.loads(line) for line in printed.getvalue().splitlines()]
assert len(incomplete_rows) == 1
assert incomplete_rows[0]["operation_id"] == "op-open"
assert incomplete_rows[0]["event"] == "put_away_batch_started"

with patch.dict(os.environ, {}, clear=True):
    try:
        list(query_events.remote_records(args))
    except ValueError as error:
        assert "TOKEN" in str(error)
        assert "query-secret" not in str(error)
    else:
        raise AssertionError("missing query credential must fail")

print("remote Axiom query contract checks passed")
