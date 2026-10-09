import http from 'node:http';
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const prompt = readFileSync(new URL('./george.txt', import.meta.url), 'utf8');
export const MODEL = 'google/gemini-2.5-flash-lite';
const silence = { speak: false, text: '' };
const schema = {
  type: 'object', additionalProperties: false, required: ['speak', 'text'],
  properties: { speak: { type: 'boolean' }, text: { type: 'string' } }
};

export function parseRemark(value) {
  if (!value || Object.keys(value).sort().join(',') !== 'speak,text' ||
      typeof value.speak !== 'boolean' || typeof value.text !== 'string')
    throw new Error('invalid_remark');
  const text = value.text.trim();
  // Native TMP interprets tags. Keep the model's output plain bounded speech.
  if ((!value.speak && value.text !== '') || (value.speak && !text) ||
      text.length > 140 || /[<>\x00-\x1f\x7f]/u.test(text))
    throw new Error('invalid_remark');
  return { speak: value.speak, text };
}

export function parseContext(value) {
  const fields = ['event', 'dayPart', 'weather', 'biome', 'wet', 'cold',
    'tubBurning', 'playerSeated', 'recentRemarks'];
  if (!value || Object.keys(value).sort().join(',') !== fields.sort().join(','))
    throw new Error('invalid_context');
  if (value.event !== 'approach' || !['morning', 'day', 'evening', 'night'].includes(value.dayPart))
    throw new Error('invalid_context');
  for (const key of ['weather', 'biome'])
    if (typeof value[key] !== 'string' || value[key].length > 64 || /[<>\x00-\x1f]/u.test(value[key]))
      throw new Error('invalid_context');
  for (const key of ['wet', 'cold', 'tubBurning', 'playerSeated'])
    if (typeof value[key] !== 'boolean') throw new Error('invalid_context');
  if (!Array.isArray(value.recentRemarks) || value.recentRemarks.length > 3 ||
      value.recentRemarks.some(t => typeof t !== 'string' || t.length > 140 || /[<>\x00-\x1f]/u.test(t)))
    throw new Error('invalid_context');
  return value;
}

export async function generate(context, { key, model = MODEL, signal, fetchImpl = fetch }) {
  const response = await fetchImpl('https://openrouter.ai/api/v1/chat/completions', {
    method: 'POST', signal,
    headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json',
      'X-OpenRouter-Title': 'Valheim George Sandbox' },
    body: JSON.stringify({ model, max_tokens: 120, temperature: .8,
      response_format: { type: 'json_schema', json_schema: {
        name: 'resident_remark', strict: true, schema } },
      messages: [{ role: 'system', content: prompt },
        { role: 'user', content: JSON.stringify(context) }] })
  });
  // Never log provider bodies: error responses can echo request headers/data.
  if (!response.ok) throw new Error(`provider_http_${response.status}`);
  const body = await response.json();
  return { remark: parseRemark(JSON.parse(body.choices?.[0]?.message?.content)),
    tokens: body.usage?.total_tokens ?? null };
}

export function createBridge({ key, model = MODEL, timeoutMs = 8000,
  requestLimit = 100, generateImpl = generate, log = console.log }) {
  let busy = false, calls = 0;
  const emit = data => log(JSON.stringify({ component: 'george-speech', ...data }));
  const server = http.createServer(async (req, res) => {
    res.setHeader('Content-Type', 'application/json');
    res.setHeader('Cache-Control', 'no-store');
    const reply = data => { if (!res.destroyed) res.end(JSON.stringify(data)); };
    if (req.method === 'GET' && req.url === '/health')
      return reply({ ready: true, model, calls, busy, requestLimit });
    if (req.method !== 'POST' || req.url !== '/remark') {
      res.statusCode = 404; return reply(silence);
    }
    // No queues or retries: an old approach should never wait behind another.
    if (busy || calls >= requestLimit) return reply(silence);
    busy = true;
    const started = performance.now();
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs);
    res.on('close', () => { if (!res.writableEnded) controller.abort(); });
    try {
      let raw = '';
      for await (const chunk of req) {
        raw += chunk;
        if (raw.length > 4096) throw new Error('request_too_large');
      }
      const context = parseContext(JSON.parse(raw));
      calls++;
      const result = await generateImpl(context, { key, model, signal: controller.signal });
      if (controller.signal.aborted) throw new Error('timeout_or_cancelled');
      reply(result.remark);
      emit({ action: result.remark.speak ? 'remark' : 'silence', call: calls,
        durationMs: Math.round(performance.now() - started), tokens: result.tokens });
    } catch (error) {
      reply(silence);
      const reason = controller.signal.aborted ? 'timeout_or_cancelled' :
        /^(invalid_context|invalid_remark|request_too_large|provider_http_\d+)$/.test(error.message)
          ? error.message : 'request_failed';
      emit({ action: 'silence', reason, durationMs: Math.round(performance.now() - started) });
    } finally { clearTimeout(timer); busy = false; }
  });
  server.requestTimeout = timeoutMs + 1000;
  server.headersTimeout = timeoutMs + 1000;
  return server;
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const key = process.env.OPENROUTER_API_KEY;
  if (!key) throw new Error('OPENROUTER_API_KEY is required in the process environment');
  const port = Number(process.env.GEORGE_SPEECH_PORT ?? 18741);
  if (!Number.isInteger(port) || port < 1024 || port > 65535) throw new Error('Invalid port');
  const server = createBridge({ key });
  server.listen(port, '127.0.0.1', () => console.log(JSON.stringify({
    component: 'george-speech', action: 'ready', port, model: MODEL, requestLimit: 100 })));
  for (const signal of ['SIGINT', 'SIGTERM']) process.on(signal, () => {
    server.close(); server.closeAllConnections();
  });
}
