# Goal Card: Capital Ship Shooting Range

Goal
- Demonstrate crew skill differentiation under identical hardware conditions.
- Elite > veteran > experienced >= rookie in reaction time, precision, and efficiency.

Setup
- Same capital ship hull and loadout for all runs.
- One spinal mount weapon with high accuracy and low spread.
- Crew tiers: rookie, experienced, veteran, elite.
- Target practice drones with predictable but non-trivial motion.
- Runs use a fixed duration and fixed seed for repeatability.

Variables
- Crew tier (primary).
- Orientation/rotation profile (slow drift, rapid yaw, target crossing angles).
- Target density and speed (low, medium, high).

Schedule Regime
- Time base: scenario (fixed duration).
- Profiles: crew roles stay on station; gunnery seat active throughout.
- Training windows: warm-up (no scoring) first 10s, live-fire scoring window, cooldown last 5s.
- Attendance rules: if any seat missing, mark run invalid.
- Reuse notes: apply to other gunnery and target acquisition drills.

Metrics / Telemetry (needs instrumentation)
- reaction_time_ms: time from target acquisition to first shot.
- hit_accuracy_pct: hits / shots.
- time_to_kill_ms: mean time to kill per target.
- shots_per_kill: mean shots per target destroyed.
- energy_spent_per_kill: optional efficiency signal.

Scoring
- Weighted composite score, e.g. 40% accuracy, 30% reaction, 20% TTK, 10% efficiency.

Pass / Fail
- Ordering: elite > veteran > experienced >= rookie for composite score.
- Minimum separation thresholds (TBD) to avoid ties that are too close to call.

Notes
- If the rotation profile makes aiming impossible, adjust drone movement or weapon stabilization.
- Add a "steady" variant to validate baseline aim behavior.
