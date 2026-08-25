# LEGACY-X Discord Server Connect

## Purpose

`LegacyX-Admin` can add a **Connect to Server** link button beneath the periodic Discord server-status embed. The button never contains a raw game-server address. It opens the public LEGACY-X landing route with the configured `LEGACYX_SERVER_ID` only:

```text
https://legacyx.cc/connect?server=<LEGACYX_SERVER_ID>
```

The landing route resolves that server ID through the Root API public-server inventory, validates the returned address, and only then opens `steam://connect/<allowlisted-address>` locally in the user's browser.

## Required central environment values

```env
LEGACYX_SERVER_ID=legacyx-match-1
LEGACYX_ADMIN_DISCORD_ENABLED=true
LEGACYX_ADMIN_DISCORD_STATUS_WEBHOOK=https://discord.com/api/webhooks/...
LEGACYX_ADMIN_DISCORD_CONNECT_URL=https://legacyx.cc/connect
```

`LEGACYX_ADMIN_DISCORD_CONNECT_URL` must be an absolute HTTPS URL. Invalid, missing, or non-HTTPS values disable the button and log a plugin error. Do not put `steam://`, an IP address, an RCON address, or a Discord webhook URL in this variable.

## Discord webhook behavior

The plugin sends its incoming webhook execution request with `with_components=true`. Discord documents that this lets non-application-owned incoming webhooks respect non-interactive components, including URL link buttons. Link buttons navigate to a URL and do not send an interaction to an app. The button URL must use HTTPS because Discord rejects custom URL schemes such as `steam://` for button URLs.

The webhook is still an incoming webhook. Keep its URL only in `CounterStrikeSharp/.env` and rotate it immediately if it is shared accidentally.

## Validation checklist

1. Make sure `LEGACYX_SERVER_ID` exactly matches a public Root API server ID.
2. Confirm `GET /api/v1/public/servers/<server-id>` returns a valid `host:port` connect address.
3. Set the central environment values and restart the plugin host.
4. Run `css_discord_status` from server console or wait for the status timer.
5. Click **Connect to Server** in Discord; the browser should load `legacyx.cc/connect` and invoke Steam after Root API validation.

## References

1. [Discord Webhook Resource — Execute Webhook components and `with_components`](https://docs.discord.com/developers/resources/webhook)
2. [Discord Component Reference — Link button URL requirements](https://docs.discord.com/developers/components/reference)
3. [Discord API discussion #6989 — custom URL schemes are rejected for link buttons](https://github.com/discord/discord-api-docs/discussions/6989)
