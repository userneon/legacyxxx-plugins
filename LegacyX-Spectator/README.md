# LEGACY-X Spectator Comms

`LegacyXSpectatorComms.dll` enforces the LEGACY-X anti-ghosting communication policy. It reapplies `sv_deadtalk 1` (living players hear their dead teammates, owner request) at load and each round start; `sv_alltalk` and `sv_full_alltalk` belong to LegacyX-MatchZy, which sets them per phase. 

Chat text is formatted and routed by LegacyX-Admin, not here (owner request 2026-10-16): `ALL | Name: text` for `say`, `TEAM | Name: text` for `say_team`, the dead and spectators only reach each other, and a team line only reaches that team. This plugin used to reroute chat as well, which sent every line twice, once as `(CT) Name: text` with no sign of all or team chat. Its `EnforceTextIsolation` setting is kept in the config for older files and no longer does anything.

> **Important:** `sv_deadtalk 1` lets dead players speak to living teammates (it does not open the enemy team); the stricter anti-ghosting `sv_deadtalk 0` was turned off on request. [1]

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
