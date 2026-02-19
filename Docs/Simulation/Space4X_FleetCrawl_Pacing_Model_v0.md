# Space4X FleetCrawl Pacing Model v0

Status: Draft for implementation
Owner: Iterator desktop lane
Date: 2026-02-19

## Goal
Allow long-form room play (including very long holds) without hard exits, while preserving run-level stakes through escalating pressure and persistent delay debt.

## Design Intent
- No hard or soft forced room timeout.
- Pressure rises the longer a player stays in a room.
- Delay has persistent consequences across later rooms.
- Very strong builds can still hold for extended periods.
- Meta progression can unlock counterplay against pressure/debt.

## Core Variables
- `room_time_s`: Seconds spent in current room.
- `difficulty_tier`: 1..5.
- `room_pressure`: Per-room escalating pressure scalar.
- `run_debt`: Persistent run-wide escalation debt.
- `meta_pressure_resist`: Meta progression multiplier reducing pressure contribution.
- `meta_debt_control`: Meta progression multiplier reducing debt accrual and/or enabling debt burn.

## Baseline Targets
Use a long-form baseline around 30 minutes per room, offset by difficulty:
- Tier 1: 36 min target (`2160s`)
- Tier 2: 32 min target (`1920s`)
- Tier 3: 30 min target (`1800s`)
- Tier 4: 27 min target (`1620s`)
- Tier 5: 24 min target (`1440s`)

## Pressure Curve
Pressure is intentionally mild at first, then increasingly menacing.

`room_pressure_raw = f(room_time_s)`

Suggested piecewise form:
- Phase A (0 to grace): low slope
- Phase B (post-grace): linear increase
- Phase C (late): super-linear surge

Example implementation:
- `grace_s = 480`
- `late_start_s = 1800`
- `phase_a = min(room_time_s, grace_s) * 0.00035`
- `phase_b = max(0, room_time_s - grace_s) * 0.00095`
- `late_t = max(0, room_time_s - late_start_s)`
- `phase_c = pow(late_t / 600.0, 1.45) * 0.65`
- `room_pressure_raw = phase_a + phase_b + phase_c`

Apply meta and clamp:
- `room_pressure = clamp(room_pressure_raw * (1 - meta_pressure_resist), 0, pressure_cap)`
- `pressure_cap` initial recommendation: `6.0`

## Run Debt
Debt accrues only when room time exceeds target for that difficulty.

- `over_target_s = max(0, room_time_s - target_room_time_s[difficulty_tier])`
- `run_debt += over_target_s * debt_gain_per_second[difficulty_tier] * (1 - meta_debt_control)`

Optional passive debt decay between rooms:
- `run_debt = max(0, run_debt - debt_decay_between_rooms)`

Optional active debt burn mechanics (meta unlock):
- Completing high-risk objectives can subtract debt.
- Boss clears can subtract fixed debt chunks.

## Threat Mapping
Map pressure + debt to spawn budget and hostility, without hard fail gates.

- `threat_scalar = 1 + room_pressure * k_pressure + run_debt * k_debt`
- `wave_budget_mult = clamp(threat_scalar, 1.0, wave_budget_cap)`
- `elite_chance = base_elite + room_pressure * elite_from_pressure + run_debt * elite_from_debt`
- `hazard_rate_mult = 1 + room_pressure * hazard_from_pressure + run_debt * hazard_from_debt`
- `reward_decay_mult = 1 / (1 + reward_decay_from_pressure * room_pressure)`

Initial coefficients:
- `k_pressure = 0.22`
- `k_debt = 0.18`
- `wave_budget_cap = 4.5`
- `base_elite = 0.05`
- `elite_from_pressure = 0.04`
- `elite_from_debt = 0.03`
- `hazard_from_pressure = 0.12`
- `hazard_from_debt = 0.08`
- `reward_decay_from_pressure = 0.09`

## Player-Facing Clarity
Expose pressure/debt in HUD so escalation is legible and strategic:
- Pressure meter: `Calm -> Rising -> Menacing -> Cataclysmic`.
- Debt marker: run-level icon with numeric tier.
- Tooltip: explains that delay now raises future room intensity.

## Meta Counterplay Hooks
Future progression can meaningfully push back:
- `Pressure Dampers`: reduce `meta_pressure_resist` cost-effectively.
- `Debt Purge`: converts resources/objectives into debt reduction.
- `Wallbreaker Protocol`: temporary cap increase on outgoing power under high pressure.
- `Stability Lattice`: lowers late-phase curve exponent.

## Acceptance Criteria (v0)
- Runs are never forcibly ended by room timer alone.
- Threat clearly escalates with prolonged room time.
- Long delays in earlier rooms measurably increase later-room threat.
- Very strong builds can sustain long rooms but face rising pressure/debt costs.
- Meta progression can introduce at least one debt-control and one pressure-control tool.

## Non-Goals (v0)
- No physical room graph/backtracking implementation in this slice.
- No forced extraction/evacuation mechanics in this slice.
