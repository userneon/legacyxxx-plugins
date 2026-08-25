# LEGACY-X Discord Match Mode Policy

## Default routing

Only `competitive_5v5` is allowed to emit the LegacyX-Admin Discord server-status/match card. The canonical mode is shared through the central runtime environment so Reconnect heartbeat payloads, Root API server records, and Discord routing describe the same server mode.

```env
LEGACYX_SERVER_MODE=competitive_5v5
LEGACYX_ADMIN_DISCORD_MATCH_MODES=competitive_5v5
```

## Website/API-only modes

Set the server mode to `fun` for Fun Mode and `proleague` for Pro League. These modes continue to send their real server/match state to the Root API and website, but LegacyX-Admin does not post the Discord status card because neither mode is in the default Discord allowlist.

```env
# Fun server
LEGACYX_SERVER_MODE=fun

# Pro League server
LEGACYX_SERVER_MODE=proleague
```

Do not add `fun` or `proleague` to `LEGACYX_ADMIN_DISCORD_MATCH_MODES` unless Discord publication is explicitly wanted later.

## What remains independent

Ban, mute/gag/silence, player reports, and manual admin-action logs are moderation events, not match lifecycle events. Their Discord webhooks remain controlled by their own explicit webhook settings and are not changed by the match-mode status filter.
