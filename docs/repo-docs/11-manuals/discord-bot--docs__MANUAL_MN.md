> **Хуулбар.** Эх сурвалж: [legacyxxx-discord-bot/docs/MANUAL_MN.md](https://github.com/userneon/legacyxxx-discord-bot/blob/main/docs/MANUAL_MN.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# legacyxxx-discord-bot: гарын авлага

LEGACY-X Discord server-ийн bot: тавтай морил, дүрэм, FAQ, ticket, автомод (хараал + зар), шийтгэл, admin дуудлага, server
самбар, update-ийн мэдээ, LFG, voice өрөө, цолны role. Node.js + discord.js 14, banner зурахдаа `@napi-rs/canvas`.

| Хэсэг | Агуулга |
|---|---|
| [1. Бүтэц ба ажиллах зарчим](#1-бүтэц-ба-ажиллах-зарчим) | Bot юу уншиж, юу бичдэг |
| [2. Анх суулгах](#2-анх-суулгах) | Discord application, `.env`, эрх, VPS |
| [3. Өдөр тутмын ажил](#3-өдөр-тутмын-ажил) | Deploy, log, шинэчлэл |
| [4. Командууд](#4-командууд) | Команд бүр: хэн, юу хийдэг |
| [5. Анхны тохируулга (Discord дээр)](#5-анхны-тохируулга-discord-дээр) | Аль channel-д аль командыг |
| [6. Автомод: хараал ба зар](#6-автомод-хараал-ба-зар) | Дүрэм, жагсаалт, алдааг засах |
| [7. Banner ба зураг](#7-banner-ба-зураг) | Нэг загвар, agent нэмэх |
| [8. Агуулга засах](#8-агуулга-засах) | FAQ, дүрэм, welcome |
| [9. Төлөв ба өгөгдөл](#9-төлөв-ба-өгөгдөл) | `data/*.json` |
| [10. Тест ба хөгжүүлэлт](#10-тест-ба-хөгжүүлэлт) | `npm run check`, шинэ команд нэмэх |
| [11. Алдаа засах](#11-алдаа-засах) | Шинж тэмдэг → шалтгаан |
| [12. Бусад баримт](#12-бусад-баримт) | [README.md](README.md) |

---

## 1. Бүтэц ба ажиллах зарчим

```text
Discord ◄──► bot (энэ repo, pm2: legacy-x-discord) ──HTTPS──► api.legacyx.cc (backend)
                  │
                  └── data/*.json (channel, сүүлд харсан id гэх мэт төлөв)
```

- **Нэг процесс**, нэг server (`GUILD_ID`). Slash command эхлэхэд бүртгэгдэнэ.
- Bot **мессежийн агуулга уншдаггүй** (хараалыг Discord-ын AutoMod өөрөө хаадаг). Intent: `Guilds`, `GuildMembers`
  (тавтай морил, role; **Developer Portal дээр Server Members Intent заавал асаах**), `GuildVoiceStates`, `AutoModerationExecution`.
- API-тай ярихдаа `API_TOKEN` (scope: `discord:link`, `bans:write`): `/ban`, `/link`, дуудлагын feed, update feed.
  Нийтийн өгөгдөл (`/top`, `/servers`) нууцгүй.
- Feed-үүд (шийтгэл, admin дуудлага, update, мэдээ) тодорхой хугацаанд API-г **polling** хийнэ, сүүлд харсан id-г `data/`-д хадгална.
  Тиймээс restart-ийн дараа давхар тавихгүй, суваг сонгохоос өмнөх зүйлийг тавихгүй.

| Файл | Үүрэг |
|---|---|
| `src/index.js` | Эхлэл, бүх команд, interaction чиглүүлэлт, owner-only хяналт |
| `src/welcome.js`, `card.js` | Тавтай морил: мессеж + banner |
| `src/cards.js`, `draw.js` | Бүх banner/card зурах (title banner, шийтгэл, ticket, update) |
| `src/rules.js`, `faq.js` | Дүрмийн цэс, FAQ товчнууд |
| `src/automod.js` | Хараал + зар сурталчилгааны AutoMod |
| `src/tickets.js` | Ticket систем |
| `src/penalties.js`, `calls.js`, `announcements.js`, `news.js` | Feed-үүд |
| `src/status.js`, `top.js`, `podium.js` | Server самбар, `/top` |
| `src/link.js`, `rankroles.js` | Steam холболт, цолны role |
| `src/lfg.js`, `voice.js`, `ad.js`, `clear.js`, `ban.js` | Бусад командууд |
| `src/api.js`, `store.js`, `config.js`, `replies.js` | Туслах: API, төлөв, тохиргоо, хариулт |
| `faq/*.md`, `rules/*.md` | Харуулдаг агуулга (код биш) |
| `assets/` | Фонт (Onest, Black Ops One), agent, map, цолны emblem, role icon |
| `scripts/` | `brand.mjs` (bot profile зураг), `roles.mjs` (role icon) |
| `ops/deploy.sh` | VPS дээр нэг командаар deploy |

## 2. Анх суулгах

1. **Discord application:** Developer Portal → New Application → Bot → **Reset Token** (`DISCORD_TOKEN`). **Privileged Gateway Intents → Server Members Intent: ON**.
2. **Bot-ыг урих:** OAuth2 → URL Generator → `bot` + `applications.commands`, доорх эрхүүдтэй. (Эсвэл bot эхлэхэд log-д урилгын холбоос гаргана.)
3. **ID авах:** Discord → Settings → Advanced → Developer Mode ON. Server, channel, role дээр баруун товч → Copy ID.
4. **VPS дээр:**
   ```bash
   git clone https://github.com/userneon/legacyxxx-discord-bot.git ~/legacyxxx-discord-bot
   cd ~/legacyxxx-discord-bot
   cp .env.example .env && nano .env && chmod 600 .env
   npm ci
   pm2 start ecosystem.config.cjs && pm2 save
   ```
5. **API token** (backend VPS дээр): `node --env-file=.env scripts/create-api-token.mjs legacyx-discord-bot bans:write discord:link` → `API_TOKEN`.
6. **Bot-ын role-ыг доош биш дээш тавь:** `Server settings → Roles` дээр bot-ын role нь тэдний удирдах цолны role-уудаас **дээр** байх ёстой.

### `.env`

| Хувьсагч | Заавал | Тайлбар |
|---|---|---|
| `DISCORD_TOKEN` | тийм | Bot token |
| `GUILD_ID` | тийм | Server ID |
| `WELCOME_CHANNEL_ID` | тийм | Тавтай морил гарах channel |
| `WELCOME_ROLE_ID` | үгүй | Шинэ гишүүнд өгөх role |
| `RULES_CHANNEL_ID` | үгүй | Welcome-д дүрмийн холбоос |
| `SITE_URL` | үгүй | Анхдагч `https://legacyx.cc` |
| `API_URL` | үгүй | Анхдагч `https://api.legacyx.cc` |
| `API_TOKEN` | feed/ban/link-д тийм | `discord:link`, `bans:write` |
| `NEWS_FEEDS` | үгүй | `Нэр\|https://…/rss`, таслалаар |
| `DISCORD_INVITE_URL` | үгүй | Манай урилга (зар шүүлтүүр нэвтрүүлнэ). Анхдагч `https://discord.gg/legacyx` |

### Bot-д хэрэгтэй эрх

View Channel, Send Messages, Embed Links, Attach Files, **Manage Roles**, **Manage Channels**, **Move Members**, Connect, **Manage Server**
(AutoMod тохируулах), **Timeout Members**, Manage Messages (`/clear`, `/ad`), Create Private Threads, Send Messages in Threads, Manage Threads (ticket).
Алдаа гарвал bot log-д "missing permissions in #channel" гэж яг аль нь дутууг бичнэ.

## 3. Өдөр тутмын ажил

```bash
cd ~/legacyxxx-discord-bot
bash ops/deploy.sh                      # pull → npm ci → check+тест → pm2 reload → "[ready]" хүлээх → Discord-д мэдэгдэл
pm2 logs legacy-x-discord --lines 60    # log (pm2 нэр: legacy-x-discord)
pm2 restart legacy-x-discord            # дахин асаах
```

- Deploy нь `npm run check`-ээр синтакс болон бүх тестийг (106) шалгана; унавал bot reload хийгдэхгүй.
- `faq/*.md`, `rules/*.md`-ийг засахад **restart шаардлагагүй** (`git pull` хангалттай): файлыг дарахад бүр уншина.
- Хараалын жагсаалт, зар сурталчилгааны дүрэм кодод байдаг: шинэчилсний дараа bot эхлэхдээ Discord-ын AutoMod дүрмийг өөрөө шинэчилнэ.

## 4. Командууд

`✱` нь **зөвхөн server-ийн эзэн** ажиллуулна (Manage Server-тэй ч биш).

| Команд | Хэн | Үүрэг |
|---|---|---|
| `/top [by]` | бүгд | Top 3 (EXP, K/D, win rate) зурагтай |
| `/servers` | бүгд | Server бүрийн map, тоглогч, score, Connect |
| `/link`, `/unlink` | бүгд | Steam холбох/салгах, цолны role |
| `/lfg mode [need] [note]` | бүгд | Багийн хайлт (Steam холбосон хүнд) |
| `/ban`, `/unban` | Ban Members | SteamID ban, хугацаатай эсвэл мөнхийн |
| `/clear amount` | Manage Messages | Сүүлийн 1–100 мессеж устгах |
| `/rules [section]` | Manage Server | Цэстэй самбар (section-гүй), эсвэл тухайн дүрмийг шууд |
| `/faq-setup` ✱ | эзэн | FAQ самбар (өнгөт товчнууд) |
| `/ticket-setup` ✱ | эзэн | Ticket самбар |
| `/voice-setup` ✱ | эзэн | "Join to create" voice |
| `/automod setup` ✱ | эзэн | Хараал + зар шүүлтүүр асаах (`add/remove/allow/list/off` Manage Server) |
| `/updates [off]` ✱ | эзэн | Update-ийн мэдээ ирэх channel |
| `/admincalls [role] [off]` | Manage Server | `!calladmin`/`!callmanager`/`!report` feed |
| `/penalties` | Manage Server | Шийтгэлийн feed |
| `/news` | Manage Server | CS2 мэдээний channel (монголоор) |
| `/status` | Manage Server | Live server самбар |
| `/ad set\|stop` | Manage Server | Channel-ийн доод талд байнга сэргээдэг зар |
| `/welcome-preview` | Manage Server | Welcome мессежийг өөрөөсөө урьдчилж харах |

## 5. Анхны тохируулга (Discord дээр)

Нэг удаа, тохирох channel дээр:

| Channel | Команд |
|---|---|
| #дүрэм | `/rules` (цэстэй самбар) |
| #faq | `/faq-setup` |
| #support | `/ticket-setup staff_role:@Staff log_channel:#ticket-log` |
| #шийтгэл | `/penalties` |
| #admin-дуудлага | `/admincalls role:@Staff` |
| #update | `/updates` |
| #servers | `/status` |
| #мэдээ | `/news` |
| Voice ангилал | `/voice-setup` |
| Автомод | `/automod setup log_channel:#mod-log` |

Дүрмийн мессеж өөрчлөгдвөл хуучин мессежийг устгаад командыг дахин ажиллуул. Хэн ч channel-д мессеж бичих боломжгүй (read-only) байлгаж болно.

## 6. Автомод: хараал ба зар

`/automod setup` Discord-ын **AutoMod**-ын гурван дүрмийг үүсгэнэ (мессеж илгээгдэхээс өмнө хаагдана):

| Дүрэм | Юуг хаах |
|---|---|
| **LEGACY-X · Хараал** | Монгол (кирилл + латин), орос, англи хараал; `п.и.з.д`, `f u c k` гэх мэт далд бичлэг |
| **LEGACY-X · Гадаад хараал** | Discord-ын бэлэн англи жагсаалт (хараал, доромжлол, бэлгийн агуулга). Server-д ганц |
| **LEGACY-X · Зар сурталчилгаа** | Бусад Discord server-ийн урилга, Telegram/WhatsApp группын линк, бусад community-ийн нэр (lann.mn, leet.mn, novacs.cc, rage.mn, yasha.lol, 1st.mn), `connect <ip>` |

- 10 минутад 3 удаа хаагдсан хүн **timeout** авна (`timeout_minutes`, анхдагч 10, 0 = байхгүй).
- Manage Server эрхтэй хүнийг Discord шалгадаггүй.
- Үг нэмэх: `/automod add word:<үг>` (`үг*` = эхэлсэн, `*үг*` = агуулсан); хасах `/automod remove`; андуурч хаагдсан үгийг `/automod allow word:`; `/automod list`.
- **Regex хязгаар:** Discord-ын regex нь Rust хэлбэртэй ба хэт том бол (`exceeded size limit`) setup амжилтгүй болдог. Тиймээс `automod.js` дахь
  шаблонууд `\W`, `\b`, `\d`-г богино ASCII хэлбэр болгон (`lean()`) хувиргадаг. Шинэ regex нэмэхдээ ижил хэв маягийг дага.
- Setup амжилтгүй бол Discord-ын яг алдааг staff-д ephemeral хариунд харуулна.

## 7. Banner ба зураг

- Бүх "гарчигтай banner" (support, FAQ, дүрэм) нэг загвартай: **1100×268**, зүүн талд logo, kicker, том гарчиг, нэг мөр; баруун талд agent
  (`AGENT_POSE` хүснэгт `src/cards.js` дотор). Agent бүрийн толгой ойролцоо ижил хэмжээтэй.
- Agent зураг: **арын дэвсгэргүй PNG/WebP** (`assets/img/agent-*.png`). Шинэ agent нэмэх:
  1. Зургаа `assets/img/`-д тавь.
  2. `src/draw.js` дотор `images` жагсаалтад нэмж, нэр өг.
  3. `src/cards.js` дотор `AGENT_POSE`-д `{ h, right, top }` тохируулж, `renderBanner` дахь зураглалд нэм.
  4. Урьдчилж зур: `renderBanner({ kicker, title, sub, agent: "<нэр>" })`-ийг PNG болгож шалга.
- **Update banner** (алтан, хоёр agent, голд текст): `renderUpdateBanner` (CS2 update finished).
- Фонт: Onest (текст), Black Ops One (том гарчиг): `assets/fonts/`.
- Bot-ын profile зураг: `node scripts/brand.mjs` → `assets/brand/` (Developer Portal-д гараар оруулна). Role icon: `node scripts/roles.mjs`.

## 8. Агуулга засах

| Юу | Хаана | Дараа нь |
|---|---|---|
| FAQ хариулт | `faq/<id>.md` (эхний `# ` мөр = гарчиг, 4000 тэмдэгтээс бага) | `git pull` (restart үгүй) |
| FAQ асуулт нэмэх | `faq/` шинэ файл + `src/faq.js` дахь `FAQ_TOPICS` (id, emoji, tone, label); нэг мөрт 4 товч, нийт ≤25 | deploy |
| Дүрэм | `rules/ingame.md`, `players.md`, `staff.md` | `git pull`; цэс шууд шинэчлэгдэнэ |
| Welcome текст | `src/welcome.js` | deploy |
| Хариултын өнгө (FAQ товч) | `src/faq.js`: `go` ногоон, `main` цэнхэр, `quiet` саарал, `warn` улаан | deploy |

## 9. Төлөв ба өгөгдөл

`data/` хавтас (git-д ордоггүй). Устгавал тухайн функц анхны төлөвт буцна (channel дахин сонгох хэрэгтэй).

| Файл | Юу |
|---|---|
| `boards.json` | `/status` самбарууд |
| `penalty-feed.json`, `admin-calls.json`, `announcements.json`, `news.json` | Feed-ийн channel, сүүлд харсан id |
| `rank-roles.json` | Цолны role-ийн ID (устгавал дахин үүснэ) |
| `automod.json` | Автомодын тохиргоо, нэмсэн/хассан үг, дүрмийн ID |
| `tickets.json`, `ban-issuers.json` | Ticket, ban гаргагч |
| `lfg.json`, `voice.json`, `ad.json` | LFG, voice өрөө, зар |

Нөөцлөх: `data/`-г хуулж аваарай (`cp -r data data.bak`). Нууц нь `.env` (`DISCORD_TOKEN`, `API_TOKEN`): `chmod 600`.

## 10. Тест ба хөгжүүлэлт

```bash
npm ci
npm run check      # синтакс + node --test (106 тест)
npm start          # node --env-file=.env src/index.js
```

- Шинэ команд: `src/<нэр>.js` (`SlashCommandBuilder` export + handler) → `src/index.js`-д бүртгэж, handler-д холбоно → `test/<нэр>.test.js`.
- Owner-only болгох: `src/index.js` дахь `OWNER_ONLY` багц.
- Дэлгэцийн урьдчилсан харалт: banner функцийг PNG болгон бичиж үз (Discord-д илгээхгүйгээр).
- Commit нэр нь Discord-д олон нийтэд харагдана: нэг энгийн өгүүлбэр; нууцлал/аюулгүй байдлын засвар бол `[skip announce]`.

## 11. Алдаа засах

| Шинж тэмдэг | Шалтгаан | Засах |
|---|---|---|
| Bot онлайн биш | Token буруу, pm2 унасан | `pm2 logs legacy-x-discord`; `.env` |
| Slash command харагдахгүй | Эхлэхэд бүртгэгдээгүй, `GUILD_ID` буруу | `GUILD_ID`; restart; урилга `applications.commands` |
| "missing permissions in #…" | Channel-д эрх дутуу | Channel дээр bot-д View, Send, Embed, Attach |
| Цолны role өгөхгүй | Bot-ын role доор, Manage Roles дутуу | Bot-ын role-ыг дээш |
| Welcome ирэхгүй | Server Members Intent OFF, `WELCOME_CHANNEL_ID` | Developer Portal; ID |
| `/automod setup` → `exceeded size limit` | Regex хэт том | §6 (`lean()`); шинэ regex-ийг богино бич |
| `/automod setup` → `Maximum number of AutoMod rules` | Server-д keyword дүрэм дүүрсэн (6) | Өөр bot-ын дүрмийг хас |
| Дуудлага/update feed ирэхгүй | `API_TOKEN` scope, backend хуучин, `/admincalls`/`/updates` тохируулаагүй | Token `discord:link`; backend deploy; команд |
| `/ban` алдаа | `bans:write` scope | Token дахин |
| Хаагдахгүй хараал | Жагсаалтад байхгүй | `/automod add word:` |
| Андуурч хаагдсан | Жагсаалтад байгаа | `/automod allow word:` |
| Deploy "No [ready] line" | Эхлэхэд унасан | `pm2 logs legacy-x-discord --lines 50` |

## 12. Бусад баримт

Индекс: **[README.md](README.md)**. Бүх системийн нэгтгэсэн гарын авлага: `legacyxxx-plugins` → `docs/MANUAL_MN.md`.
