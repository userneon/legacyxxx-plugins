> **Хуулбар.** Эх сурвалж: [legacyxxx-discord-bot/README.md](https://github.com/userneon/legacyxxx-discord-bot/blob/main/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# LEGACY-X Discord bot

Welcomes new members of the LEGACY-X Discord server. When someone joins, the bot:

- draws a welcome banner for that member (their avatar and name, member number, Dust II backdrop, a T or CT agent) with the site's fonts and colours;
- posts it in the welcome channel inside a crimson card, pinging only the new member, with three getting-started steps and link buttons (Website, Серверүүд, Leaders, Дүрэм);
- optionally gives them a starter role (`WELCOME_ROLE_ID`).

## Commands

Answers with a picture or card are public in the channel; every error or notice (bad SteamID, API down, nothing to lift…) is shown only to the person who ran the command.

| Command | Who | What it does |
| --- | --- | --- |
| `/top [by]` | everyone | Top 3 players on a podium image, by EXP (default), K/D or win rate, same data as legacyx.cc/leaders |
| `/servers` | everyone | Every server on the site's Play pages: map, players, state, live score, and a Connect link |
| `/ban steamid duration reason` | Ban Members | Records a SteamID ban in the database (1 hour … permanent); every CS2 server enforces it |
| `/unban steamid` | Ban Members | Lifts that SteamID's bans |
| `/link` | everyone | Privately gives a one-time link (10 min) to sign in with Steam; links Discord to the LEGACY-X account and gives the rank role |
| `/lfg mode [need] [note]` | everyone | Posts "looking for group" in any channel; joining needs `/link`. Join / Leave / Close and a Play link; pings the team when it fills; closes after 30 minutes |
| `/unlink` | everyone | Removes the link and the rank roles |
| `/voice-setup [channel]` | Manage Server | Join to create: the given voice channel (or a new "➕・Voice үүсгэх" in this category) opens a personal voice for whoever joins it |
| `/ad set` · `/ad stop` | Manage Server | A notice kept at the bottom of this channel: re-posted every few minutes (2 by default) |
| `/automod setup [log_channel] [timeout_minutes]` · `add` · `remove` · `allow` · `list` · `off` | Manage Server | Swearing filter: Mongolian (Cyrillic and Latin), Russian and English, blocked before anyone sees it |
| `/news` | Manage Server | Makes the current channel the news channel: official CS2 news and HLTV, translated into Mongolian |
| `/penalties` | Manage Server | Makes the current channel the penalties feed |
| `/faq-setup` | Manage Server | Posts the FAQ panel in the current channel: a button per question, each answers privately (ephemeral) |
| `/admincalls [role] [off]` | Manage Server | Makes the current channel the admin calls feed: every in-game `!calladmin`, `!callmanager` and `!report` is posted here, optionally pinging `role`; `off` stops it |
| `/clear amount` | Manage Messages | Deletes the channel's last 1–100 messages (Discord skips ones older than 14 days) |
| `/ticket-setup staff_role [log_channel] [manager_role]` | Manage Server | Posts the support panel with a ticket-type menu in the current channel |
| `/rules section` | Manage Server | Posts the Ingame, Discord or Staff rules in the current channel |
| `/status` | Manage Server | Puts a live, self-updating server board in the current channel |
| `/welcome-preview` | Manage Server | Shows the welcome message privately, without anyone joining |

`/top` reads the public leaderboard from `API_URL` (no secret) and caches it for a minute.

**Rules.** The texts are plain Markdown in `rules/ingame.md`, `rules/players.md` and `rules/staff.md`: the first `# ` line is the title, the rest uses Discord markdown. Edit a file, `git pull` on the VPS (no restart needed), delete the old post and run `/rules` again.

**Bans from Discord.** `/ban` and `/unban` go through the Root API, so every ban is recorded in the LEGACY-X database: it appears on legacyx.cc/penalties and the player's profile, and every CS2 server running LegacyX-Admin with central bans enabled removes the player within about 30 seconds. The answer in the channel shows a picture with the player, the staff member, the reason and the duration. Set up:

1. Backend `legacyxxx-backend` at `7f971b6` or later. On the VPS, in the backend folder, create the bot's token and put it in this bot's `.env` as `API_TOKEN`:
   `node --env-file=.env scripts/create-api-token.mjs legacyx-discord-bot bans:write discord:link`
2. LegacyX-Admin from `legacyxxx-plugins` at `4218175` or later on every CS2 server, with `LEGACYX_ADMIN_CENTRAL_BANS_ENABLED=true` and a `LEGACYX_ADMIN_PLUGIN_SECRET` token that has `bans:read` (e.g. `create-api-token.mjs legacyx-admin admin:read bans:read`).

**Steam link and rank roles.** `/link` shows the member (only them) a button that opens a one-time Root API link; they sign in with Steam and their Discord account is linked to their LEGACY-X account (one Discord per Steam account; linking again replaces it). The bot notices within seconds and edits the message to show the Steam name and rank.

- On start the bot creates the roles `Recruit`, `Operator`, `Vanguard`, `Ace`, `Apex`, `Legacy` (the site's tier colours) and `Steam Linked`, and saves their IDs in `data/rank-roles.json`. Rename, recolour or move them as you like; the bot tracks them by ID. Delete the file to have them recreated.
- Every linked member gets `Steam Linked` plus the tier of their current rank (none until their first finished match). Every 5 minutes the bot re-syncs everyone, so rank-ups and drops show up on their own; members who join again get their roles straight away. `/unlink` removes both. If the API can't be reached, nobody's roles change.
- Needs: `API_TOKEN` with the `discord:link` scope, **Manage Roles**, the bot's own role **above** these roles, and Server Members Intent (already needed for welcomes). The backend must have `PUBLIC_API_ORIGIN` set to its public address (e.g. `https://api.legacyx.cc`), since that is where the link points.

**Looking for group.** `/lfg mode [need] [note]` posts who is looking (with their rank if they used `/link`), how many people they need (1–9, default 4) and a note. Anyone can post in any channel, but only members who linked Steam with `/link` can press **Нэгдэх** (if the bot can't check, because the API is down or `API_TOKEN` is missing, nobody gets in); others are told privately to `/link` first. Members leave with **Гарах**; when the team fills, the post turns green and a new message pings everyone with a **Тоглох** link to the mode's play page. The host (or staff with Manage Messages) can **Хаах**. After 30 minutes an unfilled post is deleted and a filled one stays without buttons. One open post per member: a new `/lfg` replaces the old one. Open posts live in `data/lfg.json`, so buttons keep working after a restart.

**Join to create.** `/voice-setup` makes a hub voice channel (pick one, or the bot creates "➕・Voice үүсгэх" in the current category). Joining it gives the member their own voice, "🔊・Name", in the same category with its permissions, and moves them in; someone who already has one goes back to it. The room's chat gets a control panel only the owner (or staff with Manage Channels) can use: **Түгжих / Нээх** (who is inside can still come back), **Нуух / Харуулах**, **Нэр солих** (Discord allows two renames per 10 minutes), **Хүний тоо** (0–99), **Хүн оруулах** (even when locked), **Хүн гаргах** (disconnects and bars them), and **Эзэн болох** once the owner has left. The room is deleted when the last person leaves, also after a restart. The bot needs Manage Channels, Manage Roles, Move Members and Connect. State lives in `data/voice.json`.

**Ad at the bottom of a channel.** `/ad set` in a channel (e.g. main) opens a form: the text (Discord markdown), how many minutes between re-posts (1–60, default 2) and an optional picture. The bot posts it as an embed, and every round deletes its previous post and sends it again so it stays at the bottom where everyone sees it; if it is still the last message, nothing changes. `/ad set` again edits it (or moves it to another channel), `/ad stop` removes it. It pings nobody. State lives in `data/ad.json`.

**Swearing filter.** `/automod setup` turns on two Discord AutoMod rules, so swearing is blocked before anyone sees it and the member is told "Хараал, доромжилсон үг хориотой" (the bot needs no access to message content). **LEGACY-X · Хараал** holds Mongolian swearing in Cyrillic and Latin letters, Russian mat and extra English, plus patterns for spaced or disguised spellings (`п.и.з.д`, `f u c k`, `sh!t`); whole-word matching keeps ordinary words like "хуйх" or names like "Баасанжав" out. **LEGACY-X · Гадаад хараал** is Discord's own English profanity, slur and sexual-content list (Discord allows one per server; if the server already has one, that one stays). With `log_channel`, staff see every blocked message there. Someone blocked 3 times in 10 minutes gets a timeout (`timeout_minutes`, default 10, 0 = none). `/automod add` and `remove` change the list (`word*` = starts with, `*word*` = contains), `list` shows it, `off` turns it off. Only words that are swearing in any sentence are on the list ("ээжийн чинь" or "баас" also appear in ordinary talk, so they are not). Mongolian typed in Latin letters that Discord's English lists mistake for swearing is allowed (`goy` = гоё is an English slur, `hoer` = хоёр, `bich` = бич); when someone is blocked by mistake, `/automod allow word` lets that word through (again to take it back). The rules are re-sent to Discord every time the bot starts, so list changes in an update apply by themselves. Members with Manage Server are not filtered (Discord's rule). The bot needs Manage Server (to set AutoMod rules) and Timeout Members. State lives in `data/automod.json`.

**News channel.** Staff type `/news` in a channel; the latest five stories go up right away, however old they are, and from then on every new one (checked every 10 minutes, at most 4 at a time, nothing older than 3 days). Sources: the official CS2 news (Valve's Counter-Strike 2 announcements and patch notes, the same posts as counter-strike.net/news, labelled official and linked to their Steam page) and HLTV. `NEWS_FEEDS` (`Name|https://…/rss`, comma separated) adds any other site with an RSS feed. A source that can't be reached is logged (`[news] HLTV: … answered 403`) and the others keep working. Each headline and the article's opening (about 600 characters, cut at a sentence) are machine-translated into Mongolian with Google Translate's free web endpoint (no key, nothing to pay; it is unofficial, so Google may slow it down) and posted as an embed with the picture, the source, the time and a **Бүтнээр унших** link to the original. A story whose translation fails is tried again next time. State lives in `data/news.json`.

**Penalties channel.** Staff type `/penalties` in a channel; from then on every new penalty in the database (bans from Discord, the website or the game, mutes, gags) is posted there as its own message with a picture, and so is every lifted ban. Existing penalties are not re-posted. When more than 3 bans are lifted at once (an owner's `!cleanbans` in-game), one summary message lists them instead of a picture each. The channel and what was posted are saved in `data/penalty-feed.json`.

**Admin calls and reports.** Staff type `/admincalls` in a channel (add `role` to ping a role on every call). `!report` arrives in the same feed as a separate purple message: who reported whom (both linked to their profiles), the reason, and the server. It needs `supabase/legacy_x_admin_calls_reports.sql` applied. Every call and report carries a **Server-т орох** button that opens the site's `/connect?server=<id>` page, which looks the server up and starts CS2 through Steam (Discord buttons can't open `steam://` links themselves). When a player types `!calladmin` or `!callmanager` in game, LegacyX-Admin records it in the database (`POST /plugin/admin-calls`) and the bot posts it here within about 10 seconds: who called, the server and map, how many players, and how many staff were online (0 means nobody was there to see it in game). Calls made before `/admincalls` are not posted, and neither are ones older than 24 hours after a long outage. No webhook is needed. Needs the backend with `supabase/legacy_x_admin_calls.sql` applied, `API_TOKEN` with the `discord:link` scope (the one `/link` already uses), and View Channel, Send Messages, Embed Links and Attach Files in the channel. State lives in `data/admin-calls.json`.

**FAQ.** `/faq-setup` posts a panel (with a FAQ banner drawn by the same renderer as the support and rules banners) with one button per question (Steam холбох, Хэрхэн тоглох, Цол/EXP, Pro League, Skin тавих, Командууд, Cheat мэдэгдэх, Ban appeal). Pressing a button answers only that person, with a link to the matching page where there is one. The answers are plain Markdown in `faq/*.md` (first `# ` line = title, the rest uses Discord markdown, under 4000 characters): edit a file and `git pull` on the VPS, no restart needed, since the file is read on every press. To add a question, add a file and one line to `FAQ_TOPICS` in `src/faq.js` (Discord allows 25 buttons, 5 per row; the panel uses 4 per row). The buttons carry their own ids, so the panel keeps working after a restart and nothing is saved.

**Tickets.** `/ticket-setup staff_role [log_channel] [manager_role]` posts a panel with a picture and a menu of ticket types (Тусламж, Тоглогч report, Ban appeal, Бусад) in a text channel.

- Picking a type shows a private card in that channel with the type's questions and a **✏️ Бөглөх** button that opens the form. The menu resets for the next pick.
- **Ban appeal** asks for the account first: a SteamID in any form, a Steam profile link (`/profiles/…` or a custom `/id/…`) or a LEGACY-X profile link (`legacyx.cc/profile/…`). It immediately shows the ban from the database: its picture, duration, end time, status, reason and who issued it. Then **✏️ Appeal бичих** asks why. An account with no ban gets a green **NO BANS** card with its Steam name and picture ("Таны account зөрчилгүй байна"); a lifted or expired ban can't be appealed; input that can't be read lists the accepted forms. Each of these has an **Өөр SteamID оруулах** button.
- Sending the form creates a **private thread** under the panel (`ticket-0001-name`) with the member and the staff role's members. Its first message mentions both sides and shows a picture of what the request is about (the banned or reported player, the ban, the reason), then the details: reasons, answers, Steam and LEGACY-X profile links. A reported player is looked up by SteamID or exact name.
- An appeal is assigned straight away, with no claim button: the staff member who issued the ban from Discord is mentioned (the bot remembers issuers in `data/ban-issuers.json`; older Discord bans are matched by name). If that person has left or is no longer staff, `manager_role` is mentioned instead, or the server owner if none is set.
- Other tickets mention the staff role; staff press **✋ Хариуцах** to claim them.
- Staff controls under the first message: **✋ Хариуцах** / **↩️ Суллах** (the claimer or a manager releases), **🔁 Шилжүүлэх** (hand over to another staff member), **➕ Хүн нэмэх**, **➖ Хүн хасах** and **🔒 Хаах**. All of them are staff-only (staff, admin, manager, owner). The member who opened a ticket, staff included, never claims, controls or closes it.
- Staff press **🔒 Хаах** to close (members can't close their own ticket): a transcript goes to the log channel, then the thread is locked and archived.
- One open ticket per member. State lives in `data/tickets.json`. In the panel channel the bot needs Create Private Threads, Send Messages in Threads and Manage Threads; members need Send Messages in Threads.

**Live server boards.** Staff type `/status` in any channel and the bot puts a server board (a picture of every server, plus Connect links) there that updates by itself (checked every 30 seconds, edited only when something changes). Put boards in as many channels as you like; `/status` again in the same channel moves the board to the bottom, and deleting the message removes it. Where the boards are is saved in `data/boards.json`, so they keep updating after a restart. In that channel the bot needs View Channel, Send Messages, Embed Links and Attach Files. Tip: make the channel read-only for members.

Discord buttons can't open `steam://` links, so **Connect** goes to `legacyx.cc/connect?server=<id>`, which looks the address up from the API and opens CS2.

## 1. Create the bot (Discord Developer Portal)

1. https://discord.com/developers/applications → **New Application** → name it `LEGACY-X`.
2. **Bot** tab → **Reset Token** → copy it into `.env` as `DISCORD_TOKEN`. Never paste it in chat or commit it.
3. **Bot** tab → **Privileged Gateway Intents** → turn on **Server Members Intent** (required, or the bot never sees joins).
4. **OAuth2 → URL Generator**: scopes `bot` + `applications.commands`; bot permissions `View Channels`, `Send Messages`, `Embed Links`, `Attach Files` (for the banner), `Read Message History` and `Manage Messages` (for `/clear`), `Create Private Threads`, `Send Messages in Threads` and `Manage Threads` (for tickets), and `Manage Roles` (welcome role and rank roles), `Manage Channels`, `Move Members` and `Connect` (join to create), and `Manage Server` and `Timeout Members` (swearing filter). If the bot starts before it is invited, its log prints a ready-made invite link. Open the URL and add the bot to the server.
5. If you use a welcome role: Server Settings → Roles → drag the bot's role **above** that role.

## 2. Configure

```bash
cp .env.example .env
nano .env
```

Discord → User Settings → Advanced → **Developer Mode** on, then right-click the server / channel / role → **Copy ID**.

## 3. Run on the VPS (pm2, next to legacy-x-api)

```bash
npm ci
npm run check          # syntax + unit tests
pm2 start ecosystem.config.cjs
pm2 save
pm2 logs legacy-x-discord --lines 50
```

The log should show `[ready] serving <server>, commands registered: ...`. After a change: `bash ops/deploy.sh` (pulls `main`, installs, runs the checks and tests, reloads pm2 and waits for the `[ready]` line).

## Profile picture and banner

`node scripts/brand.mjs` draws `assets/brand/avatar.png` (1024×1024) and `assets/brand/banner.png` (1360×480) in the site's style. Upload them in the Developer Portal: **Bot → Icon** and **Banner**, and **General Information → App Icon**.

## Server role icons

`node scripts/roles.mjs` draws a 256×256 badge icon for each server role into `assets/roles/`: a bold glyph (crown, gem, star shield, `</>`, check shield, bolt, crosshair, the L mark) in the role's gradient, with a metallic sheen, a light rim and a glow in the role's colour. Role names and gradient colours are listed at the top of the script. Discord needs Boost Level 2 for role icons.

## Files

| File | What it does |
| --- | --- |
| `src/index.js` | Discord client, join handler, `/welcome-preview` |
| `src/welcome.js` | Builds the welcome message (edit texts and buttons here) |
| `src/card.js` | Draws the welcome banner PNG (@napi-rs/canvas, no system libraries needed) |
| `src/top.js`, `src/podium.js` | `/top` reply and its podium image |
| `src/status.js` | `/servers` reply and the live server boards (`/status`) |
| `src/ban.js`, `src/steamid.js` | `/ban`, `/unban`, SteamID parsing |
| `src/steamlookup.js` | Steam and LEGACY-X profile links → SteamID, Steam name and avatar (public Steam XML, no key) |
| `src/tickets.js`, `src/issuers.js` | Tickets as private threads (menu, private question cards, appeal lookup and assignment, claim, close, transcripts) |
| `src/lfg.js` | `/lfg` posts and their buttons |
| `src/link.js`, `src/rankroles.js` | `/link`, `/unlink` and the rank roles sync |
| `src/ad.js` | `/ad`: the re-posted notice |
| `src/automod.js` | `/automod`: the word lists, Discord AutoMod rules and repeat-offender timeouts |
| `src/voice.js` | Join to create rooms and their control panel |
| `src/news.js` | `/news`: reads the official news, machine-translates, posts |
| `src/penalties.js` | The penalties feed (`/penalties`) |
| `src/cards.js` | Pictures for penalties, the server list and rules headers |
| `src/clear.js` | `/clear` |
| `src/rules.js`, `rules/` | `/rules` and the rule texts |
| `src/store.js` | Saves where the boards are (`data/boards.json`, not in git) |
| `src/api.js` | Reads the public Root API and calls the token routes (bans, Discord links) |
| `src/draw.js` | Shared drawing: fonts, backdrop, avatar ring, rank emblems |
| `assets/` | Images and rank emblems from the website, fonts Black Ops One and Onest (SIL Open Font License) |
| `src/config.js` | Reads and validates `.env` |
| `test/` | Unit tests (`node --test`) |
