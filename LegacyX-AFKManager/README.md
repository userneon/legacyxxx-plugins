# LEGACY-X AFK Manager

LEGACY-X AFK Manager нь CounterStrikeSharp дээр ажиллах, MatchZy competitive server-д тохируулсан AFK management plugin юм. Энэ module нь upstream [NiGHT757/AFKManager](https://github.com/NiGHT757/AFKManager)-ийн movement/angle detection дээр суурилж, LEGACY-X-ийн ready lobby болон 5v5 match behavior-тэй нийцэх production defaults ашиглана.

## LEGACY-X policy

| Situation | Behavior |
|---|---|
| MatchZy warmup/ready lobby | AFK punishment алгасна; тоглогч accidental kick авахгүй |
| Live 5v5 player AFK | 20 секунд тутам warning, 3 warning-ийн дараа spectator руу шилжүүлнэ |
| AFK player carries C4 | Эхний warning орчимд buy zone нөхцөл хангавал амьд тоглогчид C4 шилжүүлнэ |
| Spectator | Plugin-ээр spectator болгосон player-ийг л хянана; ordinary spectators kick авахгүй |
| Spectator kick | Disabled by default; Match server-ийн observer/friend behavior-д саад болохгүй |
| Anti-camp | Disabled by default; competitive holding angle-ийг AFK гэж буруу шийтгэхгүй |
| Admin immunity | `@css/root` болон `@css/ban` flag-тэй staff exemption хэвээр |
| Map change | Plugin state map end дээр цэвэрлэгдэнэ; MatchZy map lifecycle-г эзэмшинэ |

## Why this policy

MatchZy warmup нь LEGACY-X дээр 5v5 ready lobby-ийн үүрэгтэй. Тиймээс ready дараагүй, найзаа хүлээж байгаа, server reconnect хийж байгаа тоглогчийг warmup дээр AFK гэж шийтгэхгүй. Live match эхэлсний дараа удаан хөдөлгөөнгүй player багийн тоглолтод нөлөөлөх тул эхлээд warning, дараа нь spectator transfer хийнэ. Шууд kick хийхгүй бөгөөд тоглогч `Reconnect`/Last Played flow-оор буцаж орох боломжтой үлдэнэ.

## Build

Requirements: .NET 8 SDK, Metamod:Source and CounterStrikeSharp.

```bash
cd afkmanager
dotnet build --configuration Release
```

Artifact:

```text
afkmanager/bin/Release/net8.0/AFKManager.dll
```

## Install

```text
csgo/addons/counterstrikesharp/plugins/AFKManager/AFKManager.dll
csgo/addons/counterstrikesharp/configs/plugins/AFKManager/AFKManager.json
```

Copy `config/AFKManager.json` to the CounterStrikeSharp plugin config path. If the config already exists, merge the LEGACY-X values rather than overwriting local server-specific admin flags without review.

## Integration boundary

MatchZy owns match start/end, exact 5v5 ready gate, map change and match state. AFK Manager only observes player activity and applies AFK policy. It must not register a second match-end map changer or reset MatchZy state. AdminPlus owns admin/player/server actions; AFK Manager should not be used as a second RCON/admin bridge.

## Operational commands

The plugin does not add a public kick command. Staff can use the existing CounterStrikeSharp/admin workflow for manual cases. AFK warnings, spectator transfers and C4 transfers are visible in chat and server logs.

## Upstream

Source is based on [NiGHT757/AFKManager](https://github.com/NiGHT757/AFKManager). The upstream source is retained in the Git history; this module includes LEGACY-X branding, production defaults and the MatchZy-aware policy described above.
