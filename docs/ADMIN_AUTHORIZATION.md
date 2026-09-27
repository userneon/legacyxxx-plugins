# LegacyX-Admin: in-game staff authorization

In-game staff permissions come **only** from the LEGACY-X website/database, through the Root API.
The game server stores no staff list: no SteamIDs in source, no `admins.json` written or read,
no `addadmin`. Anything the plugin cannot confirm with the API is treated as a normal player.

## Flow

```text
player connects ──► OnClientAuthorized
                     │ 1. strip any CounterStrikeSharp admin data for that SteamID
                     │ 2. POST /api/v1/plugin/admin/authorizations {serverId, steamIds}
                     ▼
Root API (scope admin:read, x-plugin-id legacyx-admin)
                     │ legacy_x.resolve_game_staff(server_id, steam_ids)
                     │   1. staff_server_assignments row for this server  → decides for this server
                     │   2. otherwise the global legacy_x.staff role        → applies to every server
                     │   3. otherwise player
                     ▼
answer ──► AuthorizationStore (in memory) ──► main thread: AdminManager.AddPlayerPermissions +
                                               SetPlayerImmunity, AdminPlus stamina/immunity maps
every AUTH_REFRESH_SECONDS: re-check all online players (revokes / role changes)
every 5 s: drop grants past expires_at or not re-confirmed within AUTH_CACHE_SECONDS
disconnect: grant and admin data removed; a lookup still in flight is ignored
unload / hot reload: all granted admin data removed / everyone re-checked
```

## Fail closed

| Situation | Result |
|---|---|
| `LEGACYX_ADMIN_API_BASE_URL` / `PLUGIN_SECRET` / `PLUGIN_ID` / `LEGACYX_SERVER_ID` missing or invalid | Nobody receives permissions; logged once at load |
| API unreachable, timeout (5 s), HTTP error, non-JSON, wrong `serverId`, duplicate entries | New players get nothing; existing grants are kept only until `AUTH_CACHE_SECONDS` after their last confirmation, then removed |
| Entry with `authorized` not `true`, `status` not `active`, unknown role, `role: player`, past or unreadable `expiresAt` | That player gets nothing |
| Requested SteamID missing from the answer | Player |
| Stale answer (older request, or the player left meanwhile) | Ignored |
| Leftover entries in `configs/admins.json` | Ignored: each player's admin data is replaced by the API answer; a warning is logged |

Tokens are never logged. Error messages carry only the HTTP status or failure kind.

## API contract

`POST /api/v1/plugin/admin/authorizations`

Headers: `Authorization: Bearer <LEGACYX_ADMIN_PLUGIN_SECRET>` (token with scope `admin:read`),
`x-plugin-id: legacyx-admin`, `Content-Type: application/json`.

```json
{ "serverId": "eu-5v5-1", "steamIds": ["76561198000000001", "76561198000000002"] }
```

- `serverId`: `LEGACYX_SERVER_ID`, 1–64 of `A-Z a-z 0-9 . _ : -`.
- `steamIds`: 1–64 SteamID64 values (the plugin splits larger servers into several requests).

`200`:

```json
{
  "serverId": "eu-5v5-1",
  "checkedAt": "2026-09-27T12:00:00.000Z",
  "players": [
    { "steamId": "76561198000000001", "authorized": true,  "role": "admin",  "status": "active",  "source": "server", "expiresAt": "2026-10-01T00:00:00.000Z" },
    { "steamId": "76561198000000002", "authorized": false, "role": "player", "status": "revoked", "source": "global", "expiresAt": null }
  ]
}
```

- One entry per distinct requested SteamID. `role`: `owner | manager | admin | staff | player`.
- `status`: `active | suspended | revoked | expired | none`. `source`: `server | global | none`.
- `authorized` is `true` only for a staff role, `status: active` and no past `expiresAt`.
- Errors: `401` missing/invalid token, `403` wrong scope or plugin identity, `400` invalid body.
- `Cache-Control: no-store`.

## Database

`legacy_x.staff_server_assignments` (service role only, RLS on):

| column | notes |
|---|---|
| `steam_id` | SteamID64 |
| `server_id` | the server's `LEGACYX_SERVER_ID` |
| `role` | `owner`, `manager`, `admin`, `staff` |
| `status` | `active`, `suspended`, `revoked` |
| `expires_at` | optional; the role ends at this time |
| unique | `(steam_id, server_id)` |

Global staff stay in `legacy_x.staff` (as before, they apply on every server): `OWNER` → owner,
`MANAGER` → manager, `ADMIN` → admin while `status = 'active'`. `DEVELOPER`/`DESIGNER` are website
roles with no in-game authority. A per-server row overrides the global role on that server, including
revoking it there. Migration: backend `supabase/legacy_x_game_staff_authorization.sql`.

## Permission mapping

Defined once in `LegacyX-Admin/Authorization/StaffPermissions.cs`.

| Role | CounterStrikeSharp flags | Immunity | Stamina |
|---|---|---:|---:|
| owner | `@css/root` + generic, kick, ban, unban, slay, changemap, chat, vote, config, cvar, rcon, cheats | 1000 | 1000 |
| manager | generic, kick, ban, unban, slay, changemap, chat, vote, config | 750 | 750 |
| admin | generic, kick, ban, slay, changemap, chat, vote | 500 | 500 |
| staff | generic, chat, vote | 250 | 250 |
| player | — | 0 | 0 |

Stamina is AdminPlus' per-command threshold (`AdminPlus.Stamina.cs`): 250 communication/info, 500
standard moderation, 750 high-impact (unban, map, slay), 1000 root and gameplay-altering commands.
A command needs both its stamina and its `@css/*` flag. MatchZy admin commands need `@css/config`
(manager and owner). `!calladmin` reaches online ADMIN/MANAGER/OWNER, `!callmanager` MANAGER/OWNER.

`addadmin` / `removeadmin` only answer that staff are managed on legacyx.cc. `adminlist` prints the
authorized staff online; `adminreload` re-checks everyone with the API now. The in-game
"ADMINISTRATION" menu is a read-only list of online staff.

## Configuration (`CounterStrikeSharp/.env`)

```dotenv
LEGACYX_SERVER_ID=eu-5v5-1
LEGACYX_ADMIN_API_BASE_URL=https://api.legacyx.cc
LEGACYX_ADMIN_PLUGIN_ID=legacyx-admin
LEGACYX_ADMIN_PLUGIN_SECRET=<token with admin:read>
LEGACYX_ADMIN_AUTH_REFRESH_SECONDS=60     # 15-600
LEGACYX_ADMIN_AUTH_CACHE_SECONDS=180      # 30-3600, above the refresh
```

When several servers share one CounterStrikeSharp folder, give each process its own server id by
pointing `LEGACYX_ENV_FILE` at a per-server env file (it is read before `CounterStrikeSharp/.env`).
Without distinct ids, server-specific assignments cannot tell the servers apart; global staff still work.

Removed settings: `LEGACYX_ADMIN_POLICY_SYNC_ENABLED`, `LEGACYX_ADMIN_POLICY_REFRESH_SECONDS`
(`scripts/validate-plugin-runtime.mjs` reports them as stale).

## Logs

All lines start with `[LegacyX.Admin]`: configuration problems, `authorized as ROLE`, `role changed`,
`revoked`, `expired or could not be re-confirmed`, lookup failures (at most once a minute).

## Tests

```bash
dotnet test tests/LegacyX.Admin.Authorization.Tests        # role mapping, parser, client, cache, live HTTP API
./scripts/package.sh && dotnet run --project tests/LegacyX.PackageLoadTest -- dist/legacyx-cs2
```
