# Goal Card: Targeting Under ECM / ECW / ECCM

Goal
- Validate targeting reliability under electronic warfare conditions.
- Confirm ECCM meaningfully restores lock quality without full immunity.

Setup
- Two fleets with identical hulls, sensors, and weapons.
- Fleet A: baseline (no ECM/ECW, no ECCM).
- Fleet B: ECM/ECW modules enabled.
- Fleet C: ECM/ECW + ECCM (countermeasures) enabled.
- Engagement at controlled ranges (short, medium, long).
- Fixed seed, fixed duration.

Variables
- ECM strength tier (low, medium, high).
- ECCM tier (low, medium, high).
- Range band (short, medium, long).
- Target density (sparse, moderate).

Metrics / Telemetry (needs instrumentation)
- lock_acquisition_time_ms (per target).
- lock_break_rate (per minute).
- time_locked_pct (percent of time with valid lock).
- shots_fired_while_locked / shots_fired_total.
- hit_accuracy_pct while locked vs unlocked.

Scoring
- Lock reliability score: weighted time_locked_pct and lock_acquisition_time_ms.
- Combat effectiveness score: hit_accuracy_pct and shots_fired_while_locked.

Pass / Fail
- ECM/ECW should degrade lock reliability vs baseline by a meaningful margin (TBD).
- ECCM should restore lock reliability vs ECM/ECW, but not exceed baseline by >X% (TBD).
- ECM/ECW should not fully break targeting (avoid near-zero lock time).

Notes
- If ECW is not stubbed, document current behavior explicitly and mark the card as blocked.
- Add a "no weapons" variant to isolate lock quality without combat noise.
