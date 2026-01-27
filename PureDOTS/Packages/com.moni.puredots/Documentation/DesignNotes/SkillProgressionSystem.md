# Skill & Progression System Concepts

## Goals
- Provide a shared, deterministic framework for worker/elite progression where primary attributes (Physique, Finesse, Will) feed derived stats and profession mastery.
- Support three core XP pools plus a General pool that can reinforce any tree while remaining deterministic and tunable.
- Integrate with crafting quality, production chains, education, military, and narrative systems.

## Core Components
- `PrimaryAttribute` enum: `Physique`, `Finesse`, `Will`.
- `XpPool` enum: `Physique`, `Finesse`, `Will`, `General`.
- `SkillSet` component:
  ```csharp
  public struct SkillSet : IComponentData
  {
      public FixedList32Bytes<SkillEntry> Entries;
      public float PhysiqueXp;
      public float FinesseXp;
      public float WillXp;
      public float GeneralXp;
  }

  public struct SkillEntry
  {
      public SkillId Id;
      public byte Level;
      public float Progress; // 0-1 within current level
      public byte MasteryTier; // e.g., Apprentice, Journeyman, Master, Legendary
  }
  ```
- `EducationStats` component: holds `EducationLevel`, `Wisdom`, modifiers to XP gain and skill cap.
- `DerivedStats` struct (cached each tick or on demand):
  - `Strength = 0.8 * Physique + 0.2 * WeaponMastery`
  - `Agility = 0.8 * Finesse + 0.2 * Acrobatics`
  - `Intelligence = 0.6 * Will + 0.4 * Education`
  - `Wisdom = 0.6 * Will + 0.4 * Lore`
  - `Health = 50 + 8 * Physique + 2 * Fortitude`
  - `Stamina = 30 + 4 * Physique + 2 * Finesse + Endurance`
  - `Mana = 20 + 5 * Will + 2 * Intelligence + Attunement`
- `TaskDifficultyProfile`:
  - Base difficulty, attribute emphasis (weight per axis), recommended tools, tech requirements.
  - Maps task types (blacksmithing, carpentry, research) to expected XP yields.
- `MaterialQualityProfile`: reused from crafting; provides multipliers for XP based on purity/rarity.
- `XpModifier` component/buffer: temporary modifiers from buffs, facilities, narrative events.

## XP Calculation & Spillover
For each completed task (crafting, construction, research, combat):
```
float baseXp = TaskDifficulty.BaseXp;
float materialFactor = MaterialQuality.XpMultiplier;
float skillGap = math.max(0f, (TaskDifficulty.RequiredLevel - CurrentSkillLevel) * SkillGapWeight);
float educationFactor = 1f + EducationStats.EducationLevel * EducationMultiplier;
float wisdomFactor = 1f + EducationStats.Wisdom * WisdomMultiplier;
float xp = baseXp * materialFactor * educationFactor * wisdomFactor * (1f + skillGap);
```
- Award the primary pool matching the action (e.g., melee hit → PhysiqueXp). Apply configurable spillover (default 10%) to other pools based on weapon weight, technique tags, or ritual flags.
- Global modifiers: rested/mentor bonuses (+50–100%), diminishing returns per time window to discourage grinding loops.
- GeneralXp sources: quests, research breakthroughs, boss victories, prosperity ticks.
- Update specific skill entry (e.g., `Blacksmithing`) with xp to increase level/progress.
- Cap contributions if skill already at soft/hard cap defined by culture/tech services.

## Level Progression & Trees
- Use deterministic curve (e.g., exponential or polynomial) defined in `SkillLevelCurve` blob.
- On level up:
  - Increment `Level`, reset `Progress`.
  - Check thresholds for `MasteryTier` upgrades.
  - Emit events (for UI, narrative, education systems).
  - Optional stat bonuses (e.g., increased crafting speed, access to higher quality outputs).
- Trees map XP pools to specialisations:
  - `Might` tree consumes PhysiqueXP (tanking, heavy weapons).
  - `Cunning` tree consumes FinesseXP (ranged, stealth, precision).
  - `Insight` tree consumes WillXp (magic, leadership, discipline).
  - GeneralXp acts as wildcard; configuration decides 1:1 conversion or efficiency modifier (e.g., 1 General = 0.8 of target pool).
- Node cost formula: `Cost = BaseCost * pow(1.15f, purchasesAlongPath)`.
- Gates combine tree tier, derived stat thresholds, optional renown/quest requirements.
- Respec rules: partial refunds (50–60%) via trainers/rituals; never full reset to maintain commitment.

## Systems
- `XpGrantSystem`: listens to task completion events (crafting, combat, research) and applies XP to relevant entities.
- `SkillLevelSystem`: processes accumulated XP, handles level-ups, mastery progression, event emission.
- `SkillDecaySystem`: optional; slowly decays unused skills when no activity (configurable).
- `SkillSchedulerIntegration`: ensure skill updates occur after task completion but before systems depending on new skill levels.
- `AutoSpendSystem`: when no player queue exists, follows policy weights (Offense/Defense/Mobility/Utility/Support/Research). Reserves configurable fraction (default 20%) of each pool and only consumes GeneralXp when milestones are reachable; heuristic inputs include recent deaths, hit chance, mana shortages, and role preferences.

## Authoring & Baking
- `SkillDefinitionCatalog`: ScriptableObject defining skill ids, default caps, associated jobs/recipes, axis weights.
- `TaskDifficultyCatalog`: mapping of tasks to base xp, difficulty, attribute weights.
- `SkillLevelCurve` asset: per-skill or global xp requirements.
- Bakers convert catalogs to blob assets used by progression systems.

## Integration Points
- **Crafting Quality**: skill level feeds into quality calculation (higher level → better `CrafterQualityModifier`).
- **Production Chains**: workforce assignment filters by skill level; xp gained from completed orders.
- **Education Service**: training institutions apply xp modifiers or grant baseline skill levels.
- **Narrative Situations**: events can grant xp boosts, mentorship, or impose skill penalties.
- **Population Traits**: species/race traits adjust xp gain rates or caps.
- **Military**: combat tasks feed into relevant skill axes (Physical for melee, Finesse for archery, Insight for tactics).
- **Telemetry**: track progression rates, identify bottlenecks, highlight high-performing workers.
- **Spellcrafting**: Will/Insight progression ties into spell creation; crafting system consumes WillXp plus material quality to determine success and unlock new spell tags.
- **Combat resolution reference**:
  - `Accuracy = WeaponMastery + 0.5 * Finesse + ItemAccuracy`.
  - `Evasion = 0.7 * Finesse + Acrobatics + LightArmorBonus`.
  - `hitChance = clamp(0.05, 0.95, 1 / (1 + exp(-(Accuracy - Evasion)/15)))`.
  - On hit, resolve block/parry (convert % damage to stamina, apply counter bonuses) before armor/resist reductions.

## Technical Considerations
- Store xp values in SoA buffers (`NativeArray<float>`) for Burst processing; consider AoSoA if iterating numerous skills per entity.
- Use `IJobChunk` to process xp grants in parallel; guard against concurrent writes with `ParallelWriter` or per-axis accumulation buffers followed by reduction.
- Ensure xp grants and level-ups record state in history buffers for rewind support.
- Deterministic random seeds for rare events (critical inspiration) derived from entity id + tick.

## Testing
- Edit-mode: validate xp calculations for varying inputs (material quality, difficulty, education).
- Playmode: simulate long-running crafting/production sessions, ensure skill levels advance predictably.
- Regression: ensure rewinding restores skill values and xp correctly.
