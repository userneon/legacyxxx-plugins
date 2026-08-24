# LEGACY-X Plugins

`legacyxxx-plugins` нь LEGACY-X community-ийн CS2 server-side plugin monorepo юм. Repository нь match lifecycle болон admin action-ийг тусдаа module болгон хадгалж, production server дээр давхар/зөрчилтэй event handler ажиллуулахгүй байх boundary-тай.

## Repository structure

```text
legacyxxx-plugins/
├── .env.example                     # one central secret-free template
├── LegacyX.Plugins.sln
├── shared/LegacyX.Shared.Configuration/
├── CounterStrikeSharp/               # future runtime placeholders only
├── configs/                          # future secret-free defaults registry
├── scripts/                          # source-only build/package/deploy placeholders
├── adminplus/
│   └── plugin/AdminPlus/
│       ├── AdminPlus.cs             # API-triggered admin command bridge
│       ├── AdminPlus.csproj
│       └── bin/                     # ignored build output
├── matchzy/
│   ├── *.cs                         # LEGACY-X MatchZy source
│   ├── MatchZy.csproj
│   ├── LegacyXCustomization.cs      # strict gate/map lifecycle
│   ├── RANK_BRIDGE.md               # plugin → API → database rank contract
│   ├── cfg/MatchZy/                  # production config presets
│   ├── lang/                         # MatchZy language files
│   ├── spawns/                       # coach/spawn data
│   └── documentation/                # upstream technical docs
├── afkmanager/                       # MatchZy-aware AFK policy
│   ├── AFKManager.cs
│   ├── AFKManager.csproj
│   ├── config/AFKManager.json        # LEGACY-X config sample
│   └── README.md
├── community/                        # XP/level/rank/clan profile commands
│   ├── LegacyXCommunity.cs
│   ├── LegacyXCommunity.csproj
│   ├── config/LegacyXCommunity.json.example
│   └── README.md
├── reconnect/                        # Last Played and safe reconnect command
│   ├── LegacyXReconnect.cs
│   ├── LegacyXReconnect.csproj
│   ├── config/LegacyXReconnect.json.example
│   └── README.md
├── spectator-comms/                  # Anti-ghosting spectator/alive comms policy
│   ├── LegacyXSpectatorComms.cs
│   ├── LegacyXSpectatorComms.csproj
│   ├── config/LegacyXSpectatorComms.json.example
│   └── README.md
├── weaponpaints-legacyx/             # API-only SkinBridge loadout runtime
├── .gitignore
├── MATCHZY_LEGACYX_CHANGELOG.md
├── AFKMANAGER_LEGACYX_CHANGELOG.md
└── README.md
```

## Plugin responsibilities

| Module | Responsibility | Production note |
|---|---|---|
| `matchzy/` | Competitive match lifecycle, ready gate, demo/stats, practice, map rotation and final rank payload | Use this for Match server; it owns `EventCsWinPanelMatch` and emits final `map_result` |
| `afkmanager/` | AFK warning, C4 transfer and spectator transfer policy | Skips MatchZy warmup; does not own map/match lifecycle |
| `adminplus/` | API-triggered player/server/admin commands | Do not install its old match lifecycle file together with MatchZy |
| `community/` | Player-facing XP, level, rank and clan lookup | Read-only; backend owns all progression and clan writes |
| `reconnect/` | Private Last Played session events and `css_reconnect` | Observes connect/disconnect only; never owns MatchZy lifecycle |
| `spectator-comms/` | Spectator/dead-only text routing and competitive voice baseline | Never changes player/team/map state; MatchZy commands pass through |
| `weaponpaints-legacyx/` | API-only SkinBridge loadout claim/apply/ack runtime | Receives scoped Root API identity from central environment; never connects directly to database |

Database audit, RCON bridge, Discord webhook, rank scoring and API are in the separate [`legacyxxx-backend`](https://github.com/userneon/legacyxxx-backend) repository. AdminPlus is frontendless; UI source is intentionally not in this repository.

## LEGACY-X MatchZy behavior

The customized MatchZy preset starts a match only when there are **exactly 5 Counter-Terrorists and exactly 5 Terrorists**, and all ten active human players have readied. `6v5`, `5v6`, `6v6`, empty slots and force-ready do not start a production match.

When a match ends, result/demo/stat persistence is allowed to complete, then the plugin shows a `PLEASE WAIT` message, clears MatchZy state and performs a soft `changelevel` to a random installed map that is different from the current map. The CS2 process is not hard-restarted.

The same final map result includes the two five-player rosters, SteamID64 identities, winner, score and player stats. It is delivered with a server-only `x-plugin-secret` to the AdminPlus API, which applies idempotent rank, XP/level and clan season updates. See [`matchzy/RANK_BRIDGE.md`](matchzy/RANK_BRIDGE.md).

## Build

Requirements for source build: .NET 8 SDK. CounterStrikeSharp runtime validation is a future server task.

```bash
./scripts/build-all.sh
```

Artifacts:

```text
matchzy/bin/Release/net8.0/MatchZy.dll
afkmanager/bin/Release/net8.0/AFKManager.dll
adminplus/plugin/AdminPlus/bin/Release/net8.0/AdminPlus.dll
community/bin/Release/net8.0/LegacyXCommunity.dll
reconnect/bin/Release/net8.0/LegacyXReconnect.dll
spectator-comms/bin/Release/net8.0/LegacyXSpectatorComms.dll
```

## Future deployment preparation

There is currently no approved CS2 server or VPS. Phase A does **not** deploy DLLs, create production secrets, or install CounterStrikeSharp. The future host layout and source-only preparation are documented in [`docs/PHASE_A_DEPLOYMENT_PREPARATION.md`](docs/PHASE_A_DEPLOYMENT_PREPARATION.md).

All modules use one central `CounterStrikeSharp/.env` file, derived from the repository root `.env.example`. The runtime loader reads API base URL, server identity, module enable switches and scoped plugin tokens from this one source. Plugin-local JSON/cfg remains only for secret-free gameplay defaults.

Only install one owner for match-end lifecycle. If MatchZy is enabled on a Match server, do not install a separate AdminPlus `MatchFlow` event handler. AFK Manager skips MatchZy warmup and must not register a second map changer. AdminPlus remains the frontendless action bridge, while MatchZy owns 5v5 readiness, rank payloads and map transitions.

## Commands

MatchZy player/admin commands include:

```text
css_ready / .ready
css_unready / .unready
css_start
css_restart
css_endmatch
css_match_status
```

AdminPlus commands include player info, team movement, respawn, money, weapons, HP, freeze, god mode, slap and controlled server actions. See `adminplus/plugin/AdminPlus/` for the source and the backend integration contract.

## Security and secrets

No real `.env`, RCON secret, webhook URL, database password or API token belongs in this repository. The committed root `.env.example` is the **only** environment template. On a future host, its real counterpart is `CounterStrikeSharp/.env` and all LEGACY-X plugins read it through `LegacyX.Shared.Configuration`. Build output, local runtime configs, `bin/`, `obj/`, `node_modules/` and secret files are ignored. Do not put `SUPABASE_SERVICE_ROLE_KEY`, `DATABASE_URL`, Steam key, browser credential or plugin token in plugin-local JSON/cfg files.

## References

- [LEGACY-X MatchZy README](matchzy/README.md)
- [LEGACY-X MatchZy customization notes](matchzy/README.md#legacy-x-customization)
- [LEGACY-X MatchZy Rank Bridge](matchzy/RANK_BRIDGE.md)
- [LEGACY-X AFK Manager](afkmanager/README.md)
- [LEGACY-X AFK Manager customization report](AFKMANAGER_LEGACYX_CHANGELOG.md)
- [LEGACY-X Community EXP & Clan changelog](COMMUNITY_LEGACYX_CHANGELOG.md)
- [LEGACY-X Community plugin](community/README.md)
- [LEGACY-X Reconnect plugin](reconnect/README.md)
- [LEGACY-X Spectator Comms plugin](spectator-comms/README.md)
- [LEGACY-X SkinBridge](weaponpaints-legacyx/README.md)
- [Plugin registry and central environment ownership](docs/PLUGIN_REGISTRY.md)
- [LEGACY-X AdminPlus](adminplus/plugin/AdminPlus/)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
- [Upstream MatchZy](https://github.com/shobhit-pathak/MatchZy)
