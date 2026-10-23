# Шинэчлэлт, шалгалтын жагсаалт (2026-10-22 ~ 2026-10-23)

Энэ нь сүүлийн өдрүүдэд нэмсэн зүйлсийг (Genesis багц, booster frame, stats-ийн дундаж, swap vote, team queue, ready хориг, `.discord`) deploy хийх, дараа нь **сервер дээр бодитоор шалгах** жагсаалт. Тест, build нь өнгөрсөн ч тоглоомын серверийн хэсгийг (`.swap`, queue) бодит тоглолтоор туршаагүй.

## 1. Өгөгдлийн сан (Supabase), яг энэ дарааллаар
Бүгд дахин ажиллуулахад аюулгүй. `legacy_x_shop_goods.sql`, `legacy_x_lx_topups.sql`, `legacy_x_cosmetics_premium.sql` өмнө нь ажилласан байх ёстой.

1. `legacy_x_cosmetics_emotes.sql`
2. `legacy_x_founder.sql` (Genesis багц, banner, title, `founder_claim`)
3. `legacy_x_genesis_rename.sql` (хэрэв 2-р файл нэр солигдохоос өмнө ажилласан бол; шинэ DB дээр юу ч хийхгүй)
4. `legacy_x_booster_podium.sql`
5. `legacy_x_cosmetics_retire_duplicates.sql` (4 давхардсан өнгийг идэвхгүй болгож LX буцаана, **нэг удаа** ажиллуулна, үнэ 500 хүртэл өссөнөөс өмнө авсан хүнд хэт буцаалт өгч магадгүй)

Шалгах: Genesis бүтээгдэхүүн байна: `select slug, kind, price_mnt, stock, ends_at from legacy_x.shop_products where kind='founder'` → `founder-pack | founder | 30000 | 500 | 2026-11-30 15:59:59+00`.

## 2. Deploy
- **Backend** (legacyxxx-backend) → дараа нь **frontend** (legacyxxx-frontend). Backend-ийг эхэлж: frontend шинэ талбар (`banners`, `titles`, `boosterPlace`…) хүлээнэ.
- **Game server:** `legacyxxx-plugins` (LegacyX-MatchZy, LegacyX-Community). `LEGACYX_COMMUNITY_DISCORD_URL` хүсвэл өөрийн урилгаа тавина.
- `nginx`: `client_max_body_size 11m;` (клан зургийн хязгаар, өмнөх шийдвэр).

## 3. Сайт дээр шалгах
| Шалгах | Хүлээх үр дүн |
|---|---|
| Shop → Featured | Genesis карт: 30,000₮, "463 of 500 left" шиг бодит тоо, "Order the Genesis pack". |
| Захиалах (өөрийн туршилтын акаунтаар) | "We got your order". Дахин захиалахад 409 "You already ordered…". |
| Staff → Shop → захиалгыг "paid" болгох | Тоглогчид **Genesis #0001**, Genesis frame, Gold Foil banner, Genesis цол ирнэ; мэдэгдэл "You are Genesis #0001". Дахин "paid" болговол өөрчлөлтгүй. |
| Профайл → Appearance → Banner/Title/Frames | Genesis зүйлс "Worn/Owned" болж өмсөгдөнө. |
| Staff → Shop → Genesis → "Sales end" | Огноо хадгалагдана; хоосон бол хугацаагүй. |
| Профайлын stats | Тоо бүрт "Avg …" ба "Top N%" (10-аас цөөн ranked тоглогч байвал харагдахгүй), дундажаас дээш ногоон, доош улаан. Recent matches дахь K/D адил. |
| Leaders → Boosters (Discord холбосон booster-аар) | Топ 3 картад Gold/Silver/Bronze frame; бусад boost хийж буй гишүүнд хуучин ягаан frame. Boost-оо зогсоосон хүнд frame алга. |
| Профайл дээр Discord тэмдэг дээр хулгана | "Discord linked" tooltip. |

## 4. Тоглоомын сервер дээр шалгах (хамгийн чухал, туршаагүй)
Бүгдийг 2+ хүнтэй, жинхэнэ match эсвэл warmup дээр.

1. **`.discord`**: тоглогчид холбоос chat-д гарна.
2. **Ready хориг (warmup):** `.r` бичээгүй хүн team сонгоход "Type .r first…"; `.r` бичсэний дараа сонгож чадна; 20 сек тутам зөвхөн өөрт нь сануулга.
3. **Match эхэлсний дараа:** орж ирсэн хүн 1.5 сек дараа spectator; T/CT дарахад дараалалд орно ("place N"); суудал чөлөөлөгдөхөд (тоглогч гарахад) дараалалд эхний хүн тэр team-д орж, chat-д зарлагдана; **Change team** товчоор T↔CT шилжих боломжгүй.
4. **`.swap` vote:** хоёр team-ийн олонх зөвшөөрөхөд side солигдоно; **round дундуур** амьд тоглогчийн буу, мөнгө, хуяг хэвээр үлдэж байгаа эсэх; 10 минутын cooldown; vote 30 сек дуусахад хүчингүй. **Эрсдэл:** `mp_swapteams` round дундуур яг хэзээ хэрэгжиж, тоглогчийг амьд үлдээдэг эсэх батлагдаагүй. Хэрэв эвдэрч байвал vote-ийг зөвхөн freeze time/warmup дээр зөвшөөрөх болгоно.

## 5. Буцаах (rollback)
- Frontend/backend: өмнөх commit руу буцаана; шинэ SQL-үүд нь зөвхөн нэмэлт (шинэ мөр, шинэ төрөл), хуучин ажиллагааг эвдэхгүй. Эсэргээр: `legacy_x_cosmetics_retire_duplicates.sql` LX буцаасан (буцаах боломжгүй).
- Genesis-ийг түр хаах: Staff → Shop → "Show in the Shop" унтраана (захиалгууд хэвээр, шинэ захиалга авахгүй).

## 6. Мэдэх эрсдэл
- Genesis-ийн "paid" гараар: буруу захиалгыг paid болговол буцаах боломжгүй (дугаар, frame олгогдсон). Төлбөрийг шилжүүлгийн утгаар нягтал.
- "Founder" нь ажилтны role тул нэр Genesis болсон; дотоод ID-ууд `founder` хэвээр.
- Зурсан frame-үүдийн эрх таны хариуцлага.
- Commit subject-ууд deploy хийхэд Discord-д автоматаар зарлагддаг (`[skip announce]` нэмбэл зарлахгүй).
