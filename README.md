# LEGACY-X CounterStrikeSharp Plugins

This repository contains **source only** for LEGACY-X CS2 server plugins. It has no game server, VPS installation, production secret, or deployed DLL. Every LEGACY-X module reads its server-side configuration from one future host file: `CounterStrikeSharp/.env`. The committed root `.env.example` is the only template.

## Canonical modules

| Module | Source project | Runtime assembly | Responsibility |
|---|---|---|---|
| LegacyX-Admin | `LegacyX-Admin/LegacyX-Admin.csproj` | `LegacyX-Admin.dll` | Full admin menus, bans, mute/gag, reports, votes, reservations; bans, unbans, mutes and gags are also recorded through the Root API; optional player `!admin` Call channel alert only |
| LegacyX-AFKManager | `LegacyX-AFKManager/LegacyX-AFKManager.csproj` | `LegacyX-AFKManager.dll` | MatchZy-aware AFK, C4 and spectator policy |
| LegacyX-Killfeed | `LegacyX-Killfeed/LegacyX-Killfeed.csproj` | `LegacyX-Killfeed.dll` | Every kill to the Root API for the website's live kill feed |
| LegacyX-Community | `LegacyX-Community/LegacyX-Community.csproj` | `LegacyX-Community.dll` | Player progress, rank and clan lookup through Root API |
| LegacyX-MatchZy | `LegacyX-MatchZy/LegacyX-MatchZy.csproj` | `LegacyX-MatchZy.dll` | Competitive match lifecycle and Match Core bridge |
| LegacyX-Spectator | `LegacyX-Spectator/LegacyX-Spectator.csproj` | `LegacyX-Spectator.dll` | Competitive spectator/alive communication isolation |
| LegacyX-Status | `LegacyX-Status/LegacyX-Status.csproj` | `LegacyX-Status.dll` | Server heartbeat (map, mode, players) and live score to the Root API every 30 s |
| LegacyX-WeaponPaints | `LegacyX-WeaponPaints/LegacyX-WeaponPaints.csproj` | `LegacyX-WeaponPaints.dll` | Root API-controlled skin/loadout claim, apply and acknowledgement |

`LegacyX-Admin` replaces the former minimal in-house AdminPlus bridge. It is based on MIT-licensed upstream source from `debr1sj/CS2-AdminPlus`; its license and attribution are retained under `LegacyX-Admin/`. The old bridge is not present, preventing duplicate admin command, ban, mute, gag, report, vote and reservation ownership.

## Data flow

Game → Root API → database, like the website and the Discord bot: no plugin opens a database
connection. What each module sends or reads, and the token scope it needs:

| Module | Root API routes | Token scopes |
|---|---|---|
| LegacyX-Admin | `/plugin/admin/authorizations`, `/plugin/bans/check`, `/plugin/bans`, `/plugin/bans/revoke`, `/plugin/bans/revoke-all` (owner `!cleanbans`), `/plugin/penalties`, `/plugin/penalties/revoke` | `admin:read bans:read bans:write` |
| LegacyX-Community | `/plugin/community/players/:steamId` | `stats:write` |
| LegacyX-MatchZy | `/plugin/matchzy/events` (rank), `/plugin/match-core/events` | `stats:write`, `matches:write` |
| LegacyX-Status | `/plugin/servers/heartbeat`, `/plugin/live-match/snapshots` | `servers:write` |
| LegacyX-Killfeed | `/plugin/killfeed/events` | `servers:write` |
| LegacyX-WeaponPaints | `/plugin/skinchanger/loadout` | `skinchanger:read` |
| LegacyX-AFKManager, LegacyX-Spectator | none | none |

One server token carries all of these scopes. Setting up a server is one command on the VPS
(`legacyxxx-backend`): `node --env-file=.env scripts/create-game-server.mjs <server-id> <host:port> [mode] [name]`
creates the token and writes the complete `.env`; unzip the package into `game/csgo/`, upload that
file as `addons/counterstrikesharp/.env`, restart. Nothing else to fill in.

## One central environment

The shared `LegacyX.Shared.Configuration` library is referenced by every project. It resolves future host process environment first, then `CounterStrikeSharp/.env`. Plugin-local JSON/cfg is allowed only for secret-free gameplay defaults or an upstream module's non-secret state.

> Never put `SUPABASE_SERVICE_ROLE_KEY`, `DATABASE_URL`, browser credentials, Root API service keys, RCON secrets, or plugin tokens in source or plugin-local JSON/cfg. Plugins speak to the LEGACY-X Root API using scoped tokens from the one central environment file.

## Build, test and package

```bash
./scripts/build-all.sh                                   # compile every plugin (Release)
dotnet test tests/LegacyX.Admin.Authorization.Tests      # LegacyX-Admin authorization unit tests
./scripts/package.sh                                     # → dist/legacyx-cs2 (runtime files only)
dotnet run --project tests/LegacyX.PackageLoadTest -- dist/legacyx-cs2   # load the package like CounterStrikeSharp does
```

`dist/legacyx-cs2/` mirrors `game/csgo/` on the server; copy its contents there:

```text
addons/counterstrikesharp/
  plugins/LegacyX-Admin/            LegacyX-Admin.dll, .deps.json, lang/
  plugins/LegacyX-AFKManager/       LegacyX-AFKManager.dll, .deps.json
  plugins/LegacyX-Community/        LegacyX-Community.dll, .deps.json
  plugins/LegacyX-Killfeed/         LegacyX-Killfeed.dll, .deps.json
  plugins/LegacyX-MatchZy/          LegacyX-MatchZy.dll, .deps.json, CsvHelper, Dapper, Microsoft.Data.Sqlite,
                                    MySqlConnector, Newtonsoft.Json, SQLitePCLRaw.*, runtimes/{linux,win}-x64 SQLite, lang/, spawns/
  plugins/LegacyX-Spectator/        LegacyX-Spectator.dll, .deps.json
  plugins/LegacyX-Status/           LegacyX-Status.dll, .deps.json
  plugins/LegacyX-WeaponPaints/     LegacyX-WeaponPaints.dll, .deps.json, MenuManagerApi, Newtonsoft.Json, lang/, gamedata/
  shared/LegacyX.Shared.Configuration/LegacyX.Shared.Configuration.dll   (loaded once for all plugins)
  .env.example                      copy to .env and fill in
cfg/MatchZy/                        MatchZy cfg files
MANIFEST.sha256
```

CounterStrikeSharp.API and its dependencies (Microsoft.Extensions.*, McMaster, Serilog, …) are never
packaged: the server's CounterStrikeSharp install provides them. The package script fails if one slips in.
In-game staff come from the website only: see [Admin authorization](docs/ADMIN_AUTHORIZATION.md).

The source-only preparation, future host layout and intentional no-deploy boundary are documented in [Phase A deployment preparation](docs/PHASE_A_DEPLOYMENT_PREPARATION.md). The per-module environment and local configuration ownership matrix is in [Plugin Registry](docs/PLUGIN_REGISTRY.md).

## References

- [LEGACY-X plugin registry](docs/PLUGIN_REGISTRY.md)
- [LEGACY-X naming and admin replacement boundary](docs/LEGACYX_MODULE_NAMING_AND_ADMIN_REPLACEMENT.md)
- [LEGACY-X Admin upstream attribution](LegacyX-Admin/README.md)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
