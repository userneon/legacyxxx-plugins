# LEGACY-X Reconnect

`LegacyXReconnect.dll` is the server-side source of truth for **Last Played** and a player-facing `css_reconnect` command. It records connect/disconnect sessions and sends a small heartbeat so the backend knows whether a recent server is still online.

## Commands

| Command | Behavior |
|---|---|
| `css_reconnect` | Looks up the player's latest online session on a different LEGACY-X server, then runs one validated reconnect command. |

The command never accepts an address from chat or a player-supplied profile field. The returned server address originates only from the backend's server registry, the plugin validates its `host:port` form again, and the backend rejects session events whose server ID/address pair is not present in `RECONNECT_SERVER_REGISTRY`.

## MatchZy boundary

MatchZy owns 5v5 readiness, live match state and soft map transitions. Reconnect only observes player connect/disconnect events; it does not move teams, change maps, restart matches or alter MatchZy ready state. A map transition normally keeps clients connected and does not require a reconnect event.

## Installation

1. Build with `dotnet build -c Release`.
2. Copy `bin/Release/net8.0/LegacyXReconnect.dll` to `csgo/addons/counterstrikesharp/plugins/LegacyXReconnect/`.
3. Copy `config/LegacyXReconnect.json.example` to the CounterStrikeSharp plugin config location as `LegacyXReconnect.json` and insert the production API URL, plugin secret, server ID and public connect address.
4. Add the same `server_id=address` pair to backend `RECONNECT_SERVER_REGISTRY`.
5. Apply `legacy_x_reconnect.sql`, restart the API and then restart the CS2 server.

## Privacy

Session data is private by default. The backend returns it only through authenticated operator or the player-specific feature API. Do not publish current-server presence or reconnect history to a public profile until a separate user visibility preference is implemented.
