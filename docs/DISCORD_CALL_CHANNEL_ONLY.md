# Discord Call Channel Only

LEGACY-X does not use Discord as a website, match, telemetry, moderation or server-status integration.

The sole allowed outbound Discord event is an in-game player `!admin` / report call. When explicitly enabled with the central `LEGACYX_ADMIN_CALL_CHANNEL_*` settings, LegacyX-Admin sends the reporter, reported player, reason and server address to one Call channel webhook. The transport refuses `@everyone` and `@here` mentions.

All other former Discord paths are disabled in source: chat, connection, server status, match state, bans, mutes, gags, admin action logs, game events and browser/website events. Those operational records remain inside the game, Root API and database.
