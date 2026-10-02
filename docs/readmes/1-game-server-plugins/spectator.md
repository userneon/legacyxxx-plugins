> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/LegacyX-Spectator/README.md](https://github.com/userneon/legacyxxx-plugins/blob/main/LegacyX-Spectator/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр байна. Энэ доторх харьцангуй (relative) холбоосууд ажиллахгүй байж болно.

# LEGACY-X Spectator Comms

`LegacyXSpectatorComms.dll` enforces the LEGACY-X anti-ghosting communication policy. It has two complementary layers. First, it reapplies the competitive CS2 voice baseline `sv_alltalk 0`, `sv_full_alltalk 0` and `sv_deadtalk 0` at load and each round start. Second, it intercepts ordinary `say` and `say_team` text, suppresses the default broadcast, and routes it only to the sender's allowed channel.

| Sender state | Can receive voice/text | Cannot receive voice/text |
|---|---|---|
| Spectator or dead | Spectator/dead channel | Any alive player |
| Alive Terrorist | Alive Terrorists | Spectators, dead players, Counter-Terrorists |
| Alive Counter-Terrorist | Alive Counter-Terrorists | Spectators, dead players, Terrorists |

> **Important:** `sv_deadtalk 0` prevents dead players from speaking to living players, while living players hear only living players under the documented setting. The server configuration therefore covers the competitive voice baseline; the plugin provides the stricter recipient-aware text routing. [1]

## MatchZy and AFK boundary

MatchZy owns readiness, match lifecycle, coach placement and map transitions. AFK Manager owns AFK warnings and spectator transfer. Spectator Comms only observes player state for communication routing. It does not change team, player life state, score, map or MatchZy ready data. Commands beginning with `!` or `.` are deliberately left to MatchZy/AdminPlus/Community so `.ready`, `.pause` and staff workflows remain functional.

Dedicated coach voice is **not** exempt by default. If LEGACY-X later wants an official tournament coach exception, create a separately reviewed policy rather than weakening the public Match server anti-ghosting rule.

## Installation

1. Build with `dotnet build -c Release`.
2. Copy `bin/Release/net8.0/LegacyXSpectatorComms.dll` to `csgo/addons/counterstrikesharp/plugins/LegacyXSpectatorComms/`.
3. Copy `config/LegacyXSpectatorComms.json.example` to `csgo/addons/counterstrikesharp/configs/plugins/LegacyXSpectatorComms/LegacyXSpectatorComms.json`.
4. Start a server with two living players and two spectators. Verify `say` and `say_team` are shown only in their channel, while MatchZy command chat remains usable.

## References

[1] [Total CS — `sv_deadtalk` command behavior](https://totalcsgo.com/commands/svdeadtalk)
