# LEGACY-X Plugin Registry and Central Environment Ownership

Every listed plugin loads the same central `CounterStrikeSharp/.env` file through `LegacyX.Shared.Configuration`. A plugin-local JSON/cfg file may retain **secret-free gameplay defaults only**. It must not contain API tokens, database credentials, Supabase keys, RCON secrets, or server identity.

| Plugin | Canonical source project | Responsibility | Central env requirement | Local config ownership |
|---|---|---|---|---|
| LegacyX-Admin | `LegacyX-Admin/LegacyX-Admin.csproj` | Full upstream admin, ban/mute/gag/report/vote/reservation/menu system | `LEGACYX_ADMIN_ENABLED`; optional Discord webhooks only in central env | File-based admin/ban/communication/menu state; no secret JSON |
| LegacyX-AFKManager | `LegacyX-AFKManager/LegacyX-AFKManager.csproj` | AFK, C4, spectator and anti-camp policy | `LEGACYX_AFKMANAGER_ENABLED` | JSON: gameplay thresholds and messages only |
| LegacyX-Community | `LegacyX-Community/LegacyX-Community.csproj` | Player progress and clan lookup | API base URL, server ID, module ID and token | JSON: enabled/chat presentation only |
| LegacyX-MatchZy | `LegacyX-MatchZy/LegacyX-MatchZy.csproj` | Match lifecycle and Match Core bridge | module switch; Match Core URL, ID, token, server ID | cfg: non-secret MatchZy gameplay and map defaults only |
| LegacyX-Reconnect | `LegacyX-Reconnect/LegacyX-Reconnect.csproj` | Session events and reconnect command | API base URL, server ID/address, module ID and token | JSON: heartbeat/chat defaults only |
| LegacyX-Spectator | `LegacyX-Spectator/LegacyX-Spectator.csproj` | Chat/voice isolation | `LEGACYX_SPECTATOR_COMMS_ENABLED` | JSON: communication policy only |
| LegacyX-WeaponPaints | `LegacyX-WeaponPaints/LegacyX-WeaponPaints.csproj` | Website-controlled cosmetic apply jobs | API base URL, server ID, module ID and token, poll interval | JSON: feature toggles/menu defaults only |

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

`ADMIN` owns full in-game moderation and menu commands. The previous minimal custom AdminPlus bridge is deliberately absent so no duplicate `css_*`, ban, mute, gag, report or reservation ownership remains.

> The central environment intentionally has no `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY`, `DATABASE_URL`, `STEAM_API_KEY`, or browser credential. Plugins communicate with the Root API only.
