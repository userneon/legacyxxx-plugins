# LEGACY-X: өдөр тутмын командууд (юу хийдэг, хэзээ ашиглах)

Команд бүрийн дор "**Ямар үүрэгтэй**" гэж бичсэн. Зам нь жишээ: өөрийн замаар солино.
Гурван газар байна: **A. Windows** (Workshop addon), **B. Game server VPS** (CS2), **C. Web VPS** (сайт, API, Discord bot).

---

## A. Windows: Workshop addon (HUD)

Нэг удаа, Git суусан байх ёстой (https://git-scm.com/download/win), CS2 → Settings → "Workshop Tools" → Install.

### A1. Анх татах (нэг л удаа)
```powershell
cd $HOME\Documents
git clone https://github.com/userneon/legacyxxx-workshop.git
```
**Үүрэг:** addon-ийн эх файлуудыг (layout, CSS, зураг) компьютерт татна.

### A2. Шинэчлэх (өөрчлөлт болгонд)
```powershell
cd $HOME\Documents\legacyxxx-workshop
git pull
```
**Үүрэг:** GitHub дээрх хамгийн сүүлийн эх файлыг татна. (Зөвхөн татна: game-д хараахан нөлөөлөхгүй.)

### A3. Build (compile)
```powershell
.\tools\build.cmd
```
**Үүрэг:** `addon\`-ийг CS2-ийн `content\csgo_addons\legacyx` руу хуулж, Valve-ийн compiler-аар `.vxml_c`, `.vcss_c`, `.vtex_c` болгоно. Эхлээд `validate.py`-аар алдааг шалгана. Workshop-д нийтлэхээс өмнө **заавал** хийнэ.

### A4. Build + өөрийн game дээр турших
```powershell
.\tools\build.cmd -Local
```
**Үүрэг:** A3-тай адил, нэмээд compile хийсэн файлуудыг `CS2\game\csgo\panorama` руу хуулна. Ингэснээр таны game **Workshop-гүйгээр** шууд харуулна (зөвхөн ТАНД). Дараа нь **CS2-ыг бүрэн хааж дахин эхлүүл** (Panorama layout-ыг cache хийдэг).

### A5. Локал туршилтын файлыг цэвэрлэх
```powershell
$g = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\panorama"
Get-ChildItem $g -Recurse -Filter "legacyx*" | Remove-Item -Force
```
**Үүрэг:** A4-ийн хуулсан `legacyx` файлуудыг устгана (Valve-ийн файлд хүрэхгүй). Үүнийг хийхгүй бол game Workshop-ийн шинэ хувилбар биш, хуучин локал файлыг харуулсаар байна. CS2 өөр дискэнд бол замыг солино.

### A6. CS2 өөр дискэнд бол
```powershell
.\tools\build.cmd -Cs2 "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive"
```
**Үүрэг:** CS2-ыг автоматаар олохгүй үед замыг гараар зааж өгнө.

### A7. Workshop-д нийтлэх (бүх тоглогчдод)
CS2 Workshop Tools нээгээд `legacyx` addon → **Publish / Update existing item** (ID `3810642940`).
**Үүрэг:** compile хийсэн хувилбарыг Steam Workshop-д тавина. Тоглогчид дараагийн орох үедээ шинэ хувилбарыг татна. Нийтлэхээс өмнө A3 заавал.

**Дараалал:** `git pull` → `build.cmd -Local` → туршилт → `A5 цэвэрлэх` → `build.cmd` (цэвэр) → Workshop-д нийтлэх.

---

## B. Game server VPS (CS2)

Бүгдийг `cd /root/legacyxxx-plugins`-аас ажиллуулна.

### B1. Plugin шинэчлэх (код өөрчлөгдсөн)
```bash
cd /root/legacyxxx-plugins
git pull
sudo ./scripts/cs2-host.sh deploy
```
**Үүрэг:** GitHub-аас шинэ plugin код татаж, build хийж, server-үүдэд суулгаад restart хийнэ. (Дараа нь `.env`-ийн холбоос хэвээр.)

### B2. Бүх зүйлийг шинэчлэх (CS2, Metamod, CounterStrikeSharp)
```bash
sudo ./scripts/cs2-host.sh update
```
**Үүрэг:** server-үүдийг зогсоож, CS2, Metamod, CounterStrikeSharp, MultiAddonManager, plugin-уудыг шинэчилнэ. Autoupdate аль хэдийн CS2 шинэчлэлийг өөрөө хийдэг.

### B3. Тохиргоо (`.env`) засах
```bash
cd /root/legacyxxx-plugins
sudo nano .env
```
Хадгал: `Ctrl+O`, Enter, `Ctrl+X`. Дараа нь restart (B4).
**Үүрэг:** plugin-ийн бүх тохиргоо (server нэр, mode, HUD асаалт, token). Энэ файл нь plugin-ийн уншдаг файлтай холбоостой, тусад нь хуулах шаардлагагүй.

### B4. Server restart
```bash
sudo systemctl restart cs2@27015
```
**Үүрэг:** server-ийг дахин асаана. `.env` өөрчилсөн, addon солисны дараа заавал. Порт бүрт тусдаа (`27016` гэх мэт). Тоглогчид тасрах тул тоглоомгүй үед хий.

### B5. HUD асаах / унтраах
```bash
sed -i 's/^LEGACYX_HUD_ENABLED=false/LEGACYX_HUD_ENABLED=true/' .env     # асаах
sed -i 's/^LEGACYX_HUD_ENABLED=true/LEGACYX_HUD_ENABLED=false/' .env     # унтраах
grep HUD_ENABLED .env
sudo systemctl restart cs2@27015
```
**Үүрэг:** `LegacyX-Hud` plugin-ийг (welcome card, rank card, knife vote) асаах эсвэл унтраана. `grep` нь одоогийн утгыг харуулна. Мөр `.env`-д байхгүй бол анхдагч нь `true`.

### B6. Workshop addon-ийг тоглогчдод татуулах
```bash
sudo ./scripts/cs2-host.sh client-addons                    # одоогийнхыг харах
sudo ./scripts/cs2-host.sh client-addons 3810642940         # тохируулах
sudo ./scripts/cs2-host.sh client-addons none               # хоослох
sudo systemctl restart cs2@27015
```
**Үүрэг:** тоглогч server-д орохдоо татах Workshop addon-ийн ID. `none` нь addon-гүй болгоно (overflow-ийг шалгахад ашиглана). Өөрчилсний дараа restart.

### B7. Server-ийн төлөв, log
```bash
sudo ./scripts/cs2-host.sh status
sudo ./scripts/cs2-host.sh logs 27015
sudo journalctl -u cs2@27015 --since "15 min ago" --no-pager | grep -i -E "overflow|reliable|disconnect|Hud" | tail -20
```
**Үүрэг:** server ажиллаж байгаа эсэх, live log, мөн алдаа хайх (overflow гэх мэт).

### B8. Шинэ server нэмэх / устгах
```bash
sudo ./scripts/cs2-host.sh add 27016 <GSLT> competitive de_mirage 10 competitive_5v5 "LEGACY-X | MATCH #2"
sudo ./scripts/cs2-host.sh remove 27016
```
**Үүрэг:** шинэ порт дээр server үүсгэх (GSLT = Steam-ийн server token) эсвэл устгах.

### B9. Discord-д update мэдэгдэл тохируулах
```bash
sudo ./scripts/announce.sh setup      # API хаяг + token (announce:write)
sudo ./scripts/announce.sh test       # туршилтын мэдэгдэл
```
**Үүрэг:** deploy болон CS2 update гарахад Discord-ын `/updates`-ээр сонгосон channel-д мэдэгдэл явуулна.

---

## C. Web VPS (сайт, API, Discord bot)

Repo бүр дотроо `bash ops/deploy.sh` гэж ажиллуулна.

| Юу | Хавтас | Команд |
|---|---|---|
| Сайт (frontend) | `~/legacyxxx-frontend` | `git pull && bash ops/deploy.sh` |
| API (backend) | `~/legacyxxx-backend` | `git pull && bash ops/deploy.sh` |
| Discord bot | `~/legacyxxx-discord-bot` | `git pull && bash ops/deploy.sh` |

**Үүрэг:** `git pull` хийж, build/шалгалт хийж, ажиллаж байгаа сервисийг шинэчилнэ. Frontend Nginx-ийн хавтас руу тавина; API, bot pm2-оор reload хийгдэнэ.

### C1. Bot-ийн log
```bash
pm2 logs legacy-x-discord --lines 60 --nostream
tail -n 200 ~/.pm2/logs/legacy-x-discord-error.log
```
**Үүрэг:** bot-ын алдаа харах. pm2 нэр нь `legacy-x-discord` (API нь `legacy-x-api`).

---

## D. Discord дээрх тохируулах командууд

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

## E. Overflow алдаа шалгах (товч)

1. `client-addons none` + restart → overflow алга бол addon шалтгаан.
2. `client-addons 3810642940` + `LEGACYX_HUD_ENABLED=false` + restart → overflow гарвал addon-ийн татагдалт, үгүй бол HUD plugin.
3. `sudo ./scripts/cs2-host.sh update` → Metamod/MultiAddonManager-ийн шинэ хувилбар.
4. Тест дууссаны дараа `LEGACYX_HUD_ENABLED=true` + addon-ийг буцааж тавь.
