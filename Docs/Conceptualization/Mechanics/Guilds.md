# Mechanic: Guilds

## Overview

**Status**: Concept / Contract  
**Complexity**: Moderate  
**Category**: Social / Political / Economy

**One-line description**: Guilds are aggregate organizations that pool members and assets, can be contracted, and exert influence across factions and empires.

## Core Concept

- Guilds are aggregate entities (like fleets, dynasties, corporations) with pooled resources and their own leadership, knowledge, and missions.
- Members can hold multiple allegiances (guild, faction, empire, business) simultaneously.
- Guilds can act politically/economically (strikes, coups, war, embassies) based on governance, outlook, and standing.
- Guilds are not tied to villages; villages may host embassies or enclaves based on relations.

## Data Contract Snapshot

Core aggregate identity (PureDOTS):
- `PureDOTS.Runtime.Aggregates.Guild`, `GuildLeadership`, `GuildMember`, `GuildOutlookSet`, `GuildEmbassy`, `GuildRelation`, `GuildVote`, `GuildTreasury`, `GuildMission`, `GuildWarState` in `puredots/Packages/com.moni.puredots/Runtime/Runtime/Aggregates/GuildComponents.cs`.

Guild economy/knowledge (PureDOTS):
- `PureDOTS.Runtime.Guild.GuildId`, `GuildWealth`, `GuildStrike`, `GuildRiot`, `GuildCoup` in `puredots/Packages/com.moni.puredots/Runtime/Runtime/Guild/GuildComponents.cs`.

Catalogs (PureDOTS):
- `GuildTypeSpec`, `GuildConfigState` in `puredots/Packages/com.moni.puredots/Runtime/Runtime/Guild/GuildCatalog.cs`.
- `GuildActionSpec`, `GuildActionConfigState` in `puredots/Packages/com.moni.puredots/Runtime/Runtime/Guild/GuildActionCatalog.cs`.

Aggregate bridge (PureDOTS):
- `AggregateIdentity`, `AggregateStats`, `AmbientGroupConditions`, `MotivationDrive` in `puredots/Packages/com.moni.puredots/Runtime/Runtime/Aggregate/AggregateComponents.cs`.
- Adapter system: `puredots/Packages/com.moni.puredots/Runtime/Systems/Guild/GuildAggregateAdapterSystem.cs`.

Space4X memberships:
- `Space4X.Registry.GuildMembershipEntry`, `GuildMemberEntry`, `BusinessGuildLink` in `space4x/Assets/Scripts/Space4x/Registry/Space4XOrganizationRelationComponents.cs`.
- Sync system: `space4x/Assets/Scripts/Space4x/Registry/Space4XOrganizationRelationSystem.cs`.

## Formation Paths

- Bottom-up charter: `PureDOTS.Runtime.Guild.GuildCharter` + `PureDOTS.Systems.Guild.GuildCharterFormationSystem`.
- Organic clustering by alignment/goals/proximity: `PureDOTS.Systems.Aggregates.GuildFormationSystem`.
- Top-down spawn stub: `PureDOTS.Systems.Guild.GuildSpawnSystem`.

## Space4X Usage Notes

- Guild affiliations are tracked per entity via `AffiliationTag` and mirrored into guild/member buffers.
- Guild standing is referenced by economy gating (see `space4x/Assets/Scripts/Space4x/Registry/Space4XEconomySystem.cs`).

## Gaps / Alignment Notes

- `GuildFormationSystem` still uses legacy `GuildMembership` while charter formation uses `GroupMembership`; consolidation needed.
- `GuildOutlookSet.IsFanatic` is set from alignment strength in formation; align with the final outlook thresholds later.

## Next Decisions

- Pick canonical knowledge/wealth/leadership components for runtime and migrate call sites.
- Decide which formation path is authoritative for Space4X slice (charter vs organic vs top-down).
