# LEGACY-X SkinBridge

`LEGACY-X SkinBridge` is the CounterStrikeSharp Skinchanger bridge for LEGACY-X. It is an **API-only** plugin: a player saves a loadout on `legacyx.cc`, the Root API queues an apply job, and this plugin claims, applies, then acknowledges that job on the CS2 server.

> Data path: **Website → Root API → apply job → SkinBridge → CS2 runtime**. The plugin never connects directly to Supabase, MySQL, or a browser.

## What it supports

SkinBridge applies the loadout categories sent by the Root API: weapon skins, knives, gloves, agents, music kits and pins. Team-scoped entries (`t`, `ct`, and both) are handled by the runtime bridge. Website-controlled loadouts work with in-game menus disabled; `!wp` and menu commands are intentionally disabled by default for LEGACY-X.

| Setting | Production default | Reason |
|---|---:|---|
| `EnableInGameMenus` | `false` | The LEGACY-X website is the loadout editor. |
| `CommandWpEnabled` | `false` | The bridge claims API jobs automatically. |
| `ApiPollSeconds` | `3` | Responsive without high-frequency API pressure; plugin clamps values to 1–30 seconds. |
| `PluginId` | `legacyx-skinbridge` | Required by Root API identity checks. |

## Requirements

The CS2 host needs CounterStrikeSharp API version 338 or newer. This project targets **.NET 8** and is built against `CounterStrikeSharp.API` 1.0.367. Copy `gamedata/weaponpaints.json` to `addons/counterstrikesharp/gamedata/weaponpaints.json`; the plugin stops safely if the gamedata file is missing.

The upstream license remains in `LICENSE`. Do not remove its notices when redistributing this LEGACY-X fork.

## Secret-safe configuration

`.env.example` is an operator template only. CounterStrikeSharp does **not** load `.env` for this plugin automatically. On the actual CS2 server, use the values to fill the generated plugin JSON file:

```text
addons/counterstrikesharp/configs/plugins/WeaponPaints/WeaponPaints.json
```

Start from [`config.example.json`](./config.example.json). The real `PluginSecret` must be a server-scoped Root API token with **both** `skinchanger:read` and `skinchanger:write` scopes. Never commit this token or paste it into frontend files.

```json
{
  "ConfigVersion": 10,
  "ApiBaseUrl": "https://api.legacyx.cc",
  "PluginId": "legacyx-skinbridge",
  "PluginSecret": "REPLACE_WITH_SERVER_SCOPED_PLUGIN_TOKEN",
  "ServerId": "REPLACE_WITH_LEGACYX_SERVER_ID",
  "ApiPollSeconds": 3,
  "EnableInGameMenus": false,
  "Website": "https://legacyx.cc/skinchanger"
}
```

`ServerId` must exactly match the server ID used by the LEGACY-X Root API. The plugin rejects an empty API URL, secret shorter than 24 characters, or empty server ID before it starts.

## Root API contract

SkinBridge sends a session event when a player connects or disconnects. It then claims jobs and acknowledges the result with the lease token supplied by the API.

| Operation | Endpoint | Result |
|---|---|---|
| Player session | `POST /api/v1/plugin/skinchanger/sessions` | Tracks which SteamID is currently on this server. |
| Claim | `GET /api/v1/plugin/skinchanger/jobs?server_id=…` | Returns time-limited loadout jobs for this exact server. |
| Acknowledge | `POST /api/v1/plugin/skinchanger/jobs/:jobId/ack` | Reports `applied` or `failed` with the lease token. |

If a player has disconnected before a claimed job is applied, the bridge reports `player_not_connected`. Runtime exceptions are acknowledged as `apply_error`. A lease token is single-use; an expired or invalid lease must not be treated as a successful apply.

## Build

```bash
cd weaponpaints-legacyx
dotnet restore WeaponPaints.csproj
dotnet build WeaponPaints.csproj -c Release
```

Copy the release output, `lang/`, and the gamedata file to the matching CounterStrikeSharp directories. Do not install or configure a game server from this repository automatically; validate the build and deploy it only on your intended CS2 host.

## Runtime checklist

1. Create a server-scoped SkinBridge API token in the Root API data store with `skinchanger:read` and `skinchanger:write`.
2. Put the real token only in the CS2 host's `WeaponPaints.json`.
3. Confirm `ServerId` is identical to the Root API server ID.
4. Load the plugin and confirm the server log does not report missing required API configuration or gamedata.
5. Connect a test Steam account, save a website loadout, and confirm the server log/API audit receives the session, claim, and `applied` acknowledgement.

## Security notes

Never expose `PluginSecret`, `SUPABASE_SERVICE_ROLE_KEY`, or database credentials to the plugin repository, browser, or screenshots. The plugin's only remote dependency is the Root API. Retry/backoff and restart-safe outbox hardening remain a future plugin runtime improvement; this repository provides the current API-compatible bridge and secret-safe deployment template.
