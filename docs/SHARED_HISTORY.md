# Shared match history

The Web client calls `/api/match-history` on its own Netlify origin. It registers eligible matches at start, then only the host submits the completed immutable result. Reads are public; writes require a current, RSA-verified Unity Authentication token for this game's project. No Netlify credentials are shipped in Web code.

The backend binds registration to the authenticated host, initial human-only roster and settings; checks completion/stat bounds, a minimum duration, payload size, name/badge text, winning threshold and a stable match ID; limits registrations per host and requests per IP. These safeguards do **not** make a browser-authoritative game cheat-proof: a deliberately modified host can falsify a roster or gameplay within the validation bounds. Anonymous Unity accounts can be created for the public project. Stronger competitive verification is outside this friends' release.

One final record per match uses Netlify Blobs `onlyIfNew`. Retries compare an immutable receipt hash; changed results cannot overwrite or duplicate a match. Completion time comes from the function. Standings derive from stored records; equal win counts use most recent victory, then ordinal character ID. Co-first characters each receive one win.

Production store: `musketeers-history-v1`, site-wide and retained across deployments. Non-published/preview hostnames use `musketeers-history-test-DEPLOY_ID`. Test data stays isolated and is never imported into production. Autotest clients do not register or submit matches; production rejects explicitly automated registrations too. Old local archives are never uploaded. GET refreshes on open, after completion and every20 seconds while the window is open.

A failed shared save preserves local results and explicitly exposes the local archive in Match History. Shared character standings continue using shared records only. Service failures never gate gameplay or Results. Three transient retries reuse the same match ID; unrecoverable registration/auth failures remain labeled local.

## Build and verify

- Pinned official SDK: `@netlify/blobs`11.1.5. User approved installation; lockfile committed. Node22.12+ required. `npm ci` for a clean checkout.
- `npm run test:history` covers auth, bot/test eligibility, immutable/concurrent writes, ownership, malformed completion, new IDs and recency/tied wins.
- `node tools/tests/shared-preview-smoke.mjs https://ALIAS--last-musketeer.netlify.app` verifies a real Unity token, rejected anonymous/bot/foreign-origin writes, one saved record and idempotent retry. It refuses production URLs and requires `Preview:true` before writing.
- `netlify deploy --dir Builds/Web --alias day6-final --no-build` deploys an isolated candidate, including functions from `netlify.toml`.
- Production remains the established `tools/deploy-web.sh` workflow. Verify `/api/match-history` returns `Preview:false` afterward. Netlify's previous published deploy can be restored through its deploy UI if release checks fail.

References: [Unity token verification](https://docs.unity.com/en-us/services-web-apis/client-auth), [Netlify Blobs conditional writes/site-wide stores](https://docs.netlify.com/build/data-and-storage/netlify-blobs/), [Function rate limits](https://docs.netlify.com/manage/security/secure-access-to-sites/rate-limiting/).
