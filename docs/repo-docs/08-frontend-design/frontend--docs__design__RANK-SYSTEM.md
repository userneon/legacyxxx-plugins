> **Хуулбар.** Эх сурвалж: [legacyxxx-frontend/docs/design/RANK-SYSTEM.md](https://github.com/userneon/legacyxxx-frontend/blob/main/docs/design/RANK-SYSTEM.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# Legacy-X rank system (v1.0)

One number per player: **EXP**. It goes **up and down** after every ranked match, so it
measures skill, not time played. Rank = the highest threshold your EXP has reached.

- Applies to: **5x5 Matches** and **Pro League**. **Fun Mode never changes EXP.**
- Lifetime (no seasons). Everyone starts at **1000 EXP** (Operator I).
- Computed in the backend (TypeScript). Applied atomically in one SQL function
  per match (receipt + `exp = exp + delta` for all players, idempotent by event id).

---

## 1. EXP change per match

```
ΔEXP = round( C × ( Result + Margin + Performance + Bonus ) )
```
then clamped to **−45 … +45** (−90 … +90 during calibration).

### Result — who won, against whom (Elo)
```
E      = 1 / (1 + 10^((R_opp − R_team) / 400))
Result = 30 × (S − E)
```
- `R_team`, `R_opp` = average EXP of each team's players **at match start**.
- `S` = 1 win · 0.5 draw · 0 loss.
- Beat a stronger team → big gain. Lose to a weaker team → big loss.
  Equal teams → ±15.

### Margin — how convincing
```
Margin = clamp( (rounds_won − rounds_lost) / 4 , −3 , +3 )
```

### Performance — how you played **compared to this lobby**
Per player, per round:
```
score = ( 2·Kills + 1·Assists − 1·Deaths + 1·EntryKills + 1·(Plants + Defuses)
          + 1·3K_rounds + 2·4K_rounds + 3·Aces + 2·Clutches_won ) / rounds_played
z           = (score − lobby_mean) / max(lobby_std, 0.05)     # over all human players
Performance = clamp( round(8 × z) , −8 , +8 )
```
- Relative to the other players in the same match, so carrying a weak lobby and
  holding your own in a strong lobby are both rewarded fairly.
- Headshots are **not** in the score (style, not impact). HS % is shown on profiles only.

### Bonus — small highlights (max +2 total)
- +1 for an ace or a 1v3+ clutch won.
- +1 for match MVP (highest `score` in the lobby).

### C — calibration
- `C = 2` for a player's **first 10 ranked matches** (places new players fast).
- `C = 1` afterwards.

---

## 2. Special cases

| Case | Rule |
|---|---|
| Player leaves and doesn't return within the Match Core rejoin window | ΔEXP = **−25** fixed (no performance), counted as a loss |
| Your team played ≥ 3 rounds short-handed because of a leaver | Your Result loss is **halved**; gains unchanged |
| Match not valid (see 3) | ΔEXP = 0 for everyone; match still saved in history |
| EXP would go below 0 | Floor at 0 |
| Bots | Never count as players, never in lobby averages |


## 2b. Daily gain cap (anti-farming)

Limits use Ulaanbaatar time (UTC+8, no daylight saving). A player's first **150 EXP gained in a day** (00:00 to
00:00) count in full. Any gain past that counts for a quarter (a 20 EXP gain becomes 5; a gain that crosses the
line is split: the part under it in full, the rest for a quarter). The **weekly limit is a hard stop**: once a
player has gained **600 EXP in a week** (Monday 00:00 to Sunday night, quarter-value gains included), further
gains are 0 until Monday. `exp_breakdown.limited` is `"daily"` or `"weekly"` on any reduced gain. Losses are
never reduced, and the limits still apply at Legacy so the top of the leaderboard cannot be farmed. Ranks are
never reset. This makes farming with friends (win-trading, feeding kills) slow.

## 3. When a match counts (valid)

All must be true:
1. The match finished normally (not cancelled / server crash).
2. **At least 8 human players** were in the match at the end (not strictly 10 —
   the old "exactly 10 original players, no fills" rule meant almost no match ever counted).
3. At least **13 rounds** were played.
4. For each player: present for **≥ 50 % of rounds**, otherwise ΔEXP = 0 for that player
   (unless they are the leaver in section 2).

Fills (players who joined mid-match) count normally for the rounds they played.

---

## 4. Ranks and thresholds

Rank names are original to Legacy-X (tiers: Recruit · Operator · Vanguard · Ace · Apex · Legacy).
Tier colors follow the CS2 rarity ladder, so rarer color = higher rank:
Recruit #b0c3d9 (Consumer) · Operator #5e98d9 (Industrial) · Vanguard #4b69ff (Mil-Spec) ·
Ace #8847ff (Restricted) · Apex #eb4b4b (Covert) · Legacy #e4ae39 (Extraordinary gold).
Ace II, Apex and Legacy emblems have a soft glow.
The old CS:GO names (Silver … Global Elite) are no longer used anywhere.

### Table 

| # | Rank | Tier emblem | Min EXP |
|---|---|---|---|
| 1 | Recruit I | circle + stars | 0 |
| 2 | Recruit II | circle + stars | 600 |
| 3 | Recruit III | circle + stars | 700 |
| 4 | Recruit IV | circle + stars | 800 |
| 5 | Recruit V | circle + stars | 900 |
| 6 | Recruit VI | circle + stars | 950 |
| 7 | Operator I | hexagon + star + small stars | **1000 — start** |
| 8 | Operator II | hexagon + star + small stars | 1100 |
| 9 | Operator III | hexagon + star + small stars | 1200 |
| 10 | Operator IV | hexagon + star + small stars | 1300 |
| 11 | Vanguard I | shield + chevrons | **1400 — Pro League unlocks** |
| 12 | Vanguard II | shield + chevrons | 1500 |
| 13 | Vanguard III | shield + chevrons | 1600 |
| 14 | Vanguard IV | shield + chevrons | 1700 |
| 15 | Ace I | diamond + wings | 1800 |
| 16 | Ace II | diamond + wings | 1950 |
| 17 | Apex | octagon + star | 2100 |
| 18 | Legacy | solid disc + star | 2300 |

- The current thresholds (0 … 67 000) were built for "+500 per win, never lose EXP".
  They must be replaced with the table above.
- **Pro League access** unlocks at 1400 and is only removed if EXP falls below
  **1350** (50 EXP buffer so players don't flip in and out).

Emblems: `ranks/rank-01.svg` … `rank-18.svg` (shape = tier, marks = step, brightness rises with rank).

## 5. Worked example

Team A (avg 1200) beats Team B (avg 1300) 13–9.

`E_A = 1 / (1 + 10^(100/400)) = 0.36`

| Player | Result | Margin | Performance | Bonus | ΔEXP |
|---|---|---|---|---|---|
| A, top fragger (z = +1.2) | 30 × (1 − 0.36) = +19.2 | +1 | +8 (cap) | +1 MVP | **+29** |
| A, weakest (z = −1.0) | +19.2 | +1 | −8 | 0 | **+12** |
| B, best player (z = +1.5) | 30 × (0 − 0.64) = −19.2 | −1 | +8 | 0 | **−12** |
| B, weakest (z = −1.2) | −19.2 | −1 | −8 (cap) | 0 | **−28** |

Winning always helps, losing always hurts — but how you played moves it by up to ±10.

## 6. What players see

After each match, the match row and the profile show the change with its breakdown:
`+18  ·  Win vs stronger team +17  ·  Margin +2  ·  Performance −1`
Store per player on the match snapshot: `exp_before`, `exp_delta`,
`exp_breakdown` (jsonb: result, margin, performance, bonus, calibration).

## 7. Data the plugin must send per player per match

steam_id, team, rounds_played, kills, deaths, assists, entry_kills,
3k/4k/5k round counts, clutches_won (with 1vX size), bomb_plants, bomb_defuses,
left_early (bool) + left_at_round. Match: rounds per team, total rounds, finished
normally (bool), human player count at end.
If the plugin doesn't send a field yet, drop that term from `score` (don't guess it)
and list what's missing.

## 8. Launch migration

Every existing player starts at 1000 EXP / Operator I (progression tables are empty,
so nothing is lost). Replace competitive_rank_definitions.minimum_exp, slug and display_name with section 4.
