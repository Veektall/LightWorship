'use strict';

// NEWAGE WORSHIP optional self-hosted zero-charge relay.
// No billing integration, database, analytics or third-party API is required.
// TLS certificate/key paths and a strong shared bearer token are supplied by the operator.

const https = require('https');
const fs = require('fs');
const crypto = require('crypto');
const { URL } = require('url');

const port = Number(process.env.PORT || 9443);
const keyFile = process.env.TLS_KEY_FILE;
const certFile = process.env.TLS_CERT_FILE;
const token = process.env.NEWAGE_RELAY_TOKEN;
if (!keyFile || !certFile || !token || token.length < 24) {
  console.error('TLS_KEY_FILE, TLS_CERT_FILE and NEWAGE_RELAY_TOKEN (24+ chars) are required.');
  process.exit(2);
}

const sessions = new Map();
function state(id) {
  if (!sessions.has(id)) sessions.set(id, { commands: [], snapshot: null, touched: Date.now() });
  const s = sessions.get(id); s.touched = Date.now(); return s;
}
function auth(req) {
  const h = String(req.headers.authorization || '');
  const expected = Buffer.from('Bearer ' + token);
  const actual = Buffer.from(h);
  return actual.length === expected.length && crypto.timingSafeEqual(actual, expected);
}
function send(res, status, value) {
  const body = Buffer.from(JSON.stringify(value));
  res.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'content-length': body.length, 'cache-control': 'no-store', 'x-content-type-options': 'nosniff' });
  res.end(body);
}
function readJson(req, limit = 262144) {
  return new Promise((resolve, reject) => {
    const chunks = []; let n = 0;
    req.on('data', c => { n += c.length; if (n > limit) { reject(new Error('body too large')); req.destroy(); } else chunks.push(c); });
    req.on('end', () => { try { resolve(JSON.parse(Buffer.concat(chunks).toString('utf8') || '{}')); } catch (e) { reject(e); } });
    req.on('error', reject);
  });
}

const server = https.createServer({ key: fs.readFileSync(keyFile), cert: fs.readFileSync(certFile), minVersion: 'TLSv1.2' }, async (req, res) => {
  try {
    if (!auth(req)) return send(res, 401, { error: 'unauthorized' });
    const u = new URL(req.url, 'https://relay.invalid');
    const m = u.pathname.match(/^\/api\/session\/([^/]+)\/(commands|snapshot)$/);
    if (!m) return send(res, 404, { error: 'not found' });
    const id = decodeURIComponent(m[1]); if (!/^[A-Za-z0-9_-]{6,80}$/.test(id)) return send(res, 400, { error: 'invalid session' });
    const s = state(id);
    if (m[2] === 'commands' && req.method === 'GET') {
      const out = s.commands.splice(0, 20); return send(res, 200, out);
    }
    if (m[2] === 'commands' && req.method === 'POST') {
      const v = await readJson(req, 32768);
      const command = String(v.command || '').slice(0, 80);
      const payload = String(v.payload || '').slice(0, 8192);
      if (!command) return send(res, 400, { error: 'command required' });
      const item = { id: crypto.randomBytes(12).toString('hex'), command, payload, createdUtc: new Date().toISOString() };
      s.commands.push(item); if (s.commands.length > 100) s.commands.shift(); return send(res, 202, item);
    }
    if (m[2] === 'snapshot' && req.method === 'POST') {
      s.snapshot = await readJson(req); return send(res, 204, {});
    }
    if (m[2] === 'snapshot' && req.method === 'GET') return send(res, 200, s.snapshot || {});
    return send(res, 405, { error: 'method not allowed' });
  } catch (e) { return send(res, 400, { error: String(e.message || e) }); }
});

setInterval(() => {
  const cutoff = Date.now() - 24 * 60 * 60 * 1000;
  for (const [id, s] of sessions) if (s.touched < cutoff) sessions.delete(id);
}, 60 * 60 * 1000).unref();

server.listen(port, '0.0.0.0', () => console.log(`NEWAGE relay listening on ${port} with TLS 1.2+`));
