# LEGACY-X MatchZy

LEGACY-X MatchZy нь CounterStrikeSharp дээр ажиллах competitive CS2 match plugin юм. Энэ module нь upstream MatchZy-ийн practice, pug, scrim, match, demo, stats, database болон Get5-compatible event capabilities дээр LEGACY-X community-ийн хатуу match lifecycle-ийг нэмсэн.

## LEGACY-X customization

| Capability | LEGACY-X behavior |
|---|---|
| Match start | Exactly 5 CT + 5 T; 6v5, 5v6, 6v6 болон бусад imbalance эхлэхгүй |
| Ready gate | Арван active human player бүгд `css_ready`/`.ready` хийх ёстой |
| Force ready | Production preset-д disabled |
| Branding | `LEGACY-X MatchZy`, `[LEGACY-X]` chat prefix, LEGACY-X hostname/start message |
| Match end | Result/demo/stat persistence-ийн дараа soft reset, hard process restart хийхгүй |
| Map transition | `PLEASE WAIT` notification болон loading/black transition бүхий `changelevel` |
| Map rotation | Configured pool-оос random map; одоогийн map-ийг дараагийн сонголтоос хасна |
| Admin bridge | AdminPlus тусдаа plugin; MatchZy нь match lifecycle, AdminPlus нь admin actions хариуцна |

## Build

Requirements: .NET 8 SDK, Metamod:Source болон CounterStrikeSharp.

```bash
cd matchzy
 dotnet build --configuration Release
```

Build artifact:

```text
matchzy/bin/Release/net8.0/MatchZy.dll
```

## Install

Build болсон `MatchZy.dll` болон module-ийн `cfg`, `lang`, `spawns` файлуудыг CS2 server-ийн CounterStrikeSharp plugin directory-д байрлуулна.

```text
csgo/addons/counterstrikesharp/plugins/MatchZy/MatchZy.dll
csgo/cfg/MatchZy/config.cfg
csgo/cfg/MatchZy/*.cfg
csgo/cfg/MatchZy/*.json
```

`cfg/MatchZy/config.cfg`-г server-ийн `csgo/cfg/MatchZy/config.cfg` руу хуулж, plugin load/restart хийсний дараа startup log-д `LEGACY-X MatchZy` харагдана.

## Player flow

Match setup ачаалсны дараа team бүрт яг таван player байх ёстой. Player бүр `.ready` chat command эсвэл `css_ready` console command-оор ready болно. Бүх арван player ready болсон ч player count яг 5v5 биш бол match эхлэхгүй.

Хэрэв server policy зөвшөөрвөл rules/Discord дээр дараах optional client bind-ийг өгч болно. Plugin нь existing keybind-ийг автоматаар overwrite хийхгүй.

```text
bind ENTER css_ready
```

Useful commands:

```text
css_ready          Ready
css_unready        Unready
css_match_status   MatchZy status
css_start          Admin start, subject to exact gate
css_restart        Admin restart
css_endmatch      Admin end
```

## Match end and map transition

MatchZy match end event-ийг хүлээж result, demo болон stats persistence эхлүүлнэ. Дараа нь `legacyx_map_transition_delay` хугацаанд `[LEGACY-X] PLEASE WAIT` notification харуулаад `ResetMatch(false)` хийж ready flags, scores, pause state, demo state болон series state-ийг цэвэрлэнэ. Үүний дараа current map-аас өөр random installed map руу `changelevel` хийнэ.

```text
Match end
→ result/demo/stat persistence
→ PLEASE WAIT
→ ResetMatch(false)
→ random non-repeat changelevel
→ new map warmup
→ new exact 5v5 ready gate
```

Hard process restart нь энэ flow-д ашиглагдахгүй. Process restart нь зөвхөн crash, maintenance эсвэл deployment үед хэрэглэнэ.

## Configuration

Production preset: `cfg/MatchZy/config.cfg`.

```text
legacyx_exact_5v5 true
legacyx_random_map_after_match true
legacyx_map_pool "de_ancient,de_anubis,de_dust2,de_inferno,de_mirage,de_nuke,de_overpass,de_vertigo"
legacyx_map_transition_delay 3.0
matchzy_minimum_ready_required 5
matchzy_allow_force_ready false
```

Map pool-д server дээр суусан map-уудыг л оруулна. Plugin current map-ийг pool-оос хасаж дараагийн map-ийг сонгоно. Pool-д нэг л valid map үлдвэл тэр map дахин сонгогдож болох тул production дээр дор хаяж хоёр valid map суулгасан байна.

## Repository boundary

`LegacyX-MatchZy/` нь match lifecycle, competitive settings, demo/stats, map transition болон MatchZy configs-ийг эзэмшинэ. `LegacyX-Admin/` нь in-game admin menu, moderation, report, vote болон reservation ownership-г эзэмшинэ. Database audit, Root API болон frontend API нь `legacyxxx-backend` repository-д үлдэнэ; MatchZy Discord event delivery байхгүй.

## Upstream

This module is based on [shobhit-pathak/MatchZy](https://github.com/shobhit-pathak/MatchZy), licensed under the repository's upstream license. Upstream README is preserved as `README.upstream.md`. Framework: [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp).
