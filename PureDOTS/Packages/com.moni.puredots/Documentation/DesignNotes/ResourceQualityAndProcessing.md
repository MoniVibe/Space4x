# Resource Quality, Lessons, and Processing Loops

_Last updated: 2025-02-XX_

## Goals
- Unify Godgame and Space4X foundational loops around the same DOTS data/contracts.
- Track quality tiers (1-600) and prevent cross-tier mixing when aggregating resources.
- Support direct harvest vs processor routes with data-driven lessons that gate high-tier output.
- Keep villagers/mining vessels behaviour identical so creative layers can extend without new engine work.

## Quality Model
| Tier | Range |
| --- | --- |
| Poor | 1-49 |
| Common | 50-99 |
| Uncommon | 100-199 |
| Rare | 200-399 |
| Epic | 400-499 |
| Legendary | 500-549 |
| Relic | 550-600 |

- Each `ResourceRegistryEntry` tracks `TierId` and `AverageQuality`.
- Storehouse piles keyed by `(ResourceType, TierId)`; new deposits only merge if the tier matches. Average quality is weighted by units per pile.
- Resource nodes (vegetation, animals, ore, crops, asteroids) store `MaxUnits`, `UnitsAvailable`, `GrowthRate`, `GrowthStage`, `Quality`, `TierId`, `ProcessingProfileId`, and `KnowledgeMask` requirements.

## Skill → Harvest Time
Legendary baseline: 5 days to cut + 5 days to process. The multiplier applies to the node’s `BaseCutDays` and `BaseProcessDays`.

| Skill Band | Multiplier (to apply on high-tier nodes) |
| --- | --- |
| 0-20 | `lerp(20x, 10x)` → can take up to 20× longer when untrained |
| 21-40 | `lerp(10x, 0.5x)` → by skill 40 total time is halved |
| 41-70 | `lerp(0.5x, 0.4x)` |
| 71-100 | `lerp(0.4x, 0.2x)` → skill 100 = 1 day cut + 1 day process on Legendary |
| >100 | `0.2f * (1 - 0.1 * log1p(skill-100))` (diminishing returns, floor ≈0.1×) |

## Skill + Lessons → Quality
- Skill alone biases the roll toward higher tiers but never guarantees them; internal tier thresholds: Rare 40, Epic 70, Legendary 90, Relic 120.
- Quality roll uses `tierWeight = saturate((skill + lessonBonus) / tierRequirement)` to skew random selection within the node’s 1-600 band.
- Without the relevant lesson, clamp final quality to `min(nodeQuality, 549)` even if skill ≥ 90. Relic output requires the matching lesson AND skill ≥ 120.
- Lessons can also lower harvest time (multiplicative) and raise base yield (additive). All are data-driven via `KnowledgeDefinition` assets.

## Lessons (Knowledge System Integration)
Extend `KnowledgeDefinition` blobs with effect arrays:
- `HarvestQualityBonus[]`: `{ResourceTag, TierIdMask, QualityCapBonus, FlatQualityBonus, YieldMultiplierBonus, HarvestTimeMultiplier}`.
- `ProcessingBonus[]`: `{ProcessorTag, OutputResourceType, YieldMultiplier, QualityBonus, ProcessTimeMultiplier}`.
- Lessons unlock via existing Heritage & Knowledge workflows; systems materialize effects into a `KnowledgeEffectBuffer` for fast lookup.

Examples:
- `Irontoak Wisdom`: unlocks Legendary/Relic tiers for `ResourceTag=Irontoak`, `QualityCapBonus=+100`, `HarvestTimeMultiplier=0.8`.
- `Master Sawmill Techniques`: `ProcessorTag=Sawmill`, `YieldMultiplier=1.2`, `QualityBonus=+50`.

## Processing Stations
New `ProcessingStationRegistry` + entry buffer:
- Fields: `StationEntity`, `StationTypeId`, `AcceptedResourceTypes`, `QueueDepth`, `ActiveJobs`, `AverageProcessSeconds`, `SkillBias`, `TierModifiers`.
- Both Godgame processors (sawmill, butchery, ore refinery, flour mill) and Space4X modules (ore refineries, orbital processors) register here.
- `DeterministicRegistryBuilder` maintains sorted entries; `RegistryMetadata` marks `SupportsSpatialQueries` for routing.

## Villager / Vessel Data
Add to `VillagerRegistryEntry` and mirrored Space4X crew entries:
- `SkillPlant`, `SkillAnimal`, `SkillMining`, `SkillProcessing` (0-255 byte fields sufficient).
- `KnowledgeMask` (bitmask for cheap checks) plus `KnowledgeEffectIndex` to jump into `KnowledgeEffectBuffer`.
- `PreferredJobMask` / `PrimaryProfession` for AI debugging.

Task selection algorithm:
1. Compute `NeedWeight` from `StorehouseRegistry` deficits and `ProcessingStation` queues.
2. `ProximityWeight` from spatial query.
3. `SkillWeight = skill / 100` (capped 1.2 with >100 skill) × knowledge bonuses.
4. Score = `NeedWeight * ProximityWeight * SkillWeight`; workers pick highest above fatigue threshold.

## Resource Growth Loop
- Vegetation/animals increment `UnitsAvailable` by `GrowthRate * GrowthModifier(soil richness, health)` until `MaxUnits`.
- Animals share the same node struct; when butchered directly they drop meat chunks; hauling to butchery runs through processors.
- Crops compute quality from soil richness + plant health; harvest skill controls final roll.
- Ore chunks behave like other nodes but usually disable `GrowthRate` (finite deposit).
- Space4X asteroids reuse the same data, enabling carriers to evaluate `TierId`, `Quality`, `UnitsAvailable`.

## Harvest Flow (Shared)
1. Worker selects node (villager or mining vessel) -> reserves units.
2. Optionally reserves nearest processor (if behaviour tree says to process).
3. Applies `HarvestTimeMultiplier(skill, lesson)` for the node’s tier.
4. Drops resource chunk (direct) or cargo entry destined for processor.
5. Processor consumes input, applies `ProcessingProfile` multipliers, outputs processed resource with new `TierId`/quality.
6. Deposit to storehouse / carrier cargo; registry updates propagate.

## Processing Profiles
`ProcessingProfile` fields:
- `ProfileId`, `InputResourceType`, `OutputResourceType`, `BaseYieldMultiplier`, `BaseQualityBonus`, `ProcessSeconds`, `RequiredSkill`, `RequiredKnowledge`, `TierUpgrade` flag.
Examples:
- `Tree.Log -> Lumber`: `+30% yield`, `+50 quality`, requires `SkillProcessing >= 40`.
- `Animal.Carcass -> Butchery`: `+40% yield`, `+60 quality`.
- `Ore.Raw -> Refined`: `+25% yield`, `OutputTier=Refined`.
- `Wheat -> Flour`: `+20% yield`, `+30 quality`, `Tier upgrade to Epic food`.

## Space4X Carrier Loop
- `CarrierIntentionSystem` queries `ResourceRegistry` for ore nodes ordered by `TierId`, `Quality`, `UnitsAvailable`, `Distance`.
- Carriers deploy mining vessels + optional strikecraft escort to the chosen asteroid.
- Mining vessels use the same harvest/processing systems; carriers host processors (ore refineries) and storehouses (cargo holds) that register in the shared registries.
- Logistics remains in `LogisticsRequestRegistry`; processed goods feed colonies via existing registry snapshots.

## Crafted Items
- Crafted outputs reference `TechTier` + `Quality` to derive modifiers (damage, durability, etc.).
- Crafting stations use the same processing profile path; quality roll uses crafter skill + lessons + input quality (tier mismatch lowers potential quality).
- Aggregated materials never mix tiers, so recipe inputs can demand specific tier bands.

## Implementation Checklist
1. **Data structs**: extend `ResourceRegistryEntry`, `StorehouseRegistryCapacitySummary`, `VillagerRegistryEntry`, add `ProcessingStationRegistry`.
2. **Systems**: update growth, harvest, processing, deposit systems to use new skill/lesson curves and pile logic.
3. **Lessons**: add effect buffers + authoring fields to knowledge definitions.
4. **Space4X**: ensure carriers/miners hook into the same processing/registry paths; update bridge snapshots to expose new metrics.
5. **Docs**: cross-link with `HeritageAndKnowledgeSystem.md` and per-game TODOs once code lands.

### Implementation Status (2025-02)
- ✅ Runtime structs/registries now track `ResourceQualityTier`, average quality, villager skill/knowledge fields, and processing-station metadata.
- ✅ Harvest loops apply the shared skill curve, clamp quality via `SkillSet` + `VillagerKnowledge`, and award XP through the placeholder `SkillSet` component.
- ✅ Lesson payloads now live in a shared `KnowledgeLessonEffectCatalog` (authored via `KnowledgeLessonEffectAuthoring`), and harvest systems evaluate those blobs to apply yield/time/quality/resource-value modifiers.
- ⏳ Lessons still flow through simple `VillagerKnowledgeFlags`; hook the full data-driven `KnowledgeDefinition` assets + effect buffers before shipping.
- ⏳ Space4X carriers/mining vessels must opt into the same `SkillSet`/knowledge contracts and populate quality tiers when spawning cargo (tracked in Space4X TODOs).

This document should guide future agents when implementing the shared loops, ensuring both games stay aligned while still allowing creative variation.
