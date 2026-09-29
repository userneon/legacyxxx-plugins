# LEGACY-X CS2 сервер: гарын авлага (Монгол)

Энэ баримт нь **шинэ VPS дээр CS2 сервер суулгаж, LEGACY-X plugin-уудыг ажиллуулах, сервер нэмэх,
шинэчлэх, алдаа засах** бүх алхмыг эхнээс нь дарааллаар нь тайлбарлана. Хаана юу ажиллуулах, юуг
хаанаас авахыг алхам бүрт тэмдэглэсэн:

| Тэмдэглэгээ | Хаана |
|---|---|
| **[Browser]** | Таны компьютерийн browser (Steam, GitHub, legacyx.cc) |
| **[Backend VPS]** | `legacyxxx-backend` (API, pm2) ажиллаж байгаа VPS |
| **[Game VPS]** | CS2 серверүүд ажиллах VPS. Backend-тэй нэг VPS байж болно |
| **[CS2]** | CS2 тоглоом доторх console (`~`) эсвэл chat |

---

## 0. Ерөнхий зураглал

```text
CS2 сервер (plugin-ууд) ──HTTPS──► api.legacyx.cc (legacyxxx-backend) ──► Supabase DB ──► legacyx.cc вэб
        ▲
        └── .env: сервер бүр API-тай ярих token (create-game-server.mjs үүсгэнэ)
```

- Plugin-ууд DB-тэй шууд холбогддоггүй. Бүгд API-аар дамжина.
- Нэг Game VPS дээр олон CS2 сервер нэг суулгацыг хуваалцана. Сервер бүр **port**-оороо
  ялгагдана: `27015` сервер вэб дээр `srv-27015` болж харагдана.
- Бүх сервер нэг `.env` файл ашиглана.
- Сервер бүрт тусдаа **GSLT** (Steam token) хэрэгтэй.

### Юу хэрэгтэй вэ

| Юу | Хаанаас | Тайлбар |
|---|---|---|
| Game VPS | Hostinger, Hetzner, г.м. | **Ubuntu 24.04** (эсвэл 22.04), x86_64. RAM: сервер бүрт 2–3 GB (+2 GB систем). Disk: 60 GB+ |
| GSLT (сервер бүрт 1) | https://steamcommunity.com/dev/managegameservers | App ID `730`. Доорх 2-р алхам |
| GitHub хандалт | https://github.com/userneon/legacyxxx-plugins | Private repo бол deploy key. Доорх 3-р алхам |
| Plugin token бүхий `.env` | `create-game-server.mjs` (Backend VPS) | Доорх 1-р алхам |
| OWNER эрх | legacyx.cc/staffpanel | In-game admin эрх зөвхөн вэбээс ирнэ |

---

## 1. `.env` файл үүсгэх (нэг удаа, машин бүрт)

**[Backend VPS]**, backend-ийн фолдерт (жишээ нь `/var/www/legacy-x-api`, pm2 ажиллаж байгаа газар):

```bash
cd /var/www/legacy-x-api
node --env-file=.env scripts/create-game-server.mjs <GAME_VPS_IP> "27015:competitive_5v5:LEGACY-X #1" "27016:fun:Fun #1"
```

Бодит жишээ:

```bash
node --env-file=.env scripts/create-game-server.mjs 187.127.109.125 "27015:competitive_5v5:LEGACY-X 5x5 #1" "27016:fun:LEGACY-X FUN #1"
```

- `<GAME_VPS_IP>`: тоглогчид холбогдох Game VPS-ийн public IP (эсвэл домэйн).
- `"port:mode:нэр"`: mode нь вэбийн аль Play хуудсанд гарахыг заана:
  `competitive_5v5` (5x5), `fun`, `proleague`. Нэр нь вэб дээр харагдах нэр.
- Үр дүн нь `legacyx-srv-<IP>.env` файл. Дотор нь API хаяг, серверүүдийн нэр/mode, **нууц token** байна.

> Энэ файлыг хэнд ч битгий өг, git-д битгий хий. Дахин ажиллуулбал шинэ token үүсгэж хуучныг нь
> хүчингүй болгоно. Тэгвэл шинэ файлыг Game VPS-т дахин хуулна.

Файлыг **[Game VPS]** руу хуулах:

- Backend, Game VPS нэг машин бол:
  ```bash
  sudo cp legacyx-srv-187.127.109.125.env /root/
  ```
- Өөр машин бол **[Backend VPS]** дээр:
  ```bash
  scp legacyx-srv-187.127.109.125.env root@187.127.109.125:/root/
  ```

---

## 2. GSLT авах (сервер бүрт 1)

**[Browser]** https://steamcommunity.com/dev/managegameservers

1. **App ID**-д `730` гэж бичнэ.
2. **Memo**-д серверээ ялгах нэр бичнэ, жишээ нь `LEGACY-X 27015`.
3. **Create** дарахад 32 тэмдэгттэй код гарна, жишээ нь `A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6`.
4. Сервер бүрт давтана: 27015-д нэг, 27016-д өөр нэг.

Шаардлага:
- Steam account утасны дугаартай байх ёстой.
- Account "limited" биш байх ёстой ($5+ худалдан авалт хийсэн).

Анхаарах зүйлс:
- Нэг token-ыг хоёр сервер **зэрэг** ашиглаж болохгүй. Тэгвэл нэг нь нөгөөгөө Steam-ээс гаргана.
- Token логт эсвэл screenshot-д ил гарвал тэр хуудаснаас **Regenerate** дарна.

---

## 3. Plugin repo-г Game VPS дээр татах (нэг удаа)

**[Game VPS]**, root эсвэл sudo эрхтэй хэрэглэгчээр (`ssh root@<GAME_VPS_IP>`) нэвтэрнэ.

Repo **public** бол:

```bash
sudo apt-get update && sudo apt-get install -y git
sudo git clone https://github.com/userneon/legacyxxx-plugins.git /opt/legacyxxx-plugins
```

Repo **private** бол deploy key (зөвхөн унших эрхтэй түлхүүр) ашиглана:

```bash
sudo ssh-keygen -t ed25519 -N "" -f /root/.ssh/legacyx_plugins -C "game-vps"
sudo cat /root/.ssh/legacyx_plugins.pub      # гарсан мөрийг хуулна
```

**[Browser]** GitHub → `userneon/legacyxxx-plugins` → **Settings → Deploy keys → Add deploy key**:
- Title: `game-vps`
- Key: дээрх мөр
- **Allow write access** чагтыг **битгий** тавь.

**[Game VPS]**:

```bash
sudo tee -a /root/.ssh/config >/dev/null <<'EOF'
Host github.com
  IdentityFile /root/.ssh/legacyx_plugins
  IdentitiesOnly yes
EOF
sudo git clone git@github.com:userneon/legacyxxx-plugins.git /opt/legacyxxx-plugins
```

> `/opt/legacyxxx-plugins` фолдерыг дараа нь зөөж болохгүй: автомат update энэ замаас ажиллана.
> Энэ фолдер доторх файлуудыг VPS дээр гараар бүү засаарай. Засвар бүр GitHub-аар орно, эс
> тэгвээс автомат `git pull` зогсоно.

---

## 4. Бүгдийг суулгах (нэг удаа)

**[Game VPS]**:

```bash
cd /opt/legacyxxx-plugins
sudo ./scripts/cs2-host.sh install --env /root/legacyx-srv-187.127.109.125.env
```

Энэ нэг command дараах бүгдийг хийнэ (эхний удаа 30–60 минут; CS2 нь ~35 GB):

1. Систем package-ууд (git, python3, .NET 8 SDK, Node), `cs2` нэртэй хэрэглэгч.
2. steamcmd болон CS2 dedicated server (`/home/cs2/cs2`).
3. Metamod:Source; `gameinfo.gi`-д автоматаар холбоно.
4. CounterStrikeSharp (.NET runtime-тай).
5. LEGACY-X-ийн 8 plugin-ыг build хийж хуулна:
   - `.env`-ийг `addons/counterstrikesharp/.env` руу хуулна.
   - `core.json` дотор `FollowCS2ServerGuidelines=false` тохируулна (skin болон Tab-ийн цолонд хэрэгтэй).
6. WeaponPaints-ийн signature-ыг шалгаж, шаардлагатай бол засна.
7. systemd service загвар (`cs2@<port>`) үүсгэнэ.
8. Автомат update-ийг асаана.

Амжилттай бол сүүлд нь `==> Installed. Next, add each server: ...` гэж гарна.

> Дахин ажиллуулахад аюулгүй. Тасалдвал ижил command-аа дахин өгнө.

---

## 5. Сервер нэмэх

**[Game VPS]**:

```bash
sudo ./scripts/cs2-host.sh add <port> <GSLT> [тоглоомын mode] [map] [тоглогчийн тоо] [вэб mode] [нэр…]
```

| Аргумент | Утга | Default |
|---|---|---|
| `port` | 27015, 27016, … (сервер бүрт өөр) | заавал |
| `GSLT` | 2-р алхмын 32 тэмдэгт | заавал |
| тоглоомын mode | `competitive`, `wingman`, `casual`, `deathmatch` | `competitive` |
| map | `de_dust2`, `de_mirage`, `de_inferno`, … | `de_dust2` |
| тоглогчийн тоо | 2–64 | `10` |
| вэб mode | `competitive_5v5`, `fun`, `proleague` | `.env`-д байгаа утга |
| нэр | вэб дээр харагдах нэр | `.env`-д байгаа утга |

Жишээнүүд:

```bash
# 5x5 competitive, 10 хүн (вэб mode, нэр .env-ээс ирнэ)
sudo ./scripts/cs2-host.sh add 27015 A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6 competitive de_dust2

# Fun сервер, casual, 20 хүн, вэб дээр "fun" хуудсанд "LEGACY-X FUN #1" нэртэй
sudo ./scripts/cs2-host.sh add 27016 F6E5D4C3B2A1F6E5D4C3B2A1F6E5D4C3 casual de_mirage 20 fun LEGACY-X FUN #1

# Wingman 2x2
sudo ./scripts/cs2-host.sh add 27017 0123456789ABCDEF0123456789ABCDEF wingman de_inferno 4 fun LEGACY-X 2x2
```

Энэ command:
- Сервер бүрийн тохиргоог `/etc/legacyx/cs2/<port>.conf`-д бичнэ.
- ufw идэвхтэй бол firewall-д port нээнэ.
- Серверийг шууд асаана; VPS restart хийсэн ч өөрөө асна.
- 30 секундын дотор вэбийн Play хуудсанд `srv-<port>` гарч ирнэ.

> **Шинэ сервер нэмэхэд `create-game-server.mjs`-ийг дахин ажиллуулах хэрэггүй.** Нэг token бүх
> port-ыг хамарна. Вэб mode болон нэрийг `add` өөрөө `.env`-д бичнэ.

**Hosting-ийн firewall.** Hostinger VPS panel → Firewall, эсвэл cloud firewall ашигладаг бол
port бүрийг (жишээ нь 27015) **UDP болон TCP**-ээр нээнэ. ufw-ээс гадна энэ нь тусдаа тохиргоо.

---

## 6. Шалгах

**[Game VPS]**:

```bash
./scripts/cs2-host.sh status          # 27015 active, 27016 active …
./scripts/cs2-host.sh logs 27015      # тухайн серверийн console (Ctrl+C гарна)
```

`logs` дотор эдгээрийг хай:

| Мөр | Утга |
|---|---|
| `Finished loading plugin LegacyX-…` | plugin ачаалагдсан (8 ширхэг) |
| `LegacyX-Status`-ийн алдаа байхгүй | вэб рүү мэдээлэл явж байна (вэбийн Play хуудсаар шалгана) |
| `… authorized as OWNER.` | таныг staff гэж таньсан (тоглоомд орсны дараа) |

Skin-ийн signature-ын шалгалт (`WeaponPaints signature is valid for this CS2 build.`) нь `install`,
`update`-ийн гаралт болон `journalctl -u legacyx-cs2-autoupdate` дотор харагдана.

**[CS2]** тоглоомын console (`~`):

```text
connect 187.127.109.125:27015
```

Сервер дээр орсны дараа:

| Хаана | Command | Юу харагдах ёстой |
|---|---|---|
| console | `meta list` | Metamod, CounterStrikeSharp |
| console | `css_plugins list` | 8 LegacyX-… plugin бүгд `LOADED` |
| chat | `!rank` | Таны LEGACY-X цол, EXP |
| chat | `!rs` | Вэб дээр сонгосон skin дахин ачаалагдана |
| Tab | — | Нэрний өмнө цол (`OPERATOR I`) эсвэл staff бол `OWNER`/`MANAGER`/`ADMIN` |
| chat | `!admin` | Staff бол admin цэс нээгдэнэ |

**[Browser]** legacyx.cc → Play хуудсанд сервер online, map болон тоглогчийн тоо харагдана.

---

## 7. OWNER / MANAGER / ADMIN эрх олгох

In-game admin эрх **зөвхөн вэбээс** ирнэ. Сервер дээр `admins.json`, SteamID жагсаалт гэж байхгүй.

1. **[Browser]** Тухайн хүн legacyx.cc-д Steam-ээр **нэг удаа нэвтэрсэн** байх ёстой.
2. OWNER нь legacyx.cc/staffpanel → **Staff** хэсэгт тухайн хэрэглэгчийг сонгож role өгнө:
   `OWNER`, `MANAGER`, `ADMIN`, … Status нь `active`.
3. **[CS2]** Тоглогч сервер дээр байвал 60 секундын дотор эрх нь ирнэ. Эсвэл reconnect хийнэ.

Вэбээс эрхийг хасвал 60–180 секундын дотор сервер дээрх эрх нь автоматаар алга болно.

---

## 8. Шинэчлэлт: автоматаар

`install` автомат update-ийг асаасан. 10 минут тутам шалгана:

| Юу гарсан | Хэзээ суулгах |
|---|---|
| CS2-ийн шинэ update (Valve) | **Шууд.** Шинэ client-тэй тоглогч хуучин сервер рүү орж чадахгүй тул хүлээхгүй |
| Үүний дараа гарсан CounterStrikeSharp шинэ хувилбар | **Шууд** (түүнийг хүртэл plugin-ууд унтраалттай байдаг) |
| CounterStrikeSharp дангаараа шинэчлэгдсэн | 05:00 цагт |
| GitHub-д plugin-ийн шинэ commit орсон | 05:00 цагт (`git pull` + build) |
| CS2 update-ийн дараа skin-ийн signature эвдэрсэн | 10 мин тутам засахыг оролдоно; олдвол 05:00-д restart |

Update хийх бүрт хадгалагддаг зүйлс:
- `.env`
- `configs/` (`core.json`, `FollowCS2ServerGuidelines=false`)
- `/etc/legacyx/cs2/<port>.conf` (GSLT, map)
- `gameinfo.gi`-ийн Metamod мөр (дахин нэмнэ)

**[Game VPS]** шалгах:

```bash
journalctl -u legacyx-cs2-autoupdate -n 50         # сүүлийн шалгалтууд
systemctl list-timers legacyx-cs2-autoupdate       # дараагийн шалгалт хэзээ
sudo ./scripts/cs2-host.sh autoupdate check        # одоо шууд шалгах
sudo ./scripts/cs2-host.sh deploy                  # GitHub-аас татаад одоо шууд шинэчлэх
sudo ./scripts/cs2-host.sh update                  # татахгүйгээр шинэчлэх
sudo ./scripts/cs2-host.sh autoupdate off          # унтраах (on: асаах)
```

05:00 цагийг өөрчлөх бол, жишээ нь 04:00:

```bash
sudo AUTOUPDATE_HOUR=4 ./scripts/cs2-host.sh autoupdate on
```

> Цаг нь VPS-ийн цагийн бүсээр тооцогдоно. Монголын цагаар болгох:
> `sudo timedatectl set-timezone Asia/Ulaanbaatar`

---

## 9. Тохиргоо өөрчлөх

| Юу | Файл (Game VPS) | Дараа нь |
|---|---|---|
| GSLT, map, тоглогчийн тоо, mode | `/etc/legacyx/cs2/27015.conf` | `sudo systemctl restart cs2@27015` |
| Plugin тохиргоо (цолны төрөл, skin, AFK…) | `/home/cs2/cs2/game/csgo/addons/counterstrikesharp/.env` | `sudo systemctl restart 'cs2@*'` |
| Вэб дээрх нэр/mode | `.env` дотор `LEGACYX_27015_SERVER_NAME=…`, `LEGACYX_27015_SERVER_MODE=…` | `sudo systemctl restart cs2@27015` |

Засах:

```bash
sudo nano /etc/legacyx/cs2/27015.conf
sudo nano /home/cs2/cs2/game/csgo/addons/counterstrikesharp/.env
```

Жишээ `27015.conf`:

```text
GSLT=A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6
MAP=de_mirage
MAXPLAYERS=10
GAME_ARGS="+game_type 0 +game_mode 1"
```

`.env`-ийн бүх тохиргооны тайлбар repo-ийн `.env.example` файлд байна.

Сервер устгах:

```bash
sudo ./scripts/cs2-host.sh remove 27017
```

Энэ нь серверийг зогсоож, тохиргоог нь устгана. Тухайн GSLT-г өөр серверт дахин ашиглаж болно.

---

## 10. Hosting panel (AMP / Hostinger game panel), terminal байхгүй бол

Terminal-гүй panel дээр script ажиллахгүй. Plugin-ийг гараар байршуулна:

1. **[Game panel]** Metamod болон CounterStrikeSharp-ийг panel-ийн tab-аас суулгана.
   Startup mode нь Metamod/CSS-тэй байх ёстой.
2. `legacyx-cs2.zip`-ийг (plugin build, repo-оос `./scripts/package.sh` гаргана) `game/csgo/` дотор
   upload хийж **panel дотор** задална. SFTP-ээр олон файл тусад нь хуулбал тасалдаж CoreLib
   алдаа гардаг.
3. 1-р алхмын `.env`-ийг `game/csgo/addons/counterstrikesharp/.env` болгон upload хийнэ.
4. `addons/counterstrikesharp/configs/core.json` дотор
   `"FollowCS2ServerGuidelines": false` болгоно.
5. Startup параметрт GSLT-ээ оруулна (`+sv_setsteamaccount <GSLT>`), серверээ restart хийнэ.

Panel дээр **автомат update байхгүй**: CS2 update гарсны дараа CounterStrikeSharp-аа panel-аас
шинэчилж, plugin zip-ээ дахин upload хийнэ.

---

## 11. Түгээмэл алдаа ба шийдэл

| Шинж тэмдэг | Шалтгаан | Шийдэл |
|---|---|---|
| `css_plugins` → `Unknown command` | Metamod/CSS ачаалагдаагүй (CS2 update `gameinfo.gi`-г дарсан) | `sudo ./scripts/cs2-host.sh update` (Metamod мөрийг дахин нэмнэ) |
| Plugin-ууд `LOADING` дээр гацсан / `CoreLib` алдаа | CounterStrikeSharp дутуу хуулагдсан | `update`-ийг дахин ажиллуулна; panel дээр zip-ээ дахин задална |
| Skin орж ирэхгүй, лог дээр `FollowCS2ServerGuidelines` | `core.json` true болсон | `core.json`-д `false` болгоод restart |
| Skin орж ирэхгүй, CS2 update-ийн дараа | Signature эвдэрсэн, шинэ нь хараахан гараагүй | Автоматаар засагдана. Шалгах: `journalctl -u legacyx-cs2-autoupdate` |
| Вэб дээр сервер харагдахгүй | `.env` байхгүй, token буруу, эсвэл API хүрэхгүй | `logs <port>` дотор `LegacyX-Status` алдааг харна; `.env`-ийг шалгана |
| `!start` "permission" гэнэ | Вэб дээр staff биш, эсвэл API хүрэхгүй (fail closed) | 7-р алхам; `logs`-оос `authorized as …` мөрийг хайна |
| Server "logged off" / GSLT алдаа | Нэг token хоёр серверт | Сервер бүрт тусдаа GSLT |
| Тоглогч холбогдож чадахгүй | Hosting-ийн firewall | UDP+TCP port нээнэ (5-р алхам) |
| `git pull failed` автомат update дээр | `/opt/legacyxxx-plugins` дотор гараар засвар хийсэн | `cd /opt/legacyxxx-plugins && sudo git status` → `sudo git checkout -- .` |
| `no space left on device` | Disk дүүрсэн | `df -h`; CS2-д 40 GB+ хэрэгтэй |

Log үзэх газрууд:

```bash
./scripts/cs2-host.sh logs 27015                   # серверийн console
journalctl -u cs2@27015 --since "1 hour ago"       # сүүлийн нэг цаг
journalctl -u legacyx-cs2-autoupdate -n 100        # автомат update
```

**[Backend VPS]** API тал:

```bash
pm2 logs legacy-x-api --lines 100
```

---

## 12. Аюулгүй байдал

- `.env`, token, GSLT, RCON нууц үгийг **git, Discord, screenshot**-д хэзээ ч бүү оруул.
  Ил гарсан бол:
  - GSLT-г Steam хуудаснаас Regenerate хийнэ.
  - Token-ыг `create-game-server.mjs` дахин ажиллуулж шинэчилнэ.
- `.env` файл `chmod 600`, эзэмшигч нь `cs2` хэрэглэгч (script өөрөө тохируулна).
- Plugin эх код дотор SteamID, нууц үг бичдэггүй. Staff эрх зөвхөн вэбээс ирнэ.
- `FollowCS2ServerGuidelines=false` нь Valve-ийн дүрмээс гадуур (skin өөрчлөх). Хамгийн муу
  тохиолдолд Valve тухайн серверийн GSLT-г хаах эрсдэлтэй. Тэгвэл шинэ GSLT аваад `<port>.conf`-д
  солино.

---

## Товч: шинэ VPS-ийг 0-ээс ажиллуулах

```bash
# [Backend VPS]
cd /var/www/legacy-x-api
node --env-file=.env scripts/create-game-server.mjs 187.127.109.125 "27015:competitive_5v5:LEGACY-X #1"
scp legacyx-srv-187.127.109.125.env root@187.127.109.125:/root/

# [Browser] steamcommunity.com/dev/managegameservers → App 730 → GSLT

# [Game VPS]
sudo git clone https://github.com/userneon/legacyxxx-plugins.git /opt/legacyxxx-plugins   # private бол 3-р алхам
cd /opt/legacyxxx-plugins
sudo ./scripts/cs2-host.sh install --env /root/legacyx-srv-187.127.109.125.env
sudo ./scripts/cs2-host.sh add 27015 <GSLT> competitive de_dust2
./scripts/cs2-host.sh status

# [CS2] connect 187.127.109.125:27015
```

Үүнээс хойш CS2 update, plugin update, skin signature засвар бүгд автоматаар явна.
