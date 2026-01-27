# Martial Mastery System

## Goals
- Establish a shared service for martial stances, styles, and knowledge thresholds that both games can consume deterministically.
- Couple stance mastery to buffs, traits, and command behaviors while staying compatible with rewind and analytics services.
- Model guild politics (alignments, forbidden arts, counters to mage guilds) and allow narrative systems to react to guild edicts.
- Support randomly generated, evolving masteries that can be recombined into hybrid stances when entities meet cross-training criteria.

## Core Components
- **MartialMasteryDefinition**: ScriptableObject authoring data capturing stance archetype, preferred weapons, training cadence, baseline modifiers, and thematic tags (discipline, alignment, doctrine).
- **MasteryKnowledgeTrack**: Blob asset describing XP thresholds, unlockable maneuvers, combo slots, risk states (fatigue, overexertion), and decay rules.
- **GuildProfile**: Defines guild alignment/outlook (benevolent, militant, mercenary, ascetic), forbidden techniques list, recruitment policies, tithe requirements, relations with mage guilds, and the guild’s stat slice across the Physical/Finesse/Will triad.
- **GuildProgressionTrack**: Captures guild-level XP, tier thresholds, mentor roster, facility upgrades, and access to tiered technique pools.
- **HybridRecipe**: Data describing how two or more stances can fuse (compatibility tags, required traits, resulting modifiers, failure outcomes).
- **MasteryRegistry**: DOTS registry hydrated at conversion; exposes lookups by stance id, doctrine, weapon family, and guild affiliation.
- **HonorStrataProfile**: Cultural configuration describing honor alignments/outlooks, rank ladders (e.g., Private→Grand Marshal), honor gain/loss triggers, and cross-culture sharing rules for compatible outlooks.

### Guild Stat Slices
- Tri-axis model (Physical, Finesse, Will) enumerates all possible slices; each slice has at least one guild archetype available per world seed (some worlds may spawn multiple competing guilds for the same slice).
- Slice metadata drives training outputs: primary/secondary stat gains, preferred weapon families, and associated buff packages.
- Default archetypes:
  - **Warrior Guilds (Physical)**: emphasize strength, vitality, melee throughput, proficiency in heavy/light melee weapons, and resilience buffs.
  - **Mage Guilds (Will)**: focus on intellect, wisdom, mana pool, spell amplification, ritual cadence, and education-level progression.
  - **Hunter Guilds (Finesse/Will)**: blend precision, awareness, stamina, mobility, and ranged weapon proficiency (bows, crossbows, firearms).
  - **Variant Mixes**: combinations such as Physical/Finesse (duelist guilds), Physical/Will (battle monks), or full tri-focus (polymath orders) provide specialized training curves, buff mixes, and gear requisition profiles.
- Slice assignments inform guild facilities, quest hooks, and AI behavior templates so residents receive consistent stat growth and stance unlock paths.

## Systems
- **MasteryGenerationSystem**: Procedurally derives new stance variants per world seed, mixing weapon focus, doctrine traits, and evolution hooks; persists templates in the registry and emits `MasteryGeneratedEvent` for narrative logging.
- **KnowledgePropagationSystem**: Manages teaching, rumor spread, and guild-sanctioned lessons; interfaces with `HeritageAndKnowledgeSystem` for lineage transfer and `QuestAndAdventureSystem` for mentoring quests; mentors unlock additional tier pools based on expertise.
- **MasteryProgressSystem**: Applies XP ticks from combat usage, sparring events, or study sessions; awards buffs when thresholds are met (`BuffSystem` integration) and toggles fatigue penalties for overtraining; watches guild tier to gate advanced maneuvers and hybrid unlocks.
- **HybridSynthesisSystem**: Evaluates entities eligible to blend stances; consumes `HybridRecipe`, checks guild permissions, and produces hybrid buff packages plus metadata for `MetricEngine`.
- **GuildGovernanceSystem**: Tracks guild leadership, edicts, forbidden arts, and diplomacy with mage guilds (`SociopoliticalDynamics` + `FactionAndGuildSystem`); issues sanctions or bonuses via buffs and quests; accrues guild XP from member accomplishments relayed through enclaves and promotes tiers when thresholds met.
- **GuildTechniqueSelectionSystem**: Allocates tiered ability picks per guild based on mentor presence, weapon preference tags, and current tier; updates available stance techniques and training curricula; unlocks counter-techniques after enemy intel dissemination.
- **GuildLorePropagationSystem**: Processes visit reports at enclaves, aggregates exploit tales, awards guild XP, and issues temporary or persistent bonuses against logged enemy archetypes; flags exceptional feats for legend candidacy.
- **LegendMemorializationSystem**: Elevates qualifying exploits into legends, generating reusable combat scripts, preferred loadouts, and ritual behaviors; influences AI stance selection and encourages members to mirror legendary techniques, gear enchantments, socketing patterns, and talisman hunts.
- **ArtifactAscensionSystem**: Promotes fully augmented gear (socketed, enchanted, runed) through rarity tiers and tracks artifacts that gain XP, knowledge, alignments, and narrative agency; hands off artifact lifecycle events to narrative, crisis, and quest systems.
- **HonorHierarchySystem**: Evaluates honor accrual/decay per entity and culture, applies glory multipliers, advances renown ranks, and synchronizes shared honor pools across cultures with matching alignment/outlook.
- **OutlookResponseSystem**: Aligns stance behavior modifiers with guild alignment (e.g., mercenary guild boosts income but attracts rival hostility); informs `EconomySystem` and `NarrativeSituations`.
- **MasteryConflictSystem**: Resolves clashes between martial and mage doctrines in scenarios or events; hooks into `EliteCrisisSystem` for escalations and `ScenarioNarratives` for branching outcomes.

## Integration
- **BuffSystem**: Stances yield deterministic buff bundles (damage, dodge offsets, cadence multipliers); forbidden arts can apply debuffs or corruption stacks.
- **SkillProgressionSystem**: Knowledge tracks feed into combat skill axes; mastery unlocks grant new action nodes in `StateMachineFramework`.
- **HeritageAndKnowledgeSystem**: Enables lineage-based training advantages, secret techniques, and cultural stance predispositions.
- **EconomySystem**: Guild dues, training fees, weapon requisition requests, and facility upkeep plug into resource registries and trade flows; slice metadata dictates commodity demand (e.g., warrior guilds seek steel and tonics, mage guilds consume reagents).
- **SociopoliticalDynamics**: Guild alignments influence faction relations, rebellion triggers, and public sentiment; mage counterbalances tracked here.
- **QuestAndAdventureSystem**: Adventures can recruit masters, obtain forbidden scrolls, or arbitrate guild disputes; quest rewards can unlock stances and capture exploit reports that feed guild XP and legend memorialization.
- **HeritageAndKnowledgeSystem**: Legends and enclave teachings propagate through lineage, granting inheritable familiarity with famed techniques or enemy counters.
- **CraftingQualitySystem & ProductionChains**: Higher-tier guilds demand specialized gear, reagents, or crafted training equipment; tier progression can unlock unique recipes or service contracts.
- **Item Augmentation Loop**: Socketing (jewels from jewelers using rare gems), enchanting (enchanters applying rare reagents), and runing (magical inscriptions) are driven by guild-requested upgrades; members prioritize enhancing signature or legendary gear, with automation hooks ensuring only top-tier items consume scarce upgrades unless abundance flags are met.
- **Rarity Ladder & Artifacts**: Successful application of all three augmentations advances items up the rarity ladder (Uncommon→Rare→Epic→Legendary→Artifact). Artifacts persist between owners, accrue experience and knowledge, adopt outlooks/alignments, and can transition into relics (vaulted items for dire crises), sentient/world-boss entities, or narrative catalysts calling heroes and changing world states.
- **Honor Economy**: Honor functions as a cultural glory multiplier and renown currency; cultures with aligned outlooks share honor strata, granting cross-faction recognition and enabling ranked titles to receive appropriate armament and armor either via guild provisioning or village logistics.
- **MetricEngine**: Telemetry snapshots capture stance adoption rates, guild influence, mastery success vs. failure outcomes for balancing.
- **PresentationGuidelines & VFXPoolingPlan**: Shared cues for stance activation, hybrid transitions, and forbidden art usage must respect pooling and rewind constraints.
- **Game Variants**:
  - *Godgame*: Divine champions can grant masteries to villages; guild politics affect pilgrimage quests and miracle prerequisites.
  - *Space4x*: Fleet marines adopt stances for boarding actions; hybrid styles may unlock zero-g maneuvers and ship boarding buffs.

## Technical
- Author registry hydration through bakers that compile definitions and guild data into Burst-friendly blobs; align with `RegistryDomainPlan`.
- Use deterministic RNG seeded per world shard to generate and evolve stances so replays match.
- Represent mastery status with enableable DOTS components (`MartialMasteryTag`, `HybridMasteryBuffer`) to minimize structural changes.
- Maintain cadence-aware schedulers (daily lessons, weekly tournaments) via `SchedulerAndQueueing`.
- Track forbidden arts via bitsets or bloom filters for quick compliance checks during combat resolution.
- Emit event buffers (`MasteryUnlockedEvent`, `GuildEdictEvent`) to avoid polling and keep rewind support in `HistorySystemGroup`.
- Provide Scriptable validation ensuring hybrid recipes reference compatible stances and guild policies don't deadlock progression.
- Integrate with inventory valuation services to rank equipment; upgrade routines target high-value or legendary-tagged items before considering common gear, unless `UpgradeAbundanceFlag` is set per guild or enclave.
- Artifact data should include XP curves, mood/outlook states, allegiance tags, crisis triggers, and ownership history; integrate with `NarrativeSituations`, `EliteCrisisSystem`, and `ScenarioNarratives` for lifecycle transitions.
- Ensure artifact promotion is deterministic: record augmentation sequence and rarity state changes for rewind; artifacts converted into relics or world bosses must register with `HistorySystemGroup` and crisis schedulers.
- Honor data needs deterministic accumulation rules, cross-culture sharing matrices, and rank entitlement tables to drive equipment provisioning and narrative recognition; integrate with `SociopoliticalDynamics` and village logistics for auto-equipping high-ranking officials.

## Testing
- **EditMode**: Validate registry hydration, RNG determinism for generated stances, hybrid compatibility checks, guild policy validation rules, and tier-gated technique pools.
- **PlayMode**: Scenario tests where entities train, unlock, and combine stances; ensure buffs apply/revoke correctly, guild edicts propagate to AI behaviors, guild XP/tier progression unlocks new curriculum, and legend memorialization influences AI loadouts as expected.
- **Rewind**: Confirm mastery progression, hybrid unlocks, and guild sanctions replay identically across rewinds.
- **Performance**: Stress check large populations (villagers or marines) learning multiple stances without exceeding frame budgets; profile Burst jobs.
- **Economy Integration**: Ensure socketing/enchanting/runing automation targets best-in-slot equipment and respects scarcity/abundance flags in both deterministic and rewind scenarios.
- **Artifact Lifecycle**: Validate artifact XP gain, alignment shifts, crisis escalation hooks, and relic vaulting; simulate narrative branches where artifacts become world bosses or summon champions.
- **Honor Progression**: Verify honor accrual, rank promotion, cross-culture sharing, and automatic gear provisioning behave deterministically and align with glory multipliers.
