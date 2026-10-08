# LEGACY-X Community Plugin

`LegacyXCommunity` нь тусдаа EXP/rank calculation хийхгүй. MatchZy-ийн `competitive_result`-ийг backend (Rank system v1) боловсруулж EXP болон rank-ийг хадгална. Энэ plugin нь тоглогчид server дотор өөрийн rank-ийг харах command өгнө.

| Command | Behavior |
|---|---|
| `css_rank`, `css_xp`, `css_level`, `css_progress` | Rank, EXP, leaderboard байр, дараагийн rank хүртэлх EXP болон Pro League төлөв харуулна. |

## Tab scoreboard rank

Тоглогч серверт ороход plugin API-аас түүний цолыг уншаад нэрийн өмнө clan tag болгон тавина: `[OPERATOR I]`. Энэ нь Tab, чат болон killfeed дээр харагдана, 2 секунд тутамд дахин тавигдана, match дуусахад цол дахин уншигдана. Тоглогч сайтын clan-д байвал clan tag нь цолын өмнө орно: `[WOLF | OPERATOR I]` (clan tag сайтаас, 8 тэмдэгтээс хэтрэхгүй; `LEGACYX_COMMUNITY_CLAN_TAG=false` унтраана). Clan tag-ийг сайт дээр өөрчлөхөд тоглогч дахин орох эсвэл match дуусахад шинэчлэгдэнэ. Сайтад бүртгэлгүй тоглогчид шошго тавихгүй. Staff (OWNER, MANAGER, ADMIN, STAFF) цолын оронд role-оо харуулна, clan-д байвал `[WOLF | OWNER]`: тэр шошгыг LegacyX-Admin тавьдаг (clan tag-ийг API-ийн authorization хариунаас авна), энэ plugin staff-ыг алгасна. MatchZy-ийн `[TEAM COACH]` шошгыг дарахгүй.

CS2 нь community серверт Rank баганын icon (`m_iCompetitiveRankType` 12) болон Premier тоог (11) тест дээр харуулаагүй тул тэдгээр нь `lx_scoreboard_type 12` / `11`-ээр туршихад л үлдсэн. `LEGACYX_COMMUNITY_SCOREBOARD_RANK_TYPE` анхны горимыг, `LEGACYX_COMMUNITY_SCOREBOARD_RANKS=false` бүгдийг унтраана.

## Welcome

Тоглогч серверт орсноос 4 секундын дараа зөвхөн тухайн тоглогчийн chat-д (бусдад харагдахгүй) гарна:

```text
LEGACY-X • Welcome to LEGACY-X, Temuujin.
LEGACY-X • Operator I · 1,240 EXP. Type !rank for more.
LEGACY-X • Your skins come from legacyx.cc. Type !rs to load them.
```

Цол, EXP нь API-аас (цолгүй бол "No rank yet…", API хүрэхгүй бол тэр мөр гарахгүй). Map солигдоход дахин
гарахгүй (нэг тоглогчид 3 цагт нэг удаа). `LEGACYX_COMMUNITY_WELCOME=false` унтраана.

## Security

Plugin нь `/api/v1/plugin/community/players/:steamId` endpoint рүү л явна. API URL, plugin identity болон scoped `x-plugin-secret` нь бүх LEGACY-X plugin-тэй адил future CS2 host-ийн **нэг** `CounterStrikeSharp/.env`-ээс уншигдана. `LegacyXCommunity.json.example` нь одоо зөвхөн secret-free chat presentation default агуулна. Staff/API secret, Supabase key, database credential-ийг plugin JSON/cfg болон Git-д хэзээ ч байрлуулахгүй.

## Build and deploy

```bash
cd LegacyX-Community
dotnet build -c Release
```

Одоогоор server байхгүй тул энэ repository нь source/build preparation л агуулна. Future host батлагдсаны дараа central `.env`, secret-free config default болон release artifact-ийг reviewed deployment procedure-ээр байрлуулна.
