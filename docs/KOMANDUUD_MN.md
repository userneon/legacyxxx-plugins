# LEGACY-X: бүх командын лавлах (Монгол)

Команд бүрд: **хаана бичих**, **хэрхэн бичих**, **хэзээ ашиглах**, **жишээ**, **гарч болох алдаа ба
шалтгаан, засах арга**. Суулгах, анхны тохиргоо: [GARIIN_AVLAGA_MN.md](GARIIN_AVLAGA_MN.md).

## Хэрхэн уншиж, ашиглах вэ

| Тэмдэглэгээ | Хаана бичих |
|---|---|
| **[VPS]** | VPS-ийн terminal (`ssh root@<IP>`) |
| **[CS2 chat]** | Тоглоом дотор `Y` (бүгдэд) эсвэл `U` (багт) дарж chat-д |
| **[CS2 console]** | Тоглоом дотор `~` товч, эсвэл серверийн console (`cs2-host.sh logs`, AMP console) |
| **[Discord]** | Discord-ийн дурын channel-д `/` гэж эхлүүлнэ |

- `<…>` = заавал бичих утга, `[…]` = бичихгүй байж болно. Хаалтыг өөрийг нь бичихгүй.
  Жишээ: `!ban <тоглогч> [минут]` → `!ban Temuujin 30`.
- Тоглоом дотор `!` болон `/` хоёулаа ажиллана (`/` бол chat-д бусдад харагдахгүй). Console-д `css_`
  гэж эхэлнэ: `!ban` = `css_ban`.
- **Тоглогчийг заах арга:** нэр (хэсэг нь ч болно), `#userid` (console-ийн `status`-аас), SteamID64,
  мөн зарим командод `@all`, `@t`, `@ct`, `@spec`.
- `sudo` = root эрхээр. Та `root`-оор нэвтэрсэн бол `sudo` бичих шаардлагагүй.

---

## 1. Deploy: шинэчлэх командууд [VPS]

Код GitHub-д шинэчлэгдсэн гэж хэлэх бүрд тухайн хэсгийг шинэчилнэ. VPS өөрөө татдаггүй (plugin-ийн
05:00-ийн автомат update-ээс бусад).

### `bash ops/deploy.sh` (API)

```bash
cd /root/legacyxxx-backend && bash ops/deploy.sh
```
- **Хэзээ:** API, backend-ийн код шинэчлэгдсэн үед.
- **Юу хийх:** `git pull` → `npm ci` → typecheck → build → `pm2 reload legacy-x-api` → `/health` шалгана.
- **Амжилттай:** `==> Done: <commit> <тайлбар> (healthy)`.

| Алдаа | Хэзээ гардаг | Засах |
|---|---|---|
| `No such file or directory` (ops/deploy.sh) | Буруу фолдерт, эсвэл script нэмэгдсэнээс хойш pull хийгээгүй | `cd /root/legacyxxx-backend && git pull && bash ops/deploy.sh` |
| `!! No .env here` | `.env`-гүй фолдерт (шинэ давхар clone) | `/root/legacyxxx-backend`-д ажиллуулна |
| `Not possible to fast-forward` | VPS дээр файл гараар зассан | `git status` → `git checkout -- <файл>` |
| `error TS…` (typecheck) | Код алдаатай commit | Надад гаралтыг явуул. Хуучин хувилбар ажилласаар байна |
| `!! The API did not answer /health` | API асахгүй байна (`.env` буруу, порт давхцсан, DB) | `pm2 logs legacy-x-api --lines 50` |
| `pm2: command not found` | pm2 суугаагүй | `npm install -g pm2` |

### `bash ops/deploy.sh` (Discord bot)

```bash
cd /root/legacyxxx-discord-bot && bash ops/deploy.sh
```
- **Хэзээ:** bot-ын код шинэчлэгдсэн үед.
- **Юу хийх:** `git pull` → `npm ci` → тест → `pm2 reload legacy-x-discord` → шинэ `[ready]` мөр гартал хүлээнэ.
- **Амжилттай:** `==> Done: <commit> <тайлбар>`.

| Алдаа | Хэзээ | Засах |
|---|---|---|
| `!! No .env here` | `.env`-гүй фолдерт | `/root/legacyxxx-discord-bot`-д ажиллуулна |
| `not ok … ` (тест) | Код алдаатай | Гаралтыг надад явуул. Хуучин хувилбар ажилласаар |
| `!! No [ready] line yet` | Bot Discord-д нэвтэрч чадсангүй (token буруу, интернэт) | `pm2 logs legacy-x-discord --lines 50` |
| `Used disallowed intents` (log-д) | Discord Developer Portal-д intent асаагүй | Bot → Privileged Gateway Intents асаах |

### `bash ops/deploy.sh` (вэб)

```bash
cd /root/legacyxxx-frontend && bash ops/deploy.sh
```
- **Хэзээ:** вэбийн өөрчлөлт (дизайн, хуудас) орсон үед.
- **Юу хийх:** `git pull` → `npm ci` → build → `/var/www/legacyx` руу хуулна → `nginx -t` → reload.
- **Амжилттай:** `==> Done: <commit> <тайлбар>`. Дараа нь browser-т **Ctrl+Shift+R**.
- **Өөр фолдер:** `WEB_ROOT=/таны/зам bash ops/deploy.sh` (Nginx-ийн зам: `grep -rn "root " /etc/nginx/sites-enabled/`).

| Алдаа | Хэзээ | Засах |
|---|---|---|
| `!! Build contains mock code` | Туршилтын өгөгдөл build-д орсон | Юу ч нийтлээгүй, сайт хэвээр. Надад хэл |
| `npm ERR! … EUSAGE` / lock file | `package-lock.json` VPS дээр өөрчлөгдсөн | `git checkout -- package-lock.json` дараа нь дахин |
| `nginx: [emerg] …` | Nginx тохиргоо алдаатай | `sudo nginx -t` гаралтыг уншина |
| Вэб хуучнаараа | Browser cache эсвэл WEB_ROOT буруу | Ctrl+Shift+R; WEB_ROOT-оо шалгах |

### `cs2-host.sh deploy` (CS2 plugins)

```bash
sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh deploy
```
- **Хэзээ:** plugin шинэчлэгдсэн, одоо шууд оруулах үед (05:00-ийг хүлээхгүй).
- **Юу хийх:** `git pull` → CS2, Metamod, CounterStrikeSharp, plugin → WeaponPaints signature шалгах → серверүүдийг restart.
- **Анхаар:** ажиллаж байгаа тоглолт тасарна (серверүүд restart хийнэ).
- **AMP panel** дээр ажиллаж байгаа бол энэ хэрэггүй: zip-ээ panel дээр задална.

Алдаанууд нь доорх `cs2-host.sh`-тэй ижил.

---

## 2. `cs2-host.sh`: CS2 сервер удирдах [VPS]

Бүгд `/opt/legacyxxx-plugins`-ээс, `sudo`-тэй (`status`, `logs`-оос бусад).

| Команд | Хэзээ | Жишээ |
|---|---|---|
| `install --env <файл> [--package <zip>]` | Шинэ VPS дээр **нэг удаа** | `sudo ./scripts/cs2-host.sh install --env /root/legacyx-srv-1.2.3.4.env` |
| `add <port> <GSLT> [mode] [map] [тоглогч] [вэб-mode] [нэр…]` | Шинэ сервер нэмэх | `sudo ./scripts/cs2-host.sh add 27016 <GSLT> casual de_mirage 20 fun LEGACY-X FUN #1` |
| `remove <port>` | Сервер устгах | `sudo ./scripts/cs2-host.sh remove 27017` |
| `deploy` | GitHub-аас татаад шинэчлэх | `sudo ./scripts/cs2-host.sh deploy` |
| `update [--package <zip>]` | Татахгүйгээр бүгдийг шинэчлэх | `sudo ./scripts/cs2-host.sh update` |
| `autoupdate on\|off\|check` | Автомат update асаах, унтраах, одоо шалгах | `sudo ./scripts/cs2-host.sh autoupdate check` |
| `status` | Аль сервер ажиллаж байгаа | `./scripts/cs2-host.sh status` → `27015 active` |
| `logs <port>` | Серверийн console (гарах: Ctrl+C) | `./scripts/cs2-host.sh logs 27015` |

- `add`-ийн тоглоомын mode: `competitive`, `wingman`, `casual`, `deathmatch`.
- `add`-ийн вэб-mode: `competitive_5v5`, `fun`, `proleague` (вэбийн аль Play хуудсанд гарах).
- `AUTOUPDATE_HOUR=4 sudo ./scripts/cs2-host.sh autoupdate on` → чимээгүй цагийг 04:00 болгоно.

| Алдаа | Хэзээ | Засах |
|---|---|---|
| `!! Run with sudo.` | root биш | Командын өмнө `sudo` |
| `!! Run install first.` | `add` хийхэд суулгаагүй | Эхлээд `install --env …` |
| `!! No …/.env yet: run install with --env first.` | `add` вэб-mode, нэр бичихэд `.env` алга | `install --env <файл>` |
| `!! --env file not found` | `.env` файлын зам буруу | `ls /root/*.env`-ээр зөв замаа олно |
| `!! The GSLT is a 32-character code…` | GSLT буруу (урт, тэмдэгт) | Steam хуудаснаас зөв хуулна |
| `!! Map name like de_dust2.` | Map-ийн нэр буруу | Жижиг үсгээр `de_mirage` г.м. |
| `!! maxplayers: 2-64.` | Тоглогчийн тоо хэт их/бага | 2–64 |
| `!! Mode must be competitive, wingman, casual or deathmatch.` | Тоглоомын mode буруу | Эдгээрийн аль нэг |
| `!! site-mode like competitive_5v5, fun…` | Вэб-mode буруу | `competitive_5v5`, `fun`, `proleague` |
| `!! No server on port 27017.` | `remove` хийх порт байхгүй | `status`-аар шалгах |
| `!! CS2 did not install…` | steamcmd татаж чадсангүй (disk, сүлжээ) | `df -h`; дахин `install` |
| `!! Could not add Metamod to gameinfo.gi` | CS2-ийн файлын бүтэц өөрчлөгдсөн | Надад хэл |
| `!! Could not find the CounterStrikeSharp Linux release.` | GitHub API хүрэхгүй / хязгаар | Хэдэн минутын дараа дахин |
| `!! No dotnet-sdk-8.0 package here` / `Node 18+ is needed` | Build хийх програм алга | Ubuntu 24.04 ашиглах, эсвэл `--package legacyx-cs2.zip` |
| `fatal: … 403` / `could not read Username` | Token асуудал | [GARIIN_AVLAGA_MN.md](GARIIN_AVLAGA_MN.md) 3-р алхам |
| `WeaponPaints signature … no maintained signature matches` | CS2 update-ийн дараа skin-ий signature хараахан гараагүй | Юу ч хийхгүй: 10 мин тутам өөрөө оролдоно. Серверүүд ажилласаар, зөвхөн skin түр унтарна |

---

## 3. VPS дээрх бусад хэрэгтэй командууд [VPS]

| Команд | Юунд |
|---|---|
| `pm2 ls` | API, bot ажиллаж байгаа эсэх (`online`) |
| `pm2 logs legacy-x-api --lines 100` | API-ийн log |
| `pm2 logs legacy-x-discord --lines 100` | Bot-ын log |
| `pm2 restart legacy-x-api` | API-г дахин асаах (код өөрчлөхгүй) |
| `pm2 describe legacy-x-api \| grep "exec cwd"` | API аль фолдероос ажиллаж байгаа |
| `systemctl status cs2@27015` | CS2 сервер ажиллаж байгаа эсэх |
| `sudo systemctl restart cs2@27015` | Нэг серверийг restart (тохиргоо өөрчилсний дараа) |
| `sudo systemctl restart 'cs2@*'` | Бүх CS2 серверийг restart (`.env` өөрчилсний дараа) |
| `journalctl -u cs2@27015 --since "1 hour ago"` | Серверийн сүүлийн 1 цагийн log |
| `journalctl -u legacyx-cs2-autoupdate -n 50` | Автомат update юу хийснийг |
| `systemctl list-timers legacyx-cs2-autoupdate` | Дараагийн автомат шалгалт хэзээ |
| `sudo nginx -t && sudo systemctl reload nginx` | Nginx тохиргоо шалгаад дахин ачаалах |
| `df -h` | Disk хэр дүүрсэн |
| `free -h` | RAM (сервер бүрт 2–3 GB) |
| `git -C <зам> status` | VPS дээр гараар зассан файл байгаа эсэх |
| `git -C <зам> checkout -- <файл>` | Гараар зассан файлыг буцаах |

### `env-backup.sh`: `.env` нөөцлөх

| Команд | Юу | Хэзээ |
|---|---|---|
| `sudo /opt/legacyxxx-plugins/scripts/env-backup.sh run` | Гурван `.env`-ийг одоо нөөцлөх | `.env` засахын өмнө, дараа |
| `… env-backup.sh on` / `off` | Өдөр бүр 04:30-д автоматаар | Нэг удаа асаана |
| `… env-backup.sh list` | Байгаа нөөцүүд | — |
| `… env-backup.sh restore <огноо-цаг>` | Сэргээх (одоогийнхоо эхлээд нөөцөлнө) | Файл устсан, эвдэрсэн |
| `… env-backup.sh bundle` | Хамгийн сүүлийн нөөцийг нэг `.tar.gz` болгох | VPS-ээс гадна хуулахын өмнө |

| Алдаа | Шалтгаан | Засах |
|---|---|---|
| `!! Run with sudo.` | root биш | `sudo` |
| `!! No .env file found to back up.` | Гурван зам бүгд буруу | `ENV_FILES="/зам/.env …" sudo -E …/env-backup.sh run` |
| `!! No backup … (see: list)` | Нөөцийн нэр буруу | `list`-ээс хуулах |

**Бүх repo-гийн төлөв нэг дор:** [GARIIN_AVLAGA_MN.md](GARIIN_AVLAGA_MN.md) 15-р хэсэг.

### `create-game-server.mjs` [VPS, backend фолдерт]

```bash
cd /root/legacyxxx-backend
node --env-file=.env scripts/create-game-server.mjs <IP> "27015:competitive_5v5:LEGACY-X #1" "27016:fun:Fun #1"
```
- **Хэзээ:** шинэ Game VPS нэмэх, эсвэл plugin token алдагдсан үед (шинэ token үүсгэж хуучныг хаана).
- **Үр дүн:** `legacyx-srv-<IP>.env` файл. Үүнийг CS2-ийн `addons/counterstrikesharp/.env` болгоно.
- **Анхаар:** дахин ажиллуулбал хуучин token ажиллахаа болино: шинэ `.env`-ээ CS2 сервер рүү заавал хуулна.

| Алдаа | Хэзээ | Засах |
|---|---|---|
| `Usage: …` | Аргумент буруу (IP, порт, mode) | Жишээн дээрх хэлбэрээр |
| `SUPABASE_URL and SUPABASE_SERVICE_ROLE_KEY must be set` | `--env-file=.env`-гүй эсвэл буруу фолдерт | backend фолдерт, `--env-file=.env`-тэй |
| `Could not create the token: …` | DB хүрэхгүй | Supabase болон `.env`-ээ шалгах |

---

## 4. Тоглоом доторх командууд: бүх тоглогч [CS2 chat]

| Команд | Юу хийх | Хэзээ |
|---|---|---|
| `!rank` | Таны цол, EXP, байр, дараагийн цол хүртэл | Хүссэн үедээ |
| `!rs` | Вэб дээр сонгосон skin-ээ дахин ачаалах | Skin солиод, эсвэл skin гараагүй үед |
| `!knife`, `!gloves`, `!agents`, `!music`, `!pins` | Цэснээс сонгох | Skin-ийг тоглоом дотроос |
| `!calladmin` / `!callmanager` | Онлайн admin / manager-ийг дуудах. Онлайн staff байхгүй ч хүсэлт Discord-ын дуудлагын channel-д (`/admincalls`) очно | Тусламж хэрэгтэй үед |
| `!report` | Тоглогчийг report хийх (цэс гарна) | Хуурсан, муу авир |
| `!admins` | Онлайн staff | — |
| `.ready` / `.unready` | Бэлэн / бэлэн биш | Match эхлэхийн өмнө |
| `.pause` / `.unpause` / `.tech` / `.tac` | Завсарлага, техникийн, тактикийн timeout | Match-ийн үеэр |
| `.stay` / `.switch` | Knife хожсон баг талаа сонгох | Knife round-ийн дараа |
| `.stop` | Round-ийг дахин тоглох хүсэлт (хоёр баг) | Damage өгөхөөс өмнө |
| `.coach t` / `.coach ct` / `.uncoach` | Coach болох / болихоо | Match mode-д |

| Тоглоом доторх мессеж | Утга |
|---|---|
| `Wait a moment before trying again.` | `!rs`-ийг хэт ойр давтсан. Хэдэн секунд хүлээнэ |
| `No rank yet. Play a 5v5 match to get one…` | Цол хараахан байхгүй: legacyx.cc-д Steam-ээр нэвтэрч 5v5 тоглоно |
| `Your rank is unavailable right now. Try again soon.` | Сайт (API) түр хүрэхгүй |
| `Ranks are off on this server.` | Энэ серверт цол унтраалттай |
| `No admin is online.` / `No manager is online.` | Дуудах хүн онлайн алга |
| `Wait 2 minutes between reports.` / `You already reported this player. Wait 3 minutes.` | Report хэт ойр |
| `You're AFK. Move or you'll be kicked in 15 seconds.` | AFK: хөдлөх хэрэгтэй |
| `Stop camping at … or you'll be killed in … seconds.` | Нэг газар хэт удаан |
| `Your chat is blocked.` | Танд gag (chat хаалт) өгсөн |
| `Too late. Damage was already done this round.` | `.stop` хэт оройтсон |
| `Not ready: …` | Бэлэн болоогүй тоглогчид |

---

## 5. Тоглоом доторх командууд: staff [CS2 chat]

Role-ийг legacyx.cc/staffpanel дээр өгнө. Role бүр доод role-ийнхоо бүх командыг ажиллуулна.
Хоёр шалгуур: role-ийн эрх **ба** stamina (STAFF 250, ADMIN 500, MANAGER 750, OWNER 1000).

`!admin` цэс: `W/S` сонгох, `E` дарах, `A` буцах, `R` гарах. Ихэнх командыг цэснээс хийж болно.

> ⚠️ **`!ban`, `!mute`, `!gag`, `!silence`-д минутаа бичихгүй бол (эсвэл `0`) шийтгэл БҮРМӨСӨН болно.**
> Түр хугацаагаар бол заавал минут бичнэ: `!ban Bat 60` = 1 цаг, `1440` = 1 өдөр, `10080` = 7 хоног.

### STAFF

| Команд | Жишээ | Юу |
|---|---|---|
| `!admin` | `!admin` | Admin цэс |
| `!mute <тоглогч> [минут] [шалтгаан]` | `!mute Bat 30 spam` | Voice хаах (минутгүй = бүрмөсөн) |
| `!unmute <тоглогч>` | `!unmute Bat` | Voice нээх |
| `!gag <тоглогч> [минут] [шалтгаан]` | `!gag Bat 15 insults` | Chat хаах (минутгүй = бүрмөсөн) |
| `!ungag <тоглогч>` | `!ungag Bat` | Chat нээх |
| `!silence` / `!unsilence` | `!silence Bat 10` | Voice + chat хоёуланг |
| `!mutelist`, `!gaglist` | — | Хаалттай тоглогчид |
| `!asay <текст>` | `!asay check discord` | Зөвхөн staff-д харагдах chat |
| `!psay <тоглогч> <текст>` | `!psay Bat stop` | Хувийн мессеж |
| `!who <тоглогч>` | `!who Bat` | Тоглогчийн мэдээлэл |
| `!vote <асуулт> <A> <B>`, `!votemap <map> <map>`, `!votekick`, `!votemute`, `!votegag` | `!votemap de_dust2 de_mirage` | Санал хураалт |

### ADMIN (дээрх + )

| Команд | Жишээ | Юу |
|---|---|---|
| `!ban <тоглогч> [минут] [шалтгаан]` | `!ban Bat 1440 cheating` | Ban (минутгүй эсвэл `0` = **бүрмөсөн**). Бүх серверт, вэб дээр ч бичигдэнэ |
| `!lastban` | — | Саяхан гарсан тоглогчийг ban хийх цэс |
| `!baninfo <тоглогч>` | `!baninfo 7656119…` | Ban-ы мэдээлэл |
| `!kick <тоглогч> [шалтгаан]` | `!kick Bat afk` | Серверээс гаргах |
| `!slap <тоглогч> <damage>` | `!slap Bat 5` | — |
| `!csay`, `!hsay <текст>` | `!csay 5 min break` | Дэлгэцийн голд |
| `!hideadmin` | — | `!admins`-аас өөрийгөө нуух |
| `!start` | — | **Match-ийг албадан эхлүүлэх** (бүгд ready болохыг хүлээхгүй) |
| `!rr` | — | **Round-ийг дахин эхлүүлэх** |
| `!voteban`, `!votesilence`, `!rvote`, `!cancelvote` | — | Санал хураалт |

### MANAGER (дээрх + )

| Команд | Жишээ | Юу |
|---|---|---|
| `!unban <тоглогч/SteamID>` | `!unban 7656119…` | Ban цуцлах |
| `!ipban <тоглогч> [шалтгаан]` | — | IP ban |
| `!banlist` | — | Ban-ы жагсаалт |
| `!map <map>`, `!wsmap <id>` | `!map de_inferno` | Map солих |
| `!slay <тоглогч>` | — | Алах |
| `!team <тоглогч> <t\|ct\|spec>`, `!swap <тоглогч>` | `!team Bat ct` | Баг солих |
| `!rename <тоглогч> <нэр>` | — | Нэр солих |
| `!restart` | — | **Match-ийг бүхэлд нь** warmup руу reset (`!rr`-ээс өөр) |
| `!endmatch`, `!forcepause`, `!forceunpause` | — | Match дуусгах, албадан завсарлах |
| `!restore <round>` | `!restore 12` | Хадгалсан round-оос сэргээх |
| `!skipveto`, `!readyrequired <тоо>`, `!roundknife`, `!playout`, `!whitelist`, `!settings` | `!readyrequired 8` | Match тохиргоо |
| `css_legacyx_fill <SteamID64> <team1\|team2> <1-5>` [console] | — | Гарсан тоглогчийн оронд түр тоглогч |

### OWNER (бүгд + )

| Команд | Юу |
|---|---|
| `!cleanbans` | **Бүх серверийн бүх ban-ыг** вэбээс цэвэрлэх (буцаах боломжгүй) |
| `!cleanall`, `!cleanmute`, `!cleangag` | Бүх шийтгэлийг цэвэрлэх |
| `!rcon <команд>`, `!cvar <нэр> [утга]` | Серверийн тохиргоо |
| `!prac`, `!match`, `!sleep`, `!exitprac` | Practice mode |
| `!money`, `!armor`, `!hp`, `!god`, `!noclip`, `!weapon`, `!strip`, `!respawn`, `!freeze`, `!beacon`, `!glow`, `!drug`, `!blind`, `!shake`, `!bury`, `!goto`, `!bring`, `!gravity` | Fun командууд (`!admin` цэсэнд байхгүй) |

### Staff-д гарах мессежүүд

| Мессеж | Хэзээ | Засах |
|---|---|---|
| `You don't have access to that.` | Role-д энэ командын эрх алга | Дээд role хэрэгтэй (staffpanel) |
| `Your role can't use this.` | Stamina хүрэхгүй | Staffpanel дээр stamina-г нэмэх, эсвэл дээд role |
| `Only staff can do that.` | MatchZy-ийн командад эрх алга (жишээ нь STAFF `!start`) | ADMIN+ хэрэгтэй |
| `No player found.` | Нэр буруу, тоглогч гарсан | Нэрийн хэсгийг, эсвэл `#userid` |
| `More than one player matches. Be more exact.` | Нэг нэрээр олон тоглогч | Урт нэр эсвэл `#userid` |
| `This player is protected.` / `… is protected.` | Тоглогч таниас дээд role-той | Дээд staff-д хандах |
| `Not on yourself.` | Өөр дээрээ | — |
| `Use !ban <player> [minutes] [reason].` | Командыг буруу бичсэн | Үзүүлсэн хэлбэрээр |
| `… is already banned.` / `Voice is already blocked.` | Давхар | — |
| `The match already started.` | `!start` match эхэлсний дараа | `.unpause` эсвэл `!restart` |
| `Leave practice first with .exitprac.` | Practice mode-д `!start` | `.exitprac` дараа нь `!start` |
| `Staff list updated.` | Вэбээс эрхийг дахин шалгасан | — |
| `Bans were not cleared. Try again.` / `Only the owner can clear all bans.` | `!cleanbans` амжилтгүй / owner биш | API-г шалгах / owner-оор |

> Staff эрх ирэхгүй бол: серверийн log-оос `authorized as OWNER` гэх мөрийг хайна
> (`./scripts/cs2-host.sh logs 27015`). API хүрэхгүй үед хэн ч эрх авахгүй (fail closed).

---

## 6. CS2 console командууд [CS2 console, сервер]

| Команд | Юунд | Хэвийн хариу |
|---|---|---|
| `meta list` | Metamod ажиллаж байгаа | `CounterStrikeSharp` жагсаалтад |
| `css_plugins list` | Plugin-ууд | 8 `LegacyX-…` бүгд `LOADED` |
| `lx_scoreboard_type <0\|11\|12>` | Tab-ийн цолны харагдах байдал (серверийн console-оос) | 0 = нэрний өмнө цол |
| `status` | Тоглогчид, `#userid` | — |
| `connect <IP>:<порт>` | Сервер рүү орох (тоглогчийн console) | — |

| Алдаа | Шалтгаан | Засах |
|---|---|---|
| `Unknown command 'css_plugins'` | Metamod/CSS ачаалагдаагүй (CS2 update `gameinfo.gi`-г дарсан) | `sudo …/cs2-host.sh update` |
| Plugin `LOADING` дээр гацсан / `CoreLib` | CSS дутуу хуулагдсан | `update` дахин; AMP бол zip-ээ дахин задлах |
| Skin гарахгүй, log-д `FollowCS2ServerGuidelines` | `core.json` true | `configs/core.json`-д `false` болгоод restart |

---

## 7. Discord командууд [Discord]

| Команд | Хэн | Юу | Жишээ |
|---|---|---|---|
| `/link` | Бүгд | Discord-оо LEGACY-X (Steam) account-тай холбох, цолны role авах | `/link` → 10 минутын холбоос |
| `/unlink` | Бүгд | Холболт болон цолны role-ийг хасах | — |
| `/top [by]` | Бүгд | Шилдэг 3 (EXP, K/D, win rate) | `/top by:kd` |
| `/servers` | Бүгд | Серверүүд, map, тоглогч, Connect холбоос | — |
| `/lfg mode [need] [note]` | Бүгд | "Баг хайж байна" пост. Нэгдэхэд `/link` хэрэгтэй | `/lfg mode:5v5 need:2` |
| `/ban steamid duration reason` | Ban Members | SteamID ban (бүх CS2 серверт) | — |
| `/unban steamid` | Ban Members | Ban цуцлах | — |
| `/clear amount` | Manage Messages | Сүүлийн 1–100 мессеж устгах (14 хоногоос хуучныг Discord алгасна) | `/clear 20` |
| `/ticket-setup staff_role [log_channel] [manager_role]` | Manage Server | Ticket самбар байрлуулах | — |
| `/status`, `/penalties`, `/news` | Manage Server | Энэ channel-ийг серверийн самбар, шийтгэлийн feed, мэдээ болгох | — |
| `/rules section` | Manage Server | Дүрэм нийтлэх (Ingame, Discord, Staff) | — |
| `/voice-setup [channel]` | Manage Server | "Join to create" voice | — |
| `/ad set` / `/ad stop` | Manage Server | Channel-ийн доор байнга байх зар | — |
| `/automod setup\|add\|remove\|allow\|list\|off` | Manage Server | Хараалын шүүлтүүр | — |
| `/welcome-preview` | Manage Server | Welcome мессежийг өөртөө харах | — |

### Ticket-ийн товчнууд (бүгд зөвхөн staff)

| Товч | Хэн | Юу |
|---|---|---|
| ✋ Хариуцах / ↩️ Суллах | Staff / хариуцсан хүн эсвэл manager | Авах, буцааж суллах |
| 🔁 Шилжүүлэх | Хариуцсан хүн эсвэл manager (хэн ч аваагүй бол аль ч staff) | Өөр staff-д |
| ➕ Хүн нэмэх / ➖ Хүн хасах | Staff | 5 хүртэл |
| 🔒 Хаах | Staff | Transcript хадгалаад түгжинэ |

### Discord-д гарах мессежүүд

| Мессеж | Утга | Засах |
|---|---|---|
| `Bot дөнгөж асч байна.` | Bot restart хийсний дараахь хэдэн секунд | Хэсэг хүлээгээд дахин |
| `Сайттай холбогдож чадсангүй.` / `Leaderboard-ийг одоо авч чадсангүй.` | API түр хүрэхгүй | `pm2 logs legacy-x-api`; хэсэг хүлээх |
| `Steam холболт одоогоор ажиллахгүй / идэвхгүй байна.` | `/link`-ийн тохиргоо (API token) дутуу | Bot-ын `.env`-ийн `API_TOKEN` |
| `⌛ Холбоосын хугацаа дууссан.` | `/link` 10 минутаас хэтэрсэн | `/link` дахин |
| `Зөвхөн нийтэлсэн хүн эсвэл staff хааж чадна.` | Бусдын LFG-г хаах гэсэн | — |
| `Ticket-ийг зөвхөн staff хаана.` | Энгийн хэрэглэгч ticket хаах гэсэн | Staff хаана |
| `Өөрийн нээсэн ticket-ийг хариуцах / удирдах / хаах боломжгүй.` | Staff өөрийн ticket дээр | Өөр staff |
| `Зөвхөн <@…> эсвэл manager суллана / шилжүүлнэ.` | Өөр хүний хариуцсан ticket | Хариуцсан хүн эсвэл manager |
| `Зөвхөн staff-д шилжүүлнэ.` / `Ticket нээсэн хүнд шилжүүлэх боломжгүй.` | Буруу хүн сонгосон | Staff сонгоно |
| Командын дэргэд `Missing permissions` / эрхийн тухай | Bot-од Discord-ийн эрх дутуу | Server Settings → Roles → bot-ын role-д эрх |

---

## 8. Git ба token [VPS]

| Команд | Юунд |
|---|---|
| `git -C <зам> pull --ff-only` | Шинэ кодыг татах (deploy өөрөө хийдэг) |
| `GIT_TERMINAL_PROMPT=0 git -C <зам> fetch origin main && echo OK` | Token ажиллаж байгаа эсэх (асуулгүй) |
| `git -C <зам> remote -v` | Remote хаяг (`https://…` байх ёстой) |
| `git remote set-url origin https://github.com/userneon/<repo>.git` | SSH-ээс https болгох |

| Алдаа | Шалтгаан | Засах |
|---|---|---|
| `403 Write access to repository not granted` | Token-д **Contents: Read-only** алга | GitHub → token → Edit → Repository permissions → Contents |
| `404` / `Repository not found` | Token-д тэр repo сонгогдоогүй | Token → Repository access-д нэмэх |
| `401` / `Authentication failed` | Token буруу, хугацаа дууссан | Шинэ token, дахин хадгалах |
| `could not read Username` | Энэ хэрэглэгч дээр token хадгалаагүй | [GARIIN_AVLAGA_MN.md](GARIIN_AVLAGA_MN.md) 3-р алхам (root-д ч) |
| `Not possible to fast-forward` / `Your local changes would be overwritten` | VPS дээр файл гараар зассан | `git status` → `git checkout -- <файл>` |
| `fatal: not a git repository` | Буруу фолдер | `cd` зөв зам руу (`/root/legacyxxx-*`, `/opt/legacyxxx-plugins`) |
| `dubious ownership` | Фолдерын эзэмшигч өөр | `git config --global --add safe.directory <зам>` |

---

## 9. Ямар үед юу хийх вэ (товч)

| Нөхцөл | Хийх |
|---|---|
| "Код push хийлээ" гэж хэлсэн | Тухайн хэсгийн deploy (1-р хэсэг) |
| CS2 шинэ update гарсан | Юу ч хийхгүй: 10 минутын дотор өөрөө шинэчилнэ (AMP бол гараар) |
| Шинэ CS2 сервер | GSLT аваад `cs2-host.sh add …` |
| Шинэ staff | legacyx.cc/staffpanel → role өгөх (тоглоомд 60 сек дотор) |
| Тоглогч хуурч байна | `!ban <тоглогч> 0 cheating` эсвэл Discord `/ban` |
| Match эхлэхгүй байна | ADMIN+: `!start`. Ready тоо: MANAGER `!readyrequired <тоо>` |
| Round буруу эхэлсэн | ADMIN+: `!rr` |
| Skin гарахгүй | Тоглогч: `!rs`. Олон хүнд: `core.json`, серверийн log |
| Вэб дээр сервер харагдахгүй | `cs2-host.sh status`, `logs <порт>`, `.env` |
| Bot хариу өгөхгүй | `pm2 ls`, `pm2 logs legacy-x-discord` |
| Вэб нээгдэхгүй | `pm2 logs legacy-x-api`, `sudo nginx -t` |
| Token-ы хугацаа дууссан (`git pull` 401) | GitHub-д шинэ token, VPS дээр дахин хадгалах |
| Юу болсныг мэдэхгүй | Гаралтыг (нууц утгуудыг хасаад) надад явуул |
