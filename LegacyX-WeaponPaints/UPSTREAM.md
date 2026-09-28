# LEGACY-X WeaponPaints Fork

This directory begins from **WeaponPaints v3.3a** by Nereziel and daffyy.

> Upstream: https://github.com/Nereziel/cs2-WeaponPaints
> License: GNU General Public License v3.0 or later (`LICENSE` in this directory)

LEGACY-X modifies the synchronization layer so the game server uses the LEGACY-X Root API rather than a direct MySQL connection. The fork must retain GPLv3 notices and corresponding source availability whenever it is distributed.

## Intentional LEGACY-X Changes

1. The plugin must not contain database URL, username, password, or direct SQL client code.
2. All player session, loadout, apply-job, and acknowledgement traffic must use authenticated Root API requests.
3. The website and game plugin must never communicate directly with each other or with the database.
4. `!ws`-style player menu commands are disabled for the production website-controlled flow.

## data/

`data/{skins,gloves,agents,music,collectibles}_en.json` are copied unchanged from upstream `Nereziel/cs2-WeaponPaints` (`website/data/`, main branch, fetched 2026-09-28). The plugin reads them for the `legacy_model` flag and the optional in-game menus; a missing file is logged and treated as an empty list.
