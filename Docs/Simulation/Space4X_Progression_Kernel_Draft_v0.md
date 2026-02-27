# Space4X Progression Kernel Draft v0

## Intent

Define one progression model that scales across combat, research, production, exploration, diplomacy, economy, ship construction/refit, and other activity domains without per-feature bespoke leveling systems.

This draft targets:

- deterministic ECS simulation
- large entity counts
- clear player-facing expression of growth
- reuse across domains (not just weapons)

## Design Principles

1. One entity model, many outcomes.
No "rookie class" vs "elite class." Labels describe current state, not different species of logic.

2. Event-driven growth, not constant per-tick grind.
Entities improve from meaningful actions/events. This keeps CPU cost bounded.

3. Sparse proficiency.
Only store proficiencies for domains an entity has actually touched.

4. Multi-stat checks are first-class.
A single outcome can be influenced by multiple stats in different compositions depending on context.

5. Derived caches.
Expensive formulas run when inputs change, then consumers read cached values.

6. Challenge-weighted progression.
Higher gains come from harder, relevant tasks; trivial repetition quickly devalues.

## Core Stat Layer

Keep five core stats as stable primitives:

- `Strength`
- `AgilityDexterity`
- `Intelligence`
- `Wisdom`
- `Luck`

Keep your archetype axes as long-horizon mutators:

- `PhysiqueAxis`
- `FinesseAxis`
- `WillAxis`

Archetype axes do not replace core stats. They bias growth rates and some checks.

## Check Composition Model

Each gameplay check resolves from a composition profile:

- weighted base stats
- one or more proficiencies
- equipment/module familiarity
- temporary state modifiers
- optional low-amplitude luck perturbation

This preserves your "duplicate meaning by composition" requirement.  
Example: deception reading can use mostly `Intelligence` in one context, or more `AgilityDexterity` + `Wisdom` in another.

Perception should be treated as a derived capability, not a sixth core stat:

- `Perception = f(AgilityDexterity, Intelligence, Wisdom, sensor/tool modifiers, state modifiers)`
- different checks can weight those inputs differently (spotting hazards vs reading intent vs artifact analysis).

### Practice opportunity weighting (Kenshi-style reference pattern)

Use challenge-scaled gain multipliers instead of flat gains.

Concept:

- compare actor effective capability against task difficulty/opponent capability
- harder-than-current tasks get higher gain multipliers (bounded)
- much easier tasks decay toward a low gain floor

Suggested normalized shape:

- `challengeDelta = taskDifficulty01 - actorCapability01`
- `challengeScalar = clamp(remap(challengeDelta), minGain, maxGain)`
- apply with novelty and quality gates, never standalone

Reference benchmark from Kenshi community-documented mechanics:

- stronger-opponent logic ranges from very low gains for easy targets to much higher gains for hard targets
- use the pattern, not exact numbers, and tune per domain

## ECS Data Contract (Draft)

### Components (per individual actor)

- `Space4XCoreStats` (`IComponentData`)
  - `Strength`, `AgilityDexterity`, `Intelligence`, `Wisdom`, `Luck` in normalized `[0..1]`

- `Space4XAptitudeAxes` (`IComponentData`)
  - `PhysiqueAxis`, `FinesseAxis`, `WillAxis` in `[0..1]`

- `Space4XProgressionState` (`IComponentData`)
  - `Level`, `UnspentXp`, `SpentXp`, `LearningRateScalar`, `Version`

- `Space4XDerivedCapabilityCache` (`IComponentData`)
  - domain-neutral resolved outputs used hot-path by systems
  - examples: `CombatAimControl`, `CombatRecoilComp`, `ResearchEfficiency`, `ProductionQuality`, `DiplomacyReadIntent`
  - `SourceVersion` for cheap staleness checks

- `Space4XMarketCapabilityCache` (`IComponentData`)
  - lightweight economy outputs used by trade/order systems
  - examples: `DealcraftScore`, `CounterpartyFamiliarity`, `MarketSense`, `BriberyRiskControl`, `ExtortionPressureControl`
  - `SourceVersion`

- `Space4XConstructionCapabilityCache` (`IComponentData`)
  - construction/refit outputs used by shipyard/module factory systems
  - examples: `BuildSpeedScalar`, `BuildWasteScalar`, `BuildQualityScalar`, `RefitTimeScalar`, `IntegrationErrorScalar`
  - `SourceVersion`

### Buffers

- `Space4XProficiencyEntry` (`IBufferElementData`, sparse)
  - `DomainId` (`uint`)
  - `FamilyId` (`uint`) optional sub-domain key (weapon family, engine family, culture/race key, etc.)
  - `Skill01`
  - `Xp`
  - `LastGainTick`
  - `LastUseTick`
  - `DecayHalfLifeTicks`

- `Space4XProgressionEvent` (`IBufferElementData`)
  - `Actor`
  - `DomainId`
  - `FamilyId`
  - `Intensity`
  - `Quality`
  - `ContextFlags`
  - `Tick`

- `Space4XEconomyProficiencyEntry` (`IBufferElementData`, sparse)
  - `ModeId` (`uint`) trade/barter/bribery/extortion/contracting
  - `CounterpartyFamilyId` (`uint`) culture/race/faction or legal group
  - `CommodityFamilyId` (`uint`) market/material category
  - `Skill01`
  - `TrustCalib01`
  - `Coercion01`
  - `MarketSense01`
  - `LastUseTick`
  - `DecayHalfLifeTicks`

- `Space4XBlueprintProficiencyEntry` (`IBufferElementData`, sparse)
  - `BlueprintId` (`uint`) specific hull/station/module blueprint
  - `HullClassId` (`uint`) corvette/frigate/station class grouping
  - `ModuleFamilyMask` (`ulong`) coarse integration family keying
  - `BuildSkill01`
  - `IntegrationSkill01`
  - `YieldEfficiency01`
  - `QualityConsistency01`
  - `LastUseTick`
  - `DecayHalfLifeTicks`

### Economy/Construction event contracts (telemetry-first v0)

- `Space4XDealEvent` (`IBufferElementData`)
  - `Actor` (`Entity`)
  - `Counterparty` (`Entity`)
  - `ModeId` (`uint`) trade/barter/bribery/extortion
  - `CounterpartyFamilyId` (`uint`) culture/race/faction bucket
  - `CommodityFamilyId` (`uint`)
  - `RegionId` (`uint`)
  - `QuotedValue`
  - `SettledValue`
  - `MarginDelta01` normalized delta vs baseline expected deal
  - `OutcomeFlags` success/fail/partial/exposed/defaulted/etc.
  - `Tick`

- `Space4XBuildEvent` (`IBufferElementData`)
  - `Actor` (`Entity`) crew/foreman/captain/automation owner
  - `Facility` (`Entity`) shipyard/module factory/station line
  - `BlueprintId` (`uint`)
  - `HullClassId` (`uint`)
  - `ModuleFamilyMask` (`ulong`)
  - `MaterialCost`
  - `LaborTime`
  - `Waste01`
  - `QualityRoll01`
  - `DurabilityRoll01`
  - `ReworkCount`
  - `CompletionFlags` on-time/late/defect/cancelled
  - `Tick`

v0 implementation policy:

- emit these events first with no gameplay output changes
- validate telemetry and stability under scenario load
- only then bind them to economy/construction capability effects

### Persistence and migration contract (required before broad rollout)

- every progression-bearing component/buffer must include `SchemaVersion`
- save/load path must support additive field migration with defaults
- unknown `DomainId`/`FamilyId` on load must map to inert fallback entries and emit telemetry warnings
- deterministic replay seed/version must be persisted with progression snapshots
- progression event streams should be checkpointed by periodic snapshots to bound replay cost
- migration tests must include at least one previous schema fixture per minor release

### Optional relation/diplomacy buffer

- `Space4XCultureProficiencyEntry` (`IBufferElementData`)
  - `CultureId` (`uint`)
  - `NegotiationSkill01`
  - `TrustCalibration01`
  - `EtiquetteSkill01`
  - `LastUseTick`

## Domain Keying

Use integer IDs (not runtime strings in hot loops):

- `DomainId`: broad activity class (`Combat`, `Research`, `Production`, `Exploration`, `Diplomacy`, `Economy`, `ConstructionRefit`, `Navigation`, etc.)
- `FamilyId`: specialization (`Kinetic`, `Beam`, `ReactorTypeA`, `SiteExcavation`, `ArtifactForensics`, `Culture.Zeta`, etc.)

Keep a static mapping table for authoring/debug UI.

Registry governance requirement:

- `DomainId`/`FamilyId` values must come from a single generated registry asset
- registry exports both runtime constants and debug-name lookup tables
- CI should fail on duplicate or unstable ID assignment

## Ownership and Aggregation Model (Missing Critical Ambiguity)

Progression ownership must be explicit to avoid contradictory behavior.

Recommended ownership scopes:

- `Individual`: default scope for crew/officer/entity learning
- `Team`: optional shared operational layer for squads/crews
- `Facility`: yard/station/factory line process proficiency
- `Hull`: ship-level integration familiarity and doctrine usage history

Rules:

- individuals always gain individual progression from acted events
- facility processes gain facility progression when line/facility events complete
- hull-level familiarity is derived from repeated module families and doctrine use
- team scope is optional and should be additive, not replacing individual growth

Transfer behavior (must be deterministic and bounded):

- moving personnel transfers only individual progression
- facility/hull progression remains with the facility/hull
- context mismatch applies temporary penalty when actor proficiency and platform familiarity diverge

## Exploration Domain (First-Class, Coupled to Research)

Exploration should be its own domain and feed research, not be merged into research directly.

Reason:

- exploration has risk/survival and uncertainty mechanics that research does not
- exploration sessions have field outcomes (injury, death, salvage, partial intel, critical discoveries)
- research consumes exploration outputs but runs under different constraints

Suggested split:

- `Exploration` domain:
  - surveying
  - excavation
  - hazard handling
  - artifact recovery
  - on-site interpretation
- `Research` domain:
  - controlled analysis
  - validation
  - synthesis into unlocks/blueprints/theory progress

### Expedition runtime concept

An expedition is a temporary runtime facility bound to a site plus a team.

Suggested runtime data (draft names):

- `Space4XExpeditionSiteState`
  - site integrity, remaining yield, hazard pressure, mystery depth, depletion state
- `Space4XExpeditionTeamState`
  - assigned actors, supplies, extraction window, fatigue/stress, risk posture
- `Space4XExpeditionCheckSchedule`
  - next check tick, cadence, budget, active check classes

Each cadence window executes bounded checks with graded outcomes:

- fail
- partial
- success
- critical success
- critical failure (can be lethal)

Site exhaustion is not guaranteed. End states include:

- fully exhausted
- partially exhausted with recoverable remnants
- aborted/evacuated
- collapsed/contaminated
- completed with unresolved mystery threads

## Economy Domain (First-Class, Coupled to Diplomacy/Production)

Economy should be first-class in the progression model, not embedded as a small diplomacy extension.

Economy subtracks (same kernel, separate families):

- `Dealcraft`: pricing, concessions, contract structure
- `ProductionCraft`: throughput, defect pressure, process reliability
- `ExtractionCraft`: yield stability by material family/site type
- `Logistics`: route quality, late/miss rates, handling losses
- `Leadership`: team coordination under deadlines/stress
- `MarketSense`: confidence-weighted trend reading from observed data

Negotiation modes should be explicit families under economy:

- `Trade` (mutual value / low hostility)
- `Barter` (direct goods exchange / opportunity cost heavy)
- `Bribery` (illegal or covert influence, high exposure risk)
- `Extortion` (coercive extraction, relation/legal blowback)

Familiarity tracks should be keyed by:

- counterparty culture/race/faction family
- region/market bucket
- commodity/material family

This gives the desired "better deals with repeated exposure" behavior without bespoke systems.

Market prediction should stay lightweight for scale:

- local rolling stats per region/commodity, not global heavy forecasting
- confidence score carried with prediction
- LOD fallbacks for distant actors/markets

Economy observability requirement:

- maintain explicit sinks/faucets ledgers by region/faction/commodity class
- publish periodic economy snapshots (raw numbers, not only aggregates) for balancing
- include correction pipeline for bad data revisions (append-only + correction records)

## Refit/Ship Construction/Customization Kernel (Alongside Economy)

Refit/construction/customization should be alongside economy, not nested inside economy.

Reason:

- economy solves value exchange and incentives
- construction/refit solves physical integration, blueprint quality, and build outcomes
- both couple tightly but have different hot-loop constraints and telemetry

Progression model for yards/facilities/crews:

- repeated builds of same blueprint increase `BlueprintId` proficiency
- repeated builds within same hull class increase class/family transfer skill
- repeated integration of the same module families increases integration reliability
- frequent large blueprint/module shifts reduce short-term effective integration skill (soft context penalty, not hard reset)

Intended outcomes from higher construction proficiency:

- lower material waste (bounded)
- faster completion (bounded)
- better durability/quality roll on completion
- fewer integration defects/rework events
- lower labor/time cost pressure through smoother execution

Applies to:

- ship hull construction
- station construction
- refit operations
- module factory lines

Economy coupling:

- economy domain computes deal quality, procurement prices, and staffing pressure
- construction/refit domain consumes those constraints and computes build/refit output quality

## Progression Flow

1. Producer systems emit `Space4XProgressionEvent` only on meaningful actions.
2. Progression kernel consumes events at bounded cadence (for example 5 Hz).
3. Kernel updates sparse proficiency entries and XP pools.
4. Kernel marks/updates `Space4XDerivedCapabilityCache`.
5. Consumer systems (combat, research, production, exploration, diplomacy) read cached outputs.

## Growth Math (Shape, Not Final Numbers)

`gain = baseGain * intensity * quality * wisdomScalar * aptitudeScalar * contextScalar * challengeScalar * noveltyScalar * saturationScalar`

Where:

- `wisdomScalar` from `Wisdom`
- `aptitudeScalar` from relevant archetype axis blend
- `contextScalar` from conditions (fatigue, stress, module quality, doctrine, etc.)
- `challengeScalar` from task-vs-capability delta (bounded)
- `noveltyScalar` suppresses repeated near-identical low-information actions
- `saturationScalar` enforces diminishing returns near mastery

Suggested saturation shape:

- exponential or power-law family curves are both acceptable
- choose one per domain and lock it for deterministic tuning
- avoid linear gain-to-infinity behavior

Decay (soft forgetting) runs only for proficiencies not used for a while, and only for the affected entries.

### Retention/rust model (Dwarf Fortress-inspired pattern)

Use staged counters instead of immediate linear decay:

- `UnusedCounter`: increments while not practiced
- `RustCounter`: starts after unused threshold; applies reversible penalties first
- `PermanentDecayCounter`: optional slow long-horizon loss for neglected domains

Recovery behavior:

- recent-use events clear rust penalties faster than they were accrued
- permanent decay recovers slowly through meaningful practice

This preserves "you get rusty" behavior without deleting hard-earned identity too aggressively.

## Luck Handling

Luck should be bounded and deterministic:

- low amplitude (for example +-3% to +-5% effect)
- seeded from deterministic simulation state
- avoid turning luck into primary driver

Use it mainly as tie-break flavor, not core throughput.

## Exploit Resistance and Gain Integrity

Progression events must be quality-gated to prevent farm loops.

Minimum controls:

- per actor/domain/family cadence gate (no unbounded same-tick spam)
- novelty weighting: repeated identical low-risk actions quickly diminish gain
- risk/quality floor: zero-risk trivial loops should grant near-zero gain after warmup
- collusion guard for economy events (same two actors repeating degenerate deals)
- anomaly telemetry for sudden gain spikes, impossible margins, or rework-free extremes

All exploit controls should be deterministic and data-driven.

## Performance / Scale Guardrails

1. Event budget cap per tick.
Overflow can be batched/merged by actor-domain pair.

2. LOD progression modes:
- `Full`: near player/active combatants
- `Reduced`: medium-importance actors
- `Aggregate`: far/offscreen populations represented as crew aggregates

3. Cache invalidation by version.
Do not recompute all derived capabilities every tick.

4. Sparse buffers only.
No fixed giant arrays per entity.

5. Fixed-step progression update group.
Run progression systems in fixed-step simulation cadence with capped catch-up to avoid runaway frame stalls.

Recommended baseline:

- run progression at lower fixed rate than core physics/combat where possible
- cap maximum catch-up steps per frame
- if over budget, degrade gracefully via deterministic event coalescing rather than unbounded backlog

6. Structural-change discipline.
Avoid sync-point heavy structural changes in hot paths; batch writes through command buffers.

7. Cross-core determinism checks.
Validate progression determinism across different worker-thread/core-count configurations.

## Integration Plan

### Phase 1: Combat alignment

- Replace ad-hoc gunnery resolution with:
  - stats composition
  - `CrewSkills.CombatSkill`
  - relevant `Space4XProficiencyEntry` family (weapon family)
  - recoil/inertia compensation from derived cache
- Keep current hit tuning knobs as safety rails.

### Phase 2: Production and research

- Emit progression events from facility production and research completion.
- Add production/research derived capabilities from same kernel.

### Phase 3: Exploration/expedition integration

- Add exploration domain/family proficiencies and event producers.
- Implement temporary expedition runtime facility and bounded check loop.
- Emit discovery artifacts/intel packets as inputs for research systems.

### Phase 4: Economy integration

- Add economy domain producers for trade/barter/bribery/extortion/contract outcomes.
- Add counterparty/commodity/region familiarity keys.
- Apply market-sense outputs to quote quality and deal confidence.

### Phase 5: Construction/refit/customization integration

- Add blueprint/family/module integration proficiencies for shipyards and module factories.
- Apply proficiency to build speed, waste, quality, and rework frequency (bounded).
- Add context penalty for frequent large blueprint/module shifts.

### Phase 6: Diplomacy cultural proficiency

- Add culture/race family proficiencies for envoys and diplomats.
- Couple diplomacy familiarity with economy dealcraft for repeated counterparties.

### Phase 7: Traits/bionics coupling

- Traits and implants modify gain scalars, ceilings, decay, and specific checks.
- Keep modifiers data-driven and bounded.

## Sprint Acceptance Gates (Definition of Done)

Do not mark a phase complete without passing its gate metrics.

- Combat gate:
  - rookie/veteran/elite separation is statistically visible over suite runs
  - recoil/inertia contribution telemetry is non-zero and directionally correct
  - deterministic replay drift stays within configured epsilon
  - replay results remain stable across core-count test matrix

- Economy gate:
  - repeated counterparty familiarity measurably improves `MarginDelta01` in non-coercive modes
  - bribery/extortion modes show higher exposure/reputation risk than trade/barter
  - collusion guard catches synthetic farm loops in test scenarios

- Construction/refit gate:
  - repeated blueprint runs reduce waste/rework within bounded caps
  - frequent blueprint/module shifts show temporary integration penalty
  - quality/durability improvements remain within configured ceilings

- Exploration gate:
  - partial and full exhaustion states both occur under controlled scenario variation
  - expedition risk posture materially shifts casualty/abort/discovery distribution

- Performance gate:
  - progression systems stay within budget at target entity count
  - no unbounded buffer growth in long-run soak tests

## Scenario Authoring Contract

Scenario templates should seed:

- core stats
- aptitude axes
- selected proficiency entries
- optional economy familiarity seeds (counterparty/commodity/region)
- optional blueprint proficiency seeds (hull class/module family)
- optional implants/traits

This gives true "same hull, different crew state" comparisons.

## Telemetry Contract (Minimum)

Per scenario or sampled interval:

- shots fired / hit / hit rate by actor tier label
- recoil error contribution
- inertia skew contribution
- deal margin delta vs baseline by mode (`Trade`, `Barter`, `Bribery`, `Extortion`)
- counterparty familiarity gain/decay by culture/race/faction
- build waste %, build duration, quality/durability roll by blueprint family
- integration defect/rework rate by module family
- proficiency gain by domain family
- decay by domain family
- derived capability deltas over runtime
- migration fallback count (unknown domain/family IDs on load)
- gain-suppression count (events ignored by exploit guards)
- progression CPU time and peak buffer length by system

This is required to prove progression behavior and avoid "label-only tiers."

## Immediate Next Slice

1. Implement core progression data contract and event buffer.
2. Wire combat first (gunnery + recoil/inertia compensation) to this kernel.
3. Update capital range scenarios to seed explicit proficiency states, not only labels.
4. Re-run capital range suite and enforce rookie target band near requested floor.
5. Add economy + construction/refit event producers with no-op gameplay effects first (telemetry-only pass).
6. Add first economy/refit probe scenarios to prove familiarity and blueprint repetition effects.
7. Add progression determinism smoke tests across core-count matrix before enabling effectful economy/refit coupling.

## External Review Inputs (Online, Feb 2026)

Applied into this draft:

- Kenshi stronger-opponent and level-based gain behavior (challenge-weighted progression reference pattern): https://kenshi.fandom.com/wiki/Stronger_Opponent_Logic
- Dwarf Fortress rust behavior and staged skill degradation/recovery inspiration: https://www.bay12games.com/dwarves/dev_2008.html and https://dwarffortresswiki.org/index.php/DF2014:Mechanic
- Unity DOTS fixed-step execution semantics (`FixedStepSimulationSystemGroup`) for bounded cadence: https://docs.unity.cn/Packages/com.unity.entities@1.0/api/Unity.Entities.FixedStepSimulationSystemGroup.html
- Unity DOTS sync-point performance guidance (batch structural changes): https://docs.unity3d.com/Packages/com.unity.entities@1.4/manual/performance-sync-points.html
- Deterministic fixed-timestep simulation baseline: https://gafferongames.com/post/fix_your_timestep/
- Real-world deterministic sim pitfalls and workload amortization patterns (Factorio FFF): https://factorio.com/blog/post/fff-415
- MMO economy observability practice (published economic reports and market datasets): https://www.eveonline.com/news/view/monthly-economic-report-february-2021
- Progression/event-stream architecture reference (event sourcing + snapshot reasoning): https://martinfowler.com/eaaDev/EventSourcing.html
