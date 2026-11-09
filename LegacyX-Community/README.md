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

**Third person (`.tp`, Fun серверт л).** `.tp` (эсвэл `.thirdperson`) гэж бичихэд камер тоглогчийн ард гарч, дахин бичихэд хэвийн болно. Cheats асаахгүй: `point_viewcontrol` камерыг тоглогчид өгч, tick бүрт ард нь зөөнө. Үхэх, round солигдох, map солигдоход өөрөө унтарна. Анхдагчаар **унтраастай**: Fun порт дээр л `LEGACYX_27016_COMMUNITY_THIRDPERSON=true` гэж асаана (ranked-д хана нөгөө талаас харах давуу тал өгдөг). Камер хана дундуур орж болно. Жинхэнэ серверт туршаагүй: хэрэв камер ард гарахгүй, эсвэл хөдөлж чадахгүй бол мэдээлнэ үү.

`.discord` (`!discord`) тоглогчид Discord-ын урилгыг chat-д харуулна. Холбоосыг `LEGACYX_COMMUNITY_DISCORD_URL` өөрчилнө (анхдагч `https://discord.gg/legacyx`).

## Security

Plugin нь `/api/v1/plugin/community/players/:steamId` endpoint рүү л явна. API URL, plugin identity болон scoped `x-plugin-secret` нь бүх LEGACY-X plugin-тэй адил future CS2 host-ийн **нэг** `CounterStrikeSharp/.env`-ээс уншигдана. `LegacyXCommunity.json.example` нь одоо зөвхөн secret-free chat presentation default агуулна. Staff/API secret, Supabase key, database credential-ийг plugin JSON/cfg болон Git-д хэзээ ч байрлуулахгүй.

## Build and deploy

```bash
cd LegacyX-Community
dotnet build -c Release
```

Одоогоор server байхгүй тул энэ repository нь source/build preparation л агуулна. Future host батлагдсаны дараа central `.env`, secret-free config default болон release artifact-ийг reviewed deployment procedure-ээр байрлуулна.

**Revenge.** Kill the player who killed you last (on this map) and you both get one line in your own chat: `LEGACY-X | Name: 3 vs Name: 2`, the kills each of you has on the other on this map (the prefix and the numbers are red, each name is in its team's color (CT blue, T yellow) and a colon sets it apart from its number, the one place chat uses red). Nobody else sees it; bots, suicides and team kills do not count, and a revenge clears itself so the next kill is not one. `LEGACYX_COMMUNITY_REVENGE=false` switches it off.

## Weapon models (Shop → Weapons)

`LegacyXWeaponModels.cs` puts a player's worn knife, Desert Eagle and AK-47 model (from `weapon_models` in the profile) on their weapon at spawn and pickup. The models come from the Workshop addon. Put their `.vmdl` paths, comma separated, in `LEGACYX_COMMUNITY_MODEL_PRECACHE` so the server precaches them each map. Not built or run yet: test on a test server first. Console: `lx_wm_status`, `lx_wm_off 1|0` (0 = off), `lx_wm_try <steamid64> <base|none>` (puts that knife under the player's knife model right now, to find which knife an own-graph model plays right on; it does not change the database), `lx_wm_agent <steamid64> <path.vmdl>` (puts a character model, for example one of the Apex seller's re-rigged agents under `agents/models/bluesen/`, on a player until the next spawn, to see whether a knife's own animations then play).

### Apex knives: weapon prefabs and ChangeSubclass (2026-11-09)

The seller's v2 archive (`LegacyOrderNew.zip`, its `README-APEX.txt`) puts every Apex knife in the addon's `scripts/weapons.vdata(_c)` as a weapon **prefab** (`apex_karambit`, `apex_balisong`; vanilla CS2 vdata plus 19 Apex prefabs, 15 of them guns whose files were never shipped) with its own `m_szWorldModel`, `m_szAnimClass` (`weapon_<name>` makes the engine use `animation/bluesen/graphs/viewmodel_knife_<name>.vnmgraph`) and `m_szAnimSkeleton`.
The engine reads those from the prefab named by the weapon's subclass, so `LegacyXWeaponModels.cs` calls `weapon.AcceptInput("ChangeSubclass", value: "apex_karambit")` on the knife **in hand** when a player equips it (never on a holstered weapon: that crashed the seller's server), at most once every 1.5 s per player, and does not `SetModel` on such a knife (it would keep the vanilla animations). The prefab name is taken from the model path `weapons/bluesen/apex/<name>/<name>.vmdl`. The character (agent) model is not involved.
The `Failed loading resource animation/bluesen/skeletons/apex_*.vnmskel_c` lines are the 17 Apex prefabs whose files are not in the addon; harmless.
Not built or run: test on one test server with one knife first. Note that `scripts/weapons.vdata_c` is a full snapshot of CS2's weapon data: after a CS2 weapon update it has to be remade, or weapons keep the old values.

**Custom knives on another knife's animations without swapping (anim_base, 2026-11-09):** a model with `anim_base` (the katanas, `paracord`) no longer needs the knife swap (which removed and gave a knife and froze a server once). Set `LEGACYX_COMMUNITY_KNIFE_SUBCLASS="paracord=<prefab>"` (the prefab's name in `scripts/weapons.vdata`, found with `Select-String -Pattern 'cord' weapons.vdata`; nothing is guessed, a base without an entry is left alone). The knife in hand then gets that subclass (`ChangeSubclass`, which resets the model to the prefab's) and 0.3 s later the knife's own model goes back on. Keep `LEGACYX_COMMUNITY_KNIFE_SWAP=false`. Not built or run.

The Apex subclass is **off by default** (`LEGACYX_COMMUNITY_APEX_SUBCLASS=true` turns it on): without `scripts/weapons.vdata_c` in the published addon there is no `apex_karambit` prefab and the knife would stay plain. Until it is published the Apex knives are put on with `SetModel` again (model only, the vanilla animations). After publishing, check `strings <item>_dir.vpk | grep -c weapons.vdata`, then set the variable and restart.

