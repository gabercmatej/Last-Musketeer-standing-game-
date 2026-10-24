import { getStore } from '@netlify/blobs';
import { authenticate, registerMatch, completeMatch, standings, HttpError } from '../lib/history-service.mjs';

export default async (request, context) => {
  const origin = request.headers.get('origin');
  const host = new URL(request.url).hostname;
  const canonical = context.site?.name + '.netlify.app';
  const isPreview = !context.deploy?.published || host !== canonical;
  const allowedOrigin = !origin || origin === new URL(request.url).origin || origin === 'https://' + canonical ||
    /^https:\/\/[a-z0-9-]+--last-musketeer\.netlify\.app$/.test(origin) || /^http:\/\/(localhost|127\.0\.0\.1):8765$/.test(origin);
  const headers = { 'Content-Type': 'application/json', 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff', 'Vary': 'Origin' };
  if (origin && allowedOrigin) Object.assign(headers, { 'Access-Control-Allow-Origin': origin, 'Access-Control-Allow-Headers': 'Authorization, Content-Type', 'Access-Control-Allow-Methods': 'GET, POST, OPTIONS' });
  const reply = (status, data) => new Response(JSON.stringify(data), { status, headers });
  if (!allowedOrigin) return reply(403, { Error: 'Origin is not allowed.' });
  if (request.method === 'OPTIONS') return new Response(null, { status: 204, headers });
  try {
    const name = isPreview ? 'musketeers-history-test-' + context.deploy.id : 'musketeers-history-v1';
    const store = getStore({ name, consistency: 'strong' });
    if (request.method === 'GET') {
      const { blobs } = await store.list({ prefix: 'matches/' });
      if (blobs.length > 2000) throw new HttpError(503, 'History needs pagination before more records can be displayed.');
      const matches = [];
      for (let i = 0; i < blobs.length; i += 16) matches.push(...await Promise.all(blobs.slice(i,i+16).map(b => store.get(b.key, { type: 'json' }))));
      const records = matches.filter(Boolean).sort((a,b) => b.CompletedUtc.localeCompare(a.CompletedUtc) || a.MatchId.localeCompare(b.MatchId));
      return reply(200, { Matches: records, Leaderboard: standings(records), Preview: isPreview });
    }
    if (request.method !== 'POST') return reply(405, { Error: 'Method not allowed.' });
    if (!request.headers.get('content-type')?.startsWith('application/json')) return reply(415, { Error: 'JSON is required.' });
    const body = await request.text();
    if (Buffer.byteLength(body) > 32768) return reply(413, { Error: 'Submission is too large.' });
    let value; try { value = JSON.parse(body); } catch { throw new HttpError(400, 'Invalid JSON.'); }
    if (!value || typeof value !== "object" || Array.isArray(value)) throw new HttpError(400, "A JSON object is required.");
    const player = await authenticate(request);
    if (value.Action === 'start') { await registerMatch(store, value, player, isPreview); return reply(200, { Registered: true, Preview: isPreview }); }
    if (value.Action === 'complete') { const created = await completeMatch(store, value, player); return reply(200, { Saved: true, Created: created, Preview: isPreview }); }
    return reply(400, { Error: 'Unknown action.' });
  } catch (error) {
    if (!(error instanceof HttpError)) console.error('Shared history request failed:', error.name);
    return reply(error.status || 503, { Error: error instanceof HttpError ? error.message : 'Shared history is temporarily unavailable.' });
  }
};
export const config = { path: '/api/match-history', rateLimit: { windowLimit: 60, windowSize: 60, aggregateBy: ['ip', 'domain'] } };
