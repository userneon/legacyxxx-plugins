# LEGACY-X CounterStrikeSharp Plugins

> **Монгол гарын авлага:** [docs/MANUAL_MN.md](docs/MANUAL_MN.md#part2): VPS суулгах, сервер нэмэх, GSLT, update, алдаа засах, бүгд жишээтэй.
> **Бүх командын лавлах:** [docs/MANUAL_MN.md](docs/MANUAL_MN.md#part4): VPS, CS2, Discord командууд, алдаа ба шалтгаан.

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

One token carries all of these scopes. Setting up a machine is one command on the VPS
(`legacyxxx-backend`): `node --env-file=.env scripts/create-game-server.mjs <host> "27015:competitive_5v5:LEGACY-X #1" "27016:fun:Fun #1"`
creates the token and writes the complete `.env`; unzip the package into `game/csgo/`, upload that
file as `addons/counterstrikesharp/.env`, restart. Nothing else to fill in.

On a host set up with `scripts/cs2-host.sh`, edit the `.env` in this repository's root (`cd legacyxxx-plugins && nano .env`);
Run `sudo ./scripts/cs2-host.sh env` once: it makes `addons/counterstrikesharp/.env` a link to that file, so an edit only needs a server restart.

Every CS2 server on a machine can share one install and that one `.env`: each process knows itself
by the port it was started with (`-port 27016`), so its id is `srv-27016`, its address
`<host>:27016`, and `LEGACYX_27016_…` lines apply to it alone (see `.env.example`).

## Run CS2 servers on a Linux VPS

`scripts/cs2-host.sh` sets up a fresh Ubuntu VPS: steamcmd, the CS2 dedicated server, Metamod,
CounterStrikeSharp, these plugins and the `.env`, then one systemd service per server (`cs2@<port>`),
all from one shared install:

```bash
sudo ./scripts/cs2-host.sh install --env legacyx-srv-HOST.env     # once
sudo ./scripts/cs2-host.sh add 27015 <GSLT> competitive de_dust2  # per server
sudo ./scripts/cs2-host.sh add 27016 <GSLT> casual de_dust2 20 fun LEGACY-X FUN #1
sudo ./scripts/cs2-host.sh deploy                                 # git pull + update, now
sudo ./scripts/cs2-host.sh update                                 # update without pulling
./scripts/cs2-host.sh status | logs 27015
```

Updates run by themselves: `install` turns on a timer (`autoupdate on|off|check`) that looks every 10
minutes. A new CS2 build is applied at once, and so is the CounterStrikeSharp release that follows it
(plugins are down until it arrives); a newer CounterStrikeSharp on its own and new commits of this
repository wait for 05:00 (`AUTOUPDATE_HOUR`) so matches are not cut. Every update re-patches
`gameinfo.gi` and keeps `.env`, `configs/` (incl. `core.json`) and each server's
`/etc/legacyx/cs2/<port>.conf`. After each update `scripts/fix-gamedata.py` checks WeaponPaints'
signature against the new `libserver.so`; if it no longer matches exactly once, it takes the one
maintained by WeaponPaints upstream or swiftlys2 that does, and keeps retrying every 10 minutes
while none does yet (skins stay off meanwhile; the servers keep running). A signature found that way
is loaded at the next quiet-hour restart. Log: `journalctl -u legacyx-cs2-autoupdate`.

## Deploy on a Linux host (terminal)

On the game VPS, in a checkout of this repository (Metamod and CounterStrikeSharp already on the servers):

```bash
git pull
./scripts/deploy.sh --env legacyx-srv-HOST.env /path/to/cs2/game/csgo [/path/to/another/game/csgo …]
```

It builds the package (or takes `--package legacyx-cs2.zip`, no .NET needed), copies only the plugin
files into each `game/csgo`, installs the `.env` made by `create-game-server.mjs` (an existing one is
kept when `--env` is left out) and touches nothing else. Restart the servers afterwards. Updating
later is the same command without `--env`. On a game panel (no terminal), upload the unzipped
`addons` and `cfg` folders into `game/csgo` and create `addons/counterstrikesharp/.env` there instead.

Website skins (WeaponPaints) and Tab rank icons (Community) write item and rank fields that
CounterStrikeSharp blocks while `FollowCS2ServerGuidelines` is `true`, its default. `deploy.sh` sets it
to `false` in `addons/counterstrikesharp/configs/core.json`; on a game panel, edit that line by hand.
Valve's server guidelines disallow changing items, so this is a deliberate choice for the server's GSLT.

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
In-game staff come from the website only: see [Admin authorization](docs/MANUAL_MN.md#part6).

Installing and running a host is documented in the [deployment guide](docs/MANUAL_MN.md#part2). The per-module environment and local configuration ownership matrix is in [plugin chapter of the manual](docs/MANUAL_MN.md#part5).

## References

- [LEGACY-X plugin registry](docs/MANUAL_MN.md#part5)
- [LEGACY-X naming and admin replacement boundary](docs/MANUAL_MN.md#part5)
- [LEGACY-X Admin upstream attribution](LegacyX-Admin/README.md)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
