# LEGACY-X AFK Manager Customization Report

## Scope

AFK Manager-ийг LEGACY-X-ийн MatchZy strict 5v5 server-д зориулж `afkmanager/` module болгон оруулсан. MatchZy match lifecycle, AdminPlus action bridge болон AFK Manager activity policy нь тусдаа boundary-тай байна.

## Upstream

Base source: [NiGHT757/AFKManager](https://github.com/NiGHT757/AFKManager). The upstream plugin provides movement/angle AFK detection, warning, C4 transfer, spectator transfer, spectator kick and anti-camp handling.

## LEGACY-X changes

| Area | LEGACY-X customization | Reason |
|---|---|---|
| Warmup | `SkipWarmup=true` | MatchZy warmup нь ready lobby тул queue/reconnect хийж байгаа player-ийг шийтгэхгүй |
| Live AFK | 20-second warning interval, 3 warnings, then spectator | Live 5v5 багт AFK player үлдээхгүй, гэхдээ шууд kick хийхгүй |
| C4 | First warning transfer from buy zone | AFK player бөмбөг барьж round-ийг эвдэхээс сэргийлнэ |
| Spectator | Kick disabled; only plugin-moved spectator tracking enabled | Ordinary spectators болон friends observing-г хамгаална |
| Anti-camp | Disabled by default | Competitive angle hold-ийг AFK гэж буруу танихаас сэргийлнэ |
| Staff | `@css/root`, `@css/ban` skip flags | Admin moderation болон live intervention тасалдахгүй |
| Branding | Module name/version/author changed to LEGACY-X | Server log болон plugin list-д тодорхой харагдана |
| API | CounterStrikeSharp API aligned to 1.0.342 | Existing LEGACY-X MatchZy build-тэй нийцүүлнэ |

## Decision rationale

LEGACY-X-ийн 5v5 server дээр AFK Manager-ийг шууд kick engine болгох нь дахин холбогдож буй тоглогчтой муу зохицно. Иймээс production default нь warning → spectator transfer байна. Kick нь spectator-аас ч disabled бөгөөд staff/queue/reconnect behavior-ийг аль болох эвдэхгүй.

Anti-camp behavior-ийг мөн default-оор идэвхгүй болгосон. Competitive player нэг байрлалд angle барьж байх үед зөвхөн position/camera хөдөлгөөнгүй гэдгээр AFK гэж буруу шийдэх эрсдэлтэй. Хэрэв тусгай public/DM server дээр anti-camp хэрэгтэй бол Match server-ийн config-оос тусад нь enable хийж болно.

## Integration boundary

MatchZy нь `EventCsWinPanelMatch`, `changelevel`, ready state, map rotation болон match reset-ийг эзэмшинэ. AFK Manager нь эдгээрийг дахин бүртгэхгүй, map end дээр өөрийн player state-г л цэвэрлэнэ. AdminPlus нь admin/player/server command bridge хэвээр үлдэнэ.

## Deployment

Build artifact:

```text
afkmanager/bin/Release/net8.0/AFKManager.dll
```

Configuration sample:

```text
afkmanager/config/AFKManager.json
```

Copy the DLL and JSON to the CounterStrikeSharp plugin/config paths described in `afkmanager/README.md`. Do not commit production secrets; this plugin uses no external token or webhook.

## Validation status

Final validation must include .NET 8 build for AFK Manager, MatchZy and AdminPlus, `git diff --check`, secret scan, and a live CS2 smoke test. The live smoke test should cover warmup ready lobby, live 5v5 AFK warning/transfer, C4 transfer, admin immunity, map change cleanup and player rejoin behavior.

## Final local validation

AFK Manager built successfully with .NET 8 Release configuration: 0 errors and 8 upstream nullability warnings. MatchZy built with 0 errors and 5 existing upstream warnings. AdminPlus built with 0 errors and 0 warnings. `git diff --check` passed, the secret scan found no candidate production secrets, and all three Release DLL artifacts were generated while remaining ignored by Git. A live CS2 smoke test is still required before enabling automatic AFK punishment on the production server.
