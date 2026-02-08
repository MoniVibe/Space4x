# Galaxy Tamagotchi (Space4X 2 TPS Sim)

**Definition**
Galaxy tamagotchi is the Space4X simulation running at roughly 2 ticks per second, intended for long-horizon, observable state evolution with low-frequency updates.

**Goals**
- Maintain a stable ~2 TPS cadence for the core sim.
- Keep time/rewind deterministic and ScenarioRunner-driven.
- Make state changes easy to observe and reason about at low cadence.

**Invariants**
- PureDOTS time/rewind is canonical.
- ScenarioRunner drives time; no game-local time systems.
- Time control semantics follow `Docs/Conceptualization/Mechanics/TimeControl.md`.

**Where TPS is applied**
- `space4x/Assets/Scripts/Space4x/SimServer/Space4XSimServerSettings.cs` (default TPS target).
- `space4x/Assets/Scripts/Space4x/SimServer/Space4XSimServerBootstrapSystem.cs` (applies time scale).
- PureDOTS runtime config asset location: see `space4x/Assets/Data/README.md`.

**Operational checklist**
- ScenarioRunner wiring matches `Docs/PureDOTS_ScenarioRunner_Wiring.md`.
- Time integration follows `Docs/PureDOTS_TimeIntegration.md`.
- Sim server target is 2 TPS and applied at bootstrap.

**Non-goals**
- No per-scene or ad hoc time overrides.
- No MonoBehaviour forcing of ECS systems into the world.

---

## Tamagotchi Sim Schema (Draft)

### 1) Identity & Profile (Canonical)
- **Authoritative storage**: `DynamicBuffer<TraitAxisValue>` (AxisId + Value).
- **Catalog**: `TraitAxisSet` points at the axis catalog blob.
- **Profile metadata**: `EntityProfile` + `ProfileApplicationState`.
- **Axis IDs in scope** (canonical set):
  - Alignment: `LawfulChaotic`, `GoodEvil`, `CorruptPure`
  - Behavior: `VengefulForgiving`, `BoldCraven`, `CooperativeCompetitive`, `WarlikePeaceful`
  - Outlook: `XenophobiaXenophilia`, `AuthoritarianEgalitarian`, `SpiritualMaterialist`, `MightMagicAffinity`
- Component axes may seed the buffer, but the buffer is the single source of truth.

### 2) Day-One Individual Modules
- **Relations**: `EntityRelation` buffer + `RelationChangedEvent` + `SocialStanding`.
- **Loyalties**: `EntityLoyalty` + `SecondaryLoyalty` + `LoyaltyEvent` (primary/secondary targets).
- **Wealth**: `IndividualWealth` + `AssetHolding` for SimIndividuals; `SocialStats`/`VillagerWealth` for fame/glory/renown where applicable.
- **Limbs**: `LimbState` buffer (per-limb health/modifiers).
- **Memory**: `MemoryEntry` + `MemoryAddRequest` buffers (event-driven memory).

### 3) Aggregates (Follow-on Pass)
- `CollectiveAggregate` + member buffers for group entities.
- Aggregate profile = weighted blend of member axes; aggregate influence flows back to members.
- Government/regime emerges from aggregate `AuthoritarianEgalitarian` (policy + authority form).

### 4) Drift & Baselines
- **Event-driven**: action footprints and social events push trait deltas.
- **Low-frequency tick**: per-tick deterministic “coin flip” (stable hash of StableId + Tick).
- **Drift scope**: applies to all axes **except** `GoodEvil` and `CorruptPure` (event-only).
- **Coupling**: `CorruptPure` biases `LawfulChaotic` drift (corrupt → chaos pressure; pure → order pressure).
- **Baseline targets**: loyalty-weighted profile targets.
  - For each loyalty target, read its trait axes.
  - Blend toward target axes by loyalty strength, capped by target extent.
  - Drift moves toward target but never beyond it.

### 5) Fitting-In Pressure (Mismatch)
- Profile mismatch vs loyalty target increases **mood/morale penalties** and **relation friction**.
- These penalties are inputs to decision bias, relation decay, and loyalty change events.
