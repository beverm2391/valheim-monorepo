import { test } from 'node:test';
import assert from 'node:assert/strict';
import http from 'node:http';
import { randomUUID } from 'node:crypto';
import { mkdtempSync, rmSync, readFileSync, mkdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { createBridge, generate, parseRemark } from './bridge.mjs';
import { createTrace, readTrace } from './trace.mjs';

const context = { event: 'approach', visitorName: 'Lab visitor', dayPart: 'day', weather: 'Clear', biome: 'Meadows',
  wet: false, cold: false, tubBurning: true, playerSeated: false, recentRemarks: [] };
const silent = { speak: false, text: '' };
const provider = content => ({ ok: true, json: async () => ({ model: 'google/test-model',
  choices: [{ message: { content } }], usage: { prompt_tokens: 300, completion_tokens: 20,
    total_tokens: 320, cost: .0001, private: 'excluded' } }) });

async function withBridge(options, run) {
  const directory = mkdtempSync(join(tmpdir(), 'george-trace-test-'));
  const traceFile = join(directory, 'bridge.jsonl'), records = [];
  const server = createBridge({ key: 'test-only-key', traceFile,
    log: r => records.push(JSON.parse(r)), ...options });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const url = `http://127.0.0.1:${server.address().port}/remark`;
  const post = (value = context, id = randomUUID()) => fetch(url, { method: 'POST',
    headers: { 'X-George-Request-Id': id }, body: JSON.stringify({ requestId: id, context: value }) })
    .then(r => r.json());
  try { await run({ post, records, traceFile, url, events: id => readTrace([traceFile], id) }); }
  finally {
    server.closeAllConnections(); await new Promise(resolve => server.close(resolve));
    rmSync(directory, { recursive: true, force: true });
  }
}

test('live file recovers exact prompt, context, parsed reply, timing and usage by trigger ID', async () => {
  let sent;
  await withBridge({ generateImpl: (value, options) => generate(value, { ...options,
    fetchImpl: async (url, request) => {
      assert.equal(url, 'https://openrouter.ai/api/v1/chat/completions');
      assert.equal(request.headers.Authorization, 'Bearer test-only-key');
      sent = JSON.parse(request.body);
      return provider('{"speak":true,"text":"The water is better than the wind."}');
    } }) }, async ({ post, records, traceFile, events }) => {
      const id = randomUUID(), reply = await post(context, id), trace = events(id);
      assert.equal(reply.requestId, id);
      assert.equal(reply.reason, 'model_speech');
      assert.equal(reply.text, 'The water is better than the wind.');
      assert.equal(reply.model, 'google/test-model');
      assert.equal(reply.tokens, 320);
      assert.ok(reply.durationMs >= 0);
      assert.deepEqual(trace.map(e => e.phase), ['request_received', 'provider_request', 'provider_completed', 'bridge_response']);
      assert.deepEqual(trace[0].context, context);
      assert.deepEqual(trace[1].request, sent);
      assert.equal(sent.messages[0].content, readFileSync(new URL('./george.txt', import.meta.url), 'utf8'));
      assert.deepEqual(JSON.parse(sent.messages[1].content), context);
      assert.equal(sent.response_format.json_schema.strict, true);
      assert.deepEqual(trace[2].parsedReply, { speak: true, text: reply.text });
      assert.equal(trace[2].usage.cost, .0001);
      assert.ok(!readFileSync(traceFile, 'utf8').includes('test-only-key'));
      assert.ok(!JSON.stringify(records).includes(reply.text));
      assert.ok(!trace.some(e => e.phase === 'display_submitted'));
      assert.ok(!JSON.stringify(trace).includes('excluded'));
    });
});

test('silence, invalid model output and provider failures retain distinct safe causes', async () => {
  for (const [response, reason, completed] of [
    [provider('{"speak":false,"text":""}'), 'model_silence', true],
    [provider('{"speak":true,"text":"<b>Hello</b>"}'), 'invalid_remark', true],
    [provider('false'), 'invalid_remark', true],
    [provider('null'), 'invalid_remark', true],
    [provider('malformed dialogue'), 'model_reply_json', true],
    [{ ok: false, status: 401, json: () => { throw new Error('private provider body'); } }, 'provider_http_401', false],
    [{ ok: true, json: async () => { throw new Error('private provider body'); } }, 'provider_response_json', false]
  ]) await withBridge({ generateImpl: (value, options) => generate(value, { ...options,
    fetchImpl: async () => response }) }, async ({ post, events, records }) => {
      const id = randomUUID(), reply = await post(context, id);
      assert.equal(reply.reason, reason);
      assert.equal(reply.speak, false); assert.equal(reply.text, '');
      assert.equal(events(id).some(e => e.phase === 'provider_completed'), completed);
      assert.equal(events(id).at(-1).reason, reason);
      assert.ok(!JSON.stringify([...events(id), ...records]).includes('private'));
    });
  for (const remark of [{ speak: false, text: 'Hello' }, { speak: true, text: 'x'.repeat(141) },
    { speak: true, text: '' }]) assert.throws(() => parseRemark(remark));
});

test('rejected context and malformed envelopes correlate without persisting unknown payloads', async () => {
  let calls = 0;
  await withBridge({ generateImpl: async () => { calls++; throw new Error('private exception'); } },
    async ({ post, events, url, traceFile }) => {
      const id = randomUUID();
      assert.equal((await post({ ...context, inventory: ['private inventory'] }, id)).reason, 'invalid_context');
      assert.deepEqual(events(id).map(e => e.phase), ['request_failed', 'bridge_response']);
      const malformedId = randomUUID();
      const malformed = await fetch(url, { method: 'POST', headers: { 'X-George-Request-Id': malformedId },
        body: 'private invalid JSON' }).then(r => r.json());
      assert.equal(malformed.requestId, malformedId); assert.equal(malformed.reason, 'request_json');
      // Live Unity serialization once omitted nested context while retaining
      // the valid UUID. Keep this separate from an actual ID mismatch.
      const missingContext = await fetch(url, { method: 'POST', headers: { 'X-George-Request-Id': id },
        body: JSON.stringify({ requestId: id }) }).then(r => r.json());
      assert.equal(missingContext.requestId, id);
      assert.equal(missingContext.reason, 'invalid_request_envelope');
      assert.equal(calls, 0);
      assert.equal((await post()).reason, 'provider_network_failure'); assert.equal(calls, 1);
      assert.ok(!readFileSync(traceFile, 'utf8').includes('private'));
    });
});

test('timeout, busy and cap differ; rejected requests do not release the active call', async () => {
  let started;
  const active = new Promise(resolve => { started = resolve; });
  await withBridge({ timeoutMs: 100, requestLimit: 1, generateImpl: (value, { signal }) => {
    started();
    return new Promise((resolve, reject) => signal.addEventListener('abort', () => reject(new Error('aborted'))));
  } }, async ({ post, events }) => {
    const id = randomUUID(), first = post(context, id);
    await active;
    assert.equal((await post()).reason, 'bridge_busy');
    assert.equal((await post()).reason, 'bridge_busy');
    assert.equal((await first).reason, 'provider_timeout');
    assert.equal(events(id).at(-1).reason, 'provider_timeout');
    assert.equal((await post()).reason, 'call_limit');
  });
});

test('disconnect records cancellation and late completion without pretending delivery', async () => {
  let started, disconnected, release;
  const active = new Promise(resolve => { started = resolve; });
  const aborted = new Promise(resolve => { disconnected = resolve; });
  await withBridge({ generateImpl: (value, { signal }) => {
    signal.addEventListener('abort', disconnected); started();
    return new Promise(resolve => { release = resolve; });
  } }, async ({ url, events, post }) => {
    const id = randomUUID();
    const client = http.request(url, { method: 'POST', headers: { 'X-George-Request-Id': id } });
    client.on('error', () => {});
    client.end(JSON.stringify({ requestId: id, context }));
    await active; client.destroy(); await aborted;
    release({ remark: silent });
    await post({ ...context, inventory: [] });
    const trace = events(id);
    assert.ok(trace.some(e => e.phase === 'provider_completed'));
    assert.equal(trace.at(-1).phase, 'response_undeliverable');
    assert.equal(trace.at(-1).reason, 'client_disconnected');
    assert.ok(!trace.some(e => e.phase === 'bridge_response'));
  });
});

test('trace redacts escaped secret strings and reports a write failure once', () => {
  const directory = mkdtempSync(join(tmpdir(), 'george-trace-test-'));
  const file = join(directory, 'trace.jsonl'), failures = [], key = 'secret"\\value';
  try {
    const trace = createTrace(file, { key, onError: reason => failures.push(reason) });
    assert.ok(trace.write({ content: `hello ${key}`, nested: { text: key } }));
    assert.deepEqual(readTrace([file])[0].nested, { text: '[REDACTED]' });
    rmSync(file); mkdirSync(file);
    assert.equal(trace.write({}), false); assert.equal(trace.write({}), false);
    assert.equal(trace.error, 'trace_write_failed');
    assert.deepEqual(failures, ['trace_write_failed']);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});

test('query joins game and bridge timestamps and ignores an unfinished live append', () => {
  const directory = mkdtempSync(join(tmpdir(), 'george-trace-test-'));
  const game = join(directory, 'game.jsonl'), bridge = join(directory, 'bridge.jsonl');
  try {
    const id = randomUUID();
    writeFileSync(game, JSON.stringify({ utc: '2026-10-09T10:00:00.1234560Z',
      source: 'game', requestId: id, phase: 'trigger' }) + '\n{"unfinished":');
    writeFileSync(bridge, JSON.stringify({ utc: '2026-10-09T10:00:00.124Z',
      source: 'bridge', requestId: id, phase: 'provider_request' }) + '\n');
    assert.deepEqual(readTrace([bridge, game], id).map(e => e.phase), ['trigger', 'provider_request']);
    assert.deepEqual(readTrace([bridge, game], randomUUID()), []);
  } finally { rmSync(directory, { recursive: true, force: true }); }
});


test('next preview reads the edited prompt and keeps one snapshot for provider and trace', async () => {
  let prompt = 'First user-authored prompt', sent;
  await withBridge({ promptLoader: () => prompt, generateImpl: (value, options) => generate(value, { ...options,
    fetchImpl: async (url, request) => { sent = JSON.parse(request.body); return provider('{"speak":false,"text":""}'); } }) },
    async ({ post, events }) => {
      const first = randomUUID(); await post(context, first);
      assert.equal(sent.messages[0].content, prompt);
      assert.deepEqual(events(first).find(e => e.phase === 'provider_request').request, sent);
      prompt = 'Second user-authored prompt';
      const second = randomUUID(); await post({ ...context, event: 'talk', visitorName: 'Ben' }, second);
      assert.equal(sent.messages[0].content, prompt);
      assert.equal(JSON.parse(sent.messages[1].content).visitorName, 'Ben');
      assert.deepEqual(events(second).find(e => e.phase === 'provider_request').request, sent);
    });
});
