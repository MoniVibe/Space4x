# Fleetcrawl Loot/Shop/Module-Limb Foundations

This slice adds standalone contracts and deterministic helpers without modifying room director or camera HUD systems.

## New contracts

### Module limbs
- `FleetcrawlLimbQualityTier`
- `FleetcrawlLimbSlot`
- `FleetcrawlModuleType`
- `FleetcrawlLootArchetype`
- `FleetcrawlLimbSharingMode`
- `FleetcrawlComboTag`
- `FleetcrawlWeaponBehaviorTag`
- `FleetcrawlSkillFamily`
- `FleetcrawlModuleLimbDefinition`
- `FleetcrawlLimbAffixDefinition`
- `FleetcrawlHullSegmentDefinition`
- `FleetcrawlTrinketDefinition`
- `FleetcrawlGeneralItemDefinition`
- `FleetcrawlSetBonusDefinition`
- `FleetcrawlRolledLimb`
- `FleetcrawlRolledItem`
- `FleetcrawlOwnedItem`

### Shop/offer
- `FleetcrawlOfferRuntimeTag`
- `FleetcrawlOfferGenerationConfig`
- `FleetcrawlOfferGenerationCache`
- `FleetcrawlOfferRefreshRequest`
- `FleetcrawlCurrencyShopCatalogEntry`
- `FleetcrawlLootOfferCatalogEntry`
- `FleetcrawlCurrencyShopOfferEntry`
- `FleetcrawlLootOfferEntry`
- run-state components used as offer inputs:
  - `FleetcrawlRunLevelState`
  - `FleetcrawlRunExperience`
  - `FleetcrawlRunShardWallet`
  - `FleetcrawlRunChallengeState`

### Upgrade path
- `FleetcrawlModuleUpgradeDefinition`
- `FleetcrawlResolvedUpgradeStats`
- `FleetcrawlModuleUpgradeResolver`

## Deterministic keys

### Limb rolls
`seed + roomIndex + level + slot + stream`

Implemented in:
- `FleetcrawlDeterministicLimbRollService.ComputeRollHash`
- `FleetcrawlDeterministicLimbRollService.RollLimb`
- `FleetcrawlDeterministicLimbRollService.RollHullSegment`
- `FleetcrawlDeterministicLimbRollService.RollTrinket`
- `FleetcrawlDeterministicLimbRollService.RollGeneralItem`

### Offer generation
`seed + roomIndex + level + xp + shards + challenge + nonce` plus channel/slot/refresh stream

Implemented in:
- `FleetcrawlDeterministicOfferGeneration.ComputeSignature`
- `FleetcrawlDeterministicOfferGeneration.ComputeOfferHash`

## ECS flow

1. `Space4XFleetcrawlLootShopBootstrapSystem`
- creates runtime entity
- seeds default shop catalog, loot catalog, limb definitions, affixes

2. `Space4XFleetcrawlOfferGenerationSystem`
- reads run-state components from existing director entity
- generates deterministic currency and loot offers
- handles `FleetcrawlOfferRefreshRequest`

3. `Space4XFleetcrawlModuleUpgradeBootstrapSystem`
- seeds default module upgrade definitions

4. `FleetcrawlModuleUpgradeResolver`
- resolves rolled limb + affix + combo tag effects into module/weapon/movement multipliers
- can fold in owned item archetypes and set bonuses via `ResolveAggregateWithInventory`

## Extension points

- Add new limb families by appending `FleetcrawlModuleLimbDefinition` rows.
- Add new affix pools by appending `FleetcrawlLimbAffixDefinition` rows.
- Add hull segment archetypes by appending `FleetcrawlHullSegmentDefinition` rows.
- Add trinket archetypes by appending `FleetcrawlTrinketDefinition` rows.
- Add general item archetypes by appending `FleetcrawlGeneralItemDefinition` rows.
- Add set/manufacturer combo behavior by appending `FleetcrawlSetBonusDefinition` rows.
- Add new shop items via `FleetcrawlCurrencyShopCatalogEntry`.
- Add new loot offer channels/slots via `FleetcrawlLootOfferCatalogEntry`.
- Add new module combo behavior via `FleetcrawlModuleUpgradeDefinition.RequiredTags`.
- Trigger explicit refreshes from other systems by creating `FleetcrawlOfferRefreshRequest` entities.
