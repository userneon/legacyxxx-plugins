> **Хуулбар.** Эх сурвалж: [legacyxxx-frontend/README.md](https://github.com/userneon/legacyxxx-frontend/blob/main/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X Frontend

This repository contains the LEGACY-X community-facing React frontend. The original visual design, color system, routes, components, motion, and UI/UX are retained. Integration work is intentionally limited to endpoint adapters, environment configuration, and truthful loading/error/empty states.

## Run locally

Install with the checked-in lockfile, then supply only a public API origin. Browser environment files must never contain `API_SECRET`, plugin secrets, RCON credentials, or Supabase service-role credentials.

```bash
npm ci
VITE_API_URL=https://api.legacy-x.example npm run dev
```

Build the static bundle with:

```bash
npm run build
```

## Current public integrations

The application’s data layer uses browser-safe, rate-limited read endpoints under `/api/public` for rank, XP, community, reconnect-heartbeat server directory, and overview statistics. These calls are made without operator or plugin credentials.

| Frontend area | Browser-safe endpoint | State |
|---|---|---|
| Leaderboard | `GET /api/public/rank/leaderboard` + `GET /api/public/community/experience` | Integrated |
| Live servers | `GET /api/public/servers` | Integrated from reconnect heartbeats |
| Home statistics | `GET /api/public/overview` | Integrated from truthful server/clan/match read models |
| Public player community data | `GET /api/public/community/players/:steamId` | Backend-ready adapter boundary |
| Steam login, wallet, store, skinchanger, tournament and write actions | Future authenticated consumer API | Not fabricated; protected UI states remain in place |

## Security boundary

The existing AdminPlus operator API (`/api/*`) remains server/operator-only. It is not a browser API. The `x-api-secret` header and all plugin/server secrets stay outside the frontend bundle. Public CORS is restricted by the API’s `FRONTEND_PUBLIC_ORIGINS` allowlist; authenticated consumer endpoints will require a separate Steam-authenticated API layer.

## Deployment note

Set `VITE_API_URL` at build time to the production API origin. Configure the same frontend origin in the API host’s `FRONTEND_PUBLIC_ORIGINS` comma-separated allowlist. Do not use wildcard CORS for user-authenticated routes.
