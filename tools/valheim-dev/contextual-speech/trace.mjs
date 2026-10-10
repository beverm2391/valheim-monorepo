import { appendFileSync, mkdirSync, readFileSync, openSync, closeSync, realpathSync } from 'node:fs';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

// Append on each event, independently of a wrapper's stdout buffering. Only
// explicit trace payloads enter this file; never pass headers/provider errors.
export function createTrace(file, { key = '', onError = () => {} } = {}) {
  mkdirSync(dirname(file), { recursive: true, mode: 0o700 });
  closeSync(openSync(file, 'wx', 0o600));
  let error = null;
  return {
    file,
    get error() { return error; },
    write(event) {
      try {
        // Defense against accidental inclusion, including successful model text.
        const line = JSON.stringify({ utc: new Date().toISOString(), source: 'bridge', ...event },
          (_, value) => key && typeof value === 'string' ? value.split(key).join('[REDACTED]') : value);
        appendFileSync(file, line + '\n', { encoding: 'utf8', mode: 0o600 });
        return true;
      } catch {
        if (!error) onError('trace_write_failed');
        error = 'trace_write_failed';
        return false;
      }
    }
  };
}

export function readTrace(files, requestId) {
  return files.flatMap(file => {
    // Writers terminate every event with a newline. A live read may catch the
    // final append in progress; retrying will include it once it is complete.
    const lines = readFileSync(file, 'utf8').split('\n');
    lines.pop();
    return lines.filter(Boolean).map(line => JSON.parse(line))
      .filter(event => !requestId || event.requestId === requestId);
  })
    .sort((a, b) => Date.parse(a.utc) - Date.parse(b.utc));
}

if (process.argv[1] && fileURLToPath(import.meta.url) === realpathSync(process.argv[1])) {
  const args = process.argv.slice(2);
  let requestId;
  if (args[0] === '--request-id') { args.shift(); requestId = args.shift(); }
  if (!args.length || !requestId && process.argv.includes('--request-id'))
    throw new Error('Usage: node trace.mjs [--request-id UUID] FILE.jsonl ...');
  for (const event of readTrace(args, requestId)) console.log(JSON.stringify(event));
}
