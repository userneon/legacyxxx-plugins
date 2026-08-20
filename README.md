# LEGACY-X Plugins

`legacyxxx-plugins` нь LEGACY-X community-ийн CS2 server-side plugin monorepo юм. Repository нь match lifecycle болон admin action-ийг тусдаа module болгон хадгалж, production server дээр давхар/зөрчилтэй event handler ажиллуулахгүй байх boundary-тай.

## Repository structure

```text
legacyxxx-plugins/
├── adminplus/
│   └── plugin/AdminPlus/
│       ├── AdminPlus.cs             # dashboard/admin command bridge
│       ├── AdminPlus.csproj
│       └── bin/                     # ignored build output
├── matchzy/
│   ├── *.cs                         # LEGACY-X MatchZy source
│   ├── MatchZy.csproj
│   ├── LegacyXCustomization.cs      # strict gate/map lifecycle
│   ├── cfg/MatchZy/                  # production config presets
│   ├── lang/                         # MatchZy language files
│   ├── spawns/                       # coach/spawn data
│   └── documentation/                # upstream technical docs
├── .gitignore
└── README.md
```

## Plugin responsibilities

| Module | Responsibility | Production note |
|---|---|---|
| `matchzy/` | Competitive match lifecycle, ready gate, demo/stats, practice, map rotation | Use this for Match server; it owns `EventCsWinPanelMatch` |
| `adminplus/` | Dashboard-triggered player/server/admin commands | Do not install its old match lifecycle file together with MatchZy |

Database audit, RCON bridge, Discord webhook and API are in the separate [`legacyxxx-backend`](https://github.com/userneon/legacyxxx-backend) repository. Frontend source is intentionally not in this repository.

## LEGACY-X MatchZy behavior

The customized MatchZy preset starts a match only when there are **exactly 5 Counter-Terrorists and exactly 5 Terrorists**, and all ten active human players have readied. `6v5`, `5v6`, `6v6`, empty slots and force-ready do not start a production match.

When a match ends, result/demo/stat persistence is allowed to complete, then the plugin shows a `PLEASE WAIT` message, clears MatchZy state and performs a soft `changelevel` to a random installed map that is different from the current map. The CS2 process is not hard-restarted.

## Build

Requirements: .NET 8 SDK, Metamod:Source and CounterStrikeSharp on the server.

```bash
# MatchZy
cd matchzy
dotnet build --configuration Release

# AdminPlus
cd ../adminplus/plugin/AdminPlus
dotnet build --configuration Release
```

Artifacts:

```text
matchzy/bin/Release/net8.0/MatchZy.dll
adminplus/plugin/AdminPlus/bin/Release/net8.0/AdminPlus.dll
```

## Deployment

Install each plugin in its own CounterStrikeSharp directory and copy the corresponding configuration assets.

```text
csgo/addons/counterstrikesharp/plugins/MatchZy/MatchZy.dll
csgo/addons/counterstrikesharp/plugins/AdminPlus/AdminPlus.dll
csgo/cfg/MatchZy/config.cfg
csgo/cfg/MatchZy/*.cfg
csgo/cfg/MatchZy/*.json
```

Only install one owner for match-end lifecycle. If MatchZy is enabled on a Match server, do not install a separate AdminPlus `MatchFlow` event handler. AdminPlus should remain the action bridge, while MatchZy owns 5v5 readiness and map transitions.

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

No `.env`, RCON secret, webhook URL, database password or API token belongs in this repository. Build output, local configs, `bin/`, `obj/`, `node_modules/` and secret files are ignored. Production secrets belong in `legacyxxx-backend` deployment configuration or the server secret store.

## References

- [LEGACY-X MatchZy README](matchzy/README.md)
- [LEGACY-X MatchZy customization notes](matchzy/README.md#legacy-x-customization)
- [LEGACY-X AdminPlus](adminplus/plugin/AdminPlus/)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
- [Upstream MatchZy](https://github.com/shobhit-pathak/MatchZy)
