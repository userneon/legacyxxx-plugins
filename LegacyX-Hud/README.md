# LEGACY-X Hud

Shows the LEGACY-X Workshop HUD (repository `legacyxxx-workshop`) to players. The Workshop addon draws; this
plugin creates the `custom_hud_layout` entity through **PanoramaManager** and fills it per player: texts by
Label id, states by toggling classes. Every id is listed in the addon's `CONTRACT.md`.

**Status: first version, a pipeline test.** A welcome announcement (`legacyx_notify` → `ann`) is shown to a
player a few seconds after joining, and `!lxhud` shows it again. Rank card, match card, knife vote and the
`!admin` panel come after this works on a server.

## Needs

* CounterStrikeSharp 1.0.374 or newer (the `custom_hud_layout` API).
* The Workshop addon published and sent to players: `mm_client_extra_addons <ID>` (MultiAddonManager).
* `LEGACYX_HUD_ENABLED=true` (default) in `CounterStrikeSharp/.env`.

## If nothing shows

1. Console at start: `[LEGACY-X Hud] Ready. Layout ...`, or `Could not spawn ...: <reason>`.
2. The default layout path is the compiled name (`...legacyx_notify.vxml_c`, as PanoramaManager's examples use). If that is wrong for this game build, set
   `LEGACYX_HUD_NOTIFY_LAYOUT=panorama/layout/custom_game/legacyx_notify.xml` in the .env.
3. `css_panorama_diag` (from PanoramaManager) shows the entity and per-player state.
4. The player must have downloaded the addon: map change once after the first join.

## Build

This project targets **net10.0** (CounterStrikeSharp.API 1.0.376 and PanoramaManager 0.4.3 ship nothing else), so the
build machine needs the .NET 10 SDK next to the .NET 8 one the other plugins use.

`dotnet build LegacyX-Hud/LegacyX-Hud.csproj -c Release`. The class and method names of PanoramaManager
(`Panorama.Init/Spawn/Shutdown`, `PanelHandle.SetVariableFor/SetClassFor/CaptureInput/Dispose`) were taken from
its README; if the compiler disagrees, adjust `LegacyXHud.cs`, nothing else depends on them.
