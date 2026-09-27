# LEGACY-X Plugin Integration Matrix

| Plugin | Website / Root API contract | Database ownership | Required host values | Runtime validation |
|---|---|---|---|---|
| LegacyX-Admin | Player moderation, reports and server address are game-local. Only `!admin`/report may send an optional Call channel alert. | AdminPlus file state; no direct database access. | `LEGACYX_ADMIN_ENABLED`, server identity; optional `LEGACYX_ADMIN_CALL_CHANNEL_*`. | Module loads; report alert is tested only with a dedicated Call channel. |
| LegacyX-AFKManager | No website transaction contract. | No direct database access. | `LEGACYX_AFKMANAGER_ENABLED`. | AFK/C4/spectator policy on a staging server. |
| LegacyX-Community | Scoped Root API player-progress/clan lookup. | Root API only. | API base URL, server ID, `COMMUNITY_PLUGIN_ID/TOKEN`. | Token must be scoped to the intended server/module. |
| LegacyX-MatchZy | Optional Match Core event bridge. | Root API only when Match Core is enabled. | `MATCHZY_ENABLED`; Match Core URL/ID/token/server ID when enabled. | Start/end a staging match and inspect Root API audit/event result. |
| LegacyX-Spectator | Game-local chat/voice isolation. | No direct database access. | `LEGACYX_SPECTATOR_COMMS_ENABLED`. | Verify live-player versus spectator chat/voice isolation. |
| LegacyX-WeaponPaints | Website-created Skinchanger jobs are claimed and acknowledged by the plugin. | Root API owns loadout persistence and job state; plugin has no database credentials. | API base URL, server ID, `SKINBRIDGE_PLUGIN_ID/TOKEN`, poll seconds. | Equip a staging loadout; observe queued → applied/failed acknowledgement. |

## Mandatory boundaries

No plugin receives a Supabase credential, database password, website session/JWT or browser secret. No browser executes RCON, shell, SQL or plugin command. The Root API validates module identity, server identity and payloads before it writes database state or exposes a job.

## WeaponPaints end-to-end state

```text
Website loadout selection
  -> Root API validates player/session and persists loadout
  -> Root API queues a server-scoped Skinchanger job
  -> LegacyX-WeaponPaints claims the job with its scoped token
  -> plugin applies valid cosmetics for the connected SteamID
  -> plugin acknowledges applied or failed
  -> Root API records the job result for website visibility
```

The plugin deliberately does not implement game-to-database mutation commands. Website/API remains the only authoritative loadout writer.
