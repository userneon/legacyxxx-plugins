# LEGACY-X Reconnect

`LegacyXReconnect.dll` is the server-side source of truth for **Last Played** and a player-facing `css_reconnect` command. It records connect/disconnect sessions and sends a small heartbeat so the backend knows whether a recent server is still online.

## Commands

| Command | Behavior |
|---|---|
| `css_reconnect` | Looks up the player's latest online session on a different LEGACY-X server, then runs one validated reconnect command. |

The command never accepts an address from chat or a player-supplied profile field. The returned server address originates only from the backend's server registry, the plugin validates its `host:port` form again, and the backend rejects session events whose server ID/address pair is not present in `RECONNECT_SERVER_REGISTRY`.

## MatchZy boundary

MatchZy owns 5v5 readiness, live match state and soft map transitions. Reconnect only observes player connect/disconnect events; it does not move teams, change maps, restart matches or alter MatchZy ready state. A map transition normally keeps clients connected and does not require a reconnect event.

## Future host preparation

1. Build source with `dotnet build -c Release` when a compatible build environment is available.
2. Future CS2 host дээр repository root `.env.example`-ийг `CounterStrikeSharp/.env` болгон secret-free placeholder-оос нь server-local scoped token-тойгоор бэлдэнэ.
3. `LEGACYX_API_BASE_URL`, `LEGACYX_RECONNECT_PLUGIN_ID`, `LEGACYX_RECONNECT_PLUGIN_TOKEN`, `LEGACYX_SERVER_ID`, `LEGACYX_SERVER_ADDRESS` нь Reconnect-ийн ганц runtime identity source байна.
4. `config/LegacyXReconnect.json.example` нь зөвхөн mode, heartbeat, chat default агуулна; API URL, server ID/address, token оруулахгүй.
5. Server ID/address pair-ийг Root API-ийн `RECONNECT_SERVER_REGISTRY`-д reviewed backend deployment-оор бүртгэнэ.

Одоогоор CS2 server/VPS байхгүй учраас DLL copy, runtime config, API restart, server restart хийхгүй.

## Privacy

Session data is private by default. The backend returns it only through authenticated operator or the player-specific feature API. Do not publish current-server presence or reconnect history to a public profile until a separate user visibility preference is implemented.
