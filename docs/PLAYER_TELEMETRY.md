# LEGACY-X Player Telemetry

`LegacyX-PlayerTelemetry` collects **server-side CS2 match telemetry only** and sends it to the Root API with a scoped `stats:write` plugin identity. It never connects directly to Supabase, never reads a player's PC processes, screen, local files, private IP address, or browser data.

## Collected events

| Event | Collected fields |
|---|---|
| `round_snapshot` | SteamID64, current round, active session seconds, kills, deaths, damage dealt/taken, map, server mode, match reference |
| `player_disconnected` | The same match snapshot plus disconnect round, `client_disconnect` method, and the bounded engine-reason availability marker |

The disconnect method is an **observed server lifecycle category**, not a claim about a player. CounterStrikeSharp's standard disconnect event does not provide a reliable client network failure reason; therefore the plugin records `client_disconnect` and `engine_disconnect_reason_unavailable` rather than inventing a reason. Future reviewed server/admin actions may report `admin_kick`, `admin_ban`, or `server_shutdown` explicitly.

## Derived analytics

The additive migration exposes player summary fields for average active seconds per match, average round reached, average disconnect round, disconnect count, total kills/deaths, damage dealt/taken, kill-death ratio, and damage exchange ratio. These are descriptive performance signals; they are not an automated ban, matchmaking, or staff-permission decision.

## Required host environment

```dotenv
LEGACYX_PLAYER_TELEMETRY_ENABLED=true
LEGACYX_PLAYER_TELEMETRY_PLUGIN_ID=legacyx-player-telemetry
LEGACYX_PLAYER_TELEMETRY_PLUGIN_TOKEN=REPLACE_WITH_SCOPED_TOKEN
```

`LEGACYX_API_BASE_URL`, `LEGACYX_SERVER_ID`, and `LEGACYX_SERVER_MODE` are shared values and must also be set in the root CounterStrikeSharp `.env`.

## Deployment order

1. Wait for Supabase MCP recovery; do not apply the migration through browser code.
2. Apply `supabase/legacy_x_player_telemetry.sql` through the approved database workflow.
3. Create the scoped Root API plugin identity `legacyx-player-telemetry` with `stats:write` only.
4. Place the compiled plugin and shared configuration DLL on the CS2 host, then add the central environment variables.
5. Run a controlled match, disconnect a test SteamID after a known round, and confirm one idempotent event per event ID.
