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

**Fun commands.** Besides freeze, beacon, shake, blind, drug, speed, god and the rest, `!launch <target> [power]` (100 to 1500, default 600) throws a player up in the air and `!size <target> <0.3-3>` / `!unsize <target>` makes the player's model big or small (the hit box stays the same). All need `@css/slay` and are in the `!admin` menu under Fun, Player FX.

**Seven more fun commands** (all `@css/slay`; the ones that switch something on switch it off when run again; every effect ends at the next spawn): `!burn <target> [seconds]` (4 hp every half second, orange glow, default 5 s, up to 30), `!bounce <target> [seconds]` (springs up each time they touch the ground, default 20 s), `!lowgrav <target> [scale]` (only that player falls slowly, scale 0.1 to 5, default 0.25, 1 restores), `!fling <target>` (throws them the way you look), `!blink <target> [distance]` (moves them forward 100 to 1000 units, default 350), `!tprandom <target>` (to a random living player) and `!drop <target>` (drops their weapon). In the `!admin` and `!cc` menus under Fun, Player FX, and in Staff → Roles. Not built or run yet.

**Sixteen wild fun commands** (all `@css/slay`, on/off ones switch off when run again, effects end at the next spawn): `!jetpack` (hold jump to fly), `!doublejump`, `!sky <target> [height]`, `!pull`, `!shockwave [radius]`, `!explode [radius]`, `!vampire` (+50 hp per kill), `!infammo`, `!gift` (random weapon), `!fov <target> [60-150]`, `!ghost`, `!chicken` (chicken model until respawn), `!pop` and `!aura <target> [pulse|ribbon|lightning|stardust] [seconds]` (these two use the Workshop addon's `particles/legacyx/*` effects, tinted through control point 16), plus `!roulette` (random player, random effect) and `!chaos [seconds]` (roulette every 5 s). Built, **not run in game**: try each on a test server first, especially `!chicken`, `!pop` and `!aura`.

**Troll cards on the HUD** (all `@css/slay`; needs LegacyX-Hud and the Workshop addon's `legacyx_troll` layout, silent to the rest of the server): `!fakeban`, `!fakevac`, `!fakecrash`, `!fakehack` (each `<target> [seconds]`, a fake warning that turns into "JUST KIDDING" for its last third), `!scare <target> [text]` (a red full-screen flash, default "BOO!") and `!banner <target> <text>` (a short message, letters, digits and `. , ! ? ' + - : ( )` only). Not run in game yet.

**Match modes** (owner only, `@css/root`, **on any server at any time**, everyone is told in chat who switched it; switching restarts the game, a map change or `off` ends it): `!retake` (the bomb is planted on a random site at freeze end, Ts around it with guns, CTs come from their spawn with a kit), `!1v1` (each round one T and one CT, whoever fought least, with the same random weapon; the rest wait dead), `!randomcheats` (each round everyone gets a random wallhack, aimlock, silent aim or bunny hop and is told which), `!whwh` (wallhack for both teams), `!modeoff`; `!gamemode [retake|1v1|cheats|wh|off]` does the same and with no word says the current mode. Built, **not run in game**: the retake site comes from the map's `func_bomb_target` boxes and the planted bomb is a spawned `planted_c4`, so check where the Ts land and that the bomb ticks and can be defused before relying on it.

**Retake spawns** (owner only, `@css/root`; owner request 2026-11-06): `!retake` is only as good as where it puts people, so the Owner marks the spots once per map by standing on them: `!retakespawn <T|CT|BOMB> <A|B>` (where you stand, the way you look; BOMB = where the bomb is planted, one per site), `!retakespawnremove` (the nearest, within 250 units), `!retakespawns` (counts, and shows each spot for six seconds: T orange, CT blue, BOMB red). They are kept per map in `addons/counterstrikesharp/configs/retake/<map>.json`. A site with at least one T and one CT spot is played from them (each player on a spot of their team, more players than spots get a small offset); any other site, or a map with none marked, keeps the computed fallback (Ts in a ring round the site). Mark about 5 T spots and 5 CT spots (CT a little way from the site, where a retake starts) and a BOMB spot per site; copy the json to every server.

**When a ban is given** (every kind: `!ban`, team, IP, the site's central bans, CS2AC's): the banned player is **frozen where they stand and loses their weapons**, sees a **full-screen red ban screen** (LegacyX-Hud `lx_troll_hud ... banned`, layout in the Workshop addon) with the length and the reason for four seconds, and is then disconnected. The whole match reads one **red** chat line: who banned whom, the length and the reason (`LegacyXChat.Alert`, the one place chat is not neutral). The ban is stored first, so reconnecting is refused whatever happens in those four seconds. On the website the banned player's profile gets a Banned title and censored stat cards, they leave the ladders, and the Discord bot gives them the Banned role (only the support channel visible); lifting the ban brings all of it back.

**CS2AC detections** (`scripts/cs2-host.sh cs2ac install`): CS2AC's punishment command is `css_cs2ac_flag <steamid64> <detection>` (console only). The detection goes to the website first (`POST /plugin/anticheat/flags`), which stores it as evidence (table `anticheat_flags`, `GET /moderation/anticheat-flags`) and answers **ban** or **flag**: never a ban for staff or someone already banned, in warmup, on a Fun server, in one of the match modes, for a holder of the cheat toys, or when the same detector fired on 3+ other players within a minute (the server, not cheaters); heavy detectors (aimbot, aimlock, silent aim, antiaim, doubletap) ban at once, medium ones (inhuman accuracy, irregular behavior, bhop, autostrafe, hyperscroll, DLL injection) on the second detection within ten minutes, the rest never alone. Only "ban" runs `css_ban` (ban screen, red chat line); a flag is shown to the staff online and logged. If the website cannot be asked, the guards that need no history run here and only a heavy detector bans. Kick detectors (Desubticking, Nulls, Subtick Spam) still run `css_kick`.

**`!cheats [0|1]`** (root only): switches `sv_cheats` on or off on any server and at any time; with no number it toggles. A moment later the value is read back and the reply says what it really is (or that it could not be changed). Off a Fun server and outside warmup everyone is told in chat that it was turned on; the end of the next warmup still puts it back to 0. `!cvar sv_cheats` stays blocked. A root player's own console `sv_cheats 1` is also taken over when the game lets it reach the server (a CS2 client may refuse to send it, being a server-only setting); `css_cheats 1` in the console always does.

**New fun commands** (all `@css/slay`, one command switches the effect on and off, every effect ends at the next spawn): `!invisible <target>` hides the player and their weapons, `!rainbow <target> [seconds]` cycles the model's colour (30 s by default, up to 300), `!spin <target> [seconds]` spins the player's view (5 s by default, up to 60) and `!rocket <target>` throws the player up and blows them up a moment later. They are in the `!admin` and `!cc` menus under Fun, Player FX, and in Staff → Roles on the website. Not built or run yet.

**`!cc`** (content creators only): opens the Fun menu as the "Creator" menu in the same centre HTML menu as `!admin`, which a creator cannot open. It lists only the commands the website grants the creator's role (Staff → Roles); the servers read a change there within a minute, so ticking or unticking a command on the site adds it to or removes it from the menu. Anyone else is told it is for content creators. Not built or run yet.

**The `!admin` menu.** `!admin` opens the chat menu (Staff, Players, Server, Bans).

**Typed commands stay out of chat.** `.r`, `!r`, `!admin` and the like still run, but the game's own chat line for them (`[ALL] Name: .r`) is dropped (a `!`, `/` or `.`, a letter, one word, then the end or a space; `...gg` is ordinary chat).

**Play access.** On connect the server asks the API (`POST /plugin/players/access`, the `bans:read` token it already has) whether the player is signed in on legacyx.cc and whether they are blocked for leaving ranked matches (3rd leave in 24 hours: 30 minutes, 30 more each time after). If not, the player gets a chat message saying what to do (sign in on the website and press Connect, or when they can play again) and is removed 10 seconds later. Staff (`@css/generic`) are never removed, and nobody is removed when the API cannot be reached. `LEGACYX_ADMIN_REQUIRE_LOGIN=false` and `LEGACYX_ADMIN_ENFORCE_LEAVE_COOLDOWN=false` switch the two parts off (a Fun server may not want the cooldown).

Bans (`css_ban`, `css_ipban`, the `!admin` menu) live only in the LEGACY-X database and apply on every server; this server keeps no `banned_user.cfg` or `banned_ip.cfg`. A restart loses nothing, and the website, Discord and the game all show the same list. From the console or RCON, `css_ban` and `css_unban` accept a SteamID64, `STEAM_X:Y:Z` or `[U:1:N]` for players who are not online, and `css_unban` also accepts an IP address.

The module's ban, admin, communication, and menu files are functional state files managed by upstream AdminPlus. They contain no API tokens, database credentials, or webhooks.

## Languages

Upstream languages live in `lang/` and retain their complete keys. Every supplied locale uses the `LEGACY-X Admin` prefix. A complete Mongolian translation is intentionally a separate reviewed localization task; the plugin falls back to English when the client has no matching translation file.

## Build

```bash
dotnet build LegacyX-Admin.csproj --configuration Release
```

This repository contains source only. It does not install a CS2 server, write a real central environment file, or deploy any DLL.
