> **Хуулбар.** Эх сурвалж: [legacyxxx-workshop/docs/MANUAL_MN.md](https://github.com/userneon/legacyxxx-workshop/blob/main/docs/MANUAL_MN.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# legacyxxx-workshop: гарын авлага

Тоглоом доторх LEGACY-X **HUD**: CS2 Workshop addon (Panorama layout, CSS, зураг). Тоглогчийн CS2 үүнийг Steam Workshop-оос татна;
юуг хэзээ, хэнд харуулахыг server талын `LegacyX-Hud` plugin шийднэ.

| Хэсэг | Агуулга |
|---|---|
| [1. Энэ repo юу вэ](#1-энэ-repo-юу-вэ) | HUD-ийн дэлгэцүүд, ажиллах зарчим |
| [2. Анх бэлдэх](#2-анх-бэлдэх) | Windows, Git, Workshop Tools |
| [3. Өөрчлөлт бүрийн цикл](#3-өөрчлөлт-бүрийн-цикл) | pull → build → туршилт → нийтлэл |
| [4. Бүтэц](#4-бүтэц) | Хавтас, tool-ууд |
| [5. Addon ↔ plugin гэрээ](#5-addon--plugin-гэрээ) | Id, текст, class |
| [6. Дизайны дүрэм](#6-дизайны-дүрэм) | Тоглоомд нөлөөлдөг хязгаарлалт |
| [7. Шинэ дэлгэц, зураг нэмэх](#7-шинэ-дэлгэц-зураг-нэмэх) | Алхамууд |
| [8. Server талд](#8-server-талд) | MultiAddonManager, plugin |
| [9. Алдаа засах](#9-алдаа-засах) | Overflow, харагдахгүй байх |
| [10. Бусад баримт](#10-бусад-баримт) | [README.md](README.md) |

---

## 1. Энэ repo юу вэ

| Layout | Тоглогч юу харах |
|---|---|
| `legacyx_notify` | **Тавтай морил карт** (дэлгэцийн голд 7 секунд: server, нэр, цол), дээрээс буудаг зарлал, нэг мөр toast, round эхлэхэд цолны card, цол ахих/буурах |
| `legacyx_match` | Ranked match бүрийн EXP дүн (голд том card, дараа нь баруун талд жижиг) |
| `legacyx_knife` | Хутганы тойргийн талын санал (Stay / Switch, нууц санал, **10 секунд**), A/D ба E товчоор |
| `legacyx_admin` | `!admin` цэс: тоглогч, үйлдэл, map, ban, staff |

- Addon нь **зөвхөн зурна**. Server дээрх `LegacyX-Hud` plugin `custom_hud_layout` entity үүсгээд тоглогч бүрт **текст бөглөж, class асаана**.
  Plugin зураг, өнгө, байрлал илгээж чадахгүй, тиймээс бүх төлөв addon-д урьдчилан зурагдсан, plugin зөвхөн class сонгоно.
- Дизайн нь legacyx.cc-тэй ижил: хар шил, цагаан текст, crimson зөвхөн `-X` болон "энэ" зураас.
- Тоглогч addon-ийг `mm_client_extra_addons <Workshop ID>`-аар (MultiAddonManager) орохдоо татна. Одоогийн Workshop ID: `3810642940`.

## 2. Анх бэлдэх

**Windows компьютер дээр** (Workshop Tools Windows-д л байдаг):

1. **Git** суулгана: https://git-scm.com/download/win
2. CS2 → Settings → **Workshop Tools** гэж хайгаад **Install**. Тоглоомыг хааж Steam татаж дуустал хүлээнэ. `Counter-Strike Global Offensive`
   хавтсанд `game`-ийн хажууд `content` хавтас гарвал бэлэн.
3. (Сонголт) Python 3: `tools/validate.py`-г build скрипт ажиллуулахад ашиглана. Байхгүй бол алгасана (CI шалгана).
4. Repo-г татна:
   ```powershell
   cd $HOME\Documents
   git clone https://github.com/userneon/legacyxxx-workshop.git
   ```

## 3. Өөрчлөлт бүрийн цикл

```powershell
cd $HOME\Documents\legacyxxx-workshop
git pull                            # 1. шинэ эх файл татах
.\tools\build.cmd -Local            # 2. шалгах, compile, өөрийн CS2 дээр харуулах
```

3. **CS2-ыг бүрэн хааж дахин нээ** (Panorama layout-ыг session турш cache хийдэг).
4. Server рүү ор; харагдац, байрлал, емблем зөв эсэхийг шалга (welcome card орсноос 5 секундын дараа гарна).
5. Сайн бол **локал туршилтын файлыг цэвэрлэ** (эс бөгөөс чи Workshop-ийн биш хуучин локал файлыг харна):
   ```powershell
   $g = "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\panorama"
   Get-ChildItem $g -Recurse -Filter "legacyx*" | Remove-Item -Force
   ```
6. Цэвэр build хийж **Workshop-д нийтэл**:
   ```powershell
   .\tools\build.cmd
   ```
   Дараа нь CS2 Workshop Tools → `legacyx` addon → **Publish / Update existing item** (ID `3810642940`). Нийтлэхээс өмнө `build.cmd` **заавал**.

`build.cmd` юу хийдэг:

| Алхам | Тайлбар |
|---|---|
| CS2-ыг олно | Steam library-д хайна; олдохгүй бол `-Cs2 "<зам>"` |
| `validate.py` | Layout/CSS-ийн чимээгүй алдааг барина |
| Хуулна | `addon\` → `CS2\content\csgo_addons\legacyx` |
| Compile | `resourcecompiler.exe`-ээр `.vtex_c`, `.vsvg_c`, `.vcss_c`, `.vxml_c` → `CS2\game\csgo_addons\legacyx` |
| `-Local` үед | Compile хийсэн panorama файлыг `CS2\game\csgo\panorama` руу хуулна |

`Not compiled:` гарвал жагсаалтыг (мөн CS2 console `~` дахь улаан мөрүүдийг) хуулж илгээ. Дэлгэрэнгүй: `tools/build.ps1`.

> **Хэсэг нэмэлт:** Workshop-ийн нийтлэлт нь **git-ээс биш**, `CS2\game\csgo_addons\legacyx` (compile хийсэн)-ээс явна. `git pull` хийснээр Workshop шинэчлэгдэхгүй.

## 4. Бүтэц

```text
addon/                                  → content\csgo_addons\legacyx
  addoninfo.txt                          IsPlayable 0
  panorama/layout/custom_game/*.xml      4 layout
  panorama/styles/custom_game/*.css      legacyx.css (нийтлэг), legacyx_assets.css (үүсгэгдсэн), layout бүрт нэг
  panorama/images/custom_game/legacyx/   PNG + .vtex (≈126)
  panorama/images/icons/skillgroups/     18 цолны SVG (Tab scoreboard)
tools/
  validate.py                            layout/CSS шалгагч (CI-д push бүрт)
  make_vtex.py                           PNG бүрийн .vtex үүсгэнэ
  make_assets_css.py                     rank-*, tier-*, ic-*, map-*, p0..p100 class-уудыг үүсгэнэ
  build.ps1 / build.cmd                  Windows: хуулах + compile
CONTRACT.md                              Бүх id, текст, class (plugin-ы гэрээ)
.github/workflows/validate.yml           CI
```

Зургийн эх: legacyxxx-frontend (цолны emblem, талын emblem, logo, map), lucide icon; PNG болгож экспортлосон.

## 5. Addon ↔ plugin гэрээ

Plugin addon-той **гурван зүйлээр** ярина:

| Plugin | Яаж | Жишээ |
|---|---|---|
| Текст тавих | Dialog variable, Label-ийн id-тай ижил нэртэй (`{s:<id>}`) | `wc_name` = `Temuulen` |
| Class асаах/унтраах | Panel-ийн id-аар | `ann` + `open`, `rank_fill` + `p40` |
| Товч дарахыг хүлээх | Button-ы id server-т ирнэ | `knife_switch`, `act_ban` |

Plugin **үүсгэж чадахгүй**: panel, зураг, өнгө, хэмжээ, байрлал. Layout бүр нэг нэргүй root panel + `lx_<нэр>` wrapper-тэй.
Нийтлэг class: `open` (харагдана), `shown` (цонх нээлттэй + хулгана барина), `hidden`, `sel`, `mine`, `active`, `disabled`, `p0…p100`, `rank-1…18`, `tier-*`, `side-t/ct`, `ic-*`.

**Бүх id, текст, class-ийн жагсаалт: [../CONTRACT.md](../CONTRACT.md).** Id нэмэх/өөрчлөх бол CONTRACT.md болон plugin (`LegacyX-Hud`)-г хамт өөрчилнө.

Welcome card (`legacyx_notify`): `wc`, `wc_server`, `wc_name`, `wc_emblem` (`rank-N`, `hidden` rank-гүй үед), `wc_rankrow`, `wc_rank` (+ `tier-*`), `wc_exp`.

## 6. Дизайны дүрэм

Эдгээр нь тоглоомд **чимээгүй** эвдэрдэг (layout зүгээр л харагдахгүй). `tools/validate.py` боломжтой хэсгийг шалгана.

- Зөвхөн `Panel`, `Label`, `Image`, `Button`. Атрибут: `id`, `class`, `hittest`, `text`, `src`. Inline `style`, текст оруулах талбар, script **байхгүй**.
- Root panel-д `id` **бүү** өг. Stylesheet-ийг `s2r://panorama/styles/custom_game/<нэр>.vcss_c`-ээр оруул.
- Зураг нь panel-ийн background (`.vtex`, **BGRA8888**), `<Image>` + стандарт icon биш. DXT5 нь Panorama-д ягаан-цэнхэр харагдана.
- `@keyframes` `transform` дээр **ажиллахгүй**: хөдөлгөөн нь class-аар эхэлдэг transition (`open`, `shown`). Өргөн/өндөр/харагдах байдал animate хийгдэхгүй.
- Таслалтай selector байхгүй; `@keyframes` нэр хашилттай; `margin`/`padding` 4 утгатай.
- Layout өөрчлөгдсөн бол **addon-ийг дахин нийтлэхгүй бол тоглогчид хүрэхгүй**. Plugin-ы deploy үүнд нөлөөлөхгүй.
- Хэмжээ чухал: файл олон болох тусам тоглогчийн холболтын үед татах жагсаалт томорно (§9 overflow).

## 7. Шинэ дэлгэц, зураг нэмэх

**Зураг:** PNG-г `addon/panorama/images/custom_game/legacyx/`-д тавь → `python3 tools/make_vtex.py` → (rank/ic/map class хэрэгтэй бол) `python3 tools/make_assets_css.py` → build.
**Текст/панел:** layout XML-д Label (`text="{s:<id>}"`) эсвэл Panel нэм → CSS → `CONTRACT.md`-д id-г нэм → plugin-д тавих код (`LegacyX-Hud`).
**Шинэ layout:** `legacyx_<нэр>.xml` + `legacyx_<нэр>.css` → `tools/build.ps1`-ийн `$groups` автоматаар олно → plugin-д `LEGACYX_HUD_<..>_LAYOUT` тохиргоо.
Бүгдийн дараа: `python3 tools/validate.py` → `build.cmd -Local` → game дээр шалга.

## 8. Server талд

- **Addon татуулах:** `sudo ./scripts/cs2-host.sh client-addons 3810642940` + `sudo systemctl restart cs2@<port>` (legacyxxx-plugins).
  Хоослох/шалгах: `client-addons none`, `client-addons` (юу ч гүй бол одоогийн утга).
- **Plugin:** `LegacyX-Hud` (CounterStrikeSharp 1.0.374+, `custom_hud_layout` API, PanoramaManager). `.env`: `LEGACYX_HUD_ENABLED=true`, `LEGACYX_HUD_RANK_CARD=true`, `LEGACYX_<port>_SERVER_NAME`.
  Layout path-ыг plugin default-оор `…/legacyx_notify.vxml_c` (compile хийсэн нэр) гэж ашигладаг; буруу гарвал `LEGACYX_HUD_NOTIFY_LAYOUT`-д `.xml` эх замыг өг.
- **Хэзээ харагдах:** Тоглогч addon-ийг татаж дуусаагүй байвал HUD харагдахгүй; map нэг удаа солигдсоны дараа тогтдог.

## 9. Алдаа засах

| Шинж тэмдэг | Шалтгаан | Засах |
|---|---|---|
| Server рүү орохдоо **overflow** алдаа | Addon-ийн файл/хэмжээ их, MultiAddonManager хуучин | `client-addons none`-оор шалга; `cs2-host.sh update`; addon-ийг багасга (ашиглаагүй зураг/layout хас) |
| HUD огт харагдахгүй | Addon татагдаагүй, plugin унтраалттай, layout path буруу | `client-addons`, `LEGACYX_HUD_ENABLED`, server log `[LEGACY-X Hud]` мөр |
| Шинэ дизайн харагдахгүй | Дахин нийтлээгүй, эсвэл хуучин локал файл | Нийтэл; §3-ын 5-р алхам (локал файл устгах); CS2 дахин эхлүүл |
| Зарим панел харагдахгүй | Plugin шинэ id-г мэдэхгүй (addon шинэ, plugin хуучин эсвэл эсрэгээр) | Хоёуланг нь deploy/нийтлэх; `CONTRACT.md` харьцуул |
| Ягаан-цэнхэр зураг | DXT5 хэлбэр | `.vtex` BGRA8888 (`make_vtex.py` зөв хийдэг) |
| `build.cmd` "Workshop Tools are not installed" | Tools суугаагүй | §2 |
| `Not compiled:` жагсаалт | Файлын алдаа | Жагсаалтаас файлыг гараар compile хийж мессежийг үз; `validate.py` |
| `git pull` хийсэн ч Workshop шинэчлэгдээгүй | Нийтлээгүй | §3-ын 6-р алхам |

## 10. Бусад баримт

Индекс: **[README.md](README.md)**. Plugin тал, server, Discord: `legacyxxx-plugins` → `docs/MANUAL_MN.md`.
