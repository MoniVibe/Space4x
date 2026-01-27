# Scenario Narrative Concepts

## Goals
- Deliver dynamic, elite-driven narrative scenarios where the player (especially in Godgame) can intervene, influence outcomes, and earn unpredictable rewards.
- Support a variety of setups (ambushes, duels, rescues, heists) tied to elite personalities, alignments, and situational triggers.
- Integrate with sociopolitical dynamics, elite crisis, buff/miracle systems, and narrative situations.

## Scenario Definitions
- `ScenarioDefinition` asset:
  - `ScenarioId`, `Title`, `Description`.
  - `TriggerConditions`: alignment/outlook, factions involved, current situations (war, raid, feud), relationship thresholds.
  - `Actors`: roles (Initiator elite, Opponent, Ally, Victim).
  - `Location` constraints (settlement, caravan route, dungeon, ship).
  - `PlayerInterventionOptions`: miracle costs, choices, skill checks.
  - `OutcomeTable`: success/failure branches with rewards/buffs/memory impacts.
  - `Randomness` parameters for unpredictability (weighted outcomes).

## Example Scenarios
- **Fanatic Duelist Ambush**:
  - Actor: chaotic warlike noble.
  - Trigger: noble travelling with caravan, high aggression, opportunity feuds.
  - Flow: noble allows ambush; player can encourage battle, attempt protection, or capture.
  - Outcomes: proper fight (reputation gain with warlike factions), capture-and-escape (noble prays for buffs, potential massacre), divine witness (player awards favor).
- **Duel of Brothers**:
  - Actors: two brothers with conflicting goals.
  - Trigger: family feud, high tension, opposing quests.
  - Player can intervene with ritual negotiation, buff/support one side, or sabotage duel.
  - Rewards: favor from chosen side, family morale shift, possible reconciliation/permanent feud.
- **Tragic Champion Duel**:
  - Actors: former friends forced to fight.
  - Trigger: opposing quests, external coercion.
  - Intervention: miracles to free them, challenge duel outcome, or accept tragedy for heroic relic.
  - Outcomes: saved champions, martyrdom buffs, or cursed relic.
- **Damsel Rescue**:
  - Actor: noble kidnapping (raid or dark ritual).
  - Trigger: relation thresholds, witness account.
  - Intervention: direct rescue, stealth infiltration, prayer for miracle.
  - Rewards: alliance, blessings, penalties if failed.
- **High Stakes Heist/Assassination**:
  - Actors: corrupt elites, guild agents.
  - Trigger: materialistic, chaotic alignment, guild mission.
  - Intervention: support heist, capture criminals, double-cross.
  - Rewards: fortune, reputation shift, buff/debuffs.

## System Flow
1. `ScenarioTriggerSystem` scans narrative state each cadence for eligible elites/situations; uses randomness to spawn `ActiveScenario` entity.
2. `ScenarioSetupSystem` prepares actors, location, initial buffs, schedules timeline events.
3. `ScenarioInteractionSystem` exposes choices to player (handle UI, ritual options) and collects AI responses.
4. `ScenarioResolutionSystem` applies outcome branches, rewards, buffs, memories, updates sociopolitical metrics.
5. `ScenarioCleanupSystem` removes scenario entities or transitions to follow-up scenarios.

## Player Intervention (Godgame emphasis)
- Ritual interface for miracles (grant buffs, pacify, inspire, punish).
- Resource spend (favor, mana, alignment currency) to sway outcomes.
- Observing only may still grant narrative insights or unlock future favor quests.

## Integration Points
- **EliteCrisisSystem**: scenario outcomes adjust security, tension, war risk.
- **SociopoliticalDynamics**: success/failure spawns situations (revenge raids, rebellions).
- **BuffSystem**: apply scenario-specific buffs/debuffs (heroic boon, curse, fear).
- **EconomySystem**: heists/assassinations affect market stability, wealth transfer.
- **FactionAndGuildSystem**: shift reputation, loyalty.
- **MetricEngine**: log scenario counts, success rates, favor gained.
- **NarrativeSituations**: scenarios may chain into multi-step story arcs.

## Technical Considerations
- Use deterministic random seeds (scenario id + tick) for unpredictable outcomes while preserving replay consistency.
- Store scenario state in SoA buffers (progress, actors, time remaining).
- Use history buffers for scenario events to support rewind and analytics.
- Schedule heavy computations (choice evaluation) sparingly, only when active.

## Testing
- Unit tests for trigger conditions and branching logic.
- Integration tests covering example scenarios under various player choices.
- Determinism tests ensuring same interventions yield consistent results on replay.
- Narrative tests verifying chained scenarios behave correctly.
