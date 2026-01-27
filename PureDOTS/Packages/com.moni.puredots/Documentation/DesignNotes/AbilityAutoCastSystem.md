# Ability Auto-Cast System

## Goals
- Provide a deterministic, Burst-friendly service that evaluates when entities should auto-cast spells or abilities without player input.
- Share auto-cast rules between PureDOTS runtime systems and per-game adapters so both Godgame and Space4x reuse the same evaluation core.
- Support configurable trigger conditions, priority queues, cooldown/resource management, and narrative constraints for abilities originating from martial masteries, mage guilds, or other registries.

## Core Components
- **AutoCastProfile**: Authoring data describing ability id, trigger conditions (health thresholds, enemy proximity, buff/debuff states), priority weight, cooldown overrides, and resource budgets.
- **AutoCastConditionSet**: Blob asset compiling condition predicates (stat comparisons, tag checks, environmental probes) into Burst-evaluable graphs.
- **AutoCastState** (`IComponentData`): Tracks per-entity cooldown timers, resource reserves earmarked for auto-cast, last evaluation timestamp, and suppression flags (player override, forbidden zone).
- **AutoCastQueueEntry** (`IBufferElementData`): Stores pending ability casts with priority score, target selection info, and deterministic ordering hash.
- **AutoCastRegistry**: Central registry mapping abilities to their auto-cast profiles, condition sets, and adapter callbacks for presentation commands.

## Systems
- **AutoCastEvaluationSystem**: Iterates entities with auto-cast capability, evaluates condition sets, computes priority scores, and enqueues eligible abilities into deterministic queues.
- **AutoCastCooldownSystem**: Updates cooldown timers, resource pools, and clears suppression flags once requirements reset; integrates with `SchedulerAndQueueing` for cadence alignment.
- **AutoCastTargetingSystem**: Resolves target queries (self, ally, enemy, area) via `PerceptionSystem`, `SpatialServicesConcepts`, or `UniversalNavigationSystem` depending on ability metadata.
- **AutoCastExecutionSystem**: Dequeues approved abilities, issues command buffer calls into combat/spellcasting systems, and emits `AutoCastExecutedEvent` for analytics and rewind.
- **AutoCastSuppressionSystem**: Listens for player overrides, narrative restrictions, or guild edicts (`LegendMemorializationSystem`, `GuildGovernanceSystem`) to temporarily disable certain auto-casts.
- **AutoCastAdapterBridge**: Game-specific system that translates `AutoCastExecutedEvent` into per-project presentation and animation triggers (Godgame divine hand effects, Space4x ship VFX).

## Integration
- **BuffSystem**: Auto-casts may apply defensive/offensive buffs; condition sets use buff state checks to trigger reactions (e.g., cleanse when poisoned).
- **Combat Resolver**: Execution system feeds ability commands into shared combat resolution pipelines; ensures initiative and turn-order respect auto-cast outputs.
- **ResourceAuthoringAndConsumption**: Resource budgets for mana, stamina, ammo ensure auto-casts respect existing consumption rules.
- **SchedulerAndQueueing**: Aligns evaluation cadence (e.g., every N simulation ticks) and staggered processing to avoid spikes.
- **StateMachineFramework**: Auto-cast states integrate with entity behavior states, allowing certain states to unlock or forbid abilities.
- **MetricEngine**: Records auto-cast frequency, success rates, and resource efficiency for balancing.
- **NarrativeSituations & QuestAndAdventureSystem**: Narrative flags can temporarily enable/disable auto-casts (e.g., scripted duels, stealth missions).
- **MartialMasterySystem**: Stance-based abilities register auto-cast profiles; guild legend memorials influence condition sets (trigger on legendary foe archetypes).
- **Honor Hierarchy**: High-rank officers may gain exclusive auto-cast abilities or stricter suppression rules to align with cultural honor expectations.
- **Game Variants**:
  - *Godgame*: Villager priests auto-cast miracles when settlements are threatened; divine champions trigger stance combos during sieges.
  - *Space4x*: Ship AI auto-fires abilities (shield pulses, EMP bursts) based on fleet command directives and threat matrices.

## Technical
- Condition sets compiled into SoA data (threshold arrays, tag masks, cooldown structs) for Burst evaluation; avoid branching by using bitmask operations.
- Deterministic priority resolution via stable sorting (priority weight, ability id, entity id) to keep replays consistent.
- Support autority toggles: player-issued commands can temporarily suppress or queue specific abilities; ensure synchronization between client and server in multiplayer contexts.
- Provide authoring validation to catch conflicting conditions, missing resource budgets, or mutually exclusive auto-casts.
- Use `IEnableableComponent` flags for quick enable/disable without structural changes.
- Ensure auto-cast events are recorded in `HistorySystemGroup` so rewinds re-enqueue the same abilities in the same order.
- Hook into `PresentationGuidelines` to keep VFX/animation dispatch decoupled from logic and rewind-safe.

## Testing
- **EditMode**: Validate condition set compilation, priority resolution, and deterministic ordering across seeds.
- **PlayMode**: Simulate combat scenarios ensuring auto-casts trigger under correct conditions, respect cooldown/resource limits, and interact properly with player overrides.
- **Rewind**: Confirm ability queues and executed events replay identically across rewind cycles.
- **Performance**: Stress test large numbers of entities with multiple auto-cast abilities, profiling evaluation and targeting jobs.
- **Integration**: Verify adapters in Godgame and Space4x translate execution events into the correct presentation/commands without desync.
