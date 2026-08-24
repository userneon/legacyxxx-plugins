# LEGACY-X Admin

`LegacyX-Admin` is LEGACY-X's complete CounterStrikeSharp admin module. It is vendored from [`debr1sj/CS2-AdminPlus`](https://github.com/debr1sj/CS2-AdminPlus) at revision `1225a03ee4f393ea9c98c7b636327078c2914c08`, then renamed and adapted to LEGACY-X's central environment policy.

The upstream project is MIT-licensed. Its `LICENSE` remains included in this folder, and its author remains attributed in the runtime module metadata. The LEGACY-X customization does not claim authorship of the upstream admin system.

## Ownership

This module is the only owner of in-game admin menus, `css_*` moderation actions, bans, mutes, gags, reports, votes, reservations, player management, and optional Discord audit logging. Do not co-install the old minimal LEGACY-X AdminPlus bridge or another plugin that owns the same commands and state files.

## Central configuration

The module reads `CounterStrikeSharp/.env` through `LegacyX.Shared.Configuration`.

| Setting group | Purpose |
|---|---|
| `LEGACYX_ADMIN_ENABLED` | Enables or disables the module before command/event registration. |
| `LEGACYX_ADMIN_DISCORD_ENABLED` | Enables optional Discord audit delivery. |
| `LEGACYX_ADMIN_DISCORD_*_WEBHOOK` | Central-only Discord webhook values; never add these to plugin-local JSON. |
| `LEGACYX_SERVER_ADDRESS` | Shared display address for optional Discord status logs. |

The module's ban, admin, communication, and menu files are functional state files managed by upstream AdminPlus. They contain no API tokens, database credentials, or Discord webhooks.

## Languages

Upstream languages live in `lang/` and retain their complete keys. Every supplied locale uses the `LEGACY-X Admin` prefix. A complete Mongolian translation is intentionally a separate reviewed localization task; the plugin falls back to English when the client has no matching translation file.

## Build

```bash
dotnet build LegacyX-Admin.csproj --configuration Release
```

This repository contains source only. It does not install a CS2 server, write a real central environment file, or deploy any DLL.
