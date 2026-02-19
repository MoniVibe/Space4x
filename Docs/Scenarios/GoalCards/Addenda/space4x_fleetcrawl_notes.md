# Addendum: FleetCrawl Room and Mutation Notes
Date: 2026-02-19
Owner: shonh

## Room Archetypes (v0)
- `resource_room`: mining and extraction pressure, lower hostile intensity.
- `derelict_room`: salvage-adjacent pressure with unstable hostile spikes.
- `swarm_room`: combat-heavy survival room with low extraction opportunity.

## Equipment Gate Matrix (Design Contract)
- Mining tools and cargo modules gate `resource_room` efficiency.
- Scanner/sensors bias gate scouting certainty and room risk reads.
- Combat modules and escort count gate `swarm_room` survivability.
- Tradeoff rule: each combat-centric swap reduces cargo throughput budget.

## Cargo vs Lethality Tradeoff
- Cargo-forward craft should clear objective throughput but suffer under intercept.
- Lethality-forward escorts should stabilize combat but carry less objective value.
- Run value target: mixed composition should outperform single-extreme builds over many rooms.

## Flagship Segment Mutation (Contract-First)
- Source channels: loot, mission rewards, caches.
- Segment classes: cargo spine, armor spine, weapon spine, utility spine.
- Constraint bundle: power, mass, hardpoint compatibility, cargo floor.
- Mutation checkpoint: apply between rooms, never mid-room for v0.

## Progression and Meta Hooks
- Meta unlocks expand available segment class pools and module quality bands.
- Room clear bonuses should feed mutation choices rather than only raw currency.
- High-risk room completion should increase rare segment drop chance.

## Projectile and Combat Slice Tie-In
- FleetCrawl must exercise existing projectile combat and attack-run telemetry.
- Combat room tuning should preserve deterministic replay under fixed seed.
- Follow-up: add FleetCrawl-specific projectile stress variants after baseline pack is stable.

## Follow-Up Queue
- Add FleetCrawl question IDs (`space4x.q.fleetcrawl.*`) once v0 behavior is stable.
- Add explicit room chain state and fail-forward run summary fields.
- Add flagship segment mutation runtime application and telemetry evidence.
- Keep v1 room contract tuned in `Assets/Scenarios/space4x_fleetcrawl_room_contract_v1.json`.
- Keep demo scenario aligned in `Assets/Scenarios/space4x_fleetcrawl_room_contract_demo_micro.json`.
