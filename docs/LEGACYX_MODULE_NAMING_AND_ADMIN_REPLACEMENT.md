# LEGACY-X Module Naming and Admin Replacement

## Approved replacement boundary

The minimal custom `adminplus/plugin/AdminPlus` bridge is removed from the source repository. Its player-action commands are intentionally not retained because upstream AdminPlus becomes the sole owner of admin menus, bans, communication sanctions, reports, votes, reservations, and player/server command handling. This prevents duplicate `css_*` registration and conflicting file/state ownership.

The upstream source baseline is `debr1sj/CS2-AdminPlus` commit `1225a03ee4f393ea9c98c7b636327078c2914c08`. It is MIT-licensed. The copied `LICENSE` file and upstream attribution must remain with the LEGACY-X customized source.

## Canonical LEGACY-X module names

| Existing module | Canonical source folder | Runtime assembly / display name | Primary owner |
|---|---|---|---|
| Minimal AdminPlus bridge | `LegacyX-Admin/` | `LegacyX-Admin.dll` / `LEGACY-X Admin` | Full upstream AdminPlus, customized for LEGACY-X |
| AFKManager | `LegacyX-AFKManager/` | `LegacyX-AFKManager.dll` / `LEGACY-X AFK Manager` | AFK, C4, spectator and anti-camp policy |
| Community | `LegacyX-Community/` | `LegacyX-Community.dll` / `LEGACY-X Community` | Player-facing XP/rank/clan lookup |
| MatchZy | `LegacyX-MatchZy/` | `LegacyX-MatchZy.dll` / `LEGACY-X MatchZy` | Competitive lifecycle and Match Core bridge |
| Reconnect | `LegacyX-Reconnect/` | `LegacyX-Reconnect.dll` / `LEGACY-X Reconnect` | Last Played and reconnect session events |
| Spectator Comms | `LegacyX-Spectator/` | `LegacyX-Spectator.dll` / `LEGACY-X Spectator` | Anti-ghosting spectator/alive communication policy |
| WeaponPaints SkinBridge | `LegacyX-WeaponPaints/` | `LegacyX-WeaponPaints.dll` / `LEGACY-X WeaponPaints` | Website-controlled cosmetic claim/apply/ack |

## Central configuration policy

Every module continues to read the one future host file `CounterStrikeSharp/.env` through `LegacyX.Shared.Configuration`. The root `.env.example` is the canonical template. Plugin-local JSON/cfg remains only for secret-free behavior defaults and state that an upstream plugin owns locally.

The full admin plugin remains file-based for bans, admin groups, communication records and menu bindings. It does **not** receive Supabase, database, browser or backend service-role credentials. Its only optional Discord setting is the player `!admin` / report Call channel path; no other webhook adapter is present in source.

## Runtime and deployment boundary

This migration changes source names, assemblies, central configuration wiring, and localization preparation only. It does not install a CS2 server, deploy a DLL, create real tokens, configure the optional Call channel, copy runtime files, or delete/convert any live ban or moderation data.
