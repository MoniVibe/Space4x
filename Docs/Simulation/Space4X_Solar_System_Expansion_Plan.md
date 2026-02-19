# Space4X Solar System Expansion Execution Plan

Status: Draft v1 (planning only, no execution yet)
Date: 2026-02-18
Depends on: `Docs/Simulation/Space4X_Solar_System_Expansion_Architecture.md`
Continuum contract: `Docs/Simulation/Space4X_Orbit_To_Surface_Continuum_Contract.md`

## 1) Plan Goal

Implement the solar-system expansion in controlled phases so each architecture point is validated before full rollout.

## 2) Work Phases

### Phase 0 - Baseline and Guardrails

Scope:

- Capture current baseline metrics (entity counts, sim cost, frame cost, tick determinism hash).
- Define debug profile (LOD off) and production profile (LOD/streaming on).
- Lock target budgets per tier.
- Use checklist contract in `Docs/Simulation/Space4X_Solar_System_Phase0_Checklist.md`.
- Use target scenario `Assets/Scenarios/space4x_solar_phase0_baseline.json`.

Acceptance:

- Baseline report committed.
- Debug and production profiles selectable at runtime/config.
- No behavioral drift in current scenarios.
- Required Phase 0 question pack passes (`mining.progress`, `perf.summary`, `perf.budget`).

### Phase 1 - Hierarchical Activation Framework

Scope:

- Introduce activation tiers for deep/orbital/local contexts.
- Gate expensive systems to local bubble only.
- Keep far entities on-rails/aggregated updates.

Acceptance:

- Off-bubble combat/movement systems do not execute for far entities.
- Behavior near player remains unchanged.
- Deterministic tick results preserved.

### Phase 2 - Precision and Reference-Frame Hardening

Scope:

- Audit all new solar content to ensure frame-safe position ownership.
- Ensure transitions between tiers/frames preserve continuity.
- Add tests for round-trip transforms and transition invariants.

Acceptance:

- No visible jitter regressions during bubble transitions.
- Determinism tests pass for repeat runs with same seed.
- Frame continuity assertions pass.

### Phase 3 - Liveliness Content Generation v1

Scope:

- Add deterministic seeded spawning for:
  - Stations (ownership + role mix)
  - Asteroid fields (density bands)
  - Hidden loot caches (scanner/discovery gated)
  - Hazard/anomaly points
- Add spacing and density controls per ring/band.

Acceptance:

- Generated systems show non-trivial object variety in each seeded run.
- Same seed reproduces same layout and object classes.
- Spawn density remains within budget caps.

### Phase 4 - Massive Planetoid Representation

Scope:

- Add `PlanetoidBody`-class data and staged representation.
- Implement approach-dependent representation tiers (far/mid/near).
- Introduce planetoid patch anchors for hosted entities/interactions.

Acceptance:

- Planetoids read as massive during approach.
- Anchored entities remain stable relative to body frame.
- No large-coordinate precision artifacts in near gameplay bubble.

### Phase 5 - Interaction Layer

Scope:

- Stations: docking + mission + market hooks standardized.
- Asteroid fields: mining/hazard modifiers.
- Hidden caches: discovery, claim, reward lifecycle.
- Planetoid-local entities: attach/spawn/despawn policy.

Acceptance:

- All new object classes support at least one meaningful gameplay interaction.
- Discovery loop is testable in headless and playable slices.
- No interaction components break existing scenario startup.

### Phase 6 - Streaming and Density Scaling

Scope:

- Add content streaming/activation windows by tier and distance.
- Increase content density gradually with profile-based caps.
- Keep static visual populations instancing-first.

Acceptance:

- Performance remains within agreed budgets at target density.
- No frame spikes from mass structural churn.
- Profile switch between debug and production is stable.

### Phase 7 - Scenario and Tooling Integration

Scope:

- Extend scenario JSON schema with solar-system expansion profile fields.
- Add authoring defaults for quick iteration.
- Add diagnostics and reports for content counts and activation-tier distribution.

Acceptance:

- Scenario authors can tune system liveliness without code edits.
- Diagnostics provide clear visibility into per-tier counts and costs.
- Existing scenarios continue to load.

### Phase 8 - Final Validation and Rollout

Scope:

- Run presentation and headless contract checks on expansion scenarios.
- Compare baseline vs post-expansion metrics.
- Roll out in staged content packs if needed.

Acceptance:

- Determinism and performance gates pass.
- Visual/readability goals pass manual review.
- Rollout checklist signed off.

## 3) Orbit-to-Surface Continuum Workstream (Appended)

These are concrete execution steps for continuous orbit -> approach -> surface play space.

1. Step C1 - Add continuum config and tier definitions
- Deliverables:
  - `Space4XContinuumConfig` singleton with tier radii and hysteresis.
  - `Space4XContinuumTier` enum/state component.
- Acceptance:
  - Tier classification is deterministic from planet-relative radius ratio.

2. Step C2 - Add planet + surface patch frame ownership
- Deliverables:
  - `PlanetoidBody` metadata component.
  - `SurfacePatchAnchor` entities with local patch frame data.
- Acceptance:
  - Surface-hosted entities can be expressed in patch-local meters.

3. Step C3 - Build tier classification and transition systems
- Deliverables:
  - Classification system from frame position to tier.
  - Transition system with enter/exit hysteresis thresholds.
- Acceptance:
  - No tier thrash near boundaries under normal movement.

4. Step C4 - Add tier-based simulation gating
- Deliverables:
  - Enableable-gated high-cost systems for non-local tiers.
  - Explicit local-bubble requirements for combat-heavy updates.
- Acceptance:
  - Off-tier entities do not run full-cost combat/movement loops.

5. Step C5 - Add promotion/demotion adapters across tiers
- Deliverables:
  - Proxy representation for far-tier entities.
  - Promotion path from proxy -> full entity behavior near active zones.
- Acceptance:
  - Entity identity and authority remain stable through transitions.

6. Step C6 - Add approach shell content and battle envelopes
- Deliverables:
  - Tier D approach-shell spawning rules (orbital platforms, hazards, objectives).
  - Battle envelope constraints for Tier B/C/D.
- Acceptance:
  - Large engagements can stage from high orbit into near-orbital approach.

7. Step C7 - Add surface patch activation and hosting rules
- Deliverables:
  - Patch activation radius policy around active units/objectives.
  - Patch entity cap and eviction policy.
- Acceptance:
  - Surface entities stay tiny/stable while nearby planet scale feels massive.

8. Step C8 - Add rendering precision and depth guardrails
- Deliverables:
  - Camera-relative transform validation for continuum tiers.
  - Near/far and depth-precision guardrail checks for approach sequences.
- Acceptance:
  - No visible precision jitter spikes during Tier C -> D -> E descent.

9. Step C9 - Add scenario schema controls for continuum tuning
- Deliverables:
  - Scenario config fields for tier thresholds and activation limits.
  - Defaults in a dedicated continuum baseline scenario.
- Acceptance:
  - Designers can tune continuum density/scale without code edits.

10. Step C10 - Add headless telemetry and acceptance questions
- Deliverables:
  - Continuum metrics (tier occupancy, transition counts, thrash count).
  - Required question pack for continuum sanity.
- Acceptance:
  - Continuum scenarios provide deterministic, machine-checkable pass/fail output.

## 4) Mapping to Agreed Architecture Points

1. Hierarchical world scale -> Phases 1, 6
2. Precision/frame ownership -> Phase 2
3. Livelier content (stations/fields/caches/hazards) -> Phases 3, 5
4. Vastness feel and traversal weight -> Phases 1, 3, 6
5. Massive planetoids with hosted entities -> Phases 4, 5
6. Streaming/instancing and debug-vs-production profiles -> Phases 0, 6

## 5) Risk Register

Risk: Over-simulating far entities.
Mitigation: Tier gating is mandatory before density increase.

Risk: Precision jitter during frame transitions.
Mitigation: Phase 2 tests are blocking gates.

Risk: Debug mode (LOD off) hides future perf issues.
Mitigation: Maintain separate production profile and run both profiles in validation.

Risk: Content noise without gameplay value.
Mitigation: Phase 5 interaction acceptance requires meaningful hooks per object class.

## 6) Go/No-Go Before Implementation

Start execution only when:

- Architecture doc is approved.
- Phase budgets are set and accepted.
- First target scenario(s) are chosen for rollout.
