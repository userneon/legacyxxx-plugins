# LEGACY-X Hud

Shows the LEGACY-X Workshop HUD (repository `legacyxxx-workshop`) to players. The Workshop addon draws; this
plugin creates the `custom_hud_layout` entity through **PanoramaManager** and fills it per player: texts by
Label id, states by toggling classes. Every id is listed in the addon's `CONTRACT.md`.

**Screens**: welcome card (server, player and rank in one box, centre of the screen, 7 s), the knife-round side vote, and the menu below.
The vote runs in the 10 second freeze time after the knife round: the winners click the **Stay** or **Switch** card with the mouse
(nothing is chosen at first, clicking the other card changes it, 10 seconds). Most clicks wins; a tie or no clicks is Stay. Stay keeps the sides,
Switch swaps the teams, and a few seconds later the normal round starts. The `!lxhud` test, the round-start
rank card, the match result cards, rank up / down and the "Skins updated." toast were removed.

Another plugin calls it through a server command: `lx_hud_knife start <2|3> | stop` (LegacyX-MatchZy, which applies the result with `lx_knife_choice <slot> stay|switch`).
The welcome card and the menu read the API like LegacyX-Community does (`LEGACYX_API_BASE_URL`,
`LEGACYX_COMMUNITY_PLUGIN_ID/TOKEN`); without them the rank part stays empty.

**Admin panel** (staff, `!admin`): when this plugin is loaded, LegacyX-Admin opens the panel instead of its chat menu (`lx_hud_admin open <slot>`; it
looks for the `lx_hud_admin_ready` sentinel). Players page: the list, a player's SteamID, IP (only for staff who may ban), penalties, and Kick, Ban…, Slay,
Respawn, Mute, Gag, Silence, Move team. Server page: change map, back to warmup (second click within 10 seconds), clean weapons, restart round. The Ban…
step picks Ban or IP ban, a length and a reason; mute, gag and silence ask for a length. Every button goes back to LegacyX-Admin as
`lx_admin_do <actorSlot> <action> …`, which checks that staff member's own rights, so the greyed-out buttons are only a hint. Hold R closes it.
Bans (who issued and who lifted a ban, filters, "Lift this ban" through `!unban`), Logins (the latest server logins) and Staff (read only) read
the API with this plugin's game server token (`admin:read`, `/api/v1/plugin/admin/bans | logins | staff`); when the API cannot be reached they say so. IP bans
are kept by the game servers, so they are not in the Bans list.

**Menu** (`!menu`, or hold E in warmup / before the round, hold R to close): Welcome, Skins (knives, gloves, guns, agents) and
Settings. Clicks, opening, closing and picking a skin play soft CS2 UI sounds (`itemtile_rollover_09`, `menu_focus`,
`cards_rollover_01`, `itemtile_click_02`) quietly on the player's client with `playvol`. `LEGACYX_HUD_MENU_SOUND_VOLUME` (0 to 1,
default 1) scales them, `LEGACYX_HUD_MENU_SOUNDS=false` turns them off, `LEGACYX_HUD_MENU_SOUND_CLICK` / `_OPEN` / `_BACK` /
`_PICK` change a sound (a `sounds/...vsnd_c` path from the game, empty = silent). `LEGACYX_HUD_MENU_KEYS=false` turns the E / R keys off.

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
