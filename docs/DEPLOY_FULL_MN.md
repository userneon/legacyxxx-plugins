# Бүрэн deploy-ын зам: эхнээс нь дарааллаар (2026-10-23)

Энэ нь `DEPLOY_CHECK_2026-10-23_MN.md`-г орлоно: тэр файл Genesis, booster, emote, match-ийн шинэчлэлтийг хамарсан. Энд тэрнээс хойш нэмсэн бүх зүйл (admin сайт, Settings, чат, creator хүсэлт, серверийн карт ба тоглогчийн удирдлага, клан лидер, SteamID-гүй сонголт) нэг дор, **яг хийх дарааллаар** байна. Тус бүр дээр "шалгах" мөр бий: дараагийн алхам руу шалгалтгүй битгий орно.

Дараалал өөрчлөхгүй: **өгөгдлийн сан → backend → admin сайт → website → тоглоомын сервер.** Яагаад гэвэл шинэ frontend шинэ талбар, шинэ endpoint хүлээнэ. Backend-гүйгээр website гарвал чат, staff цэс, creator хүсэлт алдаа өгнө.

## 0. Эхлэхээс өмнө
1. Supabase → Database → Backups: **backup авсан эсэхээ** шалга. Боломжтой бол нэг удаа scratch project руу restore хийж үз.
2. 4 repo-н `main` дээр бүх өөрчлөлт push хийгдсэн: `legacyxxx-backend`, `legacyxxx-frontend`, `legacyxxx-plugins`. (`legacyxxx-discord-bot`-д энэ удаа өөрчлөлт **байхгүй**, deploy хийхгүй.)
3. VPS-д non-root deploy хэрэглэгчээр нэвтэрсэн, `git`, `node` (20.19+ эсвэл 22.12+), `pm2`, `nginx` ажиллаж байна.

## 1. DNS ба сертификат (admin сайт)
1. DNS дээр `admin.legacyx.cc`-ийг `legacyx.cc`-тэй **ижил серверийн IP** руу A бичлэгээр заана.
2. Сертификат: `sudo certbot --nginx -d admin.legacyx.cc`
3. Шалгах: `dig +short admin.legacyx.cc` нь серверийн IP-г өгнө.

## 2. Өгөгдлийн сан (Supabase → SQL editor), яг энэ дарааллаар
Бүгд дахин ажиллуулахад аюулгүй. Өмнө нь ажилласан гэдэгт итгэлгүй бол дахин ажиллуулахад болно (**нэг л үл хамаарах зүйл:** 4-р файл, доор тайлбартай).

| # | Файл | Юу хийдэг |
|---|---|---|
| 1 | `legacy_x_cosmetics_emotes.sql` | emote төрөл, 8 emote, `game_key` |
| 2 | `legacy_x_founder.sql` | Genesis багц, banner, title, `founder_claim` |
| 3 | `legacy_x_genesis_rename.sql` | Founder → Genesis нэр (2-р файл нэр солигдохоос өмнө ажилласан бол; шинэ DB дээр юу ч хийхгүй) |
| 4 | `legacy_x_booster_podium.sql` | Booster Gold/Silver/Bronze frame |
| 5 | `legacy_x_cosmetics_retire_duplicates.sql` | 4 давхардсан өнгийг унтраана, LX буцаана. **Нэг л удаа** ажиллуул (хоёр дахь удаа дахин буцаалт өгөхгүй ч үнэ өөрчлөгдсөн бол буцаалтын дүн өөр) |
| 6 | `legacy_x_creator_applications.sql` | creator хүсэлтийн хүснэгт |
| 7 | `legacy_x_site_settings.sql` | Staff → Settings-ийн хүснэгт |
| 8 | `legacy_x_public_chat.sql` | нийтийн чатын хүснэгт |

Эдгээрийг ажиллуулахаас өмнө `legacy_x_shop_goods.sql`, `legacy_x_lx_topups.sql`, `legacy_x_cosmetics_premium.sql` өмнө нь ажилласан байх ёстой (production-д ажилласан).

Шалгах (SQL editor дээр):
```sql
select to_regclass('legacy_x.creator_applications'), to_regclass('legacy_x.site_settings'), to_regclass('legacy_x.chat_messages');
select slug, kind, price_mnt, stock, ends_at from legacy_x.shop_products where kind='founder';
```
Эхний мөр 3 хүснэгтийн нэрийг (null биш), хоёр дахь мөр `founder-pack | founder | 30000 | 500 | 2026-11-30 …` өгнө.

## 3. Backend (api.legacyx.cc)
1. Серверийн `.env` дээр нэмнэ (`legacyxxx-backend/.env`):
   - `ADMIN_ORIGIN=https://admin.legacyx.cc`
   - Хэрэв серверийн удирдлага (Staff → Servers) идэвхгүй байсан бол: `STAFF_PANEL_ENABLED=true`
   - Одоо байгаа `FRONTEND_ORIGIN=https://legacyx.cc`, `POST_LOGIN_REDIRECT`, `AUTH_COOKIE_DOMAIN=.legacyx.cc` хэвээр. **Түлхүүр, secret-ийг repo-д бүү оруул.**
2. Deploy: `cd legacyxxx-backend && bash ops/deploy.sh` (pull, install, typecheck, build, pm2 reload, `/health` шалгалт).
3. Шалгах:
   - `curl -s https://api.legacyx.cc/health` → `{"ok":true,...}`
   - `curl -s https://api.legacyx.cc/api/v1/public/chat` → `{"messages":[]}` (SQL 8 ажилласан бол алдаа гарахгүй)
   - `pm2 logs legacy-x-api --lines 50` дээр "site settings not read" гэсэн анхааруулга **байхгүй** (байвал SQL 7 ажиллаагүй).

## 4. Admin сайтын nginx
1. Файлуудыг хуулна (website-ийн snippet-ийн хажууд):
   ```bash
   cd legacyxxx-frontend
   sudo cp ops/nginx/legacyx-admin.conf /etc/nginx/snippets/
   ```
2. `/etc/nginx/sites-available/` дотор шинэ `server` блок (certbot `listen 443 ssl` мөрүүдийг нэмнэ):
   ```nginx
   server {
       server_name admin.legacyx.cc;
       root /var/www/legacyx-admin;
       include /etc/nginx/snippets/legacyx-admin.conf;
   }
   ```
   Дараа нь `sudo ln -s` (хэрэв шаардлагатай) → `sudo nginx -t && sudo systemctl reload nginx`.
3. Website-ийн nginx-д `client_max_body_size 11m;` (клан зургийн хязгаар) байгаа эсэхийг шалга.

## 5. Admin сайт (admin.legacyx.cc) **website-ээс өмнө**
```bash
cd legacyxxx-frontend
bash ops/deploy-admin.sh
```
Шалгах:
- `https://admin.legacyx.cc` нээгдэнэ, "Sign in with Steam to open Staff".
- Steam-ээр нэвтэрвэл **буцаад admin.legacyx.cc** руу ирнэ (legacyx.cc руу биш), Staff хуудас харагдана. Хэрэв legacyx.cc руу буцвал backend-д `ADMIN_ORIGIN` ороогүй/pm2 `--update-env` хийгдээгүй.
- Role-ийн дагуу tab: Admin → Overview, Checks, Penalties, Players; Manager нэмж Clans, Creators, Shop, Servers; Owner нэмж Team, Settings.

## 6. Website (legacyx.cc)
```bash
cd legacyxxx-frontend
bash ops/deploy.sh
```
(Мөн Genesis, booster, emote зэрэг өмнөх frontend өөрчлөлтүүдийг хамт авна.)
Шалгах:
- `legacyx.cc/staff` → `admin.legacyx.cc/staff` руу шилжинэ. Sidebar-ын "Staff" нь admin сайтын линк.
- Баруун доод буланд чатын дугуй товч харагдана.

## 7. Тоглоомын сервер (plugins)
`DEPLOY_CHECK_2026-10-23_MN.md`-ийн 2 ба 4-р хэсгийг дага:
```bash
cd legacyxxx-plugins
./scripts/deploy.sh <csgo-dir> [<csgo-dir> …]
```
- Шинэ **LegacyX-Emotes** plugin-ы `.env`: `LEGACYX_EMOTES_ENABLED=true`. `LEGACYX_COMMUNITY_DISCORD_URL`-д өөрийн Discord урилга.
- Сервер **дахин асаахгүй** (script restart хийдэггүй): тоглогч байхгүй үед өөрөө restart хий.
- Дараа нь **бодит тоглолт дээр** `.discord`, ready хориг, team queue, `.swap`, `.e`-г шалга. Round дундуурх `.swap` нь эрсдэлтэй гэдгийг тэр файл заасан.

## 8. Бүх зүйл ажиллаж байгааг шалгах (өөрийн Owner акаунтаар)
| Шалгах | Хүлээх үр дүн |
|---|---|
| Чат: нээж "hello" бичих | Мессеж гарна; дахин шууд бичвэл "Wait N seconds"; холбоос бичвэл "Links are not allowed" |
| Чат: staff-ын хогийн сав | Мессеж устна |
| Creators хуудас → "Become a creator" (өөр акаунтаар) | Хүсэлт илгээгдэнэ → Staff → Creators → Applications дээр гарна → Approve хийвэл creator болж мэдэгдэл очно |
| Staff → Overview | "Needs you", Servers мөр, tab дээрх тоонууд бодит |
| Staff → Servers → сервер сонгох | "Live on this server" дотор тоглогчийн картууд (тоглогчтой сервер дээр); товч дээр tooltip |
| Staff → Settings | 7+ тохиргоо; нэгийг өөрчилж Save → "Changed" шошго → Reset |
| Staff → Shop → Cosmetics | Нэг cosmetic-ийн үнийг засаад Shop хуудсанд шинэчлэгдэнэ |
| Staff → Clans → Moderate → Leader | Тоглогч сонгож "Make leader" (туршилтын клан дээр) |
| Шинэ шийтгэл / Check хүсэх / Staff нэмэх | SteamID бичихгүй, тоглогчийн жагсаалтаас сонгоно |
| Genesis, booster, профайлын stats | `DEPLOY_CHECK_2026-10-23_MN.md`-ийн 3-р хэсэг |

## 9. Алдаа гарвал
- **Admin сайтад нэвтэрсний дараа "signed out"**: `AUTH_COOKIE_DOMAIN=.legacyx.cc` ба `ADMIN_ORIGIN` зөв эсэх, backend `pm2 reload … --update-env` хийсэн эсэх. Хөтчийн cookie нь `.legacyx.cc` домайнтай эсэх.
- **Чат "Could not load"**: SQL 8 ажиллаагүй, эсвэл backend deploy хийгдээгүй.
- **Settings "Only the Owner"**: Owner role-той нэвтэрсэн эсэхээ шалга.
- **Буцаах:** frontend, backend бүр `git revert` + `bash ops/deploy.sh`. SQL-ийн шинэ хүснэгтүүд (6, 7, 8) нэмэлт тул буцаахад саад болохгүй. 5-р файлын LX буцаалт буцахгүй.

## 10. Дараа нь санах зүйл
- "paid" товч гараар (буруу дарсныг буцаахгүй). Genesis-ийн мөнгөн дүнг шалгаад дарна.
- Тоглоомын серверийн шинэ функцүүдийг (swap, queue, emote) жинхэнэ тоглолтоор туршаагүй: эхний өдөр хянаж бай.
- Анхдагч тохиргоог Staff → Settings дээр өөрчилж болно (code өөрчлөх хэрэггүй).
