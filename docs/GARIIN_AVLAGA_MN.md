# LEGACY-X: гарын авлага (Монгол)

Энэ баримт нь **шинэ VPS дээр CS2 сервер суулгаж, LEGACY-X plugin-уудыг ажиллуулах, сервер нэмэх,
шинэчлэх, алдаа засах**, мөн **вэб, API, Discord bot-ыг нэг командаар шинэчлэх** бүх алхмыг
эхнээс нь дарааллаар нь тайлбарлана.

**Өдөр тутмын командууд ба тэдгээрийн үүрэг** (Windows build, server restart, HUD асаах/унтраах, Discord): [KOMANDUUD_GARIIN_AVLAGA_MN.md](KOMANDUUD_GARIIN_AVLAGA_MN.md).

**Бүх командын дэлгэрэнгүй лавлах** (хэрхэн бичих, хэзээ, жишээ, алдаа ба шалтгаан):
[KOMANDUUD_MN.md](KOMANDUUD_MN.md). Хаана юу ажиллуулах, юуг
хаанаас авахыг алхам бүрт тэмдэглэсэн:

| Тэмдэглэгээ | Хаана |
|---|---|
| **[Browser]** | Таны компьютерийн browser (Steam, GitHub, legacyx.cc) |
| **[Backend VPS]** | `legacyxxx-backend` (API), Discord bot, вэб ажиллаж байгаа VPS |
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
- Нэг VPS дээрх бүх CS2 сервер **нэг** plugin `.env` файл ашиглана (доорх №3).
- Сервер бүрт тусдаа **GSLT** (Steam token) хэрэгтэй.

### Юу хэрэгтэй вэ

| Юу | Хаанаас | Тайлбар |
|---|---|---|
| Game VPS | Hostinger, Hetzner, г.м. | **Ubuntu 24.04** (эсвэл 22.04), x86_64. RAM: сервер бүрт 2–3 GB (+2 GB систем). Disk: 60 GB+ |
| GSLT (сервер бүрт 1) | https://steamcommunity.com/dev/managegameservers | App ID `730`. Доорх 2-р алхам |
| GitHub token | GitHub → Settings → Developer settings → Fine-grained tokens | 4 repo бүгд private. Уншдаг token, VPS дээр нэг удаа хадгална. Доорх 3-р алхам |
| Plugin token бүхий `.env` | `create-game-server.mjs` (Backend VPS) | Доорх 1-р алхам |
| OWNER эрх | legacyx.cc/staffpanel | In-game admin эрх зөвхөн вэбээс ирнэ |

---

## 1. `.env` файл үүсгэх (CS2 plugin-ийн, VPS-д нэг удаа)

VPS дээр **гурван өөр `.env`** байдаг. Тус бүр өөр программд, өөр агуулгатай. Нэг файл болгож
**болохгүй**: API-ийн `.env`-д DB-г бүхэлд нь удирдах Supabase service-role key бий, CS2 сервер,
bot үүнийг хэзээ ч мэдэх ёсгүй.

| `.env` | Хаана | Дотор нь | Хэн уншдаг |
|---|---|---|---|
| **1. API** | `/root/legacyxxx-backend/.env` | Supabase service-role key, JWT, Steam login | Зөвхөн API |
| **2. Discord bot** | `/root/legacyxxx-discord-bot/.env` | `DISCORD_TOKEN`, API хаяг, bot-ын token | Зөвхөн bot |
| **3. CS2 plugins** | plugin repo-ийн үндсэн `.env` (жишээ `/opt/legacyxxx-plugins/.env`). Plugin-ийн уншдаг `…/counterstrikesharp/.env` нь түүн рүү заасан холбоос | API хаяг, plugin token, IP, порт бүрийн нэр/mode | **Бүх CS2 сервер хамтдаа** |
| Вэб | байхгүй | API хаяг build-д `ops/deploy.sh`-ээр орно | — |

Энэ алхам №3-ыг үүсгэнэ. Суулгасны дараа тэр `.env`-ийг plugin repo-ийн үндэс рүү хуулж, `sudo ./scripts/cs2-host.sh env`-ээр холбоно (§4-ийн төгсгөл): тэгвэл `cd /opt/legacyxxx-plugins && nano .env` гэж засаад зөвхөн restart хийхэд хангалттай. Нэг VPS-т **нэг л удаа**: дараа нь сервер нэмэхэд (`cs2-host.sh add`) шинэ
`.env` хэрэггүй, `add` өөрөө порт бүрийн мөрийг энэ файлд нэмнэ. Зөвхөн **хоёр дахь VPS** нэмбэл
тэр VPS-т өөрийн №3 хэрэгтэй.

**[Backend VPS]**, backend-ийн фолдерт (`/root/legacyxxx-backend`, pm2 ажиллаж байгаа газар):

```bash
cd /root/legacyxxx-backend
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

## 3. GitHub token ба repo татах (нэг удаа)

4 repo бүгд **private** (`legacyxxx-plugins`, `-backend`, `-frontend`, `-discord-bot`). VPS тэднийг
татахын тулд нэг **уншдаг** token-ыг нэг удаа хадгална. Үүнээс хойш username, token асуухгүй,
05:00-ийн автомат update ч ажиллана.

**[Browser]** Token үүсгэх (эсвэл байгаа token-оо засах):

1. GitHub → **Settings → Developer settings → Personal access tokens → Fine-grained tokens** →
   **Generate new token** (байгааг бол сонгоод **Edit**).
2. **Resource owner:** `userneon`.
3. **Expiration:** 1 жил (хугацаа дуусахад `git pull` зогсоно, тэгвэл шинэчилнэ).
4. **Repository access → Only select repositories:** 4 repo-г бүгдийг сонгоно.
5. **Permissions → Repository permissions → Contents: Read-only**. Анхдагч нь "No access":
   үүнийг заавал солино, эс бөгөөс `403 Write access to repository not granted` гарна.
6. **Generate / Update**. Token (`github_pat_…`)-ыг хуулна. Хэнд ч, чатад ч бүү явуул.

**[VPS]** Token-оо хадгалах. Token дэлгэц, командын түүхэнд харагдахгүй:

```bash
read -rp "GitHub username: " U; read -rsp "Token: " T; echo
printf 'https://%s:%s@github.com\n' "$U" "$T" > ~/.git-credentials
chmod 600 ~/.git-credentials
git config --global credential.helper store
unset T
```

`root` биш хэрэглэгчээр нэвтэрсэн бол root дээр мөн хуулна (plugin-ийн `sudo` script болон автомат
update root-оор ажилладаг):

```bash
sudo cp ~/.git-credentials /root/.git-credentials && sudo chmod 600 /root/.git-credentials
sudo git config --global credential.helper store
```

Token ажиллаж байгааг шалгах (4 мөр бүгд `200` байх ёстой):

```bash
T=$(sed -n 's#^https://[^:]*:\([^@]*\)@github\.com.*#\1#p' ~/.git-credentials | tr -d '\r\n ')
for r in plugins frontend backend discord-bot; do
  curl -s -o /dev/null -w "legacyxxx-$r contents: %{http_code}\n" -H "Authorization: Bearer $T" \
    https://api.github.com/repos/userneon/legacyxxx-$r/contents/README.md
done
unset T
```

| Гарсан | Утга |
|---|---|
| `200` | Зөв |
| `403` | Token-д **Contents: Read-only** алга (дээрх 5-р алхам) |
| `404` | Token-д тэр repo сонгогдоогүй (4-р алхам) |
| `401` | Token буруу эсвэл хугацаа дууссан: шинэ token үүсгээд дахин хадгална |

**[Game VPS]** Plugin repo-г татах:

```bash
sudo apt-get update && sudo apt-get install -y git
git clone https://github.com/userneon/legacyxxx-plugins.git /opt/legacyxxx-plugins
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

**Суулгасны дараа, `.env`-ээ нэг газар болгох** (хэрэв `nano .env`-ээр plugin repo-оос засахыг хүсвэл):

```bash
cd /opt/legacyxxx-plugins
sudo cp /home/cs2/cs2/game/csgo/addons/counterstrikesharp/.env .env     # одоогийн тохиргоог repo-ийн үндэс рүү
sudo ./scripts/cs2-host.sh env                                          # plugin-ийн .env-ийг түүн рүү холбоно
sudo systemctl restart 'cs2@*'
```

Энэ нь `.env` нь git-д ордоггүй (`.gitignore`) тул GitHub-д орохгүй. Дараагийн `deploy`, `update` үед хэвээр.
Хуучин файлыг `.before-link` нэрээр хадгална: ажиллаж байгааг баталгаажуулсны дараа `sudo shred -u` -оор устга.

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

Role бүр юу хийж чадахыг 13-р хэсэгт жагсаасан.

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
| Plugin тохиргоо (цолны төрөл, skin, AFK, HUD…) | plugin repo-ийн `.env` (`cd /opt/legacyxxx-plugins && sudo nano .env`) | `sudo systemctl restart 'cs2@*'` |
| Вэб дээрх нэр/mode | `.env` дотор `LEGACYX_27015_SERVER_NAME=…`, `LEGACYX_27015_SERVER_MODE=…` | `sudo systemctl restart cs2@27015` |
| CounterStrikeSharp (`ServerLanguage`, `!`/`/` trigger) | `/home/cs2/cs2/game/csgo/addons/counterstrikesharp/configs/core.json` | `sudo systemctl restart 'cs2@*'` |

Update ямар файлд хүрдэг, хүрдэггүй вэ:
- **Хэзээ ч дарагдахгүй:** `.env`, `configs/` (`core.json`, plugin JSON), `/etc/legacyx/cs2/*.conf`, `banned_*.cfg`,
  MatchZy-н `savednades.json`, `whitelist.cfg`, `database.json`. `FollowCS2ServerGuidelines`-ийг л `false` болгоно.
- **Repo-оос дахин тавигдана:** plugin DLL, lang, `cfg/MatchZy/`-ийн бусад файл (`config.cfg`, `live.cfg`…). Эдгээрийг
  сервер дээр гараар засвал дараагийн update-ээр буцна: өөрчлөлтийг repo-д оруулна.
- `gameinfo.gi`: CS2 update бүр дарж бичдэг. Metamod-ийн мөрийг скрипт өөрөө дахин нэмнэ.

Засах:

```bash
sudo nano /etc/legacyx/cs2/27015.conf
cd /opt/legacyxxx-plugins && sudo nano .env      # plugin тохиргоо (холбоосоор дамжин plugin-д очно)
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
| `403 Write access to repository not granted` | Token-д Contents эрх алга | 3-р алхам: **Contents: Read-only** |
| `could not read Username` / username, token асуусаар | Token хадгалаагүй, эсвэл өөр хэрэглэгч (sudo / sudo-гүй) | 3-р алхмын "Token-оо хадгалах"-ыг тэр хэрэглэгч дээр хийнэ |
| Remote нь `git@github.com:` | SSH хаяг, token ашиглагдахгүй | `git remote set-url origin https://github.com/userneon/<repo>.git` |
| `bash ops/deploy.sh`: файл олдохгүй | Буруу фолдерт байна, эсвэл script нэмэгдсэнээс хойш pull хийгээгүй | `cd` зөв замаар (14-р хэсэг), эхний удаа `git pull && bash ops/deploy.sh` |
| `cd legacyxxx-plugins`: олдохгүй | Plugins нь `/opt`-д байна | `cd /opt/legacyxxx-plugins` |
| Deploy: `No .env here` | Шинэ clone, `.env` хуучин фолдерт үлдсэн | Жинхэнэ фолдероо ашиглана (`pm2 describe … \| grep "exec cwd"`) |

Log үзэх газрууд:

```bash
./scripts/cs2-host.sh logs 27015                   # серверийн console
journalctl -u cs2@27015 --since "1 hour ago"       # сүүлийн нэг цаг
journalctl -u legacyx-cs2-autoupdate -n 100        # автомат update
```

**[Backend VPS]** API болон Discord bot:

```bash
pm2 logs legacy-x-api --lines 100
pm2 logs legacy-x-discord --lines 100
```

---

## 12. Аюулгүй байдал

- `.env`, token, GSLT, RCON нууц үгийг **git, Discord, screenshot**-д хэзээ ч бүү оруул.
  Ил гарсан бол:
  - GSLT-г Steam хуудаснаас Regenerate хийнэ.
  - Token-ыг `create-game-server.mjs` дахин ажиллуулж шинэчилнэ.
- `.env` файл `chmod 600`, эзэмшигч нь `cs2` хэрэглэгч (script өөрөө тохируулна).
- Plugin эх код дотор SteamID, нууц үг бичдэггүй. Staff эрх зөвхөн вэбээс ирнэ.
- GitHub token: зөвхөн **Contents: Read-only** эрхтэй, `~/.git-credentials`-д шифргүй хадгалагдана.
  Өөр газар ашиглахгүй, хугацаатай (1 жил). Ил гарвал GitHub дээр **Revoke** хийгээд шинээр үүсгэнэ.
- 4 repo бүгд private. Нууц мэдээлэл (`.env`, token) repo-д хэзээ ч commit хийгдэхгүй.
- `FollowCS2ServerGuidelines=false` нь Valve-ийн дүрмээс гадуур (skin өөрчлөх). Хамгийн муу
  тохиолдолд Valve тухайн серверийн GSLT-г хаах эрсдэлтэй. Тэгвэл шинэ GSLT аваад `<port>.conf`-д
  солино.

---

## 13. Тоглоом доторх командууд (staff)

Role-ийг legacyx.cc/staffpanel дээр өгнө (7-р алхам). Role бүр доод role-ийнхоо бүх командыг
ажиллуулна. Команд хоёр шалгуураар нээгдэнэ: role-ийн эрх, мөн **stamina** (default: STAFF 250,
ADMIN 500, MANAGER 750, OWNER 1000; вэб дээр хүн бүрээр өөрчилж болно).

| Хэн | Командууд |
|---|---|
| **Бүх тоглогч** | `!calladmin`, `!callmanager`, `!report`, `!admins`, `!rank`, `!rs` (skin ачаалах), `!knife`, `!gloves`, `!agents`; match: `.ready` `.unready` `.pause` `.unpause` `.tech` `.tac` `.stay` `.switch` `.stop` `.coach` |
| **STAFF** | `!admin` (цэс), `!mute`/`!unmute` (voice), `!gag`/`!ungag` (chat), `!silence`/`!unsilence`, `!mutelist`, `!gaglist`, `!asay`, `!psay`, `!who`, `!vote`, `!votemap`, `!votekick`, `!votemute`, `!votegag` |
| **ADMIN** | `!ban <тоглогч> [минут] [шалтгаан]`, `!lastban`, `!baninfo`, `!kick`, `!slap`, `!csay`, `!hsay`, `!hideadmin`, `!voteban`, `!votesilence`, `!rvote`, `!cancelvote`, **`!start`** (match эхлүүлэх), **`!rr`** (round дахин эхлүүлэх) |
| **MANAGER** | `!unban`, `!ipban`, `!banlist`, `!map`, `!wsmap`, `!slay`, `!team`, `!swap`, `!rename`; match: `!restart` (match-ийг бүхэлд нь reset), `!endmatch`, `!forcepause`, `!forceunpause`, `!restore`, `!skipveto`, `!readyrequired`, `!roundknife`, `!playout`, `!whitelist`, `!settings`, `css_legacyx_fill` |
| **OWNER** | `!cleanbans` (бүх серверийн ban-ыг вэбээс цэвэрлэх), `!cleanall`, `!cleanmute`, `!cleangag`, `!rcon`, `!cvar`, `!prac`, `!match`, `!sleep`, `!exitprac`, мөн `!money`, `!god`, `!noclip`, `!freeze`, `!glow` гэх мэт fun командууд (`!admin` цэсэнд байхгүй, командаар ажиллана) |

- `!rr` = зөвхөн **round** дахин эхлүүлнэ. Match-ийг бүхэлд нь warmup руу reset хийх бол `!restart`.
- Шинэ тоглогч орж ирэхэд зөвхөн өөрт нь welcome мессеж (нэр, цол, EXP) гарна. Унтраах:
  `.env`-д `LEGACYX_COMMUNITY_WELCOME=false`.
- Chat-ийн бүх текстийн дүрэм: `docs/CHAT_STYLE.md`.

---

## 14. Бүх repo-г шинэчлэх (deploy, нэг командаар)

Би кодыг GitHub руу push хийдэг ч VPS өөрөө **автоматаар татдаггүй** (plugin-ийн 05:00-ийн автомат
update-ээс бусад нь). Шинэчлэлт хийгдсэн гэж хэлэх бүрд тухайн repo-гийн deploy-г ажиллуулна.

**[VPS]** Repo бүрийн зам ба deploy:

| Repo | Зам | Deploy | Юу хийх |
|---|---|---|---|
| API (backend) | `/root/legacyxxx-backend` | `cd /root/legacyxxx-backend && bash ops/deploy.sh` | pull → `npm ci` → typecheck → build → pm2 reload → `/health` шалгана |
| Discord bot | `/root/legacyxxx-discord-bot` | `cd /root/legacyxxx-discord-bot && bash ops/deploy.sh` | pull → `npm ci` → тест → pm2 reload → шинэ `[ready]` мөрийг хүлээнэ |
| Вэб (frontend) | `/root/legacyxxx-frontend` | `cd /root/legacyxxx-frontend && bash ops/deploy.sh` | pull → `npm ci` → build → Nginx-ийн фолдер руу хуулна → Nginx reload |
| CS2 plugins | `/opt/legacyxxx-plugins` | `sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh deploy` | pull → CS2, Metamod, CSS, plugin шинэчилнэ → серверүүдийг restart |

Бүгдийг нэг дор:

```bash
cd /root/legacyxxx-backend     && bash ops/deploy.sh
cd /root/legacyxxx-discord-bot && bash ops/deploy.sh
cd /root/legacyxxx-frontend    && bash ops/deploy.sh
sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh deploy
```

- Амжилттай бол сүүлд нь `==> Done: <commit> <тайлбар>` гэж гарна.
- `git pull` нь зөвхөн fast-forward: VPS дээр гараар зассан файл байвал зогсоод юу ч эвдэхгүй.
  Засварыг буцаах: `git -C <зам> checkout -- <файл>` (жишээ нь `package-lock.json`).
- Backend, bot-ын deploy `.env` байхгүй бол эхэлмэгц зогсоно.
- Frontend-ийн Nginx фолдер анхдагчаар `/var/www/legacyx`. Өөр бол:
  `WEB_ROOT=/таны/зам bash ops/deploy.sh`. Nginx-ийн замыг харах: `grep -rn "root " /etc/nginx/sites-enabled/`
- Deploy-н дараа вэб дээр хуучин харагдвал browser-т **Ctrl+Shift+R**.
- Plugin: `deploy` = pull + шинэчлэх; `update` = pull хийхгүйгээр шинэчлэх.
- Plugin-ийн CS2 серверүүд AMP panel дээр бол `cs2-host.sh deploy` хэрэггүй: zip-ээ panel дээр задална (10-р хэсэг).

> **Эхний удаа:** `ops/deploy.sh` нь сүүлд нэмэгдсэн тул хуучин clone дээр байхгүй байж болно. Тэгвэл
> нэг удаа `git pull && bash ops/deploy.sh`. Дараагаас нь зөвхөн `bash ops/deploy.sh`.

### Discord-д update-ийн зарлал (автомат)

Deploy болон CS2-ийн update амжилттай дуусах бүрт Discord-ын нэг channel-д юу өөрчлөгдсөнийг илгээнэ.
Текстийг AI бичдэггүй, орчуулдаггүй: зөвхөн баримт.

| Юу | Зарлалын гарчиг | Шалтгаан (мөр бүр) |
|---|---|---|
| `ops/deploy.sh` (backend / bot / frontend) | `API updated` / `Discord bot updated` / `Website updated` | Энэ deploy-оор орж ирсэн commit бүрийн гарчиг, яг бичигдсэнээрээ |
| `cs2-host.sh deploy`, `update`, автомат update | `Game servers updated` | `CS2 build 41234 → 41250`, `CounterStrikeSharp v320 → v321`, plugin-ийн commit-ийн гарчиг. Доор нь restart хийгдсэн серверүүд |
| Skin-ий signature засагдаж restart | `Game servers restarted` | `WeaponPaints signature fixed` |

- Зөвхөн `docs/` эсвэл `*.md` өөрчилсөн commit, мөн тайлбартаа `[skip announce]` гэж бичсэн commit орохгүй.
  Шинэ зүйл байхгүй бол (дахин deploy хийсэн) юу ч илгээхгүй.
- Deploy амжилтгүй бол илгээхгүй. Discord унасан, webhook байхгүй бол deploy-д нөлөөлөхгүй.

Мэдэгдэл **Discord bot-оор** дамжина: скриптүүд API руу илгээж, bot өөрийн `/updates` командаар сонгосон
channel-д тавина. CS2-ийн "finished" мэдэгдэл алтан banner-тай. (Webhook-оор шууд илгээдэг хуучин арга
`announce.sh setup-webhook` хэвээр.)

**Нэг удаа тохируулах**, VPS бүр дээр:

1. **[Backend VPS]** Token үүсгэнэ (нэг удаа):
   ```bash
   cd /root/legacyxxx-backend
   node --env-file=.env scripts/create-api-token.mjs legacyx-announce announce:write
   ```
   Token нэг л удаа харагдана, хуулж ав.
2. **[Бүх VPS]**
   ```bash
   git -C /opt/legacyxxx-plugins pull --ff-only
   sudo /opt/legacyxxx-plugins/scripts/announce.sh setup   # API хаяг, token (дэлгэцэнд харагдахгүй)
   ```
   API хаяг, token `/etc/legacyx/announce.env`-д (зөвхөн root) хадгалагдана, `env-backup.sh` үүнийг хамт нөөцөлнө.
3. **[Discord]** Мэдэгдэл гарах channel дээр `/updates` (зөвхөн server-ийн эзэн). Зогсоох: `/updates off`.

Урьдчилж харах (илгээхгүй): `/opt/legacyxxx-plugins/scripts/announce.sh --title Test --commits /root/legacyxxx-frontend HEAD~3 HEAD --dry-run`

---

## 15. VPS-ийн төлөв шалгах

**[VPS]** LEGACY-X-ийн бүх repo хаана байгаа, хоцорсон эсэх, token ажиллаж байгаа эсэхийг нэг дор:

```bash
for g in $(find / -maxdepth 4 -type d -name .git -path "*legacy*" 2>/dev/null); do
  d=$(dirname "$g")
  git -C "$d" fetch -q origin main 2>/dev/null && net=ok || net=FAIL
  printf '\n== %s\n' "$d"
  printf '  repo:    %s\n' "$(git -C "$d" remote get-url origin | sed 's#https://[^@]*@#https://#')"
  printf '  commit:  %s   github: %s   behind: %s   татах эрх: %s\n' "$(git -C "$d" rev-parse --short HEAD)" "$(git -C "$d" rev-parse --short origin/main 2>/dev/null)" "$(git -C "$d" rev-list --count HEAD..origin/main 2>/dev/null)" "$net"
  printf '  өөрчлөлт: %s файл   .env: %s\n' "$(git -C "$d" status --porcelain | grep -vc '^??')" "$([ -f "$d/.env" ] && echo байна || echo алга)"
done
echo; pm2 ls | grep -E "legacy|name"
pm2 describe legacy-x-api | grep "exec cwd"; pm2 describe legacy-x-discord | grep "exec cwd"
```

| Мөр | Байх ёстой |
|---|---|
| татах эрх | `ok` (`FAIL` бол token эсвэл remote, 3-р алхам) |
| behind | `0`. `>0` бол тэр repo-д deploy хийнэ (14-р хэсэг) |
| өөрчлөлт | `0`. Бусад бол VPS дээр гараар зассан файл байна |
| .env | Backend, bot-д **байна** |
| exec cwd | `/root/legacyxxx-backend`, `/root/legacyxxx-discord-bot`: pm2 эндээс ажиллана |

**Давхар clone:** нэг repo хоёр газар байвал `exec cwd` болон `.env`-тэй нь жинхэнэ. `.env`-гүй, pm2
ашигладаггүй давхар фолдерыг устгаж болно (жишээ нь `rm -rf /opt/legacyxxx-frontend`). Эргэлзвэл
устгахаасаа өмнө асуугаарай. `/opt/legacyxxx-plugins`-ийг бүү устга (plugin-ийн ганц clone).

---

## 16. Discord ticket

Ticket-ийн эхний мессежийн доорх товчнууд, **бүгд зөвхөн staff-д** (staff role эсвэл Manage Server):

| Товч | Хэн | Юу |
|---|---|---|
| ✋ Хариуцах / ↩️ Суллах | Staff (суллах: хариуцсан хүн эсвэл manager) | Ticket-ийг авах, буцааж суллах |
| 🔁 Шилжүүлэх | Хариуцсан хүн эсвэл manager (хэн ч аваагүй бол аль ч staff) | Өөр staff-д шилжүүлнэ |
| ➕ Хүн нэмэх / ➖ Хүн хасах | Staff | 5 хүртэл хүн. Ticket нээсэн хүн болон bot-ыг хасахгүй |
| 🔒 Хаах | Staff | Transcript-ийг log channel-д хадгалаад түгжинэ |

- Ticket **нээсэн хүн** (staff байсан ч) өөрийн ticket-ийг хариуцаж, удирдаж, хааж чадахгүй.
- Ban appeal нь ban өгсөн staff-д автоматаар очно. Тэр өөрөө appeal нээсэн бол manager role (эсвэл server owner)-д очно.

---

## 17. `.env`-ийг хамгаалах: устгахгүй, алдахгүй

`.env` нь git-д **ордоггүй** (зориуд). Тиймээс GitHub-аас сэргээж болохгүй: VPS дээрээ, мөн VPS-ээс
гадна нөөцтэй байх ёстой. Deploy, update (`ops/deploy.sh`, `cs2-host.sh update/deploy`) `.env`-д хүрдэггүй.

**1. Эрхийг хаах** (зөвхөн эзэмшигч уншина):

```bash
chmod 600 /root/legacyxxx-backend/.env /root/legacyxxx-discord-bot/.env
sudo chmod 600 /home/cs2/cs2/game/csgo/addons/counterstrikesharp/.env
```

**2. Өдөр бүр автоматаар нөөцлөх** (нэг удаа асаана):

```bash
sudo /opt/legacyxxx-plugins/scripts/env-backup.sh run    # одоо нөөцлөх
sudo /opt/legacyxxx-plugins/scripts/env-backup.sh on     # өдөр бүр 04:30-д
sudo /opt/legacyxxx-plugins/scripts/env-backup.sh list   # байгаа нөөцүүд
```

Нөөц нь `/root/legacyx-env-backups/<огноо-цаг>/`-д хадгалагдана (зөвхөн root). Сүүлийн 30-ыг үлдээнэ.
Гурван `.env`-ийг бүгдийг (API, bot, CS2) авна. Аль нэг нь энэ VPS-д байхгүй бол алгасна.

**3. VPS-ээс гадна хадгалах** (сард нэг, эсвэл `.env` өөрчилсний дараа). VPS эвдэрвэл энэ л үлдэнэ:

```bash
# [VPS]
sudo /opt/legacyxxx-plugins/scripts/env-backup.sh bundle
# [Таны компьютер] (VPS-ийн IP-гаа бичнэ, файлын нэрийг bundle-ийн гаралтаас)
scp root@<VPS-IP>:/root/legacyx-env-backups/legacyx-env-<огноо>.tar.gz .
```

Энэ файлд нууц мэдээлэл бий: password manager (Bitwarden, 1Password), эсвэл шифрлэсэн flash-д
хадгална. Discord, email, Google Drive-ийн нээлттэй folder-т **бүү** хий.

**4. Сэргээх** (файл устсан, буруу засагдсан):

```bash
sudo /opt/legacyxxx-plugins/scripts/env-backup.sh list
sudo /opt/legacyxxx-plugins/scripts/env-backup.sh restore 20261001-043000
pm2 restart all && sudo systemctl restart 'cs2@*'
```

Сэргээхээсээ өмнө одоогийн файлуудыг өөрөө нөөцөлнө, тиймээс алдаж болохгүй.

**Хэзээ ч бүү ажиллуул:**
- `git clean -x` / `git clean -fdx`: git-д ордоггүй файлыг (`.env`-ийг) **устгана**. Plugin repo-ийн үндсэн `.env` (§1) мөн адил.
- repo-гийн фолдерыг `rm -rf` хийж дахин clone хийх: `.env` хамт устана. Хэрэгтэй бол эхлээд `env-backup.sh run`.

**Нэмэлт:** Hostinger VPS panel → **Snapshots / Backups**-ийг асаавал бүх VPS долоо хоног бүр нөөцлөгдөнө.

---

## 18. Game серверийг өөр VPS руу шилжүүлэх (жишээ нь Монгол VPS)

Вэб, API, Discord bot **Hostinger дээрээ үлдэнэ**. Зөвхөн CS2 серверүүд шинэ VPS руу нүүнэ. Хоёр terminal:

| Тэмдэглэгээ | Terminal |
|---|---|
| **[Hostinger]** | Одоогийн VPS: вэб, API, Discord bot, хуучин CS2 серверүүд |
| **[Шинэ VPS]** | Монгол VPS: CS2 серверүүд энд шилжинэ |

**Юу нүүх вэ:** plugin `.env`, `/etc/legacyx/cs2/*.conf` (GSLT, map), `banned_user.cfg`/`banned_ip.cfg`
(локал ban), `communication_data.json` (mute/gag), `plugins/LegacyX-MatchZy/matchzy.db` (MatchZy stats),
`cfg/MatchZy` (lineup, whitelist, spawn), `configs/` (`core.json`,
plugin тохиргоо). Өөрчлөгдөх зүйл ганц мөр: `.env` дахь `LEGACYX_SERVER_HOST=<ШИНЭ-IP>`. Backend, bot,
вэб, nginx, SSL, Supabase-д хүрэхгүй. Тоглогч, ban, match бүгд Supabase-д байгаа тул алдагдахгүй.

**Дүрэм:**
- Port-уудаа (27015, 27016…) болон `LEGACYX_SERVER_ID_PREFIX`-ийг **өөрчлөхгүй**. Тэгвэл `srv-27015` хэвээр
  үлдэж, вэб дээрх сервер бүрийн staff эрх шууд ажиллана.
- `LEGACYX_API_BASE_URL` нь `https://api.legacyx.cc` байх ёстой. `127.0.0.1`/`localhost` бол өөр VPS-ээс хүрэхгүй.
- Хоёр VPS дээр CS2-ийг **зэрэг бүү ажиллуул**: ижил GSLT, ижил server ID хоорондоо мөргөлдөнө.

### Шинэ VPS-ийг сонгохдоо (Монгол provider-оос асуух)

| Шаардлага | Яагаад |
|---|---|
| Ubuntu 24.04/22.04, x86_64, RAM сервер бүрт 2–3 GB (+2), disk 60 GB+ | `cs2-host.sh`-ийн шаардлага |
| **Өөрийн public IPv4** (NAT биш) | Тоглогчид шууд `connect IP:27015` хийнэ |
| **UDP** port нээж болдог | CS2 UDP-ээр ажиллана |
| **Гадаад traffic** хязгааргүй эсвэл хангалттай | Эхний таталт ~35 GB, CS2 update бүр хэдэн GB |
| DDoS хамгаалалт | Game серверт хамгийн түгээмэл халдлага |

### 1-р шат: бэлтгэл (серверүүд ажилласаар)

```bash
# [Шинэ VPS] API-тай холбогдож байгааг шалгах: {"ok":true,...} гэж гарах ёстой
curl -sS https://api.legacyx.cc/health

# [Hostinger] одоогийн plugin .env-ийг шинэ VPS руу илгээх
scp /home/cs2/cs2/game/csgo/addons/counterstrikesharp/.env root@<ШИНЭ-IP>:/root/legacyx-game.env

# [Шинэ VPS] цагийн бүс: 05:00-ийн update Монголын цагаар явна (UTC бол 13:00-д серверүүд restart болно)
sudo timedatectl set-timezone Asia/Ulaanbaatar && timedatectl | grep -E "Time zone|synchronized"

# [Шинэ VPS] GitHub token хадгалах (3-р алхам), зөвхөн plugins repo-г татах
git clone https://github.com/userneon/legacyxxx-plugins.git /opt/legacyxxx-plugins

# [Шинэ VPS] IP солиод шалгах, дараа нь суулгах (удаан: ~35 GB)
sudo sed -i 's/^LEGACYX_SERVER_HOST=.*/LEGACYX_SERVER_HOST=<ШИНЭ-IP>/' /root/legacyx-game.env
grep -E '^LEGACYX_(API_BASE_URL|SERVER_HOST|SERVER_ID_PREFIX)=' /root/legacyx-game.env
sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh install --env /root/legacyx-game.env
```

### 2-р шат: шилжүүлэх (серверүүд ~5–10 минут унтарна)

```bash
# [Hostinger] автомат update-ийг зогсоож, CS2 серверүүдийг унтраах
sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh autoupdate off
for c in /etc/legacyx/cs2/*.conf; do sudo systemctl disable --now "cs2@$(basename "$c" .conf)"; done

# [Hostinger] хамгийн сүүлийн ban, mute, stats, тохиргоог нэг файл болгоод илгээх
sudo bash -c '
CS=/home/cs2/cs2/game/csgo
OUT=/root/legacyx-game-$(date +%Y%m%d-%H%M).tar.gz
LIST=$(for p in /etc/legacyx $CS/cfg/banned_user.cfg $CS/cfg/banned_ip.cfg $CS/cfg/MatchZy \
  $CS/addons/counterstrikesharp/.env $CS/addons/counterstrikesharp/configs \
  $(find $CS/addons/counterstrikesharp/plugins \( -name communication_data.json -o -name matchzy.db \) 2>/dev/null); do
  [ -e "$p" ] && echo "${p#/}"; done)
umask 077; tar -czf "$OUT" -C / $LIST && tar -tzf "$OUT"
'
scp /root/legacyx-game-*.tar.gz root@<ШИНЭ-IP>:/root/

# [Шинэ VPS] задлах, IP-г дахин тохируулах (архивт хуучин IP байгаа), серверүүдийг асаах
sudo tar -xzf /root/legacyx-game-*.tar.gz -C /
ENVF=/home/cs2/cs2/game/csgo/addons/counterstrikesharp/.env
sudo sed -i 's/^LEGACYX_SERVER_HOST=.*/LEGACYX_SERVER_HOST=<ШИНЭ-IP>/' $ENVF
sudo chown -R cs2:cs2 /home/cs2/cs2/game/csgo/cfg /home/cs2/cs2/game/csgo/addons
for c in /etc/legacyx/cs2/*.conf; do p=$(basename "$c" .conf); sudo ufw allow "$p"; sudo systemctl enable --now "cs2@$p"; done
sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh status

# [Шинэ VPS] .env-ийн өдөр тутмын нөөц, түр файл устгах
sudo env ENV_FILES=$ENVF /opt/legacyxxx-plugins/scripts/env-backup.sh on
sudo rm /root/legacyx-game.env
```

Provider-ийн panel дээр ч CS2 port-уудыг (TCP+UDP) нээнэ. 80/443 шинэ VPS-д хэрэггүй.

### Шалгах

- legacyx.cc → Play: сервер **шинэ IP**-тэйгээ 30 секундын дотор гарна. Connect хийж орно.
- **[CS2]** `!admin`, welcome мессеж, `!rs` ажиллаж байна. Discord дээр `/status` ажиллаж байна.
- Log-д `Too many requests` гарвал **[Hostinger]** backend `.env` дахь `API_RATE_LIMIT_MAX`-ийг нэмээд
  `bash ops/deploy.sh` ажиллуулна.
- Гадаад холболт тасарвал staff эрх fail closed байдлаар хаагдаж, вэбийн ban шалгагдахгүй. Локал ban
  хэвээр ажиллана. Холболт тогтворгүй бол `LEGACYX_ADMIN_AUTH_CACHE_SECONDS`-ийг (30–3600) өсгөнө.

### Дараа нь

- **Буцаах:** **[Шинэ VPS]** `for c in /etc/legacyx/cs2/*.conf; do sudo systemctl disable --now "cs2@$(basename "$c" .conf)"; done`,
  дараа нь **[Hostinger]** дээр мөн тэр давталтыг `enable --now`-оор ажиллуулна. Тиймээс Hostinger дээрх
  CS2-ийг 1–2 өдөр бүү устга.
- Бүх зүйл хэвийн бол **[Hostinger]** port бүрт `sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh remove <port>`
  ажиллуулна. Дараа нь `/home/cs2`-ийг устгавал 35 GB чөлөөлөгдөнө.
- Шинэ GSLT ашиглах тохиромжтой үе: `/etc/legacyx/cs2/<port>.conf` → `GSLT=` → `sudo systemctl restart cs2@<port>`.

---

## Товч: шинэ VPS-ийг 0-ээс ажиллуулах

```bash
# [Browser] GitHub fine-grained token: 4 repo, Contents: Read-only (3-р алхам)
# [VPS] token хадгалах: 3-р алхмын "Token-оо хадгалах"

# [Backend VPS]
cd /root/legacyxxx-backend
node --env-file=.env scripts/create-game-server.mjs 187.127.109.125 "27015:competitive_5v5:LEGACY-X #1"
scp legacyx-srv-187.127.109.125.env root@187.127.109.125:/root/

# [Browser] steamcommunity.com/dev/managegameservers → App 730 → GSLT

# [Game VPS]
git clone https://github.com/userneon/legacyxxx-plugins.git /opt/legacyxxx-plugins
cd /opt/legacyxxx-plugins
sudo ./scripts/cs2-host.sh install --env /root/legacyx-srv-187.127.109.125.env
sudo ./scripts/cs2-host.sh add 27015 <GSLT> competitive de_dust2
./scripts/cs2-host.sh status

# [CS2] connect 187.127.109.125:27015
```

Үүнээс хойш CS2 update, plugin update, skin signature засвар бүгд автоматаар явна. Вэб, API,
Discord bot-ыг шинэчлэх: 14-р хэсэг.
