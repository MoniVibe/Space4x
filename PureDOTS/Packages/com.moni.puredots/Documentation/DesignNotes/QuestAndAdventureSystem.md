# Quest & Adventuring System Concepts

## Goals
- Allow entities (villagers, elites, colonists) to pursue quests based on initiative, alignment, desires, and village state.
- Support multiple simultaneous quests with varied durations (timed vs. indefinite).
- Handle party formation (solo vs. band), travel logistics, and interaction with village workforce accounting.
- Respect village policies (e.g., authoritarian restrictions) and content availability (horses, wagons, stagecoaches).

## Core Components
- `QuestDefinition` asset:
  - `QuestId`, `Title`, `Description`.
  - `Type` (Adventure, Rescue, Heist, Research, Pilgrimage).
  - `DurationType` (timed, indefinite, ritual, periodic trigger).
  - `AlignmentRequirements`, `InitiativeThreshold`, `VillageStateConstraints`.
  - `PartySize`, `PreferredCompanions` (guild, family, faction).
  - `Rewards` (favor, loot, buffs, reputation, memories).
- `QuestDesireProfile` per entity:
  - Weighted desire vector for quest types, motivations (glory, wealth, faith, knowledge, rebellion).
- `ActiveQuest` component:
  ```csharp
  public struct ActiveQuest : IBufferElementData
  {
      public QuestId Id;
      public float StartTime;
      public float Duration;
      public QuestStatus Status; // Planning, Active, Paused, Completed, Failed
      public Entity PartyLeader;
      public DynamicBuffer<Entity> PartyMembers;
      public QuestFlags Flags; // Timed, Indefinite, Ritual, Restricted
  }
  ```
- `AdventureState` component on entity:
  - `HomeVillage`, `IsCountedInWorkforce`, `TravelMode`, `Destination`, `TravelProgress`.
- `VillagePolicy` component:
  - `AllowAdventures`, `TravelRestrictions`, `StagecoachAvailability`, `HorseOwnership`.

## Decision Flow
1. `QuestOpportunitySystem` scans definitions each cadence, checks triggers (global events, narrative hooks, village state, player actions).
2. `QuestDesireSystem` per entity calculates propensity using alignment, desire profile, current situation (e.g., boredom, ambition, desperation).
3. `VillagePolicySystem` evaluates permissions: authoritarian villages may disallow leaving; some villagers ignore rules (chaotic).
4. `QuestSelectionSystem` assigns quests:
   - Entities may choose multiple quests if desires align and policies allow.
   - Contemplates current commitments, risk, potential rewards.
5. `PartyFormationSystem` groups companions if quest type encourages it (banding, guild missions).
6. `WorkforceAccountingSystem` marks adventurers as temporarily absent (remove from workforce metrics but keep village allegiance).

## Travel Mechanics
- Travel mode determined by distance, availability:
  - Foot (short distances).
  - Horse (if domesticated and accessible) for medium/long distances.
  - Wagon/stagecoach for long routes or inter-village travel (requires service availability).
- Integrate with `UniversalNavigationSystem` for pathfinding.
- `TravelMode` influences speed, resource consumption, and event risk (bandits, storms).
- Stagecoach services operate as economic entities (guilds/villages offering transport) with scheduling.

## Quest Progression
- `QuestProgressSystem` updates quest status (time remaining, objectives met).
- `QuestEventSystem` handles outcomes: success, failure, branching events, narrative triggers.
- `QuestRewardSystem` distributes rewards (buffs, loot, favor, metric updates).
- `QuestReturnSystem` restores adventurers to village; re-integrate into workforce and register experiences (skill gains, memories).

## Integration Points
- **NarrativeSituations**: quests spawn situations (rescue, duel) and tie into scenario narratives.
- **BuffSystem**: apply quest-specific buffs (blessings, curses) during journey and on completion.
- **SkillProgression**: grant XP gains tied to quest type.
- **EconomySystem**: quests consume resources, pay stagecoach fees, deliver loot to markets.
- **SociopoliticalDynamics**: quest outcomes can influence tensions, collective memories (heroic tales, tragedies).
- **MobileSettlementSystem**: adventurers may board ships/fleets for distant quests.
- **MetricEngine**: track `active_quests`, `quest_success_rate`, `adventure_population` vs workforce.

## Authoring & Config
- `QuestCatalog` defined per domain (Godgame, Space4x) but sharing base structure.
- `DesireProfile` assets for personality archetypes (fanatic, scholar, mercenary).
- `VillagePolicyProfile` mapping government types to adventure permissions.
- Event hook definitions (global quest offerings, player-triggered quests).

## Technical Considerations
- Store active quests in SoA buffers for performance.
- Use scheduler for timed quests (e.g., weekly check-ins, deadlines).
- Ensure determinism by sorting quest selection decisions (tie-breaking by entity id).
- For stagecoach services, maintain schedules via trade route system.
- Rewind: record quest events; reapply on playback.

## Testing
- Unit tests: quest selection criteria, policy enforcement, travel mode determination.
- Integration tests: quest progress under various travel modes, multiple concurrent quests.
- Narrative tests: ensure quests trigger scenario narratives correctly.
- Determinism tests for quest selection and outcome replay.
