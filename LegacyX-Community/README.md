# LEGACY-X Community Plugin

`LegacyXCommunity` нь тусдаа EXP/rank calculation хийхгүй. MatchZy-ийн completed 5v5 final map result-ийг backend processing хийж XP, level, competitive rank болон clan season score-г хадгална. Энэ plugin нь тоглогчид server дотор өөрийн profile-ийг харах command UX өгнө.

| Command | Behavior |
|---|---|
| `css_xp`, `css_level`, `css_progress` | XP, level, competitive rating/tier, clan tag харуулна. |
| `css_clan` | Clan membership, role болон clan season contribution context харуулна. |

## Security

Plugin нь `/api/v1/plugin/community/players/:steamId` endpoint рүү л явна. API URL, plugin identity болон scoped `x-plugin-secret` нь бүх LEGACY-X plugin-тэй адил future CS2 host-ийн **нэг** `CounterStrikeSharp/.env`-ээс уншигдана. `LegacyXCommunity.json.example` нь одоо зөвхөн secret-free chat presentation default агуулна. Staff/API secret, Supabase key, database credential-ийг plugin JSON/cfg болон Git-д хэзээ ч байрлуулахгүй.

## Build and deploy

```bash
cd community
dotnet build -c Release
```

Одоогоор server байхгүй тул энэ repository нь source/build preparation л агуулна. Future host батлагдсаны дараа central `.env`, secret-free config default болон release artifact-ийг reviewed deployment procedure-ээр байрлуулна.

Clan self-service creation/join/leave нь server chat command биш. Энэ нь moderation, offensive tag prevention болон ownership dispute үүсгэх эрсдэлтэй тул LEGACY-X backend/staff workflow-оор удирдана.
