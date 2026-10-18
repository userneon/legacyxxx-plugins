# LEGACY-X Community Plugin

`LegacyXCommunity` нь тусдаа EXP/rank calculation хийхгүй. MatchZy-ийн `competitive_result`-ийг backend (Rank system v1) боловсруулж EXP болон rank-ийг хадгална. Энэ plugin нь тоглогчид server дотор өөрийн rank-ийг харах command өгнө.

| Command | Behavior |
|---|---|
| `css_rank`, `css_xp`, `css_level`, `css_progress` | Rank, EXP, leaderboard байр, дараагийн rank хүртэлх EXP болон Pro League төлөв харуулна. |

## Tab scoreboard rank

Тоглогч серверт ороход plugin API-аас түүний цолыг уншаад нэрийн өмнө clan tag болгон тавина: `[OPERATOR I]`. Энэ нь Tab, чат болон killfeed дээр харагдана, 2 секунд тутамд дахин тавигдана, match дуусахад цол дахин уншигдана. Тоглогч сайтын clan-д байвал clan tag нь цолын өмнө орно: `[WOLF | OPERATOR I]` (clan tag сайтаас, 8 тэмдэгтээс хэтрэхгүй; `LEGACYX_COMMUNITY_CLAN_TAG=false` унтраана). Clan tag-ийг сайт дээр өөрчлөхөд тоглогч дахин орох эсвэл match дуусахад шинэчлэгдэнэ. Сайтад бүртгэлгүй тоглогчид шошго тавихгүй. Staff (OWNER, MANAGER, ADMIN, STAFF) цолын оронд role-оо харуулна, clan-д байвал `[WOLF | OWNER]`: тэр шошгыг LegacyX-Admin тавьдаг (clan tag-ийг API-ийн authorization хариунаас авна), энэ plugin staff-ыг алгасна. MatchZy-ийн `[TEAM COACH]` шошгыг дарахгүй.

Rank баганад **манай өөрийн rank зураг** гарна. Арга (ABKAM2023/CS2-LR-FakeRank-ийн "custom ranks"-тай ижил): `m_iCompetitiveRankType` = **12**, `m_iCompetitiveRanking` = **18 + rank (19–36)**. Workshop addon-ий `skillgroup19.svg … skillgroup36.svg` нь тэр дугаарын зураг. CS2 өөрөө 19-өөс дээш дугаартай зураггүй тул өөрийн (CS:GO) зургаа буцааж дарж чадахгүй. Өмнө нь ranking 1–18 тавьдаг байсан, тэр үед CS2 өөрийн CS:GO зургаа гаргадаг байсан.

Хэрэглэх: addon-ийг республиш хийх (`tools\build.cmd`), server дээр `deploy`. `lx_scoreboard_type 12` (default), `11` (Premier тоо), `0` (зөвхөн clan tag). `LEGACYX_COMMUNITY_SCOREBOARD_ICON_OFFSET` (default 18) нь манай зургийн эхний дугаарыг 19 болгодог. `LEGACYX_COMMUNITY_SCOREBOARD_RANKS=false` бүгдийг унтраана.

**Анхааруулга:** CS2-LR-FakeRank-ийн README "энэ plugin GSLT token-ы ban-д хүргэж болзошгүй" гэж бичсэн. Rank талбарыг server-ээс хуурамчаар бичих нь Valve-ийн заавартай зөрчилдөх магадлалтай. Бидний plugin энэ талбарыг өмнө нь бичиж байсан.

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

**Revenge.** Kill the player who killed you last (on this map) and you both get a line in your own chat: "Revenge! You got X back." / "X got revenge on you." Nobody else sees it; bots, suicides and team kills do not count, and a revenge clears itself so the next kill is not one. `LEGACYX_COMMUNITY_REVENGE=false` switches it off.
