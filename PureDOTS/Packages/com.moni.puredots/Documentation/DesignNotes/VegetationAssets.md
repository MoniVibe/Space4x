# Vegetation Data Assets Plan

## Purpose
- Define the ScriptableObject and blob layouts that drive vegetation growth, health, harvest, and reproduction systems.
- Provide guidance for authoring workflows and data ownership before Alpha implements bakers.
- Capture open questions around persistence and rewind serialization.
- Ensure data contracts match runtime expectations in `Docs/TruthSources/RuntimeLifecycle_TruthSource.md`, integration glue in `Docs/TODO/SystemIntegration_TODO.md`, and rewind guidance in `Docs/DesignNotes/RewindPatterns.md`.

## Asset Overview
- **VegetationSpeciesCatalog.asset**
  - ScriptableObject stored under `Assets/Data/Vegetation/Species/`.
  - Single source of per-species tuning consumed by bakers.
  - Serialized fields:
    - `List<SpeciesEntry> species`.
    - `float globalHealthRecoveryScale = 1`.
    - `float globalDamageScale = 1`.
    - `float defaultSpawnDensity = 0.1f`.
    - `AnimationCurve ageScaleCurve` (baked to lookup table).
  - `SpeciesEntry` fields:
    - `string speciesId`.
    - `Color debugColor`.
    - `StageDurations durations` (seedling, growing, mature, flowering, fruiting, dying, respawnDelay).
    - `GrowthSettings growth` (baseRate, perStageMultiplier[7], seasonalMultiplier[4]).
    - `HarvestSettings harvest` (resourceTypeId, maxYield, cooldownSeconds, replenishCurve, partialPenalty).
    - `HealthSettings health` (maxHealth, regenPerSecond, damagePerDeficit, droughtToleranceSeconds, frostToleranceSeconds, dyingThreshold).
    - `EnvironmentThresholds environment` (waterMin, waterMax, lightMin, lightMax, soilMin, soilMax, pollutionMax, windMax, tempMin, tempMax).
    - `ReproductionSettings reproduction` (cooldownSeconds, seedsPerEvent, spreadRadius, offspringCap, maturityStageRequired, gridPadding, respawnPrefab).
    - `RandomSeeds seeds` (growthSeed, reproductionSeed, lootSeed, visualSeed).
  - **Blob Layout (`VegetationSpeciesCatalogBlob`)**
    - `int speciesCount`.
    - `BlobArray<SpeciesBlob>` species.
    - `float globalHealthRecoveryScale`.
    - `float globalDamageScale`.
    - `float defaultSpawnDensity`.
    - `BlobArray<float> ageScaleSamples` (fixed 64 entries).
    - `struct SpeciesBlob`
      - `ushort speciesIndex`.
      - `BlobString speciesId`.
      - `float stageDurations[7]`.
      - `float growthStageMultiplier[7]`.
      - `float growthSeasonMultiplier[4]`.
      - `HarvestBlob harvest`.
      - `HealthBlob health`.
      - `EnvironmentBlob environment`.
      - `ReproductionBlob reproduction`.
      - `RandomSeedsBlob seeds`.
    - `struct HarvestBlob`
      - `ushort resourceTypeIndex`.
      - `float maxYield`.
      - `float cooldownSeconds`.
      - `float partialPenalty`.
      - `ushort replenishCurveIndex` (points into shared curve bank).
    - `struct HealthBlob`
      - `float maxHealth`.
      - `float regenPerSecond`.
      - `float damagePerDeficit`.
      - `float droughtToleranceSeconds`.
      - `float frostToleranceSeconds`.
      - `float dyingThreshold`.
    - `struct EnvironmentBlob`
      - `float idealWaterRange[2]`.
      - `float idealLightRange[2]`.
      - `float idealSoilRange[2]`.
      - `float pollutionMax`.
      - `float windMax`.
      - `float tempRange[2]`.
    - `struct ReproductionBlob`
      - `float cooldownSeconds`.
      - `ushort seedsPerEvent`.
      - `float spreadRadius`.
      - `ushort offspringCap`.
      - `byte maturityStageRequired`.
      - `float gridPadding`.
      - `EntityGuid respawnPrefabGuid` (resolved during baking).
    - `struct RandomSeedsBlob`
      - `uint growthSeed`.
      - `uint reproductionSeed`.
      - `uint lootSeed`.
      - `uint visualSeed`.

- **VegetationEnvironmentProfile.asset**
  - One asset per biome under `Assets/Data/Vegetation/Environment/`.
  - Serialized fields:
    - `string biomeId`.
    - `AnimationCurve dailyLight`.
    - `AnimationCurve seasonalWater`.
    - `AnimationCurve seasonalTemperature`.
    - `float soilRegenerationPerSecond`.
    - `float droughtPenaltyScale`.
    - `float frostPenaltyScale`.
    - `float pollutionDissipationPerSecond`.
    - `uint environmentSeed`.
    - Optional references to authoring data (weather presets, irrigation volumes).
  - Baker output:
    - `VegetationEnvironmentConfig` singleton containing:
      - `BlobArray<float> dailyLightSamples` (24 values).
      - `BlobArray<float> seasonalWaterSamples` (4 values, one per season).
      - `BlobArray<float> seasonalTemperatureSamples` (4 values).
      - Scalars for soil regeneration, penalties, dissipation.
      - `uint environmentSeed`.

- **Curve Bank (`VegetationCurveBank.asset`)**
  - Stores reusable `AnimationCurve` entries for growth/harvest replenish curves.
  - Baker compiles curves into `BlobArray<float>` lookups shared by species via `replenishCurveIndex`.

## Baker Responsibilities
- Validate unique `speciesId` and assign ascending `ushort` indices.
- Map external resource ids (strings) to indices via `ResourceTypeCatalog`.
- Resolve `respawnPrefabGuid` to actual `Entity` during baking; capture fallback if prefab missing.
- Quantize curve samples to avoid non-deterministic evaluation at runtime.
- Write a `VegetationSpeciesLookup` singleton with references to catalog blob and curve bank.
- Populate per-entity components:
  - `VegetationSpeciesIndex` (ushort).
  - `VegetationRandomState` (uint samples processed).
  - `VegetationEnvironmentState` initialised from biome profile.

## Environment Profile Ownership and Updates
- Profiles are authored centrally in `Assets/Data/Vegetation/Environment/`. The shared library is the single source of truth; level bootstraps reference a profile asset via `VegetationEnvironmentProfileAuthoring`.
- Runtime selection:
  - The bootstrap GameObject assigns the profile for the active scene. Subscenes may override by pointing to a different profile asset if biome boundaries change mid-level.
  - Profiles can be hot-swapped in editor playmode for iteration, but runtime swaps must flow through a deterministic command (e.g., weather director) so rewinds capture the change.
- Update cadence:
  - Profiles are treated as immutable during simulation. Short-term changes (storms, irrigation) emit events that modulate the sampled values per tick without editing the base asset.
  - Long-term tuning happens by editing the ScriptableObject and rebaking; curves are sampled into blobs during conversion so runtime sees the new values on next build.
- Rewind behaviour:
  - `VegetationEnvironmentState` on each entity stores the last sampled values and tick. During rewind, the state is restored from snapshots and the deterministic event stream reconstructs the grid-wide modifiers.
  - Environment grid systems replay recorded weather/irrigation events per tick, ensuring the same samples are re-applied after rewind without needing to snapshot the entire grid.

## Tuning Workflow
- Designers edit Species Catalog, Environment Profile, and Curve Bank ScriptableObjects in their respective data folders; assets remain under version control and respect GUID stability.
- Curves are sampled by bakers into fixed-size lookup tables. Runtime systems never evaluate `AnimationCurve` directly.
- Resource ids stay human-readable in assets but are resolved to ushort indices via `ResourceTypeCatalog` during baking.
- Scene teams assign the relevant environment profile and catalog authoring components in the bootstrap prefab so levels automatically pick up the latest data.
- Manual species overrides (legacy prefabs) must provide a valid `VegetationSpeciesIndex`. Catalog entry `0` is reserved as the default and must exist in every catalog. If an override lacks a catalog reference or index, the baker assigns index `0` and logs a warning; more granular overrides require adding a proper catalog asset.

## Rewind and Serialization Notes
- Blob assets (species catalog, environment config, curve bank) are immutable and excluded from snapshots. Entities carry only indices and random state counters.
- `VegetationEnvironmentState`, `VegetationRandomState`, and harvest/reproduction history buffers must be included in the state root to restore deterministic behaviour.
- Environment grid reconstruction depends on recorded events; ensure any new environmental modifier also emits deterministic commands so playback reproduces identical values.

## Decisions
1. **Environment profile ownership** - Profiles live in the shared data library. Authoring components in scenes reference the desired profile; runtime edits occur through deterministic events rather than modifying assets.
2. **Environment sampling rewind** - Rewind restores per-entity environment states and replays weather/irrigation events. Grid-level buffers need not be snapshotted beyond what is required to reapply deterministic events.
3. **Curve bank versioning** - Curve entries carry stable GUID-backed indices. Editor tooling blocks reordering or deletion that would shift indices, forcing explicit migration steps when necessary.
4. **Prefab sourcing** - Species entries reference prefab GUIDs stored under `Assets/Prefabs/Vegetation/`. `VegetationSpawnConfig` supplies placement rules and default prefabs for fallback, keeping catalog-defined prefabs as the authoritative source.

## Outstanding Blockers
- None at this time. Tooling for curve bank GUID validation and environment-event recording is tracked separately; no additional stakeholder decisions are required for asset ownership or rewind behaviour.

## Example Asset Entries
- **Species (Tree_Highland)**
  - `speciesId = "tree_highland"`
  - Durations: seedling 45, growing 180, mature 120, flowering 45, fruiting 240, dying 90, respawnDelay 360
  - Growth: baseRate 0.6, stageMultipliers `[0.8, 1.0, 1.2, 1.0, 1.1, 0.5]`, seasonal `[1.1, 1.0, 0.9, 0.6]`
  - Health: maxHealth 140, regen 1.5, damagePerDeficit 12, droughtTolerance 120, frostTolerance 45, dyingThreshold 30
  - Harvest: resource `"Wood"`, maxYield 18, cooldown 75, partialPenalty 0.4, replenishCurve index 2
  - Environment: water 35-85, light 40-90, soil 50-100, pollution 0.3, wind 0.4, temp 5-28
  - Reproduction: cooldown 420, seedsPerEvent 3, spreadRadius 6, offspringCap 9, maturityStageRequired = Fruiting, gridPadding 1, prefab GUID referencing `Assets/Prefabs/Vegetation/Tree_Highland.prefab`
  - Seeds: growth 0xA12F33, reproduction 0xB88954, loot 0x199337, visual 0xCCDD11
- **Environment Profile (Biome_Alpine)**
  - `biomeId = "alpine_daylight"`
  - dailyLight curve sampled to `[0.1, 0.1, 0.2, 0.3, 0.45, 0.6, 0.8, 0.9, 1.0, 0.95, 0.8, 0.6, 0.4, 0.25, 0.15, 0.1, 0.1, 0.1, 0.05, 0.05, 0.05, 0.05, 0.05, 0.05]`
  - seasonalWater `[0.7, 1.0, 0.9, 0.5]`, seasonalTemperature `[0.3, 0.75, 0.55, 0.2]`
  - soilRegeneration 0.02, droughtPenaltyScale 1.3, frostPenaltyScale 1.5, pollutionDissipation 0.015, environmentSeed 0xDEADBEEF
- **Curve Bank (GrowthReplenish.asset)**
  - Entry GUID `2b7f2ef3-...` (index 0) -> linear curve
  - Entry GUID `6a0a4bb7-...` (index 1) -> fast rise / slow decay
  - Entry GUID `f19ae123-...` (index 2) -> sigmoid used by Tree_Highland
- **Harvest Command Example**
  - Queue element: `villager=Entity(112)`, `vegetation=Entity(5401)`, `speciesIndex=3`, `requestedAmount=5`, `issuedTick=14400`, `commandId=0x0003_90FA`
- **Reproduction Spawn Payload**
  - Parent species index 3, uses reproduction seed `0xB88954 ^ parentEntityIndex ^ currentTick` to select offsets, enforcing `gridPadding=1` before enqueuing spawn for prefab `Tree_Highland.prefab`.
- **Testing allocation note**
  - Blob assets constructed in playmode tests (catalogs, environment configs, curve banks) must be disposed with `blob.Dispose()` at test teardown to avoid leaking persistent allocations in the Unity test runner.
