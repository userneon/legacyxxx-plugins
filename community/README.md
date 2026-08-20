# LEGACY-X Community Plugin

`LegacyXCommunity` нь тусдаа EXP/rank calculation хийхгүй. MatchZy-ийн completed 5v5 final map result-ийг backend processing хийж XP, level, competitive rank болон clan season score-г хадгална. Энэ plugin нь тоглогчид server дотор өөрийн profile-ийг харах command UX өгнө.

| Command | Behavior |
|---|---|
| `css_xp`, `css_level`, `css_progress` | XP, level, competitive rating/tier, clan tag харуулна. |
| `css_clan` | Clan membership, role болон clan season contribution context харуулна. |

## Security

Plugin нь `/api/plugin/matchzy/community/players/:steamId` endpoint рүү л явна. Server дээр **`x-plugin-secret`** хадгална; staff `x-api-secret`-ийг CS2 config-д хэзээ ч байрлуулахгүй. `LegacyXCommunity.json.example`-ийг live server дээр private `LegacyXCommunity.json` болгоод secret/URL-г солино. Private runtime config-ийг Git-д commit хийхгүй.

## Build and deploy

```bash
cd community
dotnet build -c Release
```

Deploy `bin/Release/net8.0/LegacyXCommunity.dll`-г CounterStrikeSharp plugin folder руу, private config-г `csgo/addons/counterstrikesharp/configs/plugins/LegacyXCommunity/LegacyXCommunity.json` руу байрлуулна.

Clan self-service creation/join/leave нь server chat command биш. Энэ нь moderation, offensive tag prevention болон ownership dispute үүсгэх эрсдэлтэй тул LEGACY-X backend/staff workflow-оор удирдана.
