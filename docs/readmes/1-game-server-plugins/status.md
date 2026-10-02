> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/LegacyX-Status/README.md](https://github.com/userneon/legacyxxx-plugins/blob/main/LegacyX-Status/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LegacyX-Status

Reports the CS2 server to the LEGACY-X API every `LEGACYX_STATUS_INTERVAL_SECONDS` (30):

- `POST /api/v1/plugin/servers/heartbeat`: server id, name, connect address, GOTV address, map, mode and the human players on it (SteamID and name). The website's Play pages, home "Players Online", the Discord `/servers` and `/status` boards and "playing now" on profiles read this.
- `POST /api/v1/plugin/live-match/snapshots`: state (waiting, live, paused, ended), round, T and CT score, and each team's players with ping, kills, deaths and assists.

Game → API → database; nothing is stored on the game server. If the API can't be reached the server shows as offline on the website until the next report.

## Settings (`CounterStrikeSharp/.env`)

| Variable | |
|---|---|
| `LEGACYX_API_BASE_URL` | `https://api.legacyx.cc` |
| `LEGACYX_SERVER_ID` | Unique per server (`srv-1`) |
| `LEGACYX_SERVER_ADDRESS` | `host:port` players connect to |
| `LEGACYX_SERVER_MODE` | `competitive_5v5`, `fun…` or `proleague`: picks the Play page |
| `LEGACYX_SERVER_NAME` | Optional display name (default: `hostname`) |
| `LEGACYX_SERVER_GOTV_ADDRESS` | Optional GOTV `host:port` |
| `LEGACYX_STATUS_PLUGIN_TOKEN` | API token with `servers:write` |
| `LEGACYX_STATUS_PLUGIN_ID` | `legacyx-live-snapshot` (the snapshot route only accepts this) |
| `LEGACYX_STATUS_INTERVAL_SECONDS` | 10–60, default 30 |
| `LEGACYX_STATUS_ENABLED` | `false` turns it off |
| `LEGACYX_STATUS_KEEP_AWAKE` | default `true`: sets `sv_hibernate_when_empty 0` so an empty server keeps reporting (a hibernating server runs no timers) |

Without the token, server id or address it logs what is missing and sends nothing. The token is never logged.
