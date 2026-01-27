# Faction, Guild, and Organization System Concepts

## Goals
- Provide a shared framework for factions/empires, guilds/companies, bands/armies, and fleets/armadas across projects.
- Align registries, diplomacy, compliance, morale, and economic integration.
- Support multi-layer membership (individuals belonging to factions, guilds, fleets simultaneously) with conflict resolution.

## Faction/Empire Model
- `FactionRegistry`:
  ```csharp
  public struct FactionRegistryEntry : IBufferElementData
  {
      public Entity FactionEntity;
      public FactionId Id;
      public float Influence;
      public float Reputation;
      public FactionStatusFlags Flags;
  }
  ```
- `FactionProfile` component:
  - Alignment vector, culture, doctrines, home territory.
  - Policies (tax rates, military stance) linking to economy/buff systems.
- `DiplomacyState` component between factions:
  - Relation score, treaty types, war status, embargo flags.
- `FactionMembership` buffer on individuals/settlements:
  - `FactionId`, `Role`, `Loyalty`, `Compliance`.
- Systems:
  - `FactionInfluenceSystem`: updates influence, spreads ideology via spillover.
  - `DiplomacyUpdateSystem`: resolves relation changes, treaty triggers, conflict declarations.
  - `FactionPolicySystem`: applies economic buffs/policies.

## Guilds & Companies
- `GuildRegistry` similar to factions but focused on specialization (trade, craft, military support).
- `GuildProfile`:
  - Domain (trade, craft, research), dues, collaboration policies (ties into industrial spillover).
- `GuildMembership` buffer with rank, contribution, benefits.
- Systems:
  - `GuildCollaborationSystem`: boosts spillover, skill progression.
  - `GuildContractSystem`: manages internal trade/work orders.
  - `GuildConflictSystem`: handles guild schisms, rivalries.

## Bands & Armies
- `BandRegistry` already present in runtime (combat module). Extend with doc coverage:
  - `BandProfile`: type (militia, professional army), morale, supply state.
  - `BandCommand` component for orders, hierarchical structure.
  - `BandMoraleSystem`: integrates with buffs, perception, supply.
  - `BandLogisticsSystem`: pulls from resource/economy for upkeep.
  - `BandCompliance`: ensures alignment/outlook interplay (Space4x compliance).

## Fleets & Armadas
- `FleetRegistry` (Space4x) with `FleetProfile`: class, flagship, capacity, command traits.
- `FleetOfficer` components referencing captains (alignment/outlook), skill progression.
- `FleetMorale`, `FleetSupply` components.
- Systems:
  - `FleetNavigationSystem` (mobile settlements integration).
  - `FleetTacticsSystem`: applies buffs/abilities, integrates with skill progression.
  - `FleetComplianceSystem`: ensures crew alignment vs faction doctrines.

## Cross-Layer Membership & Compliance
- Individuals may belong to multiple organizations (faction, guild, band, fleet).
- Maintain `AffiliationSet` component with references and weights.
- `ComplianceSystem` (Space4x alignment TODO) resolves conflicts: raises pressure when ethics clash (mutiny, deserter events).
- Integrate with buff system (mutiny debuffs, loyalty buffs).

## Integration Points
- **Economy**: factions/guilds own markets, levying taxes/tariffs; bands/fleets consume supply via trade routes.
- **Industrial Sectors**: guilds enhance collaboration; factions set industrial policies.
- **Mobile Settlements**: captains align with faction/guild goals; splinter groups form new factions.
- **Metric Engine**: track influence, loyalty, compliance, war intensity, trade balance per faction/guild.
- **Narrative**: events triggered by guild schisms, faction coups, fleet mutinies, band victories.
- **Buff System**: statuses like `WarFooting`, `GuildIncentive`, `FleetInspired` applied via buff triggers.
- **Perception**: faction-specific visibility (fog-of-war, rumor networks).
- **Quest & Adventure**: urgent counter-ops (rescue leaks, assassinate traitors) issued to high-skill members; forced assignments impact grievances based on outlook alignment.
- **Interrogation & Leaks**: captured members face interrogation influenced by morale/willpower; intel leaks enable raids/extortion, while guilds/factions run counter-operations to silence leaks. Interrogations are typically unlawful outside active conflicts except against openly hostile archetypes (assassins/thieves).

## Authoring & Config
- `FactionCatalog`, `GuildCatalog` assets: alignment vectors, doctrines, policies, default relations.
- `BandTemplate`, `FleetTemplate` for unit compositions, morale rules.
- Bakers convert to blobs consumed by registries.

## Technical Considerations
- Registries follow deterministic builder pattern; integrate with `RegistryContinuityContracts`.
- SoA storage for influence, morale arrays to keep Burst-friendly.
- Systems scheduled in appropriate groups (diplomacy in gameplay, morale in combat, policy in economy).
- Rewind support: record diplomacy events, membership changes via history buffers.
- Use `SchedulerAndQueueing` for periodic policy evaluations, loyalty checks.

## Testing
- Unit tests for registry build, membership add/remove, compliance calculations.
- Integration tests for diplomacy state transitions, policy effects on economy/industrial sectors.
- Simulation tests for guild collaboration, fleet morale under supply stress.
- Determinism tests for wars/splinter events record/playback.
