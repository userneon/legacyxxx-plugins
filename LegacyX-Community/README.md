# LEGACY-X Community Plugin

`LegacyXCommunity` нь тусдаа EXP/rank calculation хийхгүй. MatchZy-ийн `competitive_result`-ийг backend (Rank system v1) боловсруулж EXP болон rank-ийг хадгална. Энэ plugin нь тоглогчид server дотор өөрийн rank-ийг харах command өгнө.

| Command | Behavior |
|---|---|
| `css_rank`, `css_xp`, `css_level`, `css_progress` | Rank, EXP, leaderboard байр, дараагийн rank хүртэлх EXP болон Pro League төлөв харуулна. |

## Security

Plugin нь `/api/v1/plugin/community/players/:steamId` endpoint рүү л явна. API URL, plugin identity болон scoped `x-plugin-secret` нь бүх LEGACY-X plugin-тэй адил future CS2 host-ийн **нэг** `CounterStrikeSharp/.env`-ээс уншигдана. `LegacyXCommunity.json.example` нь одоо зөвхөн secret-free chat presentation default агуулна. Staff/API secret, Supabase key, database credential-ийг plugin JSON/cfg болон Git-д хэзээ ч байрлуулахгүй.

## Build and deploy

```bash
cd LegacyX-Community
dotnet build -c Release
```

Одоогоор server байхгүй тул энэ repository нь source/build preparation л агуулна. Future host батлагдсаны дараа central `.env`, secret-free config default болон release artifact-ийг reviewed deployment procedure-ээр байрлуулна.
