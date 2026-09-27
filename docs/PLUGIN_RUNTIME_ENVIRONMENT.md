# LEGACY-X plugin runtime environment

Every CounterStrikeSharp module loads its configuration from the **one** host-local file:

```text
addons/counterstrikesharp/CounterStrikeSharp/.env
```

The real file is never committed. Do not put a Supabase service key, database password, browser cookie, Steam API secret, or website JWT in it. Plugins call the Root API with their own scoped plugin token; the Root API alone accesses the database.

## Required shared identity

```dotenv
LEGACYX_API_BASE_URL=https://api.legacyx.cc
LEGACYX_SERVER_ID=REPLACE_WITH_DATABASE_SERVER_UUID
LEGACYX_SERVER_ADDRESS=HOST:PORT
LEGACYX_SERVER_MODE=competitive_5v5
```

## CS2 and RCON readiness

RCON is reserved for a future reviewed, server-side executor. None of the current plugins opens an RCON connection or exposes a raw RCON command to the browser.

```dotenv
LEGACYX_RCON_HOST=127.0.0.1
LEGACYX_RCON_PORT=27015
LEGACYX_RCON_PASSWORD=REPLACE_WITH_LONG_RANDOM_SERVER_ONLY_PASSWORD
```

Keep `LEGACYX_RCON_HOST` on the server's private/loopback network. The RCON password must not appear in website code, plugin JSON, Discord, database rows, source control, screenshots, or chat.

## Module switches and scoped API identities

```dotenv
LEGACYX_ADMIN_ENABLED=true
LEGACYX_AFKMANAGER_ENABLED=true
LEGACYX_COMMUNITY_ENABLED=true
LEGACYX_MATCHZY_ENABLED=true
LEGACYX_SPECTATOR_COMMS_ENABLED=true
LEGACYX_SKINBRIDGE_ENABLED=true

LEGACYX_COMMUNITY_PLUGIN_ID=legacyx-community
LEGACYX_COMMUNITY_PLUGIN_TOKEN=REPLACE_WITH_SCOPED_TOKEN
LEGACYX_MATCHZY_MATCH_CORE_ENABLED=false
LEGACYX_MATCHZY_MATCH_CORE_PLUGIN_ID=legacyx-match-core
LEGACYX_MATCHZY_MATCH_CORE_PLUGIN_TOKEN=REPLACE_WITH_SCOPED_TOKEN
LEGACYX_MATCHZY_MATCH_CORE_API_URL=https://api.legacyx.cc/api/v1/plugin/match-core/events
LEGACYX_SKINBRIDGE_PLUGIN_ID=legacyx-skinbridge
LEGACYX_SKINBRIDGE_PLUGIN_TOKEN=REPLACE_WITH_SCOPED_TOKEN
LEGACYX_SKINBRIDGE_POLL_SECONDS=3

LEGACYX_ADMIN_API_BASE_URL=https://api.legacyx.cc
LEGACYX_ADMIN_PLUGIN_ID=legacyx-admin
LEGACYX_ADMIN_PLUGIN_SECRET=REPLACE_WITH_SCOPED_TOKEN
LEGACYX_ADMIN_AUTH_REFRESH_SECONDS=60
LEGACYX_ADMIN_AUTH_CACHE_SECONDS=180
```

In-game staff permissions come only from the Root API; see [Admin authorization](ADMIN_AUTHORIZATION.md).

WeaponPaints/SkinBridge is database-ready through this route:

```text
Website -> Root API -> database -> scoped skinchanger job -> LegacyX-WeaponPaints -> CS2 player state
```

It deliberately does not receive direct database credentials. A job is acknowledged as `applied` or `failed`; the website never claims that an in-game equip succeeded before the plugin acknowledgement returns.

## Call channel only

Discord is optional and has exactly one allowed outbound use: a player `!admin` / report call alert. All ban, mute, gag, chat, connect, disconnect, server status, match, game, moderation-action and website events stay within the website/API/database.

```dotenv
LEGACYX_ADMIN_CALL_CHANNEL_ENABLED=false
LEGACYX_ADMIN_CALL_CHANNEL_WEBHOOK=
# Optional: exact Discord role mention only, for example <@&123456789012345678>.
LEGACYX_ADMIN_CALL_CHANNEL_MENTION=
```

Set `LEGACYX_ADMIN_CALL_CHANNEL_ENABLED=true` only after the call channel webhook is created. The plugin validates HTTPS Discord webhook URLs and rejects `@everyone` / `@here` mentions.

## Preflight checklist

Before enabling a module, confirm that the corresponding Root API plugin identity exists, its token scope is restricted to that module/server, `LEGACYX_SERVER_ID` matches the database server row, and the plugin starts without a configuration error. Do not enable a module merely because a value is present in the environment file.
