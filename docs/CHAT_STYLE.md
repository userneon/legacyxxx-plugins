# LEGACY-X in-game text: one rule for every plugin

In-game text looks like legacyx.cc: dim, muted, neutral, with crimson only in the wordmark.
`LegacyXChat.System()` (shared library) applies the colors to every system chat line, so a module
cannot break them by accident. Write new text to match anyway.

| | Rule |
|---|---|
| Prefix | `LEGACY-X •`: "LEGACY-" white, "X" crimson, "•" grey, like the website logo. Added by `LegacyXChat.System`; never write a prefix yourself |
| Case | normal sentences: capital first letter, the rest lower case. Keep game words as players write them: AFK, CT, T, HP, EXP, StatTrak |
| Body | dim grey (`{grey}`, or `{default}`) |
| Emphasis | `{white}` for names, numbers, maps and commands. Go back with `{grey}` |
| Green | `{green}` only for something that worked or is on: "Skins updated.", "You're ready.", "Live." |
| Errors | plain grey, no color. Say what happened and, if useful, what to do: "Wait a moment before trying again." |
| Other colors | none. Any other tag or ChatColors code turns white. Crimson (`{brand}`) is for the prefix only |
| Length | one or two short sentences, ending with a period |
| Words | plain game words. No "plugin", "API", "sync", "refresh", "config", "cvar", "JSON" or error codes |
| Punctuation | no `!` for emphasis, no `…`, no emoji, no `[TAGS]`, no ALL CAPS |

Examples:

```text
LEGACY-X • Skins updated.                                            (green)
LEGACY-X • You're AFK. Move or you'll be kicked in 15 seconds.        (15 white)
LEGACY-X • Temuujin was kicked for being AFK.                        (name white)
LEGACY-X • Admin777 banned Player for 30 minutes. Reason: Cheating.
LEGACY-X • Operator I · 1,240 EXP · #12 · 260 to Operator II
```

The `!admin` menu (center screen) follows the same look: the `LEGACY-X` wordmark and the menu name in
dim grey on top, rows in grey, the selected row white with the crimson bar the website uses for the
active page, and the keys in small grey at the bottom.

Only `lang/en.json` in each plugin is written to this rule. Other languages are the upstream
translations; their colors still follow the rule through `LegacyXChat`, but their wording does not.
CounterStrikeSharp shows English unless the server language (`core.json`) or a player's `!lang` picks another.
