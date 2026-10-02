# LEGACY-X: бүрэн гарын авлага

Энэ нэг файл нь `docs/` хавтсан дахь өмнөх бүх баримтыг нэгтгэсэн. Суулгах, өдөр тутмын командууд, бүх командын
лавлах, plugin-ууд, admin эрх, Discord, chat-ийн хэв маяг нэг дор байна.

| Хэсэг | Юу | Хэзээ уншихыг |
|---|---|---|
| [1. Ерөнхий зураглал](#part1) | Системийн бүтэц, гурван машин | Эхлээд |
| [2. Суулгах ба тохируулах](#part2) | VPS, CS2, token, GSLT, `.env`, update, нөөцлөлт, нүүлгэлт | Шинэ VPS, алдаа засах |
| [3. Өдөр тутмын командууд](#part3) | Windows build, server restart, HUD, Discord, команд бүрийн үүрэг | Өдөр бүр |
| [4. Командын бүрэн лавлах](#part4) | Бүх команд: хэрхэн, хэзээ, алдаа ба шалтгаан | Тодорхой команд хайх |
| [5. Plugin-ууд ба тохиргоо](#part5) | Plugin бүр, `.env` хувьсагч, token, хил хязгаар | Plugin нэмэх, засах |
| [6. Admin эрх ба in-game командууд](#part6) | Staff эрх яаж ирдэг, `!ban` гэх мэт | Admin тохируулах |
| [7. Discord](#part7) | Bot юу хийдэг, game server Discord-той яаж холбогддог | Discord тохируулах |
| [8. Chat-ийн хэв маяг](#part8) | In-game текстийн дүрэм | Шинэ текст бичих |
| [9. Түүх, хүчингүй болсон шийдвэрүүд](#part9) | Өмнө нь юу байсан, яагаад өөрчлөгдсөн | Эргэлзэхэд |

> Шинэ мэдээлэл нэмэхдээ зөвхөн энэ файлыг засна. Тусдаа нэмэлт баримт үүсгэхгүй.


<a id="part1"></a>
## Хэсэг 1. Ерөнхий зураглал

```text
Тоглогч ──► CS2 server (plugin-ууд) ──HTTPS──► api.legacyx.cc ──► Supabase DB ──► legacyx.cc (вэб)
                                                     │
                                                     └──► Discord bot (API-г уншиж channel-д тавина)
```

Гурван машин / газар байдаг:

| Газар | Юу байдаг | Тэмдэглэгээ |
|---|---|---|
| **Windows (таны компьютер)** | Workshop addon (HUD) build хийж нийтлэх | Хэсэг 3-ын A |
| **Game VPS** | CS2 server-үүд, LEGACY-X plugin-ууд | Хэсэг 3-ын B |
| **Web VPS** | Вэб (Nginx), API (backend), Discord bot | Хэсэг 3-ын C |

Үндсэн дүрэм:
- Plugin-ууд DB-тэй шууд холбогддоггүй: **game → API → DB**. Plugin-д Supabase, DB, RCON, Steam, Discord нууц байхгүй.
- Game server **Discord руу огт бичдэггүй**. Discord-ын бүх зүйлийг (дуудлага, report, шийтгэл, update-ийн мэдээ)
  API-аар дамжуулан **Discord bot** өөрөө тавина (Хэсэг 7).
- In-game admin эрх зөвхөн вэбээс (staff panel) ирнэ (Хэсэг 6).
- Нууц (`.env`, token, GSLT) git-д ордоггүй (Хэсэг 2-ын §17).

> **Замын тэмдэглэл.** Гарын авлагад plugin repo-г `/opt/legacyxxx-plugins`, backend, bot-ыг `/root/legacyxxx-*` гэж бичсэн. Танай машин дээр
> өөр замтай (жишээ нь `/root/legacyxxx-plugins`) бол командуудын замыг өөрийнхөөрөө соль: утга нь ижил.


<a id="part2"></a>
## Хэсэг 2. Суулгах ба тохируулах

Энэ баримт нь **шинэ VPS дээр CS2 сервер суулгаж, LEGACY-X plugin-уудыг ажиллуулах, сервер нэмэх,
шинэчлэх, алдаа засах**, мөн **вэб, API, Discord bot-ыг нэг командаар шинэчлэх** бүх алхмыг
эхнээс нь дарааллаар нь тайлбарлана.

**Өдөр тутмын командууд ба тэдгээрийн үүрэг** (Windows build, server restart, HUD асаах/унтраах, Discord): [KOMANDUUD_GARIIN_AVLAGA_MN.md](#part3).

**Бүх командын дэлгэрэнгүй лавлах** (хэрхэн бичих, хэзээ, жишээ, алдаа ба шалтгаан):
[KOMANDUUD_MN.md](#part4). Хаана юу ажиллуулах, юуг
хаанаас авахыг алхам бүрт тэмдэглэсэн:

| Тэмдэглэгээ | Хаана |
|---|---|
| **[Browser]** | Таны компьютерийн browser (Steam, GitHub, legacyx.cc) |
| **[Backend VPS]** | `legacyxxx-backend` (API), Discord bot, вэб ажиллаж байгаа VPS |
| **[Game VPS]** | CS2 серверүүд ажиллах VPS. Backend-тэй нэг VPS байж болно |
| **[CS2]** | CS2 тоглоом доторх console (`~`) эсвэл chat |

---

### 0. Ерөнхий зураглал

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

#### Юу хэрэгтэй вэ

| Юу | Хаанаас | Тайлбар |
|---|---|---|
| Game VPS | Hostinger, Hetzner, г.м. | **Ubuntu 24.04** (эсвэл 22.04), x86_64. RAM: сервер бүрт 2–3 GB (+2 GB систем). Disk: 60 GB+ |
| GSLT (сервер бүрт 1) | https://steamcommunity.com/dev/managegameservers | App ID `730`. Доорх 2-р алхам |
| GitHub token | GitHub → Settings → Developer settings → Fine-grained tokens | 4 repo бүгд private. Уншдаг token, VPS дээр нэг удаа хадгална. Доорх 3-р алхам |
| Plugin token бүхий `.env` | `create-game-server.mjs` (Backend VPS) | Доорх 1-р алхам |
| OWNER эрх | legacyx.cc/staffpanel | In-game admin эрх зөвхөн вэбээс ирнэ |

---

### 1. `.env` файл үүсгэх (CS2 plugin-ийн, VPS-д нэг удаа)

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

### 2. GSLT авах (сервер бүрт 1)

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

### 3. GitHub token ба repo татах (нэг удаа)

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

### 4. Бүгдийг суулгах (нэг удаа)

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

### 5. Сервер нэмэх

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

### 6. Шалгах

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

### 7. OWNER / MANAGER / ADMIN эрх олгох

In-game admin эрх **зөвхөн вэбээс** ирнэ. Сервер дээр `admins.json`, SteamID жагсаалт гэж байхгүй.

1. **[Browser]** Тухайн хүн legacyx.cc-д Steam-ээр **нэг удаа нэвтэрсэн** байх ёстой.
2. OWNER нь legacyx.cc/staffpanel → **Staff** хэсэгт тухайн хэрэглэгчийг сонгож role өгнө:
   `OWNER`, `MANAGER`, `ADMIN`, … Status нь `active`.
3. **[CS2]** Тоглогч сервер дээр байвал 60 секундын дотор эрх нь ирнэ. Эсвэл reconnect хийнэ.

Вэбээс эрхийг хасвал 60–180 секундын дотор сервер дээрх эрх нь автоматаар алга болно.

Role бүр юу хийж чадахыг 13-р хэсэгт жагсаасан.

---

### 8. Шинэчлэлт: автоматаар

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

### 9. Тохиргоо өөрчлөх

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

### 10. Hosting panel (AMP / Hostinger game panel), terminal байхгүй бол

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

### 11. Түгээмэл алдаа ба шийдэл

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

### 12. Аюулгүй байдал

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

### 13. Тоглоом доторх командууд (staff)

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

### 14. Бүх repo-г шинэчлэх (deploy, нэг командаар)

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

#### Discord-д update-ийн зарлал (автомат)

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

### 15. VPS-ийн төлөв шалгах

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

### 16. Discord ticket

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

### 17. `.env`-ийг хамгаалах: устгахгүй, алдахгүй

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

### 18. Game серверийг өөр VPS руу шилжүүлэх (жишээ нь Монгол VPS)

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

#### Шинэ VPS-ийг сонгохдоо (Монгол provider-оос асуух)

| Шаардлага | Яагаад |
|---|---|
| Ubuntu 24.04/22.04, x86_64, RAM сервер бүрт 2–3 GB (+2), disk 60 GB+ | `cs2-host.sh`-ийн шаардлага |
| **Өөрийн public IPv4** (NAT биш) | Тоглогчид шууд `connect IP:27015` хийнэ |
| **UDP** port нээж болдог | CS2 UDP-ээр ажиллана |
| **Гадаад traffic** хязгааргүй эсвэл хангалттай | Эхний таталт ~35 GB, CS2 update бүр хэдэн GB |
| DDoS хамгаалалт | Game серверт хамгийн түгээмэл халдлага |

#### 1-р шат: бэлтгэл (серверүүд ажилласаар)

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

#### 2-р шат: шилжүүлэх (серверүүд ~5–10 минут унтарна)

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

#### Шалгах

- legacyx.cc → Play: сервер **шинэ IP**-тэйгээ 30 секундын дотор гарна. Connect хийж орно.
- **[CS2]** `!admin`, welcome мессеж, `!rs` ажиллаж байна. Discord дээр `/status` ажиллаж байна.
- Log-д `Too many requests` гарвал **[Hostinger]** backend `.env` дахь `API_RATE_LIMIT_MAX`-ийг нэмээд
  `bash ops/deploy.sh` ажиллуулна.
- Гадаад холболт тасарвал staff эрх fail closed байдлаар хаагдаж, вэбийн ban шалгагдахгүй. Локал ban
  хэвээр ажиллана. Холболт тогтворгүй бол `LEGACYX_ADMIN_AUTH_CACHE_SECONDS`-ийг (30–3600) өсгөнө.

#### Дараа нь

- **Буцаах:** **[Шинэ VPS]** `for c in /etc/legacyx/cs2/*.conf; do sudo systemctl disable --now "cs2@$(basename "$c" .conf)"; done`,
  дараа нь **[Hostinger]** дээр мөн тэр давталтыг `enable --now`-оор ажиллуулна. Тиймээс Hostinger дээрх
  CS2-ийг 1–2 өдөр бүү устга.
- Бүх зүйл хэвийн бол **[Hostinger]** port бүрт `sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh remove <port>`
  ажиллуулна. Дараа нь `/home/cs2`-ийг устгавал 35 GB чөлөөлөгдөнө.
- Шинэ GSLT ашиглах тохиромжтой үе: `/etc/legacyx/cs2/<port>.conf` → `GSLT=` → `sudo systemctl restart cs2@<port>`.

---

### Товч: шинэ VPS-ийг 0-ээс ажиллуулах

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

<a id="part3"></a>
## Хэсэг 3. Өдөр тутмын командууд

Команд бүрийн дор "**Ямар үүрэгтэй**" гэж бичсэн. Зам нь жишээ: өөрийн замаар солино.
Гурван газар байна: **A. Windows** (Workshop addon), **B. Game server VPS** (CS2), **C. Web VPS** (сайт, API, Discord bot).

---

### A. Windows: Workshop addon (HUD)

Нэг удаа, Git суусан байх ёстой (https://git-scm.com/download/win), CS2 → Settings → "Workshop Tools" → Install.

#### A1. Анх татах (нэг л удаа)
```powershell
cd $HOME\Documents
git clone https://github.com/userneon/legacyxxx-workshop.git
```
**Үүрэг:** addon-ийн эх файлуудыг (layout, CSS, зураг) компьютерт татна.

#### A2. Шинэчлэх (өөрчлөлт болгонд)
```powershell
cd $HOME\Documents\legacyxxx-workshop
git pull
```
**Үүрэг:** GitHub дээрх хамгийн сүүлийн эх файлыг татна. (Зөвхөн татна: game-д хараахан нөлөөлөхгүй.)

#### A3. Build (compile)
```powershell
.\tools\build.cmd
```
**Үүрэг:** `addon\`-ийг CS2-ийн `content\csgo_addons\legacyx` руу хуулж, Valve-ийн compiler-аар `.vxml_c`, `.vcss_c`, `.vtex_c` болгоно. Эхлээд `validate.py`-аар алдааг шалгана. Workshop-д нийтлэхээс өмнө **заавал** хийнэ.

#### A4. Build + өөрийн game дээр турших
```powershell
.\tools\build.cmd -Local
```
**Үүрэг:** A3-тай адил, нэмээд compile хийсэн файлуудыг `CS2\game\csgo\panorama` руу хуулна. Ингэснээр таны game **Workshop-гүйгээр** шууд харуулна (зөвхөн ТАНД). Дараа нь **CS2-ыг бүрэн хааж дахин эхлүүл** (Panorama layout-ыг cache хийдэг).

#### A5. Локал туршилтын файлыг цэвэрлэх
```powershell
$g = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\panorama"
Get-ChildItem $g -Recurse -Filter "legacyx*" | Remove-Item -Force
```
**Үүрэг:** A4-ийн хуулсан `legacyx` файлуудыг устгана (Valve-ийн файлд хүрэхгүй). Үүнийг хийхгүй бол game Workshop-ийн шинэ хувилбар биш, хуучин локал файлыг харуулсаар байна. CS2 өөр дискэнд бол замыг солино.

#### A6. CS2 өөр дискэнд бол
```powershell
.\tools\build.cmd -Cs2 "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive"
```
**Үүрэг:** CS2-ыг автоматаар олохгүй үед замыг гараар зааж өгнө.

#### A7. Workshop-д нийтлэх (бүх тоглогчдод)
CS2 Workshop Tools нээгээд `legacyx` addon → **Publish / Update existing item** (ID `3810642940`).
**Үүрэг:** compile хийсэн хувилбарыг Steam Workshop-д тавина. Тоглогчид дараагийн орох үедээ шинэ хувилбарыг татна. Нийтлэхээс өмнө A3 заавал.

**Дараалал:** `git pull` → `build.cmd -Local` → туршилт → `A5 цэвэрлэх` → `build.cmd` (цэвэр) → Workshop-д нийтлэх.

---

### B. Game server VPS (CS2)

Бүгдийг `cd /root/legacyxxx-plugins`-аас ажиллуулна.

#### B1. Plugin шинэчлэх (код өөрчлөгдсөн)
```bash
cd /root/legacyxxx-plugins
git pull
sudo ./scripts/cs2-host.sh deploy
```
**Үүрэг:** GitHub-аас шинэ plugin код татаж, build хийж, server-үүдэд суулгаад restart хийнэ. (Дараа нь `.env`-ийн холбоос хэвээр.)

#### B2. Бүх зүйлийг шинэчлэх (CS2, Metamod, CounterStrikeSharp)
```bash
sudo ./scripts/cs2-host.sh update
```
**Үүрэг:** server-үүдийг зогсоож, CS2, Metamod, CounterStrikeSharp, MultiAddonManager, plugin-уудыг шинэчилнэ. Autoupdate аль хэдийн CS2 шинэчлэлийг өөрөө хийдэг.

#### B3. Тохиргоо (`.env`) засах
```bash
cd /root/legacyxxx-plugins
sudo nano .env
```
Хадгал: `Ctrl+O`, Enter, `Ctrl+X`. Дараа нь restart (B4).
**Үүрэг:** plugin-ийн бүх тохиргоо (server нэр, mode, HUD асаалт, token). Энэ файл нь plugin-ийн уншдаг файлтай холбоостой, тусад нь хуулах шаардлагагүй.

#### B4. Server restart
```bash
sudo systemctl restart cs2@27015
```
**Үүрэг:** server-ийг дахин асаана. `.env` өөрчилсөн, addon солисны дараа заавал. Порт бүрт тусдаа (`27016` гэх мэт). Тоглогчид тасрах тул тоглоомгүй үед хий.

#### B5. HUD асаах / унтраах
```bash
sed -i 's/^LEGACYX_HUD_ENABLED=false/LEGACYX_HUD_ENABLED=true/' .env     # асаах
sed -i 's/^LEGACYX_HUD_ENABLED=true/LEGACYX_HUD_ENABLED=false/' .env     # унтраах
grep HUD_ENABLED .env
sudo systemctl restart cs2@27015
```
**Үүрэг:** `LegacyX-Hud` plugin-ийг (welcome card, rank card, knife vote) асаах эсвэл унтраана. `grep` нь одоогийн утгыг харуулна. Мөр `.env`-д байхгүй бол анхдагч нь `true`.

#### B6. Workshop addon-ийг тоглогчдод татуулах
```bash
sudo ./scripts/cs2-host.sh client-addons                    # одоогийнхыг харах
sudo ./scripts/cs2-host.sh client-addons 3810642940         # тохируулах
sudo ./scripts/cs2-host.sh client-addons none               # хоослох
sudo systemctl restart cs2@27015
```
**Үүрэг:** тоглогч server-д орохдоо татах Workshop addon-ийн ID. `none` нь addon-гүй болгоно (overflow-ийг шалгахад ашиглана). Өөрчилсний дараа restart.

#### B7. Server-ийн төлөв, log
```bash
sudo ./scripts/cs2-host.sh status
sudo ./scripts/cs2-host.sh logs 27015
sudo journalctl -u cs2@27015 --since "15 min ago" --no-pager | grep -i -E "overflow|reliable|disconnect|Hud" | tail -20
```
**Үүрэг:** server ажиллаж байгаа эсэх, live log, мөн алдаа хайх (overflow гэх мэт).

#### B8. Шинэ server нэмэх / устгах
```bash
sudo ./scripts/cs2-host.sh add 27016 <GSLT> competitive de_mirage 10 competitive_5v5 "LEGACY-X | MATCH #2"
sudo ./scripts/cs2-host.sh remove 27016
```
**Үүрэг:** шинэ порт дээр server үүсгэх (GSLT = Steam-ийн server token) эсвэл устгах.

#### B9. Discord-д update мэдэгдэл тохируулах
```bash
sudo ./scripts/announce.sh setup      # API хаяг + token (announce:write)
sudo ./scripts/announce.sh test       # туршилтын мэдэгдэл
```
**Үүрэг:** deploy болон CS2 update гарахад Discord-ын `/updates`-ээр сонгосон channel-д мэдэгдэл явуулна.

---

### C. Web VPS (сайт, API, Discord bot)

Repo бүр дотроо `bash ops/deploy.sh` гэж ажиллуулна.

| Юу | Хавтас | Команд |
|---|---|---|
| Сайт (frontend) | `~/legacyxxx-frontend` | `git pull && bash ops/deploy.sh` |
| API (backend) | `~/legacyxxx-backend` | `git pull && bash ops/deploy.sh` |
| Discord bot | `~/legacyxxx-discord-bot` | `git pull && bash ops/deploy.sh` |

**Үүрэг:** `git pull` хийж, build/шалгалт хийж, ажиллаж байгаа сервисийг шинэчилнэ. Frontend Nginx-ийн хавтас руу тавина; API, bot pm2-оор reload хийгдэнэ.

#### C1. Bot-ийн log
```bash
pm2 logs legacy-x-discord --lines 60 --nostream
tail -n 200 ~/.pm2/logs/legacy-x-discord-error.log
```
**Үүрэг:** bot-ын алдаа харах. pm2 нэр нь `legacy-x-discord` (API нь `legacy-x-api`).

---

### D. Discord дээрх тохируулах командууд

| Команд | Хэн | Үүрэг |
|---|---|---|
| `/faq-setup` | Зөвхөн эзэн | Түгээмэл асуултын самбар тавина |
| `/ticket-setup` | Зөвхөн эзэн | Ticket самбар тавина |
| `/voice-setup` | Зөвхөн эзэн | "Join to create" voice тохируулна |
| `/automod setup` | Зөвхөн эзэн | Хараал + зар сурталчилгааны шүүлтүүр асаана |
| `/updates` (`off`) | Зөвхөн эзэн | Шинэчлэлтийн мэдээ ирэх channel сонгоно |
| `/rules` | Manage Server | Цэстэй дүрмийн самбар (`section`-гүй) эсвэл дүрэм шууд тавина |
| `/admincalls` | Manage Server | `!calladmin` / `!report` ирэх channel |
| `/penalties` | Manage Server | Шийтгэлийн мэдээ ирэх channel |
| `/news`, `/status` | Manage Server | Мэдээ, server-ийн самбар |
| `/automod add/remove/allow` | Manage Server | Хориотой үг нэмэх / хасах / зөвшөөрөх |

---

### E. Overflow алдаа шалгах (товч)

1. `client-addons none` + restart → overflow алга бол addon шалтгаан.
2. `client-addons 3810642940` + `LEGACYX_HUD_ENABLED=false` + restart → overflow гарвал addon-ийн татагдалт, үгүй бол HUD plugin.
3. `sudo ./scripts/cs2-host.sh update` → Metamod/MultiAddonManager-ийн шинэ хувилбар.
4. Тест дууссаны дараа `LEGACYX_HUD_ENABLED=true` + addon-ийг буцааж тавь.

<a id="part4"></a>
## Хэсэг 4. Командын бүрэн лавлах

Команд бүрд: **хаана бичих**, **хэрхэн бичих**, **хэзээ ашиглах**, **жишээ**, **гарч болох алдаа ба
шалтгаан, засах арга**. Суулгах, анхны тохиргоо: [GARIIN_AVLAGA_MN.md](#part2).

### Хэрхэн уншиж, ашиглах вэ

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

### 1. Deploy: шинэчлэх командууд [VPS]

Код GitHub-д шинэчлэгдсэн гэж хэлэх бүрд тухайн хэсгийг шинэчилнэ. VPS өөрөө татдаггүй (plugin-ийн
05:00-ийн автомат update-ээс бусад).

#### `bash ops/deploy.sh` (API)

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

#### `bash ops/deploy.sh` (Discord bot)

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

#### `bash ops/deploy.sh` (вэб)

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

#### `cs2-host.sh deploy` (CS2 plugins)

```bash
sudo /opt/legacyxxx-plugins/scripts/cs2-host.sh deploy
```
- **Хэзээ:** plugin шинэчлэгдсэн, одоо шууд оруулах үед (05:00-ийг хүлээхгүй).
- **Юу хийх:** `git pull` → CS2, Metamod, CounterStrikeSharp, plugin → WeaponPaints signature шалгах → серверүүдийг restart.
- **Анхаар:** ажиллаж байгаа тоглолт тасарна (серверүүд restart хийнэ).
- **AMP panel** дээр ажиллаж байгаа бол энэ хэрэггүй: zip-ээ panel дээр задална.

Алдаанууд нь доорх `cs2-host.sh`-тэй ижил.

---

### 2. `cs2-host.sh`: CS2 сервер удирдах [VPS]

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
| `fatal: … 403` / `could not read Username` | Token асуудал | [GARIIN_AVLAGA_MN.md](#part2) 3-р алхам |
| `WeaponPaints signature … no maintained signature matches` | CS2 update-ийн дараа skin-ий signature хараахан гараагүй | Юу ч хийхгүй: 10 мин тутам өөрөө оролдоно. Серверүүд ажилласаар, зөвхөн skin түр унтарна |

---

### 3. VPS дээрх бусад хэрэгтэй командууд [VPS]

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

#### `env-backup.sh`: `.env` нөөцлөх

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

**Бүх repo-гийн төлөв нэг дор:** [GARIIN_AVLAGA_MN.md](#part2) 15-р хэсэг.

#### `create-game-server.mjs` [VPS, backend фолдерт]

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

### 4. Тоглоом доторх командууд: бүх тоглогч [CS2 chat]

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

### 5. Тоглоом доторх командууд: staff [CS2 chat]

Role-ийг legacyx.cc/staffpanel дээр өгнө. Role бүр доод role-ийнхоо бүх командыг ажиллуулна.
Хоёр шалгуур: role-ийн эрх **ба** stamina (STAFF 250, ADMIN 500, MANAGER 750, OWNER 1000).

`!admin` цэс: `W/S` сонгох, `E` дарах, `A` буцах, `R` гарах. Ихэнх командыг цэснээс хийж болно.

> ⚠️ **`!ban`, `!mute`, `!gag`, `!silence`-д минутаа бичихгүй бол (эсвэл `0`) шийтгэл БҮРМӨСӨН болно.**
> Түр хугацаагаар бол заавал минут бичнэ: `!ban Bat 60` = 1 цаг, `1440` = 1 өдөр, `10080` = 7 хоног.

#### STAFF

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

#### ADMIN (дээрх + )

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

#### MANAGER (дээрх + )

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

#### OWNER (бүгд + )

| Команд | Юу |
|---|---|
| `!cleanbans` | **Бүх серверийн бүх ban-ыг** вэбээс цэвэрлэх (буцаах боломжгүй) |
| `!cleanall`, `!cleanmute`, `!cleangag` | Бүх шийтгэлийг цэвэрлэх |
| `!rcon <команд>`, `!cvar <нэр> [утга]` | Серверийн тохиргоо |
| `!prac`, `!match`, `!sleep`, `!exitprac` | Practice mode |
| `!money`, `!armor`, `!hp`, `!god`, `!noclip`, `!weapon`, `!strip`, `!respawn`, `!freeze`, `!beacon`, `!glow`, `!drug`, `!blind`, `!shake`, `!bury`, `!goto`, `!bring`, `!gravity` | Fun командууд (`!admin` цэсэнд байхгүй) |

#### Staff-д гарах мессежүүд

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

### 6. CS2 console командууд [CS2 console, сервер]

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

### 7. Discord командууд [Discord]

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

#### Ticket-ийн товчнууд (бүгд зөвхөн staff)

| Товч | Хэн | Юу |
|---|---|---|
| ✋ Хариуцах / ↩️ Суллах | Staff / хариуцсан хүн эсвэл manager | Авах, буцааж суллах |
| 🔁 Шилжүүлэх | Хариуцсан хүн эсвэл manager (хэн ч аваагүй бол аль ч staff) | Өөр staff-д |
| ➕ Хүн нэмэх / ➖ Хүн хасах | Staff | 5 хүртэл |
| 🔒 Хаах | Staff | Transcript хадгалаад түгжинэ |

#### Discord-д гарах мессежүүд

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

### 8. Git ба token [VPS]

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
| `could not read Username` | Энэ хэрэглэгч дээр token хадгалаагүй | [GARIIN_AVLAGA_MN.md](#part2) 3-р алхам (root-д ч) |
| `Not possible to fast-forward` / `Your local changes would be overwritten` | VPS дээр файл гараар зассан | `git status` → `git checkout -- <файл>` |
| `fatal: not a git repository` | Буруу фолдер | `cd` зөв зам руу (`/root/legacyxxx-*`, `/opt/legacyxxx-plugins`) |
| `dubious ownership` | Фолдерын эзэмшигч өөр | `git config --global --add safe.directory <зам>` |

---

### 9. Ямар үед юу хийх вэ (товч)

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

<a id="part5"></a>
## Хэсэг 5. Plugin-ууд ба тохиргоо

Бүх plugin нэг л тохиргооны файлыг `LegacyX.Shared.Configuration`-аар уншдаг: **plugin repo-ийн үндсэн `.env`**
(Хэсэг 2-ын §1, §4). Plugin-ийн уншдаг `addons/counterstrikesharp/.env` нь түүн рүү заасан холбоос (`cs2-host.sh env`).
Plugin-д зөвхөн нууцгүй gameplay-ийн анхдагч утга (JSON/cfg) байж болно: API token, DB нууц, RCON, server identity орохгүй.

### Plugin бүр

| Plugin | Үүрэг | Гол тохиргоо (`.env`) |
|---|---|---|
| `LegacyX-Admin` | Ban, mute, gag, report, vote, цэс; staff эрх API-аас | `LEGACYX_ADMIN_ENABLED`, `LEGACYX_ADMIN_PLUGIN_SECRET` (admin:read), `LEGACYX_ADMIN_AUTH_*`, `LEGACYX_ADMIN_CENTRAL_BANS_*`, `LEGACYX_ADMIN_CENTRAL_PENALTIES_ENABLED` |
| `LegacyX-AFKManager` | AFK, C4, spectator, anti-camp | `LEGACYX_AFKMANAGER_ENABLED` |
| `LegacyX-Community` | Цол, EXP, welcome chat, Tab дахь цол | `LEGACYX_COMMUNITY_*` (WELCOME, SCOREBOARD_RANKS, SCOREBOARD_RANK_TYPE) |
| `LegacyX-Hud` | Workshop HUD: welcome card, rank card, match дүн, knife vote | `LEGACYX_HUD_ENABLED`, `LEGACYX_HUD_RANK_CARD` |
| `LegacyX-Killfeed` | Kill feed (сайтын дээд талын ticker) | `LEGACYX_KILLFEED_ENABLED` |
| `LegacyX-MatchZy` | Match lifecycle, EXP, Match Core | `LEGACYX_MATCHZY_ENABLED`, `…_RANK_ENABLED`, `…_MATCH_CORE_ENABLED` |
| `LegacyX-Spectator` | Spectator болон амьд тоглогчийн chat/voice тусгаарлалт | `LEGACYX_SPECTATOR_COMMS_ENABLED` |
| `LegacyX-Status` | Server-ийн төлөв, map, тоглогч, score-г вэбэд 30 секунд тутам | `LEGACYX_STATUS_*` (INTERVAL_SECONDS, KEEP_AWAKE) |
| `LegacyX-WeaponPaints` | Вэбээс сонгосон skin-ийг тоглогчид оноох | `LEGACYX_SKINBRIDGE_*` (POLL_SECONDS) |

### Server-ийн тохиргоо

```dotenv
LEGACYX_API_BASE_URL=https://api.legacyx.cc
LEGACYX_SERVER_HOST=<public IP эсвэл домэйн>
LEGACYX_SERVER_ID_PREFIX=srv            # port 27015 → srv-27015
LEGACYX_SERVER_MODE=competitive_5v5     # competitive_5v5 | fun | proleague
LEGACYX_27015_SERVER_NAME=LEGACY-X | MATCH #1     # port бүрд тусдаа утга давуу
LEGACYX_PLUGIN_TOKEN=<token>            # create-game-server.mjs үүсгэнэ
```

Port-ийн дугаартай хувьсагч (`LEGACYX_27016_SERVER_MODE=fun`) тэр server дээр нийтийн утгыг давна. Нэр, mode нь вэбийн
Play хуудас, home дэх тоонд харагдана. Өөрчилсний дараа `sudo systemctl restart cs2@<port>`.

Бүрэн жагсаалт, тайлбар: repo-ийн `.env.example`.

### Token ба хил хязгаар

- Нэг machine-д нэг token (`create-game-server.mjs`): scope нь `admin:read bans:read bans:write stats:write matches:write servers:write skinchanger:read`.
  Модуль тус бүрийн тусгай token (`LEGACYX_<MODULE>_PLUGIN_TOKEN`) байвал түүнийг давуу хэрэглэнэ.
- Plugin-д `SUPABASE_*`, `DATABASE_URL`, `STEAM_API_KEY`, Discord token байхгүй. Plugin зөвхөн Root API-тай ярина.
- Browser дээрээс RCON, shell, SQL, plugin команд ажиллуулахгүй. API нь module болон server identity-г шалгасны дараа л бичнэ.
- RCON: одоогийн plugin-ууд RCON ашиглахгүй. Ирээдүйн, хянагдсан server-тал executor-д зориулан тусгаарласан.
  RCON нууц үгийг plugin JSON, Discord, DB, git, screenshot, chat-д хэзээ ч бүү оруул.
- Module-ийг асаахаасаа өмнө: тохирох plugin token байгаа эсэх, scope нь зөв эсэх, `LEGACYX_SERVER_ID` DB-ийн server-тэй
  таарах эсэх, plugin алдаагүй ачаалагдсан эсэхийг шалгана (`logs <port>`).

### Skin (WeaponPaints) хэрхэн ажилладаг

```text
Вэб дээр skin сонгох → API хадгална → server-тэй холбосон job үүсгэнэ
  → LegacyX-WeaponPaints job-ийг өөрийн token-оор авч, тоглогчид оноож → "applied" эсвэл "failed" гэж баталгаажуулна
  → API үр дүнг хадгалж вэбэд харуулна
```

Plugin DB-д бичихгүй, нууцгүй. Вэб нь plugin баталгаажуулахаас өмнө "skin тавигдлаа" гэж хэлэхгүй.

### Нэршил ба admin plugin-ийн хил

- Хуучин минимал `AdminPlus` bridge repo-оос хасагдсан: admin цэс, ban, comms sanction, report, vote, reservation-ийн
  цорын ганц эзэн нь upstream AdminPlus (`debr1sj/CS2-AdminPlus`, commit `1225a03e…`, MIT) дээр суурилсан `LegacyX-Admin`.
  Давхар `css_*` бүртгэл гарахгүйн тулд. MIT `LICENSE` болон зохиогчийн тэмдэглэл үлдэх ёстой.
- Admin plugin файл-суурьтай (ban, group, comms record, цэс); Supabase/DB нууц авдаггүй. Эрх API-аас, ban/penalty
  API-д бичигдэнэ (Хэсэг 6).
- Нэршил: `LegacyX-<Нэр>/` хавтас → `LegacyX-<Нэр>.dll` → `LEGACY-X <Нэр>` харагдах нэр.

### Plugin → API холбоос (товч)

| Plugin | API-тай холбоо |
|---|---|
| Admin | staff эрх (admin:read), ban шалгах (bans:read), ban/penalty бичих (bans:write), `!report`/`!calladmin` дуудлага |
| Community | тоглогчийн цол, EXP, clan (scoped token) |
| MatchZy | match дуусгавар, EXP (`/plugin/matchzy/events`), lifecycle (`/plugin/match-core/events`) |
| Status | server төлөв, live score (`/plugin/servers/heartbeat`, `/plugin/live-match/snapshots`) |
| WeaponPaints | skin job авах, баталгаажуулах |
| Hud | цол, match дүнг унших (Community-тэй адил) |
| Killfeed | kill feed илгээх |
| Spectator, AFKManager | API-гүй, game-local |


<a id="part6"></a>
## Хэсэг 6. Admin эрх ба in-game командууд

### Staff эрх яаж ирдэг

In-game staff permissions come **only** from the LEGACY-X website/database, through the Root API.
The game server stores no staff list: no SteamIDs in source, no `admins.json` written or read,
no `addadmin`. Anything the plugin cannot confirm with the API is treated as a normal player.

### Flow

```text
player connects ──► OnClientAuthorized
                     │ 1. strip any CounterStrikeSharp admin data for that SteamID
                     │ 2. POST /api/v1/plugin/admin/authorizations {serverId, steamIds}
                     ▼
Root API (scope admin:read, x-plugin-id legacyx-admin)
                     │ legacy_x.resolve_game_staff(server_id, steam_ids)
                     │   1. staff_server_assignments row for this server  → decides for this server
                     │   2. otherwise the global legacy_x.staff role        → applies to every server
                     │   3. otherwise player
                     ▼
answer ──► AuthorizationStore (in memory) ──► main thread: AdminManager.AddPlayerPermissions +
                                               SetPlayerImmunity, AdminPlus stamina/immunity maps
every AUTH_REFRESH_SECONDS: re-check all online players (revokes / role changes)
every 5 s: drop grants past expires_at or not re-confirmed within AUTH_CACHE_SECONDS
disconnect: grant and admin data removed; a lookup still in flight is ignored
unload / hot reload: all granted admin data removed / everyone re-checked
```

### Fail closed

| Situation | Result |
|---|---|
| `LEGACYX_ADMIN_API_BASE_URL` / `PLUGIN_SECRET` / `PLUGIN_ID` / `LEGACYX_SERVER_ID` missing or invalid | Nobody receives permissions; logged once at load |
| API unreachable, timeout (5 s), HTTP error, non-JSON, wrong `serverId`, duplicate entries | New players get nothing; existing grants are kept only until `AUTH_CACHE_SECONDS` after their last confirmation, then removed |
| Entry with `authorized` not `true`, `status` not `active`, unknown role, `role: player`, past or unreadable `expiresAt` | That player gets nothing |
| Requested SteamID missing from the answer | Player |
| Stale answer (older request, or the player left meanwhile) | Ignored |
| Leftover entries in `configs/admins.json` | Ignored: each player's admin data is replaced by the API answer; a warning is logged |

Tokens are never logged. Error messages carry only the HTTP status or failure kind.

### API contract

`POST /api/v1/plugin/admin/authorizations`

Headers: `Authorization: Bearer <LEGACYX_ADMIN_PLUGIN_SECRET>` (token with scope `admin:read`),
`x-plugin-id: legacyx-admin`, `Content-Type: application/json`.

```json
{ "serverId": "eu-5v5-1", "steamIds": ["76561198000000001", "76561198000000002"] }
```

- `serverId`: `LEGACYX_SERVER_ID`, 1–64 of `A-Z a-z 0-9 . _ : -`.
- `steamIds`: 1–64 SteamID64 values (the plugin splits larger servers into several requests).

`200`:

```json
{
  "serverId": "eu-5v5-1",
  "checkedAt": "2026-09-27T12:00:00.000Z",
  "players": [
    { "steamId": "76561198000000001", "authorized": true,  "role": "admin",  "status": "active",  "source": "server", "expiresAt": "2026-10-01T00:00:00.000Z" },
    { "steamId": "76561198000000002", "authorized": false, "role": "player", "status": "revoked", "source": "global", "expiresAt": null }
  ]
}
```

- One entry per distinct requested SteamID. `role`: `owner | manager | admin | staff | player`.
- `status`: `active | suspended | revoked | expired | none`. `source`: `server | global | none`.
- `authorized` is `true` only for a staff role, `status: active` and no past `expiresAt`.
- Errors: `401` missing/invalid token, `403` wrong scope or plugin identity, `400` invalid body.
- `Cache-Control: no-store`.

### Database

`legacy_x.staff_server_assignments` (service role only, RLS on):

| column | notes |
|---|---|
| `steam_id` | SteamID64 |
| `server_id` | the server's `LEGACYX_SERVER_ID` |
| `role` | `owner`, `manager`, `admin`, `staff` |
| `status` | `active`, `suspended`, `revoked` |
| `expires_at` | optional; the role ends at this time |
| unique | `(steam_id, server_id)` |

Global staff stay in `legacy_x.staff` (as before, they apply on every server): `OWNER` → owner,
`MANAGER` → manager, `ADMIN` → admin while `status = 'active'`. `DEVELOPER`/`DESIGNER` are website
roles with no in-game authority. A per-server row overrides the global role on that server, including
revoking it there. Migration: backend `supabase/legacy_x_game_staff_authorization.sql`.

### Permission mapping

Defined once in `LegacyX-Admin/Authorization/StaffPermissions.cs`.

| Role | CounterStrikeSharp flags | Immunity | Stamina |
|---|---|---:|---:|
| owner | `@css/root` + generic, kick, ban, unban, slay, changemap, chat, vote, config, cvar, rcon, cheats, `@legacyx/match` | 1000 | 1000 |
| manager | generic, kick, ban, unban, slay, changemap, chat, vote, config, `@legacyx/match` | 750 | 750 |
| admin | generic, kick, ban, slay, changemap, chat, vote, `@legacyx/match` | 500 | 500 |
| staff | generic, chat, vote | 250 | 250 |
| player | — | 0 | 0 |

Stamina is AdminPlus' per-command threshold (`AdminPlus.Stamina.cs`): 250 communication/info, 500
standard moderation and `!rr`, 750 high-impact (unban, map, slay), 1000 root and gameplay-altering
commands. A command needs both its stamina and its `@css/*` flag. MatchZy admin commands need
`@css/config` (manager and owner), except `!start`, which `@legacyx/match` also opens (admin and up).
`!rr` restarts the round (LegacyX-Admin); resetting the whole match is MatchZy's `!restart`. `!calladmin` reaches online ADMIN/MANAGER/OWNER, `!callmanager` MANAGER/OWNER.

`addadmin` / `removeadmin` only answer that staff are managed on legacyx.cc. `adminlist` prints the
authorized staff online; `adminreload` re-checks everyone with the API now. The in-game
"ADMINISTRATION" menu is a read-only list of online staff.

### Configuration (`CounterStrikeSharp/.env`)

```dotenv
LEGACYX_SERVER_ID=eu-5v5-1
LEGACYX_ADMIN_API_BASE_URL=https://api.legacyx.cc
LEGACYX_ADMIN_PLUGIN_ID=legacyx-admin
LEGACYX_ADMIN_PLUGIN_SECRET=<token with admin:read>
LEGACYX_ADMIN_AUTH_REFRESH_SECONDS=60     # 15-600
LEGACYX_ADMIN_AUTH_CACHE_SECONDS=180      # 30-3600, above the refresh
```

When several servers share one CounterStrikeSharp folder, give each process its own server id by
pointing `LEGACYX_ENV_FILE` at a per-server env file (it is read before `CounterStrikeSharp/.env`).
Without distinct ids, server-specific assignments cannot tell the servers apart; global staff still work.

Removed settings: `LEGACYX_ADMIN_POLICY_SYNC_ENABLED`, `LEGACYX_ADMIN_POLICY_REFRESH_SECONDS`
(`scripts/validate-plugin-runtime.mjs` reports them as stale).

### Logs

All lines start with `[LegacyX.Admin]`: configuration problems, `authorized as ROLE`, `role changed`,
`revoked`, `expired or could not be re-confirmed`, lookup failures (at most once a minute).

### Tests

```bash
dotnet test tests/LegacyX.Admin.Authorization.Tests        # role mapping, parser, client, cache, live HTTP API
./scripts/package.sh && dotnet run --project tests/LegacyX.PackageLoadTest -- dist/legacyx-cs2
```

### In-game команд (inventory)

Энэ жагсаалт нь `LegacyX-Admin` source-д бүртгэгдсэн command-ууд дээр үндэслэсэн. Chat дотор `!` prefix ашиглана. Console alias нь `css_` prefix-тэй; жишээ нь `!ban` болон `css_ban` нь ижил handler руу орно.

> Энэ бол command inventory. OWNER, MANAGER, ADMIN зэрэг website/staff role-д яг аль command нээхийг тусдаа allowlist policy-оор шийднэ. Frontend visibility нь permission биш; plugin тал өөрөө permission шалгана.

### Target хэлбэр

| Хэлбэр | Тайлбар |
|---|---|
| `<target>` | Player name, `#userid`, SteamID эсвэл тухайн command зөвшөөрсөн selector |
| `all` / `@all` | Бүх player |
| `@t`, `@ct`, `@spec` | Terrorist, Counter-Terrorist, Spectator group |
| `[value]` | Optional argument |

### Player moderation

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!ban` | `css_ban` | `!ban <nick/steamid/ip/userid> [duration] [reason]` | Player ban |
| `!ipban` | `css_ipban` | `!ipban <nick/ip/steamid/userid> [reason]` | IP ban |
| `!unban` | `css_unban` | `!unban <nick/ip/steamid>` | Ban цуцлах |
| `!lastban` | `css_lastban` | `!lastban` | Сүүлд disconnect хийсэн player list |
| `!baninfo` | `css_baninfo` | `!baninfo <target>` | Ban мэдээлэл |
| `!kick` | `css_kick` | `!kick <nick> [reason]` | Server-ээс гаргах |
| `!mute` | `css_mute` | `!mute <target> [duration] [reason]` | Voice mute |
| `!gag` | `css_gag` | `!gag <target> [duration] [reason]` | Text chat gag |
| `!unmute` | `css_unmute` | `!unmute <target>` | Voice mute цуцлах |
| `!ungag` | `css_ungag` | `!ungag <target>` | Gag цуцлах |
| `!silence` | `css_silence` | `!silence <target> [duration] [reason]` | Mute + gag |
| `!unsilence` | `css_unsilence` | `!unsilence <target>` | Mute + gag цуцлах |
| `!mutelist` | `css_mutelist` | `!mutelist` | Mute list |
| `!gaglist` | `css_gaglist` | `!gaglist` | Gag list |

### Player and server actions

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!slap` | `css_slap` | `!slap <target> <damage>` | Damage slap |
| `!slay` | `css_slay` | `!slay <target>` | Player kill |
| `!money` | `css_money` | `!money <target> <amount>` | Money set |
| `!armor` | `css_armor` | `!armor <target> <amount>` | Armor set |
| `!rr` | `css_rr` | `!rr` | Round restart |
| `!map` | `css_map` | `!map <map>` | Map change |
| `!wsmap` / `!workshop` | `css_wsmap` / `css_workshop` | `!wsmap <workshop_id>` | Workshop map change |
| `!who` | `css_who` | `!who <nick/steamid>` | Player identity/info |
| `!players` | `css_players` | `!players` | Console player list |
| `!rename` | `css_rename` | `!rename <target> <name>` | Player rename |
| `!team` | `css_team` | `!team <target> <t|ct|spec>` | Team placement |
| `!swap` | `css_swap` | `!swap <target>` | Team swap |

### Announcement and communication

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!asay` | `css_asay` | `!asay <message>` | Admin chat announcement |
| `!csay` | `css_csay` | `!csay <message>` | Center-screen announcement |
| `!hsay` | `css_hsay` | `!hsay <message>` | HUD announcement |
| `!psay` | `css_psay` | `!psay <target> <message>` | Player private message |
| `!admins` | `css_admins` | `!admins` | Online admins list |
| `!hideadmin` | `css_hideadmin` | `!hideadmin` | `!admins` list-д харагдах эсэхийг солих |
| `css_report` / `css_calladmin` | — | Report arguments server config-аас хамаарна | Player `!report` / `!calladmin` → вэбсайтад бүртгэгдэж, Discord bot-ын `/admincalls` channel-д очно (webhook хэрэггүй) |

### Vote and match utility

| Chat command | Console alias | Syntax | Үйлдэл |
|---|---|---|---|
| `!vote` | `css_vote` | `!vote <question> <option1> <option2> [option3...]` | Custom vote |
| `!votemap` | `css_votemap` | `!votemap <map1> <map2> [map3...]` | Map vote |
| `!rvote` | `css_rvote` | `!rvote` | Vote дахин нээх |
| `!cancelvote` | `css_cancelvote` | `!cancelvote` | Active vote цуцлах |
| `!votekick` | `css_votekick` | `!votekick` | Kick vote |
| `!voteban` | `css_voteban` | `!voteban` | Ban vote |
| `!votegag` | `css_votegag` | `!votegag` | Gag vote |
| `!votemute` | `css_votemute` | `!votemute` | Mute vote |
| `!votesilence` | `css_votesilence` | `!votesilence` | Silence vote |

### Advanced and fun commands

Эдгээр нь source дээр `@css/slay`, `@css/cheats`, `@css/kick` permission-оор бүртгэгдсэн. Competitive/Pro League дээр default-оор нээхгүй байх нь зүйтэй.

| Permission | Commands |
|---|---|
| `@css/slay` | `css_freeze`, `css_unfreeze`, `css_gravity`, `css_bury`, `css_unbury`, `css_beacon`, `css_shake`, `css_unshake`, `css_blind`, `css_unblind`, `css_clean`, `css_goto`, `css_bring`, `css_hrespawn`, `css_1up`, `css_drug`, `css_undrug`, `css_glow`, `css_color` |
| `@css/cheats` | `css_revive`, `css_respawn`, `css_noclip`, `css_weapon`, `css_strip`, `css_sethp`, `css_hp`, `css_speed`, `css_unspeed`, `css_god` |
| `@css/kick` | `css_team`, `css_swap` |

### High-risk owner-only commands

| Command | Эрсдэл | Proposed policy |
|---|---|---|
| `!rcon <command>` / `css_rcon` | Arbitrary server command | OWNER-only; website browser UI-д raw RCON огт бүү гарга |
| `!cvar <cvar> [value]` / `css_cvar` | Runtime server setting өөрчилнө | OWNER-only; allowlist-тэй executor ашиглах |
| `css_cleanbans`, `css_cleanipbans`, `css_cleansteambans` | Historical penalty data mass-delete | Default disabled; manual database backup + explicit owner procedure шаардлагатай |
| `css_cleanall`, `css_cleanmute`, `css_cleangag` | Punishment cleanup | Default disabled; audit-required owner operation |
| `!addadmin`, `!removeadmin`, `!adminreload` | In-game admin source өөрчилнө | OWNER-only; canonical `staff` database model-тэй reconcile хийхээс өмнө production-д нээхгүй |

### Plugin administration

| Command | Console alias | Үйлдэл |
|---|---|---|
| `!admin` | `css_admin`, `css_adminmenu` | Admin menu нээх |
| `!banlist` | `css_banlist` | Ban list menu |
| `!adminhelp` | `css_adminhelp` | Detailed command help |
| `!adminlist` | `css_adminlist` | Plugin admin list |
| `!addadmin` | `css_addadmin` | Plugin admin add |
| `!removeadmin` | `css_removeadmin` | Plugin admin remove |
| `!adminreload` | `css_adminreload`, `css_admin_reload` | Plugin admin cache reload |
| `!version` | `css_version` | LEGACY-X Admin version |

### Permission model next step

Source-ийн generic permission namespaces нь `@css/root`, `@css/ban`, `@css/kick`, `@css/slay`, `@css/cheats`, `@css/generic`, `@css/reservation` байна. Дараагийн алхамд OWNER, MANAGER, ADMIN болон бусад community role тус бүрт эдгээрээс яг ямар permission/command өгөхийг explicit allowlist болгон шийднэ.

<a id="part7"></a>
## Хэсэг 7. Discord

**Дүрэм:** game server Discord руу webhook, token ашиглахгүй. Discord-ын бүх гаралтыг **LEGACY-X Discord bot** өөрөө
API-г уншиж тавина. (Хуучин `LEGACYX_ADMIN_CALL_CHANNEL_*` тохиргоо хүчингүй, `.env`-д байвал үл тоомсорлоно.)

### Bot юу хийдэг

| Чиглэл | Юу | Тохируулах |
|---|---|---|
| Тавтай морил | Шинэ гишүүнд banner-тай мэндчилгээ | `WELCOME_CHANNEL_ID` (bot-ын `.env`) |
| Admin дуудлага | `!calladmin`, `!callmanager`, `!report` → channel-д, "Server-т орох" товчтой | `/admincalls` |
| Шийтгэл | Шинэ ban, mute, gag-ийн card | `/penalties` |
| Server-ийн төлөв | Live self-updating самбар, `/servers`, `/status` | `/status` |
| Update-ийн мэдээ | Deploy, CS2 update (алтан banner-тай "finished") | `/updates` (+ `announce.sh setup`) |
| Дүрэм | Цэстэй самбар, сонгосон дүрмийг зөвхөн сонгосон хүнд | `/rules` |
| FAQ | Товчтой самбар, хариулт зөвхөн хүнд | `/faq-setup` |
| Ticket | Appeal, report, тусламж | `/ticket-setup` |
| Автомод | Хараал, бусад server-ийн урилга/зар хаах | `/automod setup` |
| Мэдээ, LFG, voice | CS2 мэдээ, хамтрагч хайх, "join to create" voice | `/news`, `/lfg`, `/voice-setup` |
| Rank role | `/link`-ээр Steam холбосон хүнд цолны role | `/link` |

Эзний л ажиллуулдаг: `/faq-setup`, `/ticket-setup`, `/voice-setup`, `/automod setup`, `/updates`.

### Server рүү орох товч

Discord товч `steam://` нээж чадахгүй тул "Server-т орох" нь `legacyx.cc/connect?server=<id>` хуудас руу очиж, тэр хуудас
Steam-ийг нээнэ (Join, Copy IP, тоглогчдын жагсаалттай).

### Хяналт

- Bot `API_TOKEN` (scope `discord:link`, `bans:write`) ашиглана. Үүнийг game server-т бүү өг.
- Мэдэгдэл нь зөвхөн баримт: коммитын гарчиг, build дугаар. AI бичдэггүй, орчуулдаггүй.
- Хэзээ ч `@everyone`, `@here` mention-гүй (зөвхөн /admincalls-д тохируулсан role).


<a id="part8"></a>
## Хэсэг 8. Chat-ийн хэв маяг

In-game text looks like legacyx.cc: dim, muted, neutral, with crimson only in the wordmark.
`LegacyXChat.System()` (shared library) applies the colors to every system chat line, so a module
cannot break them by accident. Write new text to match anyway.

| | Rule |
|---|---|
| Prefix | `LEGACY-X •`: "LEGACY-" white, "X" crimson, "•" grey, like the website logo. Added by `LegacyXChat.System`; never write a prefix yourself |
| Case | "LEGACY-X" is always in capitals. Everything else is a normal sentence: capital first letter, the rest lower case. Keep game words as players write them: AFK, CT, T, HP, EXP, StatTrak |
| Body | dim grey (`{grey}`, or `{default}`) |
| Emphasis | `{white}` for names, numbers, maps and commands. Go back with `{grey}` |
| Green | `{green}` only for something that worked or is on: "Skins updated.", "You're ready.", "Live." |
| Errors | plain grey, no color. Say what happened and, if useful, what to do: "Wait a moment before trying again." |
| Other colors | none. Any other tag or ChatColors code turns white. Crimson (`{brand}`) is for the prefix only |
| Length | one or two short sentences, ending with a period |
| Words | plain game words. No "plugin", "API", "sync", "refresh", "config", "cvar", "JSON" or error codes |
| Punctuation | no `!` for emphasis, no `…`, no emoji, no `[TAGS]`, no ALL CAPS |

Examples:

```text
LEGACY-X • Skins updated.                                            (green)
LEGACY-X • You're AFK. Move or you'll be kicked in 15 seconds.        (15 white)
LEGACY-X • Temuujin was kicked for being AFK.                        (name white)
LEGACY-X • Admin777 banned Player for 30 minutes. Reason: Cheating.
LEGACY-X • Operator I · 1,240 EXP · #12 · 260 to Operator II
```

The `!admin` menu (center screen) follows the same look: the `LEGACY-X` wordmark and the menu name in
dim grey on top, rows in grey, the selected row white with the crimson bar the website uses for the
active page, and the keys in small grey at the bottom.

Only `lang/en.json` in each plugin is written to this rule. Other languages are the upstream
translations; their colors still follow the rule through `LegacyXChat`, but their wording does not.
CounterStrikeSharp shows English unless the server language (`core.json`) or a player's `!lang` picks another.

<a id="part9"></a>
## Хэсэг 9. Түүх, хүчингүй болсон шийдвэрүүд

Өмнө нь тусдаа файл байсан, одоо хүчингүй болсон зүйлс:

| Өмнөх баримт | Тэр үеийн агуулга | Одоо |
|---|---|---|
| `DISCORD_CALL_CHANNEL_ONLY`, `DISCORD_MATCH_MODE_POLICY`, `DISCORD_SERVER_CONNECT` | "Discord-д зөвхөн `!admin` дуудлагын webhook" | Webhook байхгүй; бүх Discord гаралт bot-оор (Хэсэг 7) |
| `PHASE_A_DEPLOYMENT_PREPARATION` | "Энэ үед CS2 server, VPS байхгүй; зөвхөн source" | Server ажиллаж байна; суулгах нь Хэсэг 2 |
| `PLUGIN_RUNTIME_ENVIRONMENT` | `.env`-ийг `…/counterstrikesharp/CounterStrikeSharp/.env`-д | Одоо plugin repo-ийн `.env` (холбоосоор), Хэсэг 5 |
| `PLUGIN_REGISTRY`, `PLUGIN_INTEGRATION_MATRIX` | 6 plugin | 9 plugin (Status, Hud, Killfeed нэмэгдсэн), Хэсэг 5 |
| `LEGACYX_MODULE_NAMING_AND_ADMIN_REPLACEMENT` | AdminPlus солилт, нэршил | Хэсэг 5-д нэгтгэсэн |

Хуучин файлууд git түүхэнд хадгалагдсан: `git log --follow docs/<файл>`.
