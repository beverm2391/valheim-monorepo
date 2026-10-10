import http from 'node:http';
import { readFileSync, realpathSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { join } from 'node:path';
import { randomUUID } from 'node:crypto';
import { createTrace } from './trace.mjs';

const prompt = readFileSync(new URL('./george.txt', import.meta.url), 'utf8');
export const MODEL = 'google/gemini-3.5-flash-lite';
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

export function modelRequest(context, model) {
  return { model, max_tokens: 120, temperature: .8,
    response_format: { type: 'json_schema', json_schema: {
      name: 'resident_remark', strict: true, schema } },
    messages: [{ role: 'system', content: prompt },
      { role: 'user', content: JSON.stringify(context) }] };
}

export async function generate(context, { key, model = MODEL, signal, fetchImpl = fetch,
  onProviderReply = () => {} }) {
  const response = await fetchImpl('https://openrouter.ai/api/v1/chat/completions', {
    method: 'POST', signal,
    headers: { Authorization: `Bearer ${key}`, 'Content-Type': 'application/json',
      'X-OpenRouter-Title': 'Valheim George Sandbox' },
    body: JSON.stringify(modelRequest(context, model))
  });
  // Never log provider bodies: error responses can echo request headers/data.
  if (!response.ok) throw new Error(`provider_http_${response.status}`);
  let body;
  try { body = await response.json(); } catch { throw new Error('provider_response_json'); }
  const content = body?.choices?.[0]?.message?.content;
  const usage = Object.fromEntries(['prompt_tokens', 'completion_tokens', 'total_tokens', 'cost']
    .filter(k => Number.isFinite(body?.usage?.[k])).map(k => [k, body.usage[k]]));
  const actualModel = typeof body?.model === 'string' && /^[\w./:-]{1,128}$/.test(body.model) ? body.model : model;
  let parsed, parsedOk = false;
  try { parsed = JSON.parse(content); parsedOk = true; } catch { /* Recorded before validation below. */ }
  onProviderReply({ model: actualModel, usage,
    content: typeof content === 'string' ? content.slice(0, 4096) : null,
    contentTruncated: typeof content === 'string' && content.length > 4096,
    // Keep only dialogue fields, never arbitrary extra provider/model fields.
    parsedReply: parsed && typeof parsed === 'object' ? {
      speak: typeof parsed.speak === 'boolean' ? parsed.speak : null,
      text: typeof parsed.text === 'string' ? parsed.text.slice(0, 4096) : null } : null });
  if (typeof content !== 'string') throw new Error('model_content_missing');
  if (!parsedOk) throw new Error('model_reply_json');
  return { remark: parseRemark(parsed), model: actualModel, usage,
    tokens: usage.total_tokens ?? null };
}

export function createBridge({ key, model = MODEL, timeoutMs = 8000,
  requestLimit = 100, generateImpl = generate, log = console.log, traceFile } = {}) {
  let busy = false, calls = 0;
  const emit = data => log(JSON.stringify({ component: 'george-speech', ...data }));
  const sessionId = randomUUID();
  const trace = createTrace(traceFile ?? join(process.env.GEORGE_TRACE_DIR ??
    fileURLToPath(new URL('./traces/', import.meta.url)), `bridge-${sessionId}.jsonl`),
  { key, onError: reason => emit({ action: 'trace_error', reason }) });
  trace.write({ sessionId, phase: 'session_started', model, timeoutMs, requestLimit });
  const server = http.createServer(async (req, res) => {
    res.setHeader('Content-Type', 'application/json');
    res.setHeader('Cache-Control', 'no-store');
    const reply = data => { if (!res.destroyed) res.end(JSON.stringify(data)); };
    if (req.method === 'GET' && req.url === '/health')
      return reply({ ready: true, model, calls, busy, requestLimit, sessionId,
        traceFile: trace.file, traceError: trace.error });
    if (req.method !== 'POST' || req.url !== '/remark') {
      res.statusCode = 404; return reply(silence);
    }
    const validId = id => typeof id === 'string' && /^[\da-f]{8}(-[\da-f]{4}){3}-[\da-f]{12}$/i.test(id);
    const headerId = req.headers['x-george-request-id'];
    let requestId = validId(headerId) ? headerId : randomUUID();
    let ownsCall = false, abortReason, completed;
    const started = performance.now();
    const duration = () => Math.round(performance.now() - started);
    const record = event => trace.write({ sessionId, requestId, ...event });
    const respond = (remark, reason, metadata = {}) => {
      const result = { requestId, ...remark, reason, model, durationMs: duration(), tokens: -1, ...metadata };
      record({ phase: res.destroyed ? 'response_undeliverable' : 'bridge_response', ...result });
      reply(result);
      emit({ action: remark.speak ? 'remark' : 'silence', requestId, reason,
        durationMs: result.durationMs, tokens: result.tokens });
    };
    const controller = new AbortController();
    const abort = reason => {
      if (controller.signal.aborted) return;
      abortReason = reason;
      record({ phase: 'request_cancelled', reason, durationMs: duration() });
      controller.abort();
    };
    const timer = setTimeout(() => abort('provider_timeout'), timeoutMs);
    res.on('close', () => { if (!res.writableEnded) abort('client_disconnected'); });
    try {
      let raw = '';
      for await (const chunk of req) {
        raw += chunk;
        if (raw.length > 4096) throw new Error('request_too_large');
      }
      let input;
      try { input = JSON.parse(raw); } catch { throw new Error('request_json'); }
      if (!input || Object.keys(input).sort().join(',') !== 'context,requestId' ||
          !validId(input.requestId) || headerId && input.requestId !== headerId)
        throw new Error('invalid_request_id');
      requestId = input.requestId;
      const context = parseContext(input.context);
      record({ phase: 'request_received', context });
      // No queues or retries: an old approach never waits behind another.
      if (busy || calls >= requestLimit) return respond(silence, busy ? 'bridge_busy' : 'call_limit');
      if (controller.signal.aborted) throw new Error(abortReason);
      busy = ownsCall = true;
      calls++;
      record({ phase: 'provider_request', call: calls, request: modelRequest(context, model) });
      const result = await generateImpl(context, { key, model, signal: controller.signal,
        onProviderReply: evidence => { completed = evidence;
          record({ phase: 'provider_completed', durationMs: duration(), ...evidence }); } });
      if (!completed) record({ phase: 'provider_completed', durationMs: duration(),
        parsedReply: result.remark, model: result.model ?? model, usage: result.usage ?? {} });
      if (controller.signal.aborted) throw new Error(abortReason);
      respond(parseRemark(result.remark), result.remark.speak ? 'model_speech' : 'model_silence', {
        model: result.model ?? model, tokens: result.tokens ?? -1 });
    } catch (error) {
      const reason = controller.signal.aborted ? abortReason :
        /^(invalid_context|invalid_remark|invalid_request_id|request_json|request_too_large|provider_response_json|model_content_missing|model_reply_json|provider_http_\d+)$/.test(error.message)
          ? error.message : 'provider_network_failure';
      record({ phase: 'request_failed', reason, durationMs: duration() });
      respond(silence, reason);
    } finally { clearTimeout(timer); if (ownsCall) busy = false; }
  });
  server.on('close', () => trace.write({ sessionId, phase: 'session_ended', calls }));
  server.requestTimeout = timeoutMs + 1000;
  server.headersTimeout = timeoutMs + 1000;
  return server;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === realpathSync(process.argv[1])) {
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
