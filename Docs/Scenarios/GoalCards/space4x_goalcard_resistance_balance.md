# Goal Card: Resistance Balance (Damage Type X)

Goal
- Validate that resistance modules meaningfully reduce damage from matching weapon types,
  without creating hard immunity.
- Compare fleet performance under controlled loadouts.

Setup
- Fleet A: hulls with X-resistant shielding and armor modules.
- Fleet B: same hulls without resist modules (baseline).
- Fleet C: attackers armed primarily with X damage weapons.
- Match fleet sizes, hull classes, and skill tiers.
- Two runs: A vs C and B vs C.

Variables
- Damage type X (rotate through each type as a separate scenario).
- Resist strength tiers (low, medium, high).
- Engagement range (short, medium).

Metrics / Telemetry (needs instrumentation)
- damage_taken_by_type: total and per-target.
- time_to_disable_ms: mean time to first disable / kill.
- shield_hp_curve / armor_hp_curve over time.
- effective_dps_after_resist.

Scoring
- Survival ratio: TTK(A vs C) / TTK(B vs C).
- Damage reduction ratio by type.

Pass / Fail
- A should survive longer than B against C by a meaningful margin (TBD).
- A should not be effectively invulnerable (TTK ratio upper bound TBD).

Notes
- Run with fixed seed and symmetric starting positions.
- Consider a control run vs non-X weapons to confirm specificity.
