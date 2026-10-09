import { test } from 'node:test';
import assert from 'node:assert/strict';
import { createBridge, generate, parseRemark } from './bridge.mjs';

const context = { event: 'approach', dayPart: 'day', weather: 'Clear', biome: 'Meadows',
  wet: false, cold: false, tubBurning: true, playerSeated: false, recentRemarks: [] };
const silent = { speak: false, text: '' };

async function withBridge(options, run) {
  const records = [];
  const server = createBridge({ key: 'test-only-key', log: r => records.push(JSON.parse(r)), ...options });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const url = `http://127.0.0.1:${server.address().port}/remark`;
  try { await run(body => fetch(url, { method: 'POST', body: JSON.stringify(body) }).then(r => r.json()), records); }
  finally { server.closeAllConnections(); await new Promise(resolve => server.close(resolve)); }
}

test('game snapshot goes through OpenRouter envelope and returns bounded native text', async () => {
  await withBridge({ generateImpl: (value, options) => generate(value, { ...options,
    fetchImpl: async (url, request) => {
      assert.equal(url, 'https://openrouter.ai/api/v1/chat/completions');
      const body = JSON.parse(request.body);
      assert.deepEqual(JSON.parse(body.messages[1].content), context);
      assert.equal(body.response_format.json_schema.strict, true);
      assert.equal(request.headers.Authorization, 'Bearer test-only-key');
      return { ok: true, json: async () => ({ choices: [{ message: {
        content: '{"speak":true,"text":"The water is better than the wind."}' } }],
        usage: { total_tokens: 400 } }) };
    } }) }, async (post, records) => {
      assert.deepEqual(await post(context), { speak: true, text: 'The water is better than the wind.' });
      assert.equal(records[0].action, 'remark');
      assert.ok(!JSON.stringify(records).includes('test-only-key'));
    });
});

test('unbounded context and malformed model text fail to silence without leaking errors', async () => {
  let calls = 0;
  await withBridge({ generateImpl: async () => { calls++; throw new Error('private provider payload'); } },
    async (post, records) => {
      assert.deepEqual(await post({ ...context, inventory: ['private'] }), silent);
      assert.equal(calls, 0);
      assert.deepEqual(await post(context), silent);
      assert.equal(calls, 1);
      assert.ok(!JSON.stringify(records).includes('private'));
    });
  for (const remark of [{ speak: true, text: '<b>Hello</b>' }, { speak: false, text: 'Hello' },
    { speak: true, text: 'x'.repeat(141) }, { speak: true, text: '' }])
    assert.throws(() => parseRemark(remark));
});

test('timeout silences, concurrent approaches are dropped, and the paid call cap holds', async () => {
  await withBridge({ timeoutMs: 25, requestLimit: 1, generateImpl: (value, { signal }) =>
    new Promise((resolve, reject) => signal.addEventListener('abort', () => reject(new Error('aborted')))) },
    async (post, records) => {
      const first = post(context);
      assert.deepEqual(await post(context), silent);
      assert.deepEqual(await first, silent);
      assert.equal(records[0].reason, 'timeout_or_cancelled');
      assert.deepEqual(await post(context), silent);
      assert.equal(records.length, 1);
    });
});
