> **Хуулбар.** Эх сурвалж: [legacyxxx-workshop/README.md](https://github.com/userneon/legacyxxx-workshop/blob/main/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# legacyxxx-workshop

The LEGACY-X in-game HUD: a CS2 Workshop addon of Panorama layouts, stylesheets and images, driven from the
server by the LEGACY-X plugins through CS2's `custom_hud_layout` entity (added by Valve on 2026-08-24).

| Layout | What players see |
|---|---|
| `legacyx_notify` | a welcome card in the middle (server, player, rank), announcements dropping from the top (staff message, match live, server restart, ban notice, AFK, knife result), a one-line toast, the rank card at round start, rank up / down |
| `legacyx_match` | the EXP result of every ranked match: a centre card, then a compact card on the right until the next match |
| `legacyx_knife` | the knife-round vote (Stay / Switch, secret votes, 10 seconds) for the winning team |
| `legacyx_admin` | `!admin`: players and actions, map change, every ban with your own marked, staff |

Same look as legacyx.cc: dark glass cards, white text, crimson only for the `-X` and the "this one" bar.

**Status.** Published to the Workshop and loaded by the servers through MultiAddonManager. `tools/validate.py` checks every layout and stylesheet (also in CI on every push); how a screen looks in game still has to be seen in the game (`tools/build.cmd -Local`). Mongolian manual: [docs/MANUAL_MN.md](docs/MANUAL_MN.md). All documents: [docs/README.md](docs/README.md).

## Layout

```
addon/                                   copied as-is into content\csgo_addons\legacyx
  addoninfo.txt
  panorama/layout/custom_game/*.xml       layouts (one per custom_hud_layout entity)
  panorama/styles/custom_game/*.css       legacyx.css (shared), legacyx_assets.css (generated), one per layout
  panorama/images/custom_game/legacyx/    PNG + .vtex descriptor per image
tools/
  validate.py                            checks layouts and stylesheets (runs in CI on every push)
  make_vtex.py                           writes a .vtex next to every PNG
  make_assets_css.py                     writes legacyx_assets.css (rank-*, tier-*, ic-*, map-*, p0..p100)
  build.ps1 / build.cmd                  Windows: copy into CS2 and compile with resourcecompiler
CONTRACT.md                              every id, text and class the plugin uses
```

Images come from legacyxxx-frontend (rank emblems `public/ranks`, team emblems `src/assets/sides`, logo,
map art) and lucide icons, exported as PNG. After adding or renaming a PNG run `make_vtex.py` and
`make_assets_css.py`.

## Build (Windows, whenever the design changes)

1. Install **CS2 Workshop Tools**: CS2 → Settings → search "Workshop Tools" → Install, quit the game and let
   Steam download. A `content\` folder appears next to `game\`.
2. `git pull`, then double-click `tools\build.cmd` (or run it from a terminal). It validates, copies
   `addon\` into `content\csgo_addons\legacyx`, compiles every image, stylesheet and layout, and lists
   anything that did not compile.

### Test in your own game

`tools\build.cmd -Local` also copies the compiled files into `game\csgo\panorama`, so your own CS2 loads them
without the Workshop. Restart the game after every build: Panorama caches layouts for the whole session.

### Publish

Open CS2 Workshop Tools, choose the `legacyx` addon and publish it from the tools' Workshop publishing window.
Keep the item visible to players (public or unlisted) and note its Workshop ID: the server needs it.

## Server side

* Players download the addon through **MultiAddonManager** (Metamod plugin): `mm_client_extra_addons <ID>`.
  `mm_cache_clients_with_addons 1` avoids re-sending it on every map. It is a client-only addon; the server
  itself does not mount it.
* CounterStrikeSharp 1.0.374 or newer (the `custom_hud_layout` API).
* The plugin spawns one entity per layout after the first `round_start`, and removes old ones on load.

## Rules that are easy to break (all silent in game)

* Only `Panel`, `Label`, `Image`, `Button`; attributes `id`, `class`, `hittest`, `text`, `src`. No inline
  `style`, no text entry, no scripts.
* No `id` on the root panel. Include stylesheets as `s2r://panorama/styles/custom_game/<name>.vcss_c`.
* Pictures are panel backgrounds pointing at the `.vtex` (BGRA8888), never `<Image>` with a stock icon.
* `@keyframes` on `transform` do not run: motion is a transition started by a class (`open`, `shown`).
  Width, height and visibility do not animate.
* No comma selectors; quoted `@keyframes` names; 4-value `margin`/`padding`.
* A changed layout reaches players only when the addon is republished; a plugin deploy does not touch it.

`tools/validate.py` checks the ones that can be checked without the game.
