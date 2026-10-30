# LEGACY-X Hud (the skin menu)

The in-game **Skinchanger menu** (owner request 2026-10-30, back after the HUD was retired; skins only: no rank card, no admin panel, no
knife-round vote, no welcome card, no round-start sound). The window is a Panorama layout in the LEGACY-X Workshop addon
(repository `legacyxxx-workshop`); this plugin creates its `custom_hud_layout` entity through **PanoramaManager** and fills it per player:
texts by Label id, states by toggling classes. Every id is listed in the addon's `CONTRACT.md`.

**`!ws`** opens it in warmup and before the round (freeze time); hold **R** to close it, or press Close. A header with the T / CT emblems, a
category list on the left (Pistols, SMGs, Rifles, Heavy, Agents, Gloves, Knives) and, on the right, a content area that scrolls: the guns of a group,
the knife or glove types, or the skins of one pick, 24 to a page. A pick is saved to the website loadout and applied at once through SkinBridge
(`!rs`). Everything listed comes from the website's own catalog through the plugin API; nothing is made up here. Soft CS2 UI sounds play on the
player's client for open, click, back and pick (`LEGACYX_HUD_MENU_SOUNDS=false` turns them off, `LEGACYX_HUD_MENU_SOUND_VOLUME` 0 to 1).

It reads and saves with SkinBridge's credentials (`LEGACYX_SKINBRIDGE_PLUGIN_ID` / `_PLUGIN_TOKEN`, scopes `skinchanger:read` and `:write`) and
`LEGACYX_API_BASE_URL`. Without them the menu opens but says it cannot read skins.

## Needs

* CounterStrikeSharp 1.0.374 or newer (the `custom_hud_layout` API) and **PanoramaManager** (it ships next to the plugin).
* The Workshop addon published and sent to players: `sudo ./scripts/cs2-host.sh client-addons <item id>` (MultiAddonManager). A player who has
  not downloaded it yet sees nothing when typing `!ws`: they need one map change after the first join.
* `LEGACYX_HUD_ENABLED=true` (default) in `CounterStrikeSharp/.env`.
* **.NET 10**: this plugin targets `net10.0` (CounterStrikeSharp.API 1.0.376 and PanoramaManager 0.4.3 ship nothing else), so the build machine needs
  the .NET 10 SDK next to the .NET 8 one the other plugins use: `dotnet build LegacyX-Hud/LegacyX-Hud.csproj -c Release`, or pass a
  prebuilt `--package legacyx-cs2.zip` to the host script.

## After a map change

PanoramaManager keeps the layout, but what this plugin wrote to a client is gone. On every map start the menu forgets what it sent
(`ForgetMenuState`) and lets go of the mouse and keys for everybody once they are in (after 2 s and again after 8 s), so a menu that was open
before the change never leaves the HUD or the microphone key dead.

## If nothing shows

1. Console at start: `[LEGACY-X Hud] Ready. Layout ...`, then `Spawned ...: ok` the first time `!ws` is used, or `Could not spawn ...: <reason>`.
2. The default layout path is the compiled name (`...legacyx_menu.vxml_c`). If this game build wants the source, set
   `LEGACYX_HUD_MENU_LAYOUT=panorama/layout/custom_game/legacyx_menu.xml` in the .env.
3. `css_panorama_diag` (from PanoramaManager) shows the entity and per-player state.
4. The player must have downloaded the addon: map change once after the first join. Never run a local build of the addon and the published one together
   (every file would load twice and joining fails with an overflow error).
