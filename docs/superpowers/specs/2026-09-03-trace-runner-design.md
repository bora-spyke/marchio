# Trace Runner prototype — implementation design (from spec v2.1)

Date: 2026-09-03. Source: `~/Downloads/trace_runner_prototype_spec.md`. Approved scope: Bora, "go with what you recommend".

## Decisions on open spec points

| Topic | Decision |
|---|---|
| Pickups, Magnet, mini radar | Cut for v1. Spec's own numbers say pickups are 25% of threshold; cutting removes one system, one card, one widget. Re-add if time. |
| Fill upgrade pool | Reuse the 6 existing cards (Fill Damage, Burning, Freeze, Electric Border, Heal, Bigger Multiplier), cap 2 each. Real choice instead of spec's 3-of-3. |
| Detonate / Exploding Area | Dropped. Closing an area already damages everything inside; the card would do nothing. |
| Pre-level power-up pool | 6 cards: Overload, Rapid Feed, Live Wire, Ricochet, Devil's Bargain, Iron Hull (backfill: +20% max HP, cap 2). Half mechanical. |
| Fast enemy | Kept in spawn mix (already built). Boss reused as the 80% elite wave, without arena clamp, HP scaled down. |
| Turn rate rows | Not applicable. Player is joystick direct-steer with accel/decel. Step 4 gives HP +25% only. |
| Two upgrade screens per level | Per spec: Fill upgrade on clear (levels 1-4), power-up before levels 2-5. Tune later. |
| Trophy Road persistence | PlayerPrefs. Main screen has a small Reset for pitch demos. |
| Step 3 trail width | One multiplier: line width, close radius, cut tolerance ×1.5; max trail length ×1.3. |
| UI language | English everywhere. |
| Monetization hooks | Only `Run.AddScore(source, amount)` single entry point. Rest skipped. |
| Victory lap end | Level 5 has no threshold; ends after `victoryLapDurationS` (30s). |
| Score units | 1 spec "unit²" ≈ 10 px². Tiers in px²: <4000 ×1.0, <12000 ×1.5, <25000 ×2.5, else ×4.0. |

## Architecture

- `RunPreset` (ScriptableObject): every number from spec §12 plus trophy nodes, spawn phases, score tiers. Assets: `DEMO_SHORT`, `DEMO`, `LIVE`. `GameManager.preset` selects.
- `Run` (plain class, owned by GameManager): level index, level score, threshold, level time, streak, revives left, elite flag. `AddScore(ScoreSource, raw)` is the only score entry; it weights by source, adds to level score and banks to `TrophyRoad` immediately.
- `TrophyRoad` (plain class): persistent cumulative score (PlayerPrefs), node table from preset, `Pending` nodes applied only during Play so no unlock fires on the clear screen. Exposes multipliers: damage, max HP, speed, trail width/length, extra revives, cart tier, skin/trail variant.
- `CardPool` (plain class): card defs with caps, `RollThree` drops capped cards. Two instances in `UpgradeManager`: `Fill` and `Power`. Both lost on death.
- `WaveManager` rewritten: continuous player-relative spawning, rate ramps by phase, linear rise after last phase, capped at `spawnRampCapS`; composition weights per phase; elite spawned once at 80% threshold; victory lap uses `victoryLapDensity`. `WaveTableSO` deleted.
- `Enemy`: new `SeekTrail` behavior (Type C "Breaker") targets nearest open trail point. Any enemy touching the open trail cuts it: player takes `trailCutDamage`, streak resets. Live Wire applies burn on touch before the cut.
- `AutoAttack`/`Projectile`: fire rate ÷ (1 + 0.5·RapidFeed), damage × DamageMult, Ricochet retargets nearest other enemy `bounces` times.
- `GameMode`: Menu, Play, LevelClear, FillUpgrade, PowerUp, Fail, Victory.
- Flow: Menu → Play → (threshold) LevelClear → FillUpgrade → PowerUp (from level 2) → Play. HP=0 → Fail: Revive (50% HP, restart level, keep stack + bank) or Retry (Menu, run stack reset). Heal 50% after clearing levels 2 and 4.
- UI Toolkit only. Shared builders in `UiKit`: HP bar, threshold bar, trophy bar, stack row, card. Screens: main (trophy road, two-track layout with empty premium column, play, reset, cart preview), HUD (HP, score-to-threshold with 85% pressure, stack row, unlock toast), level clear (tally → +35% line → trophy bar → stack → HP/heal → continue), fill upgrade, power-up, fail (run score, trophy bar, stack going dark, bonus missed, revive/retry), victory (total, 4 macro rewards, main menu).

## Art team list (what the build will show as placeholders)

Priority order. Everything below renders today as primitives / colored circles / text and can be swapped without code changes as long as the prefab keeps logic on the root and meshes under `Visual`.

1. **Carts (2)**: base cart, Step 4 cart. Placeholder: cylinder + nose cube, scaled up for tier 2.
2. **Cart looks (3)**: Step 1 skin, micro A color variant, micro D trail color variant. Placeholder: material color swaps.
3. **Enemy Breaker (Type C)**: new silhouette, must read as "goes for your line". Placeholder: cube variant with new color. Chaser, Fast, Ranged, Boss already have primitives.
4. **Card icons (12)**: Fill: Fill Damage, Burning Fill, Freeze Fill, Electric Border, Heal Fill, Bigger Multiplier. Power: Overload, Rapid Feed, Live Wire, Ricochet, Devil's Bargain, Iron Hull. Devil's Bargain needs a distinct frame. Placeholder: colored circles.
5. **Trophy Road nodes**: macro node (large), micro node (small), claimed/unclaimed states, 8 nodes on main screen, 4 macro reward illustrations for the victory screen. Placeholder: shapes + text.
6. **Screens (7 layouts)**: main, HUD, level clear, fill upgrade, power-up, fail, victory. Bars and cards are shared components.
7. **FX**: unlock celebration, level clear, trail cut, elite spawn. Placeholder: particle bursts.

Not needed (cut): pickup orb, heal pickup, radar.
