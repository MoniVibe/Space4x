# Heritage & Knowledge System Concepts

## Goals
- Provide shared mechanics for racial traits inherited by offspring, cultural traits acquired/lost through exposure, and knowledge transmission (skills, languages, lore).
- Support unique abilities unlocked through knowledge mastery (spells, fighting stances, techniques).
- Integrate with skill progression, quests, economy, and narrative systems.

## Core Components
- `RacialTraitDefinition` asset:
  - Trait id, description, stat modifiers, inheritance weight.
  - Optional exclusive abilities or feature toggles.
- `CulturalTraitDefinition` asset:
  - Trait id, domain (art, governance, warfare, faith), adoption difficulty, decay rate.
- `KnowledgeDefinition` asset:
  - Knowledge id, category (language, history, arcane, martial), prerequisite knowledge, unlock rewards (abilities, recipes, spells).
- `LanguageDefinition` asset:
  - Language id, family, power rating, learning difficulty, decay rate, associated culture (mother language), special abilities unlocked.
- `RacialTraitSet` component on entity:
  - Packed list of trait ids, dominance/strength values, mutation chance.
- `CulturalTraitSet` component:
  - Active cultural traits with acceptance level (0-1), decay timers, exposure sources.
- `KnowledgeSet` component:
  - Known knowledge ids with proficiency (0-1), memorization state.
- `KnowledgeProgress` component:
  - Learning tasks, tutor references, time remaining, retention decay.
- `LanguageState` component:
  - Primary spoken language, mother language, proficiency per language, heritage bonuses, decay timers.
- `CultureFervor` component:
  - Fervor value (resistance to conversion), fertility modifier, breeding willingness placeholder.
- Cultures gain bonuses when spoken language matches mother language; penalties applied when mother language lost.
- `BreedingWillingness` component:
  - Matrix score combining cultural alignment, racial compatibility, xenophobia/xenophilia modifiers.
  - Factors: economic stability, food availability, mood/morale, mate suitability (status, traits, alignment), culture fervor.
  - Used to drive population growth and family formation decisions.

## Inheritance & Acquisition Systems
- `RacialInheritanceSystem`:
  - On birth/creation, combine parent traits using weighted chance (dominant/recessive models).
  - Allow random mutation or rare trait introduction.
  - Reinforce traits by environment/culture (chance to strengthen ancestral traits).
- `CulturalExposureSystem`:
  - Track exposure (time spent in culture, interactions, quests) and acceptance (alignment compatibility, willingness).
  - Acquire traits when exposure surpasses threshold; decay when acceptance drops or isolation occurs.
- `LanguageInheritanceSystem`:
  - New villages adopt languages from founding population (average of parent villages). Vagrants/nomads generate new languages.
  - Ancestral languages confer learning bonuses; lost languages can be resurrected by studying relics/scrolls.
- `LanguageLearningSystem`:
  - Learning speed based on language difficulty, power, learner aptitude (traits, education).
  - Powerful languages grant unique abilities but require higher effort; those with ancestral ties learn faster.
  - Languages can be lost when no proficient speakers remain; descendants receive reduced difficulty when reviving via relic study.
- `KnowledgeTransmissionSystem`:
  - Master/apprentice/tutor relationships transfer knowledge over time.
  - Parents pass baseline knowledge to children (language, lore) depending on proficiency and time spent.
  - Supports formal education institutions (schools, guild halls) boosting transfer rates.
- `KnowledgePracticeSystem`:
  - Entities must reinforce knowledge via practice; inactivity causes decay.
  - Memorization for complex skills (spells) requires repeated reinforcement or rituals.

## Unlocks & Abilities
- `AbilityUnlockSystem` checks knowledge sets to unlock special abilities:
  - Unique spells, martial stances, crafting recipes, diplomatic options.
- Languages with power ratings unlock linguistic abilities (ritual chants, diplomacy, spellcasting) at proficiency thresholds.
- Abilities integrate with skill progression (e.g., require certain skill level + knowledge).
- Cultural traits may modify access to knowledge or abilities (e.g., language unlocks diplomacy options).
- Racial traits may unlock unique resistances or incentives (flight, night vision, regeneration).

## Integration Points
- **SkillProgressionSystem**: knowledge contributes to skill xp, mastery tiers.
- **BuffSystem**: knowledge-based buffs (ritual knowledge, battle stances).
- **ScenarioNarratives**: scenarios reward knowledge or cultural adoption; certain outcomes depend on linguistic/historical understanding.
- **Quests**: certain quest types require specific knowledge or cultural traits.
- **MobileSettlementSystem**: language proficiency affects trade, diplomacy when roaming; fervor influences splinter culture resilience.
- **EconomySystem**: knowledge enables advanced production recipes, trade advantages.
- **IndustrialSectorSystem**: knowledge of techniques boosts facility scores.
- **FactionAndGuildSystem**: guild membership tied to knowledge proficiency; cultural drift tracked through trait adoption.
- **MetricEngine**: track knowledge distribution, cultural adoption rates, racial trait prevalence.

## Authoring & Config
- `RacialCatalog`, `CulturalCatalog`, `KnowledgeCatalog` assets with editors for inheritance weights, exposure rules, prerequisites.
- Tutors/education institutions reference these catalogs; narrative events can seed new knowledge.
- Provide validation for dependency cycles and conflicting traits.

## Technical Considerations
- Use SoA storage for trait/knowledge arrays to stay Burst-friendly.
- Language proficiency stored as arrays/bitsets with decay tracking.
- Traits as bitsets or hashed ids for efficient storage.
- Inheritance logic deterministic (use hashed seeds). Exposure tracked via counters updated by scheduler.
- Knowledge practice jobs run at cadenced intervals to avoid per-frame cost.
- Rewind: record knowledge gains; reapply during playback.

## Testing
- Unit tests: inheritance probabilities, cultural adoption, knowledge transfer.
- Integration tests: ability unlocks, education institutions effectiveness.
- Determinism tests: record/replay heritage outcomes.
- Scenario tests: quests requiring specific knowledge/culture succeed/fail as expected.
- Language tests: verify inheritance, loss, resurrection, and power-based ability unlocks behave as configured.
