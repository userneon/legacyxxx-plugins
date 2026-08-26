# LEGACY-X CounterStrikeSharp Plugins

This repository contains **source only** for LEGACY-X CS2 server plugins. It has no game server, VPS installation, production secret, or deployed DLL. Every LEGACY-X module reads its server-side configuration from one future host file: `CounterStrikeSharp/.env`. The committed root `.env.example` is the only template.

## Canonical modules

| Module | Source project | Runtime assembly | Responsibility |
|---|---|---|---|
| LegacyX-Admin | `LegacyX-Admin/LegacyX-Admin.csproj` | `LegacyX-Admin.dll` | Full admin menus, bans, mute/gag, reports, votes, reservations; optional player `!admin` Call channel alert only |
| LegacyX-AFKManager | `LegacyX-AFKManager/LegacyX-AFKManager.csproj` | `LegacyX-AFKManager.dll` | MatchZy-aware AFK, C4 and spectator policy |
| LegacyX-Community | `LegacyX-Community/LegacyX-Community.csproj` | `LegacyX-Community.dll` | Player progress, rank and clan lookup through Root API |
| LegacyX-MatchZy | `LegacyX-MatchZy/LegacyX-MatchZy.csproj` | `LegacyX-MatchZy.dll` | Competitive match lifecycle and Match Core bridge |
| LegacyX-Reconnect | `LegacyX-Reconnect/LegacyX-Reconnect.csproj` | `LegacyX-Reconnect.dll` | Reconnect session events and `css_reconnect` |
| LegacyX-Spectator | `LegacyX-Spectator/LegacyX-Spectator.csproj` | `LegacyX-Spectator.dll` | Competitive spectator/alive communication isolation |
| LegacyX-WeaponPaints | `LegacyX-WeaponPaints/LegacyX-WeaponPaints.csproj` | `LegacyX-WeaponPaints.dll` | Root API-controlled skin/loadout claim, apply and acknowledgement |

`LegacyX-Admin` replaces the former minimal in-house AdminPlus bridge. It is based on MIT-licensed upstream source from `debr1sj/CS2-AdminPlus`; its license and attribution are retained under `LegacyX-Admin/`. The old bridge is not present, preventing duplicate admin command, ban, mute, gag, report, vote and reservation ownership.

## One central environment

The shared `LegacyX.Shared.Configuration` library is referenced by every project. It resolves future host process environment first, then `CounterStrikeSharp/.env`. Plugin-local JSON/cfg is allowed only for secret-free gameplay defaults or an upstream module's non-secret state.

> Never put `SUPABASE_SERVICE_ROLE_KEY`, `DATABASE_URL`, browser credentials, Root API service keys, RCON secrets, or plugin tokens in source or plugin-local JSON/cfg. Plugins speak to the LEGACY-X Root API using scoped tokens from the one central environment file.

## Build and future deployment

```bash
./scripts/build-all.sh
```

The source-only preparation, future host layout and intentional no-deploy boundary are documented in [Phase A deployment preparation](docs/PHASE_A_DEPLOYMENT_PREPARATION.md). The per-module environment and local configuration ownership matrix is in [Plugin Registry](docs/PLUGIN_REGISTRY.md).

## References

- [LEGACY-X plugin registry](docs/PLUGIN_REGISTRY.md)
- [LEGACY-X naming and admin replacement boundary](docs/LEGACYX_MODULE_NAMING_AND_ADMIN_REPLACEMENT.md)
- [LEGACY-X Admin upstream attribution](LegacyX-Admin/README.md)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp)
