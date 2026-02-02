# Space4X MVP Pass Conditions (Headless Questions)

This sheet defines the micro-scenario gates used to tighten iteration on MVP systems. Questions are opt-in via `scenarioConfig.headlessQuestions`; the default question set remains sensors/comms/movement/mining only.

## Movement
- Question: `space4x.q.movement.turnrate_bounds`
- Scenarios: `space4x_turnrate_micro.json`, `space4x_arc_micro.json`
- Pass: `space4x.movement.turn_rate_failures == 0`, `space4x.movement.turn_accel_failures == 0`, and `space4x.movement.turn_sample_count > 0`.

## Mining
- Question: `space4x.q.mining.progress`
- Scenario: `space4x_mining_micro.json`
- Pass: `space4x.mining.gather_commands > 0` and (`space4x.mining.ore_delta > 0` or `space4x.mining.cargo_delta > 0` or `space4x.mining.pass > 0.5`).
- Fail if a `MINING_STALL` blackcat is emitted.

## Comms
- Question: `space4x.q.comms.delivery`
- Scenario: `space4x_comms_micro.json`
- Pass: `space4x.comms.sent > 0`, `space4x.comms.received > 0`, `space4x.comms.delivery_ratio >= 0.8`.

- Question: `space4x.q.comms.delivery_blocked`
- Scenario: `space4x_comms_blocked_micro.json`
- Pass: `space4x.comms.sent > 0`, `space4x.comms.received == 0`, and `space4x.comms.blocked_reason` in `[1..6]`.

## Sensors
- Question: `space4x.q.sensors.acquire_drop`
- Scenario: `space4x_sensors_micro.json`
- Pass: sensors beat configured and no `PERCEPTION_STALE`, `CONTACT_GHOST`, or `CONTACT_THRASH` blackcats.

## Crew seat selection
- Question: `space4x.q.crew.sensors_selection`
- Scenario: `space4x_crew_sensors_micro.json`
- Metrics: `space4x.crew.sensors.selection.*`
- Pass: `found_seat == 1`, `has_occupant == 1`, `injured_selected == 0`.

## Crew sensors causality
- Question: `space4x.q.crew.sensors_causality`
- Scenario: `space4x_crew_sensors_causality_micro.json`
- Metrics: `space4x.sensors.acquire_time_s.*`, `space4x.sensors.crew_factor.*`
- Pass: `acquire_time_s.injured > acquire_time_s.healthy` and `acquire_time_s.delta >= 1.5` seconds.

## Crew transfer
- Question: `space4x.q.crew.transfer`
- Scenario: `space4x_crew_transfer_micro.json`
- Metrics: `space4x.crew.transfer.pass` (preferred) or `space4x.ledger.transfer_*`
- Pass: `space4x.crew.transfer.pass == 1` OR `transfer_last_seen >= transfer_tick`.

## Collision phasing
- Question: `space4x.q.collision.phasing`
- Scenario: `space4x_collision_micro.json`
- Metrics: `space4x.collision.event_count`, `COLLISION_PHASING` blackcats
- Pass: no `COLLISION_PHASING` blackcats and `event_count > 0`.

## Proof toggles (headless)
- Crew seat: `SPACE4X_HEADLESS_CREW_PROOF=1` (default on unless explicitly set to 0).
- Crew causality: `SPACE4X_HEADLESS_CREW_CAUSALITY_PROOF=1` (enabled in headless task for the causality micro).
- Crew transfer: `SPACE4X_HEADLESS_CREW_TRANSFER_PROOF=1` (default on unless explicitly set to 0).
