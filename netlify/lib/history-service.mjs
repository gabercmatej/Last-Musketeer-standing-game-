import { createPublicKey, verify, createHash } from 'node:crypto';

export const PROJECT = 'd55ea045-93c5-474d-a3e9-8003f0b0f578';
const ISSUER = 'https://player-auth.services.api.unity.com';
const CHARACTERS = ['aljaz', 'matej', 'matic', 'nik'];
export class HttpError extends Error { constructor(status, message) { super(message); this.status = status; } }
const requireValue = (valid, message) => { if (!valid) throw new HttpError(400, message); };
const integer = (n, min, max) => Number.isInteger(n) && n >= min && n <= max;
const text = (s, max) => typeof s === 'string' && s.length > 0 && s.length <= max && !/[\u0000-\u001f<>]/u.test(s);
const digest = value => createHash('sha256').update(JSON.stringify(value)).digest('hex');
let jwks, jwksAt = 0;

export function verifyUnityToken(token, keys, now = Date.now()) {
  try {
    if (typeof token !== 'string' || token.length > 12000) throw Error();
    const parts = token.split('.');
    if (parts.length !== 3) throw Error();
    const header = JSON.parse(Buffer.from(parts[0], 'base64url'));
    const claims = JSON.parse(Buffer.from(parts[1], 'base64url'));
    const key = keys.find(k => k.kid === header.kid && k.kty === 'RSA' && k.alg === 'RS256');
    if (header.alg !== 'RS256' || !key || !verify('RSA-SHA256', Buffer.from(parts[0] + '.' + parts[1]),
      createPublicKey({ key, format: 'jwk' }), Buffer.from(parts[2], 'base64url'))) throw Error();
    if (claims.iss !== ISSUER || claims.project_id !== PROJECT || !text(claims.sub, 128) ||
      !Number.isFinite(claims.exp) || claims.exp <= now / 1000 ||
      (claims.nbf !== undefined && (!Number.isFinite(claims.nbf) || claims.nbf > now / 1000 + 10))) throw Error();
    return claims.sub;
  } catch { throw new HttpError(401, 'A current game sign-in is required.'); }
}
export async function authenticate(request) {
  const value = request.headers.get('authorization') || '';
  if (!value.startsWith('Bearer ')) throw new HttpError(401, 'A current game sign-in is required.');
  if (!jwks || Date.now() - jwksAt > 8 * 3600000) {
    const response = await fetch(ISSUER + '/.well-known/jwks.json', { signal: AbortSignal.timeout(5000) });
    if (!response.ok) throw new HttpError(503, 'Game sign-in verification is temporarily unavailable.');
    jwks = (await response.json()).keys; jwksAt = Date.now();
  }
  return verifyUnityToken(value.slice(7), jwks);
}
export function validateRegistration(body, isPreview) {
  requireValue(/^[a-f0-9]{32}$/i.test(body.MatchId || ''), 'Invalid match ID.');
  requireValue(body.IsAutomated !== true || isPreview, 'Test matches cannot enter the public leaderboard.');
  requireValue(Array.isArray(body.Roster) && integer(body.Roster.length, 2, 4), 'Two to four humans are required.');
  const seen = new Set();
  const roster = body.Roster.map(p => {
    requireValue(p.IsBot === false && CHARACTERS.includes(p.CharacterId) && !seen.has(p.CharacterId), 'Bots or duplicate characters are ineligible.');
    requireValue(text(p.Name, 24), 'Invalid player name.'); seen.add(p.CharacterId);
    return { CharacterId: p.CharacterId, Name: p.Name, IsBot: false };
  });
  requireValue(['courtyard', 'bridge'].includes(body.MapId), 'Invalid map.');
  requireValue(integer(body.PointsToWin, 6, 30) && integer(body.Hearts, 1, 5), 'Invalid match settings.');
  return { MatchId: body.MatchId, Roster: roster, MapId: body.MapId, PointsToWin: body.PointsToWin,
    Hearts: body.Hearts, IsAutomated: body.IsAutomated === true };
}
export function validateCompletion(body, registration, now) {
  requireValue(body.Completed === true && integer(body.RoundCount, 1, 1000) && /^[a-f0-9]{8}$/i.test(body.FinalHash || ''), 'A completed match is required.');
  requireValue(now - registration.Started >= 10000 && now - registration.Started < 8 * 3600000, 'Match registration has expired or is too recent.');
  const record = body.Record;
  requireValue(record && record.MatchId === registration.MatchId && record.MapId === registration.MapId &&
    Array.isArray(record.Players) && record.Players.length === registration.Roster.length, 'Result roster does not match registration.');
  const seen = new Set();
  const players = record.Players.map(p => {
    const expected = registration.Roster.find(r => r.CharacterId === p.CharacterId);
    requireValue(expected && expected.Name === p.Name && !seen.has(p.CharacterId), 'Player identity changed.'); seen.add(p.CharacterId);
    requireValue(integer(p.Place, 1, record.Players.length) && integer(p.Points, -5000, 10000) &&
      integer(p.RoundWins, 0, body.RoundCount) && integer(p.Kills, 0, 4000) && integer(p.Deaths, 0, body.RoundCount), 'Invalid result statistics.');
    requireValue(Array.isArray(p.Badges) && p.Badges.length <= 32, 'Invalid badges.');
    const badges = p.Badges.map(b => { requireValue(/^[a-z0-9_-]{1,60}$/.test(b.Id || '') && text(b.Name, 100) && text(b.Description, 1000), 'Invalid badge.');
      return { Id: b.Id, Name: b.Name, Description: b.Description }; });
    return { CharacterId: p.CharacterId, Name: p.Name, Place: p.Place, Points: p.Points, RoundWins: p.RoundWins,
      Kills: p.Kills, Deaths: p.Deaths, Badges: badges };
  });
  const best = Math.max(...players.map(p => p.Points));
  requireValue(best >= registration.PointsToWin && players.some(p => p.Place === 1), 'Winning threshold was not reached.');
  requireValue(players.every(p => p.Place !== 1 || p.Points === best), 'Winning placements do not match points.');
  requireValue(players.reduce((n, p) => n + p.RoundWins, 0) <= body.RoundCount &&
    players.reduce((n, p) => n + p.Kills, 0) <= players.reduce((n, p) => n + p.Deaths, 0), 'Inconsistent statistics.');
  players.sort((a, b) => a.Place - b.Place || a.CharacterId.localeCompare(b.CharacterId, 'en'));
  return { MatchId: record.MatchId, MapId: record.MapId, Players: players, FinalHash: body.FinalHash, RoundCount: body.RoundCount };
}

export async function registerMatch(store, body, host, isPreview, now = Date.now()) {
  const value = validateRegistration(body, isPreview), key = 'pending/' + value.MatchId;
  const old = await store.get(key, { type: 'json' });
  if (old) { if (old.Host !== host || digest(old.Setup) !== digest(value)) throw new HttpError(409, 'Match registration conflicts.'); return; }
  // One host registration every20s. Conditional update prevents concurrent requests bypassing this limit.
  const rateKey = 'hosts/' + digest(host), rate = await store.getWithMetadata(rateKey, { type: 'json' });
  if (rate && now - rate.data.Last < 20000) throw new HttpError(429, 'Please wait before starting another recorded match.');
  const allowed = await store.setJSON(rateKey, { Last: now }, rate ? { onlyIfMatch: rate.etag } : { onlyIfNew: true });
  if (!allowed.modified) throw new HttpError(429, 'Please retry shortly.');
  const saved = await store.setJSON(key, { ...value, Setup: value, Host: host, Started: now }, { onlyIfNew: true });
  if (!saved.modified) { const prior = await store.get(key, { type: 'json' }); if (prior.Host !== host || digest(prior.Setup) !== digest(value)) throw new HttpError(409, 'Match registration conflicts.'); }
}
export async function completeMatch(store, body, host, now = Date.now()) {
  requireValue(body.Record && /^[a-f0-9]{32}$/i.test(body.Record.MatchId || ''), 'Invalid match ID.');
  const registration = await store.get('pending/' + body.Record.MatchId, { type: 'json' });
  if (!registration || registration.Host !== host) throw new HttpError(403, 'Only the registered match host can submit this result.');
  const normalized = validateCompletion(body, registration, now), proof = digest(normalized);
  const key = 'matches/' + normalized.MatchId;
  const existing = await store.get(key, { type: 'json' });
  if (existing) { if (existing.ReceiptHash !== proof) throw new HttpError(409, 'An immutable result already exists.'); return false; }
  const record = { ...normalized, CompletedUtc: new Date(now).toISOString(), ReceiptHash: proof };
  const saved = await store.setJSON(key, record, { onlyIfNew: true });
  if (!saved.modified) { const other = await store.get(key, { type: 'json' }); if (other.ReceiptHash !== proof) throw new HttpError(409, 'An immutable result already exists.'); }
  return saved.modified;
}
export function standings(matches) {
  return CHARACTERS.map(CharacterId => {
    const wins = matches.filter(m => m.Players.some(p => p.CharacterId === CharacterId && p.Place === 1));
    return { CharacterId, Wins: wins.length, LastWin: wins.map(m => m.CompletedUtc).sort().at(-1) || '' };
  }).sort((a,b) => b.Wins-a.Wins || b.LastWin.localeCompare(a.LastWin) || a.CharacterId.localeCompare(b.CharacterId, 'en'));
}
