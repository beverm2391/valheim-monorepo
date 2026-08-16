#!/usr/bin/env python3
"""Offline contract checks for the stdlib Axiom query path."""

from __future__ import annotations

import importlib.util
import io
import json
import os
from argparse import Namespace
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
    station=None,
    operation_id='op-"quoted',
    incomplete=False,
)
captured: dict[str, object] = {}
response = {
    "tables": [
        {
            "fields": [{"name": "_time"}, {"name": "client_id"}, {"name": "moved"}],
            "columns": [["2026-08-16T00:00:00Z"], ["client-1"], [13]],
        }
    ]
}


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
assert "['operation_id']" in apl and 'op-\\"quoted' in apl
assert apl.endswith("| order by _time desc | take 25")
assert rows[0][0] == {
    "_time": "2026-08-16T00:00:00Z",
    "client_id": "client-1",
    "moved": 13,
}
assert "query-secret" not in rows[0][1]

with patch.dict(os.environ, {}, clear=True):
    try:
        list(query_events.remote_records(args))
    except ValueError as error:
        assert "TOKEN" in str(error)
        assert "query-secret" not in str(error)
    else:
        raise AssertionError("missing query credential must fail")

print("remote Axiom query contract checks passed")
