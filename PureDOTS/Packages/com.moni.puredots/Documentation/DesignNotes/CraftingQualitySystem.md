# Crafting Quality System Concepts

## Goals
- Provide a deterministic, data-driven crafting pipeline where material purity, recipe complexity, manufacturer skill, and optional enhancers determine output quality/rarity.
- Support both simple goods and complex items (weapons, ships) while integrating with production chains, tech/culture services, and registries.
- Keep implementations Burst-friendly, using SoA layouts and shared schedulers.

## Data Model
- `MaterialPurity`: each resource stack carries purity/grade metadata (`0–1` scalar or discrete tiers). Stored in `ResourceStack` component or payload.
- `MaterialQualityProfile` blob:
  - Defines mapping from purity tiers to quality modifiers.
  - Specifies allowed enhancements (gems, boosters) and their effect weights.
- `RecipeQualityParameters`:
  - Base difficulty (`BaseQuality`, `Variance`).
  - Required skill tags/levels.
  - Tech/culture prerequisites.
  - Optional enhancement slots (`EnhancementSlotId`, max count).
- `CrafterStats` component:
  - Skill level, mastery tier, inspiration traits, tool bonuses.
  - Derived modifiers (critical success chance, failure mitigation).
- Integrate with `SkillProgressionSystem`: track skills influencing crafting efficiency; apply xp gains on successful tasks.
- `CraftingOrder` buffer element:
  ```csharp
  public struct CraftingOrder : IBufferElementData
  {
      public Entity Crafter;
      public BlobAssetReference<RecipeQualityParameters> Recipe;
      public FixedList32Bytes<MaterialStackRef> Materials;
      public FixedList16Bytes<EnhancementRef> Enhancements;
      public ushort Priority;
      public float Progress;
      public CraftingFlags Flags;
  }
  ```
- `CraftedItem` component:
  - `ItemQuality` (numeric), `RarityTier`, `Durability`, `Affixes`.
  - Optional references to registries (weapon stats, ship hull data).

## Quality Calculation
1. **Material Purity Aggregation**:
   - Combine input materials by weighting purity values by quantity.
   - Apply optional enhancement modifiers (additive/multiplicative).
   - Produce `MaterialQualityScore`.
2. **Crafter Influence**:
   - Evaluate crafter skill vs. recipe difficulty.
   - Apply trait bonuses (education, culture alignment, elite oversight).
   - Output `CrafterQualityModifier`.
3. **Tech/Culture Modifiers**:
   - Check research unlocks or cultural traditions; add multipliers or unlock special affixes.
4. **Final Quality**:
   ```
   float finalScore = baseQuality
                    + materialFactor * MaterialQualityScore
                    + crafterFactor * CrafterQualityModifier
                    + enhancementBonus;
   finalScore = clamp(finalScore, min, max);
   ```
   - Map `finalScore` to discrete `RarityTier` via thresholds (common, rare, epic, legendary).
   - Compute durability/efficacy stats using curve defined in recipe profile.

## Systems
- `CraftingOrderSubmissionSystem`: services (production chains, narrative rewards, player input) enqueue orders.
- `CraftingSchedulerSystem`: runs in `SchedulerSystemGroup`, ticking `CraftingOrder` progress, reserving materials via resource registries, assigning crafters via job boards.
- `CraftingResolutionSystem`:
  - On completion, performs quality calculation, creates crafted item entities.
  - Updates registries (`ManufacturedGoodsRegistry`, `EquipmentRegistry`) and emits events.
- `EnhancementApplicationSystem`: handles optional materials/gems, validating slots and consuming resources.
- `DurabilitySystem`: manages post-crafting wear/maintenance for items.

## Registries
- `CraftingOrderRegistry`: tracks active orders for analytics and AI decisions.
- `CraftedItemRegistry`: central list of produced items with quality metadata; integrates with inventory systems.
- `EnhancementCatalog`: registry of optional enhancement definitions, modifiers, and tech prerequisites.

## Authoring & Baking
- ScriptableObject profiles:
  - `MaterialPurityProfile`: defines purity thresholds per resource.
  - `RecipeQualityProfile`: difficulty, base stats, enhancement slots.
  - `EnhancementDefinition`: optional boosters with costs/effects.
  - `CrafterArchetypeProfile`: default skill/traits for professions.
- Bakers convert profiles to blobs and attach via authoring components (`CraftingStationAuthoring`, `CrafterAuthoring`).
- Validation ensures recipes reference valid resource ids, enhancement slots, and tech requirements.

## Integration
- **Production Chains**: crafting orders triggered as final manufacturing step or as part of component assembly.
- **Economy/Trade**: pricing influenced by output quality; demand adjusts resource value.
- **Tech/Culture**: unlocks new recipes or quality caps.
- **Population Traits**: skill growth and education pipelines feed crafter stats.
- **Narrative Situations**: reward/penalty events modify crafting results or introduce unique enhancements.
- **Telemetry**: log success rates, average quality per resource tier, material waste.

## Technical Considerations
- SoA/AoSoA buffers for orders, material stacks, crafter stats to maximize Burst efficiency.
- Use pooled buffers for material lists to avoid allocations.
- Leverage `SchedulerAndQueueing.md` for order processing cadence.
- Capture crafting events in history buffers for rewind (state machine transitions, produced item metadata).
- Provide deterministic random seeds (if required) via hashed inputs (order id, tick) to maintain consistency.

## Testing
- Edit-mode: verify quality calculations across edge cases (low purity, high skill, multiple enhancements).
- Playmode: simulate full crafting pipeline with varying materials/skills; ensure registries update and items spawn with expected stats.
- Stress: mass crafting orders (10k+) to confirm scheduler and Burst jobs handle scale.
