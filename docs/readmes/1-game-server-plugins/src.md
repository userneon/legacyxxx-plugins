> **Хуулбар.** Эх сурвалж: [legacyxxx-plugins/src/README.md](https://github.com/userneon/legacyxxx-plugins/blob/main/src/README.md), 2026-10-02-д хуулсан. Зөв, шинэ хувилбар нь эх repo дээр. Харьцангуй холбоосууд ажиллахгүй байж болно.

# Source Map

The canonical source folders are now the LEGACY-X runtime module names. The `src/` directory remains an architecture index; CounterStrikeSharp projects live at repository root to preserve their upstream-relative asset and documentation layout.

| Target source area | Canonical module path |
|---|---|
| `src/LegacyX-Admin/` | `LegacyX-Admin/` |
| `src/LegacyX-MatchZy/` | `LegacyX-MatchZy/` |
| `src/LegacyX-AFKManager/` | `LegacyX-AFKManager/` |
| `src/LegacyX-Community/` | `LegacyX-Community/` |
| `src/LegacyX-Spectator/` | `LegacyX-Spectator/` |
| `src/LegacyX-WeaponPaints/` | `LegacyX-WeaponPaints/` |

The former minimal AdminPlus bridge is removed. `LegacyX-Admin` is the complete upstream-derived in-game admin owner.
