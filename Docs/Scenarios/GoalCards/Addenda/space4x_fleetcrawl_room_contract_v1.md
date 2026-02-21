# FleetCrawl Room Contract v1
Date: 2026-02-19
Owner: shonh
Status: draft

## Intent
Normalize FleetCrawl room generation with three orthogonal axes:
- run difficulty (`easy`, `normal`, `hard`, `nightmare`)
- room class (`normal`, `elite`, `miniboss`)
- room composition (`archetype x system size x wildcard tags`)

This avoids a hard-coded room-per-combination explosion while preserving deterministic control.

## Data Files
- Contract template: `Assets/Scenarios/Templates/space4x.fleetcrawl.room_contract.v1.json`
- Concrete contract: `Assets/Scenarios/space4x_fleetcrawl_room_contract_v1.json`
- Pack catalog template: `Assets/Scenarios/Templates/space4x.fleetcrawl.pack_catalog.v1.json`
- Pack catalog: `Assets/Scenarios/space4x_fleetcrawl_pack_catalog_v1.json`
- Demo scenario: `Assets/Scenarios/space4x_fleetcrawl_room_contract_demo_micro.json`

## Contract Types
1. `RunDifficultyProfile`
- Controls global in-run scaling and unlock gating.
- Fields: `id`, `requiresMetaTag`, `budgetMultiplier`, `rewardMultiplier`, `hazardIntensityMultiplier`, `repairCostMultiplier`, `minibossChanceBonus`.

2. `RoomClassProfile`
- Controls local encounter spike and reward tension.
- Fields: `id`, `budgetMultiplier`, `rewardMultiplier`, `threatStepBonus`, `hazardSlotsDelta`, `minibossEligible`, `weight`.

3. `RoomArchetype`
- Controls objective intent and tag-driven pack selection.
- Fields: `id`, `threatLevelRange`, `systemSizeWeights`, `roomClassWeights`, `requiredPackTags`, `optionalPackTags`, `budgetShares`.

4. `SpawnPackRef`
- Reusable pack references for hazards, enemy waves, objectives, or wildcard content.
- Fields: `id`, `packType`, `budgetCost`, `weight`, `maxPerRoom`, `tags`, `requiresAnyTags`, `requiresAllTags`, `incompatibleWithTags`, `incompatiblePackIds`, `prefabRefs`.
- `prefabRefs` intentionally support the "JSON prefab" mental model (`prefabId` + count range).

## Runtime Budget Rule
`final_budget = system_base_budget * run_difficulty_multiplier * room_class_multiplier * depth_multiplier * threat_multiplier`

Contract parameters define the multipliers. Runtime uses this budget to pick `SpawnPackRef` entries by tags and cost.

## Miniboss Cadence
`minibossRules` define when miniboss rooms are legal:
- `minDepth`
- `eligibleAfterEliteRooms`
- `baseSpawnChance`
- `guaranteeByDepth`
- `cooldownRooms`

This keeps minibosses rare but guaranteed by a target depth.

## Content Authoring Rule
Author content as packs and tags, not as full matrix rows.  
Matrix rows are assembled at runtime from weights and budget rules.

## Parallel Work Fence
Desktop lane can add new pack content (`SpawnPackRef` + prefab IDs).  
Laptop lane can tune multipliers, weights, and selection rules.  
Both lanes avoid touching the same files in one slice.
