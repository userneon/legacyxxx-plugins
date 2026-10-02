> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/LegacyX-Admin/README.md](https://github.com/userneon/legacyxxx-plugins/blob/main/LegacyX-Admin/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр байна. Энэ доторх харьцангуй (relative) холбоосууд ажиллахгүй байж болно.

# LEGACY-X Admin

`LegacyX-Admin` is LEGACY-X's complete CounterStrikeSharp admin module. It is vendored from [`debr1sj/CS2-AdminPlus`](https://github.com/debr1sj/CS2-AdminPlus) at revision `1225a03ee4f393ea9c98c7b636327078c2914c08`, then renamed and adapted to LEGACY-X's central environment policy.

The upstream project is MIT-licensed. Its `LICENSE` remains included in this folder, and its author remains attributed in the runtime module metadata. The LEGACY-X customization does not claim authorship of the upstream admin system.

## Ownership

This module is the only owner of in-game admin menus, `css_*` moderation actions, bans, mutes, gags, reports, votes, reservations and player management. Its only optional Discord behavior is a player `!admin` / report Call channel alert; no moderation audit, chat, connection, status or match event is delivered to Discord. Do not co-install the old minimal LEGACY-X AdminPlus bridge or another plugin that owns the same commands and state files.

## Central configuration

The module reads `CounterStrikeSharp/.env` through `LegacyX.Shared.Configuration`.

| Setting group | Purpose |
|---|---|
| `LEGACYX_ADMIN_ENABLED` | Enables or disables the module before command/event registration. |
| `LEGACYX_ADMIN_CALL_CHANNEL_ENABLED` | Enables the optional player `!admin` / report Call channel alert. |
| `LEGACYX_ADMIN_CALL_CHANNEL_WEBHOOK` | The one central Call channel webhook; never add it to plugin-local JSON. |
| `LEGACYX_ADMIN_CALL_CHANNEL_MENTION` | Optional exact Discord role mention for the Call channel; `@everyone` and `@here` are rejected. |
| `LEGACYX_SERVER_ADDRESS` | Shared address included only in a Call channel alert. |
| `LEGACYX_ADMIN_CENTRAL_BANS_ENABLED` | Checks players against the central ban list in the LEGACY-X database (bans issued from the Discord bot) on connect and every `LEGACYX_ADMIN_CENTRAL_BANS_SWEEP_SECONDS` (default 30), and removes banned players. Uses `LEGACYX_ADMIN_API_BASE_URL` and `LEGACYX_ADMIN_PLUGIN_SECRET`; the token needs the `bans:read` scope. Fails open when the API is unreachable. |

Local bans (`css_ban`, the `!admin` menu) still live in `banned_user.cfg` on each server. Central bans live in the database and apply on every server. From the console or RCON, `css_ban` and `css_unban` accept a SteamID64, `STEAM_X:Y:Z` or `[U:1:N]` for players who are not online.

The module's ban, admin, communication, and menu files are functional state files managed by upstream AdminPlus. They contain no API tokens, database credentials, or webhooks.

## Languages

Upstream languages live in `lang/` and retain their complete keys. Every supplied locale uses the `LEGACY-X Admin` prefix. A complete Mongolian translation is intentionally a separate reviewed localization task; the plugin falls back to English when the client has no matching translation file.

## Build

```bash
dotnet build LegacyX-Admin.csproj --configuration Release
```

This repository contains source only. It does not install a CS2 server, write a real central environment file, or deploy any DLL.
