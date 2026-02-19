# Space4X Content Intent Adapter v0

Shared taxonomy version: `puredots/Docs/ContentIntent/MVP_Content_Taxonomy_v0.md`  
Adapter status: provisional (runtime in active rework)

## Purpose

Map shared intent IDs into current Space4X scenario/content data so teams can test intent now, then retune later.

## Mapping Rules

1. Keep shared intent IDs stable.
2. Use current scenario structures as temporary projection.
3. Keep placeholder readability explicit with an entity legend.

## Intent Mapping (Current MVP)

| Shared intent ID | Current Space4X projection (MVP) |
|---|---|
| `intent.civilian.shuttle` | `Carrier` with no combat package, `disposition: Civilian|Support` |
| `intent.civilian.convoy_freighter` | `Carrier` with storage-heavy config, `disposition: Civilian|Hauler|Trader` |
| `intent.civilian.liner` | `Carrier` with high "population proxy" value via docs tag + mission objective |
| `intent.civilian.salvage_tug` | `MiningVessel` or support `Carrier`, `disposition: Support|Civilian` |
| `intent.colony.frontier_outpost` | clustered protected node (carrier + deposits + mission anchor) |
| `intent.colony.industrial_hub` | economy-enabled node using production actions and throughput routes |
| `intent.colony.refuge_settlement` | protected retreat node with evacuation/escort mission emphasis |
| `intent.station.trade_post` | economy action node (business create/add/request) + mission source |
| `intent.station.refit_yard` | refit/station lane via refit scenario structures |
| `intent.station.black_market` | high-risk business node with standing/reputation pressure |
| `intent.shop.field_vendor` | temporary business inventory pulse (timed actions) |
| `intent.cache.salvage_stash` | off-lane resource deposit with limited extraction profile |
| `intent.cache.military_lockbox` | rare/high-value deposit placement in contested zone |
| `intent.cache.decoy_trap` | lure cache with hostile intercept timing |
| `intent.mission.escort_civilians` | escort/defense objective set over civilian-tagged entities |
| `intent.mission.secure_corridor` | route control objective via movement/intercept timings |
| `intent.mission.recover_cache` | timed pickup objective around cache node |
| `intent.mission.defend_colony` | survival objective for colony-proxy nodes |
| `intent.mission.intercept_raider` | explicit intercept action chain |
| `intent.boss.raider_warlord` | hostile carrier wave lead with aggressive profile |
| `intent.boss.siege_carrier` | high-durability hostile anchor with objective pressure |
| `intent.boss.salvage_predator` | opportunistic hostile reinforcement phase |

## Placeholder Entity Legend (Required in Playtests)

Use this legend in test notes/HUD overlays while art is placeholder:

- Civilian shuttle: `CIV-SH`
- Civilian convoy freighter: `CIV-CF`
- Civilian liner: `CIV-LN`
- Salvage tug: `CIV-ST`
- Frontier outpost: `COL-FO`
- Industrial hub: `COL-IH`
- Refuge settlement: `COL-RS`
- Trade post: `ST-TP`
- Refit yard: `ST-RY`
- Black market: `ST-BM`
- Field vendor: `SH-FV`
- Salvage stash: `CH-SS`
- Military lockbox: `CH-ML`
- Decoy trap: `CH-DT`
- Raider warlord boss: `B-RW`
- Siege carrier boss: `B-SC`
- Salvage predator boss: `B-SP`

## Scope Guardrails

- Do not change shared intent meaning in Space4X adapter docs.
- Do not encode final visual assumptions here.
- Keep this adapter focused on current projection only.

## Contract Mapping (Combat + Mining v0)

Space4X currently projects contract-like fields through:
- `space4x/Assets/Scripts/Space4x/Scenario/Space4XMiningScenarioSystem.cs`
- `space4x/Assets/Scenarios/*.json`

Canonical contract ownership for next passes:
- `../../puredots/Docs/Canonicity/Combat_Mining_DataContracts_v0.md`
- `../../puredots/Docs/Canonicity/Data_Contract_Canon_Sprint_v0.md`
- `../../puredots/Docs/Canonicity/canonical_contracts.v0.json`
- `../../puredots/Docs/Canonicity/canonical_contract_payloads.v0.json`
- `../../puredots/Docs/Canonicity/Payloads/*`
- `../../puredots/Docs/Canonicity/Schemas/contract.mining.v0.schema.json`
- `../../puredots/Docs/Canonicity/Schemas/contract.combat.v0.schema.json`
- `../../puredots/Docs/Canonicity/Schemas/contract.room_profile.v0.schema.json`
- `../../puredots/Docs/Canonicity/Schemas/contract.scenario_envelope.v0.schema.json`
- `../../puredots/Docs/Canonicity/Schemas/contract.mission_objective.v0.schema.json`
- `../../puredots/Docs/Canonicity/Schemas/contract.loot_cache.v0.schema.json`
- `../../puredots/Docs/Canonicity/Schemas/contract.encounter_profile.v0.schema.json`

Rule: Space4X scenario JSON may keep projection fields for compatibility, but deterministic meaning must map back to shared contract IDs.
