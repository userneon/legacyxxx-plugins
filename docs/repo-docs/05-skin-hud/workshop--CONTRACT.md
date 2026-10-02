> **Хуулбар.** Эх сурвалж: [legacyxxx-workshop/CONTRACT.md](https://github.com/userneon/legacyxxx-workshop/blob/main/CONTRACT.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# Contract between the addon and the plugins

The addon draws; the plugin (legacyxxx-plugins) decides. A `custom_hud_layout` entity shows one layout to
everyone, and the plugin changes it **per player** with three things only:

| The plugin does | How | Example |
|---|---|---|
| Set a text | a dialog variable named like the Label's id: the Label reads `{s:<its id>}`, e.g. `{s:ann_title}` | `rank_name` = `Vanguard II` |
| Toggle a class | on any panel, by id | `ann` + `open`, `rank_fill` + `p40` |
| Receive a click | a Button's id arrives on the server | `knife_switch`, `act_ban` |

It cannot create panels, send an image, a colour, a size or a position, or read a key press *from the layout*. (The server can read a player's keys itself, and the knife vote uses that.) So every
state is drawn in the addon ahead of time and picked by class. Player-typed text (names, reasons) is set
as plain text; a Label never renders markup.

Layout path for the entity: PanoramaManager's own examples spawn the compiled name, `panorama/layout/custom_game/<name>.vxml_c`. The plugin's default follows that; set `LEGACYX_HUD_<...>_LAYOUT` to the `.xml` source path if the game turns out to want it.

Each layout has one anonymous root panel (the compiler refuses an id there) and, inside it, a wrapper with the id `lx_<name>` (`lx_notify`, `lx_match`, `lx_knife`, `lx_admin`). PanoramaManager writes every variable on that wrapper (`LayoutContract.RootPanelId`), and a player only receives the entity, and can be written to, after the plugin has `Open()`ed the layout for them.

## Classes every layout understands

| Class | Where | Meaning |
|---|---|---|
| `open` | `.lx-drop` panels (ann, toast, rank, rc, mf, mc) | shown; fades/drops in, removing it fades out |
| `shown` | windows `adm`, `knife` and their dims `adm_dim`, `knife_dim` | open; the plugin also captures the mouse |
| `hidden` | anything | collapsed out of layout (unused rows, other pages, optional pieces) |
| `sel` | list rows, map tiles | the picked one (crimson bar) |
| `active` | tabs, filter chips | the current one |
| `mine` | ban rows, knife cards | the viewer's own (their ban, their vote) |
| `disabled` | action buttons | no permission; clicks are ignored server-side anyway |
| `p0` `p5` … `p100` | `*_fill`, `*_lost` | bar width in % (remove the old one when setting a new one) |
| `rank-1` … `rank-18` | emblem panels | the rank emblem |
| `tier-recruit` `tier-operator` `tier-vanguard` `tier-ace` `tier-apex` `tier-legacy` | rank-name labels | rank colour, only next to an emblem |
| `side-t` `side-ct` | `knife_*_side` | team emblem |
| `ic-<name>` | icon panels (`ann_icon`, `toast_icon`, `ban_rowN_src`) | an icon from `addon/panorama/images/custom_game/legacyx/icon_*.png`, plus `ic-logo` |

## legacyx_notify.xml

| Id | Kind | Content |
|---|---|---|
| `wc` | panel | welcome card, centre of the screen, shown for 7 s when a player joins; `open` |
| `wc_server` | text | the server's name, e.g. `LEGACY-X | MATCH #1` |
| `wc_name` | text | the player's name |
| `wc_emblem` | panel | `rank-N`; `hidden` for a player without a rank yet |
| `wc_rankrow` | panel | holds the rank and EXP; `hidden` for a player without a rank yet |
| `wc_rank` | text + class | rank name, `tier-*` |
| `wc_exp` | text | `1,540 EXP` |
| `ann` | panel | announcement from under the top bar; `open` |
| `ann_icon` | panel | `ic-*`, `ic-logo` or `rank-N` |
| `ann_title`, `ann_body` | text | e.g. `Season starts Monday` / `Ranks reset at 00:00` |
| `ann_count_box` | panel | `hidden` unless there is a countdown |
| `ann_count` | text | seconds left (`60`, `15`) |
| `toast` | panel | one line; `open`, `ok` for a green tick |
| `toast_icon` | panel | `ic-check`, `ic-circle-alert`, … |
| `toast_text` | text | `Skins updated.` |
| `rank` | panel | rank card, bottom left, at round start; `open` |
| `rank_emblem` | panel | `rank-N` |
| `rank_name` | text + class | rank name, `tier-*` |
| `rank_exp`, `rank_next`, `rank_togo` | text | `1,540 EXP`, `Vanguard III at 1,600`, `60 to go` |
| `rank_fill` | panel | `pNN` progress to the next rank |
| `rc` | panel | rank change after a match; `open` |
| `rc_old`, `rc_new` | panel | `rank-N` before / after |
| `rc_kicker` | text | `Rank up` / `Rank down` |
| `rc_name` | text + class | new rank, `tier-*` |
| `rc_body` | text | `Pro League is open to you now. 1,405 EXP · Vanguard II at 1,500` |
| `rc_unlock` | panel | `hidden` unless the change opens Pro League (Vanguard I) |

## legacyx_match.xml

Shown for every ranked match that ends (5x5, Pro League): victory, defeat, calibration, not counted.

| Id | Kind | Content |
|---|---|---|
| `mf` | panel | full card, centre, first seconds; `open` |
| `mf_mode` | text | `5x5 · de_dust2 · counted` |
| `mf_title`, `mf_score` | text | `Victory` / `16 : 11 · 14 / 9` |
| `mf_emblem` | panel | `rank-N` |
| `mf_delta` | text + class | `+18 EXP`; `up` (green), `zero` (grey), none for a loss |
| `mf_rank` | text + class | rank name, `tier-*` |
| `mf_exp` | text | `1,540 → 1,558 EXP`, or why it did not count |
| `mf_calib`, `mf_calib_text` | panel, text | `hidden` unless calibrating: `Calibration 3 / 10` |
| `mf_fill`, `mf_lost` | panel | `pNN` now / before (a loss shows the lost part lighter) |
| `mf_next`, `mf_togo` | text | `Vanguard III at 1,600`, `42 to go` |
| `mc` | panel | compact card, right, until the next match is live; `open` |
| `mc_title` | text | `Last match · Victory 16 : 11` |
| `mc_emblem`, `mc_rank`, `mc_delta`, `mc_exp`, `mc_fill`, `mc_next`, `mc_togo` | | as above |

## legacyx_knife.xml

Only for the team that won the knife round. Votes are secret: nothing shows counts or names.

| Id | Kind | Content |
|---|---|---|
| `knife`, `knife_dim` | window | `shown` for the 10 second vote |
| `knife_count` | text | `10` … `0` |
| `knife_fill` | panel | `p100` … `p0` |
| `knife_sub` | text | `Stay on Terrorists or switch to Counter-Terrorists. Most votes wins.` |
| `knife_stay`, `knife_switch` | panel | cards, no mouse: `sel` = highlighted (A / D), `mine` = confirmed (E) |
| `knife_stay_side`, `knife_switch_side` | panel | `side-t` / `side-ct` for the current and the other side |
| `knife_stay_sub`, `knife_switch_sub` | text | `Keep Terrorists · attack` / `Move to Counter-Terrorists · defend` |

A / D moves `sel` (starts on Stay), E sets `mine` on it; A / D again clears `mine` until the next E. Only confirmed votes count. Most votes wins; a tie or no votes is Stay. `.stay` / `.switch` typed in chat count as votes too. The other
team and the result go through `ann` in legacyx_notify.xml.

## legacyx_admin.xml (`!admin`)

| Id | Kind | Content |
|---|---|---|
| `adm`, `adm_dim` | window | `shown` while open |
| `adm_close` | **button** | close |
| `adm_tab_players` `adm_tab_server` `adm_tab_bans` `adm_tab_staff` | **button** | `active` on one |
| `adm_page_players` `adm_page_server` `adm_page_bans` `adm_page_staff` | panel | `hidden` on all but one |
| `adm_footer` | text | `srv-27015 · MANAGER` |

**Players.** `pl_count` text. Rows `pl_row0` … `pl_row11` (**button**, `hidden` until used, `sel` when picked)
with `pl_rowN_team`, `pl_rowN_name`, `pl_rowN_kd`, `pl_rowN_ping` (text) and `pl_rowN_rank` (`rank-N`).
Detail: `pl_av` (initials), `pl_name`, `pl_meta` (text), `pl_rank` (`rank-N`). Actions (**buttons**, `disabled`
without permission): `act_kick`, `act_ban`, `act_slay`, `act_respawn`, `act_gag`, `act_mute`, `act_silence`,
`act_team`. Ban length and reason are picked in chat or a follow-up step; this panel has no text entry.

**Server.** Map tiles (**buttons**, `sel` when picked): `map_de_dust2`, `map_de_mirage`, `map_de_inferno`,
`map_de_nuke`, `map_de_ancient`, `map_de_anubis`, `map_de_overpass`, `map_de_vertigo`, `map_de_train`, each
with `map_<id>_now` (`hidden` unless it is the current map). **Buttons** `srv_rr`, `srv_clean`, `srv_votemap`,
`srv_change` with its text `srv_change_text` (`Change to Mirage`).

**Bans** (every ban in the database, the viewer's own marked). Stats text `bans_active`, `bans_today`,
`bans_perm`, `bans_mine`. Filter **buttons** `bf_active`, `bf_mine`, `bf_lifted`, `bf_expired` and
`bs_ingame`, `bs_web`, `bs_discord` (`active`). Rows `ban_row0` … `ban_row7` (**button**, `hidden`, `sel`,
`mine`) with text `ban_rowN_name`, `_sid`, `_reason`, `_left`, `_by`, `_from`, bar `ban_rowN_bar`
(`hidden` for permanent) + `ban_rowN_fill` (`pNN` time left), `ban_rowN_you` (`hidden` unless yours),
`ban_rowN_src` (`ic-gamepad-2`, `ic-globe`, `ic-message-circle`). Detail text `bd_name`, `bd_sid`,
`bd_reason`, `bd_len`, `bd_left`, `bd_by`, `bd_from`, `bd_date`, `bd_scope`, bar `bd_fill`, **button**
`bd_unban`. Pager text `bans_page`, **buttons** `bans_prev`, `bans_next`.

**Staff.** **Button** `st_add`. Rows `st_row0` … `st_row7` (`hidden` until used) with `st_rowN_dot`
(`online`), text `st_rowN_name`, `st_rowN_role`, `st_rowN_servers`, **button** `st_rowN_remove` (`hidden`
where the viewer may not remove).
