# LEGACY-X in-game text: one rule for every plugin

In-game text looks like legacyx.cc: neutral, with white as the accent, and orange only in the wordmark.
`LegacyXChat.System()` (shared library) applies the rule to every system chat line, so a module
cannot break it by accident. Write new text to match anyway.

| | Rule |
|---|---|
| Prefix | `LEGACY-X •`: "LEGACY" white, "-X" brand orange, "•" grey. Added by `LegacyXChat.System`; never write a prefix yourself |
| Case | UPPER CASE only (forced by `System`; the `!admin` menu is upper-cased too) |
| Body | grey (`{grey}`, or `{default}`) |
| Emphasis | `{white}` for names, numbers, maps and commands. Go back with `{grey}` |
| Green | `{green}` only for something that worked or is on: `SKINS UPDATED`, `YOU ARE READY`, `LIVE` |
| Light red | `{lightred}` only for a refusal or error: `NO ACCESS`, `WAIT A MOMENT` |
| Other colors | none. Any other tag or ChatColors code turns white. Orange (`{brand}`) is for the prefix only |
| Length | one short line. Details go after ` · `: `KICKED · AFK`, `BANNED X · 30 MIN · CHEATING` |
| Words | plain game words. No "plugin", "API", "sync", "refresh", "config", "cvar", "SteamID", "JSON" or error codes |
| Punctuation | no `!`, no trailing `.`, no `…`, no emoji, no `[TAGS]` |

Examples:

```text
LEGACY-X • SKINS UPDATED                            (green)
LEGACY-X • WAIT A MOMENT                            (light red)
LEGACY-X • TEMUUJIN KICKED · AFK                    (name white)
LEGACY-X • ADMIN777 BANNED PLAYER · 30 MIN · CHEATING
LEGACY-X • OPERATOR I · 1,240 EXP · #12 · 260 TO OPERATOR II
```

The `!admin` menu (center screen) follows the same look: the `LEGACY-X` wordmark and the menu name in
grey on top, rows in grey, the selected row white with the orange bar the website uses for the active
page, and the keys in small grey at the bottom.

Only `lang/en.json` in each plugin is written to this rule. Other languages are the upstream
translations; colors and upper case still follow the rule through `LegacyXChat`, but their wording is
longer. CounterStrikeSharp shows English unless the server language (`core.json`) or a player's `!lang` picks another.
