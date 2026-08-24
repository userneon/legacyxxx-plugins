# LEGACY-X Plugin Registry and Central Environment Ownership

Every listed plugin loads the same central `CounterStrikeSharp/.env` file through `LegacyX.Shared.Configuration`. A plugin-local JSON/cfg file may retain **secret-free gameplay defaults only**. It must not contain API tokens, database credentials, Supabase keys, RCON secrets, or server identity.

| Plugin | Existing source project | Responsibility | Central env requirement | Local config ownership |
|---|---|---|---|---|
| AdminPlus | `adminplus/plugin/AdminPlus/AdminPlus.csproj` | Server console action adapter | `LEGACYX_ADMINPLUS_ENABLED` | None; command behavior remains source-owned in Phase A |
| AFKManager | `afkmanager/AFKManager.csproj` | AFK, C4, spectator and anti-camp policy | `LEGACYX_AFKMANAGER_ENABLED` | JSON: gameplay thresholds and messages only |
| Community | `community/LegacyXCommunity.csproj` | Player progress and clan lookup | API base URL, server ID, module ID and token | JSON: enabled/chat presentation only |
| MatchZy | `matchzy/MatchZy.csproj` | Match lifecycle and Match Core bridge | module switch; Match Core URL, ID, token, server ID | cfg: non-secret MatchZy gameplay and map defaults only |
| Reconnect | `reconnect/LegacyXReconnect.csproj` | Session events and reconnect command | API base URL, server ID/address, module ID and token | JSON: heartbeat/chat defaults only |
| Spectator Comms | `spectator-comms/LegacyXSpectatorComms.csproj` | Chat/voice isolation | `LEGACYX_SPECTATOR_COMMS_ENABLED` | JSON: communication policy only |
| SkinBridge | `weaponpaints-legacyx/WeaponPaints.csproj` | Website-controlled cosmetic apply jobs | API base URL, server ID, module ID and token, poll interval | JSON: feature toggles/menu defaults only |

## Canonical variable ownership

```text
LEGACYX_API_BASE_URL
LEGACYX_SERVER_ID
LEGACYX_SERVER_ADDRESS
LEGACYX_<MODULE>_ENABLED
LEGACYX_<MODULE>_PLUGIN_ID
LEGACYX_<MODULE>_PLUGIN_TOKEN
```

`MATCHZY` has a nested `MATCH_CORE` integration namespace and SkinBridge additionally owns a bounded polling interval. See root `.env.example` for the complete placeholder list.

> The central environment intentionally has no `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`, `DATABASE_URL`, `STEAM_API_KEY`, or browser credential. Plugins communicate with the Root API only.
