# Space4X Solar System Expansion Architecture

Status: Draft v1 (pre-implementation planning)
Owner: Space4X gameplay + simulation
Date: 2026-02-18
Orbit/surface continuum: `Docs/Simulation/Space4X_Orbit_To_Surface_Continuum_Contract.md`

## 1) Objective

Expand the current minimal solar-system slice into a livelier, larger-feeling system with:

- Stations, asteroid fields, hidden loot caches, and richer points of interest.
- Strong sense of scale from deep-space to close approach.
- Very large planetoids that feel massive and can host entities/interactions.

The design must remain deterministic, DOTS-friendly, and performant in FleetCrawl runtime.

## 2) Hard Constraints

- Keep simulation deterministic for seeded scenarios and headless checks.
- Avoid global full-fidelity simulation of the entire system each tick.
- Preserve existing reference-frame and orbital contracts in `Docs/Simulation/Orbits_ReferenceFrames.md`.
- Keep presentation concerns separate from simulation truth.

## 3) Target Experience

- Entering a system should immediately surface multiple meaningful objects (stations, fields, anomalies, caches).
- Travel should imply distance and traversal commitment, not a small arena feel.
- Large bodies should dominate framing at approach and support local gameplay context.
- Discovery should reward scouting (hidden caches/derelicts/restricted orbitals).

## 4) Architecture Pillars

### A. Hierarchical World Scale

Adopt explicit simulation tiers and activation scopes:

1. Deep System Tier
   - Far objects represented as coarse state only (orbit, ownership, high-level activity).
2. Orbital Tier
   - Active orbitals and POIs around target bodies/systems.
3. Local Bubble Tier
   - Full movement/combat/interaction simulation near player-relevant entities.
4. Surface/Planetoid Local Tier
   - Optional local patches/anchors for entities attached to large bodies.

Rule: only local bubble(s) run full-cost systems; higher tiers remain on-rails or aggregated.

### B. Precision + Frame Ownership

- Authoritative orbital/reference-frame state remains high precision.
- `LocalTransform` stays derived presentation-space state.
- Floating origin/origin shifting remains presentation-only.
- SOI and frame transitions remain continuity-preserving with hysteresis.

### C. Content Density Model (Liveliness)

Introduce deterministic seeded content layers:

- Orbital infrastructure: station/outpost/starbase clusters by ring/faction profile.
- Resource geography: asteroid belts, comet lanes, super-resource pockets.
- Discovery content: hidden caches, derelicts, mission-bearing satellites.
- Hazard content: storms, hazard zones, unstable anomalies.

Use rule-weighted seeded placement with minimum spacing constraints to avoid overlap/noise piles.

### D. Massive Bodies and Approach Illusion

Large bodies must be simulation entities with staged representation:

- Far: low-cost proxy and strategic metadata.
- Mid: band-level gravity/traffic/POI context.
- Near: local patches/anchors for attachable entities and surface-adjacent gameplay.

Objects can exist "on" large bodies via anchored local frames/patch roots, not by forcing single giant dynamic meshes.

### E. Streaming + Activation

- Stream in content by tier and distance/intent.
- Prefer enabling/disabling behavior over expensive structural churn where possible.
- Batch structural changes via ECB.
- Keep static visual density heavily instanced.

### F. Debug Mode Compatibility (LOD Off)

Since LOD is intentionally disabled for debugging right now:

- Keep a debug-safe profile with capped spawn counts and strict CPU budgets.
- Build all systems with runtime toggles so content scale-up is not blocked by temporary LOD state.
- Require a production profile to re-enable LOD/streaming policy before content maxima are raised.

## 5) Data and System Additions (Conceptual)

- `SolarSystemContentProfile` singleton (global tuning profile).
- `OrbitalSpawnBand`/`OrbitalSpawnSeed` data for deterministic placement.
- `HiddenCache` component/state (discoverable/claimed/respawn policy).
- `PlanetoidBody` + `PlanetoidPatchAnchor` for massive body local anchoring.
- `ActivationTier` tag/state for deep/orbital/local gating.

Exact component names can change during implementation; behavior contract is the priority.

## 6) Interaction Contract for New Objects

- Stations: docking, market, mission hooks, faction ownership.
- Asteroid fields: mining density + navigation hazard modifiers.
- Hidden caches: scanner/discovery loop, one-time or timed refresh rewards.
- Planetoids: gravity/approach effects, local anchored entities, future colony hooks.

## 7) Performance and Determinism Gates

- Determinism: same seed + same tick budget -> same spawned system layout.
- Activation: off-bubble entities do not run high-cost combat/movement loops.
- Structural stability: no per-frame mass add/remove oscillation.
- Presentation: large-body approach shows monotonic scale/readability improvement.

## 8) Feasibility

Yes, this is achievable in current architecture.

Primary risk is scope creep from "simulate everything always." The plan avoids that by enforcing tiered activation, on-rails far simulation, and deterministic data-driven spawning.

## 9) External Best-Practice Inputs (for design rationale)

- Hierarchical streaming and section activation in Unity Entities.
- Deterministic seeded procedural placement with spacing constraints.
- High-precision frame state plus local-space presentation transforms.
- Instancing-first visual density, with LOD/streaming profiles separated from debug mode.
