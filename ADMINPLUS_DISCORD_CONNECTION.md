# LEGACY-X Admin Call Channel

LegacyX-Admin does not send admin actions, bans, mutes, gags, chat, connection, server status, match events or website/dashboard events to Discord.

The only optional Discord delivery is a player `!admin` / report Call channel alert. The event contains the reporter, reported player, reason and server address. Enable it only through the central host-local `CounterStrikeSharp/.env` settings documented in `docs/DISCORD_CALL_CHANNEL_ONLY.md`.

The webhook is never placed in a plugin-local JSON file, source control, browser code, database row, screenshot or chat. Discord delivery failures do not change game moderation behavior; all authoritative records remain inside the game, Root API and database.
