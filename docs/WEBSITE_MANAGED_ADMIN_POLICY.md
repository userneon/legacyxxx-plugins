# Website-Managed In-Game Admin Policy

## Authority

The LEGACY-X website is the only production authority for creating, updating, suspending, or revoking in-game admin policy. The canonical identity is `legacy_x.users`; staff role, website permissions, game permissions, and immunity are stored only on `legacy_x.staff`.

When `LEGACYX_ADMIN_POLICY_SYNC_ENABLED=true`, LegacyX-Admin polls the Root API every 60 seconds by default. It writes an atomic local `admins.json` cache and refreshes its immunity map only after a complete signed policy response is received. If the API is unavailable, it retains the last valid cache and never clears server access because of a transient request failure.

`css_addadmin` and `css_removeadmin` are then informational only; they direct operators to the Owner-only Staff Panel. `css_adminreload` requests a safe policy refresh. This prevents console or chat commands from becoming a second source of authority.

## Required server-local environment

```dotenv
LEGACYX_ADMIN_POLICY_SYNC_ENABLED=true
LEGACYX_ADMIN_API_BASE_URL=https://api.legacyx.cc
LEGACYX_ADMIN_PLUGIN_ID=legacyx-admin
LEGACYX_ADMIN_PLUGIN_SECRET=SERVER_LOCAL_SCOPED_SECRET
LEGACYX_ADMIN_POLICY_REFRESH_SECONDS=60
```

The plugin secret remains only in the CounterStrikeSharp host `.env`. Do not place Supabase credentials, an Owner session cookie, or a browser token in the game server.

## API and database prerequisites

1. Apply `supabase/legacy_x_staff_panel.sql`.
2. Apply `supabase/legacy_x_staff_game_permissions.sql`.
3. Provision the `legacyx-admin` plugin identity with only `admin:read` scope and its server-local secret.
4. In the Owner Staff Panel, create/select an active staff record and set Steam identity, explicit `@css/*` game permissions, stamina from `0` through `1000`, and immunity from `0` through `1000`.
5. Enable policy sync and reload LegacyX-Admin.

The API returns only active staff records that have a valid SteamID64 and at least one explicit game permission. It never returns website sessions, passwords, raw database data, or staff audit records.

## Stamina and immunity

Stamina controls the command-threshold layer. The configured baseline is Owner `1000`, Manager `750`, Admin `500`, and non-admin roles `0`. The plugin applies the threshold table to player-issued chat commands, client-console commands, and menu actions. The dedicated server console remains a host-only emergency control and is not impersonated as a player.

Immunity is also an integer `0`–`1000`, but it is used only for target precedence: a larger value can outrank a smaller value in moderation checks. Stamina and immunity are independent fields even when their role baseline is the same.

Neither role, stamina, nor immunity grants a CounterStrikeSharp command on its own. Every privileged command still requires its separate selected `@css/*` permission. For example, an Admin with stamina `500` may meet the `!ban` threshold, but cannot ban until `@css/ban` is explicitly selected.

| Role | Suggested stamina | Suggested immunity |
|---|---:|---:|
| Owner | 1000 | 1000 |
| Manager | 750 | 750 |
| Admin | 500 | 500 |
| Developer / Designer / Player | 0 | 0 |

The full command threshold registry is compiled in `LegacyX-Admin/AdminPlus.Stamina.cs`. The key tiers are `250` for low-impact communication and information actions, `500` for standard moderation, `750` for high-impact server or ban administration, and `1000` for root-level and gameplay-altering operations.

## Safe rollback

Set `LEGACYX_ADMIN_POLICY_SYNC_ENABLED=false` and reload the plugin. The last generated `admins.json` cache remains intact. Do not manually edit the cache while sync is enabled; the next successful policy response deliberately replaces it.
