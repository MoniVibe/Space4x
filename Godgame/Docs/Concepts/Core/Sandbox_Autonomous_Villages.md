# Sandbox Autonomous Villages

**Status:** Draft  \
**Category:** Core  \
**Scope:** Global  \
**Created:** 2025-11-01  \
**Last Updated:** 2025-11-01

---

## Purpose

Enable a hands-off sandbox where large maps evolve from villager seeds into self-governed villages whose culture, expansion, and conflicts emerge from their shared outlooks and alignments.

**Primary Goal:** Let villages self-organize and progress without mandatory player input.  
**Secondary Goals:**
- Support observation-driven play where the player can nurture or disrupt communities at will.
- Provide a living baseline that other modes (campaign, skirmish, multiplayer) can build upon.
- Maintain systemic hooks for future Bands, Miracles, Logistics, and Alignment truth sources.

---

## System Overview

### Components

1. **World Map & Biomes:** Resource-bearing terrain tiles and nodes that seed the economy. - Resource
2. **Villager Populations:** Autonomous agents with needs, jobs, latent alignments, and personal initiative scores that shape task urgency. - Actor
3. **Village Cohesion:** Grouping logic that aggregates villagers into evolving settlements. - Rule
4. **Shared Outlook Alignment:** Open-ended set of outlooks/alignments (peaceful, expansionist, spiritual, martial, scholarly, etc.) derived from member behaviors that governs how surplus gets reinvested. - Rule
5. **Initiative Signals:** Per-entity and village-level initiative ratings determining pace, risk tolerance, and how aggressively plans execute. - Rule
6. **Player Presence:** Optional godly interventions (miracles, hand actions) that influence worship and alignment. - External Driver

### Connections

```
World Map → Provides → Resource Nodes & Vegetation
Villager Populations → Harvest → Resource Stores
Resource Stores → Sustain → Village Cohesion
Village Cohesion → Shapes → Shared Outlook Alignment
Shared Outlook Alignment → Modulates → Initiative Signals
Initiative Signals → Bias → Expansion & Band Formation Decisions
Player Presence → Modifies → Alignment, Initiative & Worship Propensity
```

### Feedback Loops

- **Positive:** Prosperous villages stockpile resources → morale rises → initiative climbs → birth/immigration increase → larger labor pool accelerates expansion.  
- **Negative:** Scarcity or disasters drain stores → morale drops → initiative collapses → villagers disperse or perish → remaining villagers redistribute, allowing recovery if conditions improve.  
- **Catalysts:** Miracles, crises, and personal events (loss, vengeance vows, festivals) spike or suppress initiative depending on each entity's outlook.  
- **Balance:** Worship feedback moderates player influence—benevolent miracles raise devotion (more prayer power), overreach or neglect shifts alignment and initiative away from the player.

---

## System Dynamics

### Inputs
- Initial village seeds and villager archetypes per biome.
- Ambient world events (weather shifts, wild resource growth/decay, neutral threats).
- Player miracles or interventions when the player chooses to act.
- Emotional catalysts: losses, victories, miracles, threats, festivals, and personal relationships that spike or suppress initiative based on entity outlook.

### Internal Processes
1. **Settlement Formation:** Villagers evaluate proximity, shared outlooks, and resource access to cluster into nascent villages.
2. **Resource Flow:** Harvesters convert node reserves into storehouse inventories; vegetation nodes regenerate, while mines remain depleted.
3. **Cultural Alignment:** Aggregates villager outlooks, recent events, and player actions to set village stance (e.g., isolationist, expansionist, zealous).
4. **Initiative Budgeting:** Entities and villages roll initiative against opportunity scores (threat, distance, effort, reward) to decide which plans execute first; lawful outlooks dampen delta and favor stability, while chaotic outlooks spike or crash initiative quickly in response to stimuli.
5. **Expansion Logic:** Surplus population and morale, weighted by initiative, trigger alignment-directed investments (e.g., isolationists fortify, expansionists spawn outposts, zealots channel surplus into miracle preparation).
6. **Worship Response:** Villages track miracle aid versus neglect/abuse, updating their worship intensity, minting worship mana, and adjusting willingness to align with the player; miracles can grant temporary initiative surges for faithful alignments.
7. **Village Founding:** Like-minded entities band together autonomously, scouting for resource nodes and forming settlements within reasonable distance of food/ore/vegetation clusters; distance tolerance depends on logistics outlooks and initiative.
8. **Knowledge Advancement:** Educated individuals researching in schools, universities, and academies contribute to tech progression; high-education, high-wisdom occupants accelerate advancement tiers when facilities are staffed.
9. **Governance Cycling:** Leadership evaluation runs periodically, checking outlook-driven rules (inheritance, elections, acclaim) to refresh elites without explicit player input.
10. **Resource Economy:** Villagers harvest primary resources (ore, wood, food), refine them into metals or specialized materials, and route goods through local production chains (toolmakers, weapon/armor smiths, artisan workshops) and trade caravans.
11. **Tribute & Miracles:** Player (or AI god) fulfills villager prayers (quests) or nurtures player-owned villages to earn tribute; tribute unlocks miracle families and grants bonuses when advanced villages pledge support.

**Personality Modulation:** Each entity's temperament filters initiative swings—stoic or lawful personalities convert shocks into gradual trend changes, while volatile or chaotic personalities react with sharp spikes or crashes. Traumas such as exile or the death of a bonded villager can impose depression debuffs (initiative floor) or vengeance buffs (initiative ceiling) depending on personal outlook and relationship strength.

**Event Resolution Roll:** When impactful events fire (death, miracle, threat, festival), entities roll a weighted outcome table driven by their outlook/alignment and purity state (`Pure`, `Neutral`, `Corrupt`). Initiative shifts act as an offset to push the probability toward faster action. Examples:
- *Lawful Materialistic:* Inherit or assume duty (business takeover) with high probability; initiative determines how quickly they reorganize operations.
- *Lawful Spiritual:* Accept loss, gain temporary worship-mana bonus, and redirect initiative toward temple service rather than retaliation.
- *Pure Evil Warlike:* Almost always channel grief into vengeance bands targeting the aggressor's village, with high initiative yielding immediate raids.
- *Good Warlike (Neutral purity):* Favor conquest and vassalization over annihilation, scaling response timing with initiative.
- *Corrupt Good Warlike:* Wrestle between ideals and grudges—often strike preemptively under the banner of “justice,” with initiative tilting whether they escalate or seek symbolic penance after.
- *Neutral Warlike:* 50/50 roll between vengeance raid or diplomatic dominance; initiative biases the outcome toward whichever option triggers sooner.
This roll framework extrapolates to every outlook/alignment combination, letting future content author new behaviors by extending the weighted tables.

### Outputs
- Village roster with state (size tier, alignment, morale, worship intensity).
- Storehouse and reserve metrics for each settlement.
- Bands/armies spawned in response to alignment goals or external pressure.
- Telemetry hooks for sandbox health (population trend, alignment-directed expansion cadence, worship-mana balance).
- Initiative telemetry for entities and villages (average, variance, spikes) to explain rapid expansions or stalls.
- Tech tier tracking (1–20 scale) derived from research institutions and educator cohorts.
- Resource stock telemetry covering raw, refined, and luxury goods per settlement.

---

## State Machine

### States
1. **Nascent:** Small cluster stabilizing basic needs. Entry: villagers share ≥1 outlook and co-locate near resources. Exit: reach minimum population & surplus threshold or initiative spike.
2. **Established:** Sustainable village managing growth. Entry: surplus resources for N ticks. Exit: either escalate (Ascendant) when initiative exceeds ambition threshold or decline (Collapsing).
3. **Ascendant:** Proactive expansion via new bands/outposts. Entry: high morale + worship/ambition trigger + sustained initiative. Exit: resource strain or morale shock.
4. **Collapsing:** Village in decline (famine, fear, abuse). Entry: morale below floor or stores exhausted. Exit: recovered back to Established or dissolve entirely.

### Transitions

```
Nascent → (Surplus Achieved) → Established
Established → (Ambition Trigger) → Ascendant
Ascendant → (Resource Crash) → Collapsing
Collapsing → (Recovery) → Established
Collapsing → (Population <= 0) → Dissolved
```

---

## Key Metrics

| Metric | Target Range | Critical Threshold |
|--------|--------------|-------------------|
| VillageCount | 3–7 active settlements | < 2 (world feels empty) |
| AverageVillageAlignment | -40 to +40 neutral spread | |±80| (polarized, risk of runaway) |
| ResourceStability | 0.8–1.2 (stores to consumption ratio) | < 0.5 (famine spiral) |
| WorshipIntensity | 0.3–0.7 (mixed devotion) | < 0.1 (player irrelevant) |
| AverageInitiative | 0.45–0.65 normalized | < 0.2 (stagnation) / > 0.85 (reckless overreach) |

### Balancing
- **Self:** Regeneration curves for vegetation and demographic caps prevent infinite growth.
- **Player:** Miracles and interventions shift alignment and initiative, allowing course corrections without micromanaging every villager.
- **System:** World events (storms, raids) periodically test stability to avoid stagnation.

---

## Scale & Scope

### Small Scale (Individual)
- Villager routines (needs, jobs, loyalty) fluctuate with local resource availability and village mood.

### Medium Scale (Village)
- Settlements adjust workforce allocation, storehouse usage, and band formation according to alignment goals.

### Large Scale (Population)
- Macro patterns reveal migration corridors, allegiance maps, and territory control without scripting.

---

## Time Dynamics

- **Short Term:** Daily cycles of harvesting, worship, morale updates, and rapid initiative jitters for chaotic entities reacting to events.  
- **Medium Term:** Seasonal shifts in resource regeneration and village ambition; lawful settlements adjust initiative slowly across these spans.  
- **Long Term:** Emergence of cultural identities, territorial borders, and persistent relations with the player.

---

## Failure Modes

- **Death Spiral:** Repeated disasters drive morale to zero → villagers disperse → settlements collapse; needs emergency aid levers.
- **Stagnation:** All villages plateau with high stores but no ambition; requires ambition triggers tied to alignment and world threats.
- **Runaway:** Single alignment dominates map; introduce faction friction or diminishing returns to keep diversity.
- **Emotional Collapse:** Chain traumas suppress initiative across a settlement (collective depression), slowing recovery unless countered by rituals or supportive miracles.

---

## Player Interaction

- **Observable:** Registry dashboards expose village stats, worship mana meters, alignment shifts, and initiative bands to explain behavioral tempo.  
- **Control Points:** Miracles, hand interactions, and targeted blessings/curses that spend or generate worship mana and can nudge initiative (calming lawfuls, stoking chaotic zeal).  
- **Learning Curve:**
  - Beginner: Watch villages thrive or falter without intervention.  
  - Intermediate: Time miracles to steer alignment and growth.  
  - Expert: Orchestrate multiple settlements, balancing fear and favor for optimal worship output.

---

## Systemic Interactions

### Dependencies
- **Villager Truth Sources:** `VillagerId`, `VillagerNeeds`, `VillagerMood`, `VillagerJob` for baseline behavior.  
- **Resource Loops:** `StorehouseInventory`, vegetation node components for economic inputs.

### Influences
- **Bands & Combat:** Alignment pushes band creation for defense or conquest.  
- **Miracles:** Acts as catalytic modifiers on morale and worship.  
- **Logistics:** Determines how surplus transfers between linked settlements.
- **Narrative Events:** Personal stories (marriages, deaths, exiles) propagate initiative modifiers through relationship graphs.

### Synergies
- Benevolent miracle streak + logistics network accelerates ascendant villages.  
- Fear-based play amplifies combat readiness at cost of worship diversity.  
- Alignments that prioritize scholarship or spirituality funnel surplus into worship mana, unlocking sustained miracle usage.
- High initiative stacked with expansionist outlooks rapidly consumes frontier resources; pairing with logistics is mandatory to avoid burnout.

---

## Leadership & Governance Patterns

### Founding & Alliances
- Villagers identify compatible outlook/alignment peers, form proto-bands, and choose settlement sites near viable resource triads (food, construction material, specialty node) within a configurable radius.
- Logistics or mercantile outlooks widen acceptable travel distance; isolationist outlooks favor defensible terrain even if resource density is lower.

### Elite Selection Rules (Examples)
- **Authoritarian / Lawful Villages:** Titles inherited along bloodlines when heirs meet minimum metrics (fame, fortune, loyalty).
- **Balanced / Neutral Villages:** Leadership determined via mixed system—coin flip between inheritance and electoral vote when vacancies appear.
- **Popular Mandate Cultures:** Elections favor candidates with highest fame/popularity scores; corrupt candidates can sway rolls via demagoguery modifiers, while pure candidates gain honesty bonuses.
- **Warlike Societies:** Vote or acclaim veteran warriors with high glory; corrupt warleaders may seize power if intimidation exceeds threshold.
- **Materialistic Communities:** Prioritize wealth/fame champions brandishing prosperity; tie-breakers reference trade success.
- **Spiritual Cultures:** Elevate high-faith individuals aligned with dominant belief (player/AI deity preference).
- **Xenophilic Settlements:** Apply race/species weighting encouraging diverse leadership; offsets combine with other outlooks to avoid monolithic picks.
- **Xenophobic Settlements:** Strong bias toward in-group lineage, resisting external candidates unless other outlooks overwhelm preference.

### Election Cadence & Eligibility
- Governance checks run on a timer (e.g., seasonal/annual) but also trigger when candidates surpass outlook-aligned thresholds (average fame/fortune/glory above village mean, research breakthroughs, major victories).
- Fanatic outlooks tighten eligibility windows (e.g., fanatic warlike requires legendary glory before candidacy); relaxed cultures accept broader participation.

These rules feed directly into aggregate outlook calculations and impact derived stats (wisdom, initiative, diplomacy) depending on the selected ruling cohort.

---

## Autonomous AI Behaviors

### Band & Army Formation
- **Chaotic Settlements:** Bands form opportunistically—multiple small war parties emerge based on personal initiative spikes. Patriotic sentiment is volatile; high patriotism can rally ad-hoc coalitions despite chaos.
- **Lawful Settlements:** Conscription flows through a single organized army. Auxiliary bands spin off for support roles (scouting, supply) but report back to the main force. Recruitment logic favors streamlined rosters and clear command structure.
- **Neutral Settlements:** Blend of the two—core army with periodic independent bands depending on situational stress.

### Aggressiveness & Task Handling
- **Outlook-Driven:** Aggregate outlook/alignment (warlike, defensive, expansionist) dictates aggression baseline. Individual tasks (defend, patrol, raid) refine behavior.
- **Aggressive Orders:** Chaotic warlike armies assigned to defend will still hunt enemies beyond village bounds, leaving smaller detachments to guard home. Lawful armies hold defensive positions, dispatching calculated offensive bands. Neutral forces adapt, mixing pursuit and defense.
- **Pursuit Radius:** Configurable per outlook; chaotic entities chase farther, lawful stay within influence radius, neutral splits difference.

### Patriotism & Loyalty
- Introduce a `Patriotism` stat per entity measuring attachment to current settlement/band. Influences willingness to answer conscription, stay on station, or defect. Lawful cultures foster stable patriotism; chaotic ones oscillate with recent events. Inputs include time spent in the aggregate, number of family members residing there, personal assets invested locally, village advancement tier, resource security, and alignment/outlook alignment between individual and leadership.
- 100% patriotism yields absolute loyalty (willingness to die for the aggregate), while near 0% triggers migration/desertion even before trouble arrives. Miracles indirectly support patriotism by sustaining prosperity but do not directly modify the stat.
- Matching outlooks/alignments accelerate patriotism gain; mismatches dampen it. Victories, supportive leadership acts, and shared assets push patriotism up, while defeats, social disparity (for egalitarians), or leadership abuses drag it down.

### Resource & Policy Conflicts
- Settlement aggregate outlook/alignments set strategic direction (trade vs hoard, offense vs defense). Individuals act according to personal ideals when managing personal property, creating micro-conflicts resolved via patriotism, leadership mandates, or faction negotiation systems.
- **Spatial Expansion:** Lawful settlements grow in orderly districts; chaotic ones sprawl more loosely. If space saturates, villages build vertically or redevelop (demolish/replace) older structures to accommodate growth. Outlook influences district emphasis (industry, worship, housing).
- **Aesthetic Morphs:** Alignment/outlook combinations reshape architecture—lawful good produces orderly gardens and luminous temples; chaotic evil favors jagged fortifications; materialistic flaunt bustling markets; spiritual villages grow shrine complexes. Higher tech tiers add lighting, statues, and banners echoing dominant ideologies.

### Miracle Reactions
- **Evasion:** Upon detecting incoming miracles (any type), villagers execute dodge routines first.
- **Post-Effect Response:** After impact, reactions depend on outcome—beneficial miracles trigger celebration/tribute, harmful ones cause fear, evacuation, or retaliation planning based on loyalty alignment.

### Inter-Village Diplomacy
- **Compatible Outlooks/Alignments:** Drive formal alliances, shared defence pacts, and political marriages between elites/rulers. Sustained cooperation can lead to full consolidation (federations, empires) with merged governance and pooled resources.
- **Conflicting Outlooks:** Villages still consider cooperation/trade unless alignments are directly opposed; hostility escalates only when ideological tension passes threshold or patriotism collapses.
- **Shared Deity:** Villages worshipping the same god suppress hostilities by default; conflict requires extreme opposition (e.g., divergent fanatic alignments). Shared worship also encourages joint prayer rituals and cooperative miracle funding.
- **Unification Process:** Consolidation enters an integration period merging elites, assets, and governance. Outlooks average across member villages; armies remain distinct but coordinate under a unified AI directive. Tech tiers converge gradually—logistics throttles integration unless settlements physically merge. Miracles during timed windows are interpreted as omens; for example, besieging armies struck by offensive miracles from their patron deity may desert or refuse orders based on collective belief and outlook alignment.
- **Multi-Deity Dynamics:** Villagers with belief in multiple gods split worship mana according to belief ratios. Rival deities can contest influence by investing mana via competing miracles; outcomes resolve on mana investment and village loyalty/faith—no formal divine diplomacy exists.
- **Deity Conversion:** Switching patron deities sparks upheaval—corrupt/evil rulers enforce conversion or executions, while neutral/good avoid lethal measures. Opposing worship structures are sacked/razed; champions/heroes receive no special treatment. Legacy prayers fade over time as allegiance shifts.

### Morale, Mood & Breakdown States
- **Individual Breakdown Spectrum:** Mood/morale collapse triggers behaviors ranging from benign (work binges, silent withdrawal, stoic endurance) to chaotic (random attacks, vandalism, dramatic despair proclamations). Expression depends on personal outlook/alignment.
- **Collective Unrest:** Like-minded groups with low morale coordinate strikes, riots, or coups. Resolution hinges on leadership concessions, miracle interventions (e.g., Joy), or security crackdowns.
- **Recovery & Migration:** Time, compassionate leadership, resource surpluses, or targeted miracles reduce breakdown frequency. Persistent despair erodes patriotism and pushes migration/self-exile.
- **Migration Destinations:** Low-patriotism villagers roll outcomes based on outlook/alignment—most seek nearby compatible settlements (matching outlooks or shared deity), some wander until welcomed, others join roaming bands/bandits if no sanctuary exists.
- **Bandit Evolution:** Chaotic deserters can coalesce into bandit bands, establish hideouts, engage in piracy on trade routes, or even seed chaotic villages if they seize resources. Neighboring settlements deploy patrols, bounties, or suppression campaigns tailored to their outlook/alignment to neutralize or negotiate with outlaw groups.
- **Disposition Modifiers:** Stoic personalities dampen initiative swings; vengeful characters spike initiative after personal harm; empathic villagers lose initiative when allies suffer. These traits interact with outlook/alignment to shape post-event behavior.
- **Lifecycle & Families:** Outlook/alignment drive courtship, marriage, and family formation. Pairs auto-match via compatible ideals, miracles, or festival rituals. Offspring inherit traits (education potential, outlook bias) and progress through the education pipeline; adults age, retire, or die. Family bonds feed patriotism and prayer priorities.
- **Justice & Crime:** Outlooks define crimes (materialists focus on theft/sabotage, spiritualists on heresy); alignments dictate punishment severity. Lawful settlements run jails and due process; chaotic ones use slave pens or pits. Pure villages favor exile/rehabilitation, corrupt/evil sacrifice offenders, good lawfuls may extend asylum to migrants. Punishments can shift alignment/patriotism—tyrannical actions fuel unrest, just rulings raise loyalty.
- **Sieges & Combat:** Patriotism determines whether villagers rally, flee, or defect. Buildings take damage/ignite; peacekeepers (non-discipline safety role) handle firefighting and hazard mitigation. Conquered settlements may be razed, vassalized, plundered, liberated, or sacrificed per occupier outlook/alignment. Miracles during battle (e.g., Joy → blood frenzy, Despair → dread) apply area buffs/debuffs equally to all caught inside, altering morale and desertion odds. Post-battle miracles follow normal rules.
- **Peacekeepers:** Dedicated internal security force patrolling borders, keeping fauna at bay, responding to fires, escorting caravans, and enforcing curfews. Good/pure peacekeepers assist villagers; corrupt/evil accept bribes. Patriotism determines willingness to die saving others. They gain combat and utility skills over time, benefit from tech upgrades, and coordinate with bands/armies during sieges while prioritizing civilian and asset protection.
- **Festivals & Rituals:** Every ~3 days villages run festivals/rituals aligned with their outlooks (production feasts, fertility rites, warrior games, devotional ceremonies). Events grant temporary buffs (e.g., productivity, harvest yield, army morale) and can be amplified with Joy or denounced with Despair, nudging outlook sentiment.
- **Disasters & Environmental Events:** Natural phenomena (storms, rain clouds, earthquakes) are physical entities gods can pick up/throw. Plagues emerge from environmental conditions. Disasters spark prayer surges, evacuations, and outlook-specific responses (spiritual rituals, material reinforcement). Deities can avert or weaponize events (e.g., fling plagued villagers into enemy villages) with alignment consequences.
- **Fauna & Monsters:** Each biome spawns wildlife with roaming/migration patterns. Outlooks determine responses—hunt, domesticate, worship as totems. Dangerous beasts near settlements trigger peacekeeper hunts, hunter guilds, or cults. Deities can bless/curse fauna via miracles.
- **Champions:** Deity-selected individuals blessed to spread belief via deeds. Champions gain boosted stats/XP scaling with their god’s tribute tier, can cast delegated miracles by drawing from the god’s mana pool, and pursue goals set by their patron. Mortals treat them as notable villagers, but champions are the sole agents capable of harming celestial beings (demons, angels, otherworld entities). Champions may form bands to tackle world bosses.
- **Heroes:** Fame/popularity/glory-based elites championing their home village. Heroes carry local gravitas, often joining the ruling elite. By default they are mortal-scale, but gods can delegate celestial damage via relics or direct empowerment when crises demand, enabling them to confront otherworldly threats.
- **World Bosses:** Unique entities spawning at scheduled times/locations. Adventurer bands (often led by champions/heroes) can defeat them for rare loot and god jewelry. Deities may nurture bosses to gain loyalty, granting bonuses or direct control. Some bosses aid villages (welcomed by peaceful/xenophilic cultures); others are feral threats ignoring boundaries.
- **Celestial Beings:** Demons, angels, and other entities enter the world randomly (events, rituals, major wars). They can terrorize or aid villages, potentially escalating into world bosses. Narrative arcs leverage their appearances; champions (or empowered heroes) are required to oppose hostile arrivals.
- **Seasons & Climate:** Ten-day seasons cycle through biome-specific weather patterns affecting resources, morale, and cultural behaviors. Races/outlooks respond differently (e.g., winter hardship, spring fertility). Deities cannot shift season timing but may influence climate within a season via miracles.
- **Death & Memorials:** Villages maintain burial sites; graves scale with fame/fortune/glory. High-belief individuals emit passive mana posthumously (tripled for martyrs). Festivals can honor the deceased, granting morale or thematic bonuses. Hovering graves reveals final stats/memories, reinforcing legacy.
---

## Technology Progression & Research

### Tech Tier Ladder
- **Tier Range:** 1 (mud-hut hamlet) → 20 (multopolis, high-tech civilization).
- **Early Tiers (1–5):** Basic huts, manual farming, improvised tools; armies wield simple weapons, minimal armor.
- **Mid Tiers (6–12):** Timber/stone architecture, organized logistics, basic metallurgy, animal-drawn transport upgrades, budding siege tech.
- **High Tiers (13–17):** Advanced crafts, proto-industrial processes, semi-automated extraction, disciplined armies with composite armor and ranged tech.
- **Apex Tiers (18–20):** Automated infrastructure, flying/engine-driven transport, power armor and weaponry, artisans crafting epic/legendary gear with ease, full aesthetic unlocks tied to outlook/alignments.

### Research Infrastructure
- **Schools:** Entry-level literacy; boost baseline knowledge growth when staffed by educated villagers.
- **Universities:** Mid-tier research hubs accelerating tech progression; require scholars with high education and wisdom.
- **Academies:** Advanced institutions unlocking specialist tech trees (military, logistics, arcana) and enabling apex-tier breakthroughs.
- **Supplementals (Optional):** Libraries, archives, observatories, laboratories—tunable modules to specialize domains (science, magic, engineering).
- **Education Pipeline:** Villagers grow through nurseries → schools → universities → academies. Progress scales with individual wisdom and will. Apprenticeships copy mentor wisdom/experience; traits can be inherited. Materialistic outlooks gain modest learning-speed bonuses.
- **Artisan Progression:** Tech tiers unlock new equipment recipes (common → rare → legendary). Outlooks influence priorities (materialists craft wealth gear, warlike focus weapons, spiritual produce relics). Champions/heroes acquire gear like others but capture elite loot via their exploits. Legendary items confer cultural bonuses, spark special prayers if lost/stolen, and boost artisan fame.

### Staffing & Progression Drivers
- Researchers contribute via education, wisdom, and relevant outlook traits (scholarly, spiritual, materialistic, etc.).
- Facilities apply multipliers based on staffing quality and capacity utilization (e.g., academies cap at small cohorts but grant large tier boosts).
- Tech tier increments trigger milestone events, unlock unique buildings/outlooks, and update registry telemetry.

### Outlook Influence
- Scholarly/spiritual/materialistic outlook weights shift which research domains receive priority (e.g., spiritual academies bias miracles, warlike bias military tech).
- Fanatic outlooks may lock certain tech paths or accelerate specialized branches at the expense of others.

### Telemetry & Feedback
- Registry snapshots broadcast current tech tier, active research rate, and facility occupancy.
- Visual evolution (architecture, lighting, VFX) escalates with tier milestones, reinforcing sandbox progression without direct player control.

All numbers/tier thresholds are tunable data so designers can iterate on pacing per game mode.

---

## Resource Economy & Logistics

### Primary Resource Families
- **Ore:** Mineral deposits with richness tiers (basic metal, rare metal); infinite mines rebalancing extraction rate by color-coded richness.
- **Wood:** Forest/vegetation stands that propagate over time; supports construction, fuel, and crafted goods.
- **Food:** Crops, gathered flora, domesticated fauna; empowers population growth and spiritual/agrarian outlook bonuses.
- **Luxury Resources:** Gems, exotic flora/fauna, crafted fineries; fuel artisan output, trade leverage, and morale spikes.
- **Building Materials:** Refined goods (planks, bricks, alloys) produced via local chains for higher-tier structures.

### Production Chains
- **Refinement:** Ore → smelters → metals/rare alloys; wood → lumber mills → planks/treated timber.
- **Crafting:** Toolmakers, blacksmiths (weapon/armor specialization), armorers, artisans each convert inputs into equipment, epic gear, or cultural goods.
- **Food Processing:** Farms → mills → bakeries; ranches → smokehouses; spiritual outlooks may dedicate surplus to rituals.

### Outlook Priorities
- **Materialistic:** Pursue surplus in all categories, emphasizing rapid production, expansion, and infrastructure.
- **Spiritual:** Hoard food, encourage population growth, funnel luxuries into worship rites.
- **Xenophilic:** Seek outreach/indenture arrangements depending on moral alignment—trade webs for good-aligned, exploitative networks for corrupt variants.
- **Warlike:** Prioritize metals, rare alloys, and gear production to outfit bands/armies.
- **Agrarian/Mercantile:** Bias food stability and market throughput respectively.

### Regeneration & Extraction
- Animals respawn on ecological timers; vegetation spreads via propagation models.
- Ore mines are infinite but reflect richness via extraction speed/reward modifiers (color-coded tiers).
- Production buildings are auto-placed by villages when prerequisites are met; specialization arises from staffing outlooks.

### Trade & Logistics
- Villages establish markets and dispatch caravans along safe routes; individuals and aggregates can participate in trade pacts.
- Outlook/alignment governs trade openness: xenophilic/mercantile societies reach out aggressively, while xenophobic/isolationist groups restrict exchanges.
- Higher tech tiers unlock improved transport (engine wagons, aerial haulers) that boost throughput and reduce travel risk.
- Trade routes arise via AI-negotiated agreements (automatic fixed pricing for allies). Caravan makeup scales with cargo value, distance, and threat level; special items (luxuries, relics) receive extra escorts but attract bandit targeting. Raided caravans trigger bounties, retaliatory bands, or patrol increases depending on loss severity.

### Telemetry & Visual Cues
- Per-village resource metrics track raw stock, refined goods, luxury reserves, and trade throughput; aggregate-level dashboards planned for later iterations.
- Storehouses and caravans display visual load cues to communicate abundance or scarcity.
- Scarcity alarms flag when critical resources drop below outlook-defined safety thresholds.
- Outlook-driven escalation: shortages trigger collective prayers that intensify as deficits grow. Reasonable outlooks (mercantile, diplomatic) prioritize diplomacy/trade agreements first; aggressive outlooks pivot to raids once peaceful options fail or patriotism erodes.

All resource rates, regeneration curves, and trade caps are tunable data to support balancing across sandbox, campaign, or multiplayer modes.

---

## Worship & Miracle System

### Worship Mana Flow
- **Belief:** Each entity pledges to a god (player or AI). Belief determines which mana pool their devotion feeds and which alignment axis their loyalty affects.
- **Faith:** Measures devotion strength; baseline worshiper contributes `1 mana/second` at faith 1, ramping linearly to `4 mana/second` at faith 100 (tunable). Place-of-worship structures (shrines, temples, cathedrals) apply additional multipliers. Spiritual-aligned worshippers gain an extra `+50–100%` bonus on top of faith scaling. Faith boosts persist over a believer's lifetime, trickling tribute/mana even after prayers are fulfilled.
- **Loyalty Axis:** Fear ↔ Respect ↔ Love describes how worshippers relate to their deity. High fear skews toward evil alignment shifts, love pushes good, respect stabilizes neutral. Loyalty feedback grants alignment points to the worshiped god and influences miracle reception.
- **Quests as Prayers:** Villagers generate prayer requests for divine assistance (combat survival, construction acceleration, feeding, healing). Answered prayers increase belief in the responding deity, raising individual faith and granting immediate tribute plus ongoing tithe over that villager's lifetime. Multiple gods may compete to respond; credit and alignment influence go to the first to complete the request, with cooperative assists granting partial rewards. Prayer chains can be shared between believers for multiplicative gains, encouraging communal rituals. Each villager maintains a current desire (visible via tooltip/inspect); prayers grow in urgency over time, can be overwritten by higher-priority crises, and aggregate into collective petitions when needs align.
- **Prayer Triggers:** Conditions include health below thresholds, hunger/starvation risk, resource scarcity, stalled construction, homelessness, and other outlook-specific needs. Urgency spikes compound when crises persist or stack (injury + hunger). Outlooks generate unique petitions (e.g., materialists praying for rich mine veins) enabling gods to bless assets proactively.
- **Request Cadence:** Fulfilled prayers impose a grace period (≈one day) before identical petitions reissue; unmet needs can reshuffle multiple times per day as urgency rises or priorities shift.

- **Tribute Loop:** Tribute is earned by fulfilling prayer quests or leveling player-controlled villages. Tribute unlocks new miracle families, while advanced villages grant special modifiers (reduced cost, unique variants). Prayers expire based on urgency (saving lives vs bumper harvest); expired requests lower faith and reduce future tribute potential.
- **Legacy Calls:** Legacy miracles (e.g., Rain Miracle) re-enter via this path—Rain costs **100 mana** baseline before modifiers.

### Casting Parameters
- Miracles expose behavioral modes per type (e.g., Rain: nourishing drizzle vs destructive downpour vs blizzard/hail when climate allows).
- Intensity slider controls footprint, duration, and potency; higher intensity exponentially increases mana costs. No cooldowns—access is gated purely by mana, tribute, and alignment requirements.
- Casting outside player-controlled influence rings applies distance-based mana surcharges (up to +1000% at extreme range), encouraging local stewardship and infrastructure expansion.
- Casting triggers village reaction checks: grateful (love/respect) communities escalate worship; fearful ones may increase obedience but also resentment if overused.
- Quest feedback loops: fulfilled prayers broadcast gratitude events; ignored or failed prayers decrease faith and generate alignment penalties toward the negligent deity. High-faith believers offer larger tribute rewards for answered prayers and continue tithing mana over time.
- Shared-belief villagers can co-sign prayers to increase payout multipliers, but only the deity who completes the request receives the prayer fulfillment credit.
- Prayer precedence: life-critical requests (self or loved ones) override other needs; outlooks reorder remaining priorities (materialistic → production, spiritual → sustenance, etc.). Villagers may reissue the same prayer with multiplicative rewards—saving the same individual repeatedly greatly amplifies loyalty and future tribute.
- Time bubbles obey edge-freeze rules: entities entering or exiting are locked until the effect ends, preventing exploits. Rewind modes require recorded state snapshots (entities, inventory, resource delta) and clamp to a configurable PureDOTS tick window; fidelity prioritizes villager state above world resources when buffers saturate.

### Planned Miracle Families (Extensible)
- **Weather & Growth:** Rain, sunlight, fertile blessings.
- **Healing & Protection:** Restore health, shields, shelter domes.
- **Destruction & Punishment:** Lightning, meteors, plagues.
- **Summoning & Manifestation:** Spawn guardians, resource nodes, or divine constructs.
- **Industrial & Logistics:** Accelerate production, teleport caravans, automate harvests.
- **Time Manipulation:** Slow, hasten, pause, or rewind localized time bubbles; intensity defines depth (e.g., minor haste vs full rewind).

### Baseline Miracle Roster (Sandbox Slice)
- **Rain:** Nourishing drizzle, heavy storm, or climate-tuned blizzard/hail with intensity slider.
- **Water:** Moisturize crops, flood terrain, cleanse corruption.
- **Meteor:** Precision strike or carpet bombardment; higher intensity escalates crater size and fallout.
- **Fire:** Ember ignition, wildfire spread, or focused inferno blast.
- **Life:** Revive fallen villagers, regrow vegetation, purify blighted zones.
- **Death:** Cull hostile forces, wither crops, impose decay debuffs.
- **Heal:** Burst heal, regenerative aura, or cleansing of ailments.
- **Electricity:** Chain lightning, storm fields, power grid jumpstarts.
- **Air:** Gust control, lift units, disperse toxins or fog.
- **Shield:** Protective domes, directional barriers, anti-projectile walls.
- **Tornado:** Mobile cyclone for crowd control or terrain reshaping.
- **Joy:** Morale surge, productivity buffs, festival-triggered loyalty shifts. Boosts fighting efficiency, accelerates tech momentum, and spurs population growth; effects last roughly one day with diminishing returns on repeated casts.
- **Despair:** Fear shockwave, productivity and morale debuffs, rebellion suppression. Reduces fighting efficiency and mood, increasing risk of mental breakdowns; mirrors Joy duration/decay with diminishing returns.
- **Time:** Area bubble that slow/haste/stop entities inside; edges freeze entrants while active; high intensity enables localized rewinds (rolling back entity state/resources within scope) at steep mana cost.
- **Gating (MVP):** All miracles above are available baseline except `Death`, which remains tribute-locked initially to validate the unlock pipeline.

Numbers (costs, cooldowns, loyalty modifiers) remain tunable to balance sandbox pacing and alignment playstyles.

---

## Alignment & Outlook Taxonomy

### Alignment Axes (Godgame Baseline)
- **Moral Axis:** Good ↔ Neutral ↔ Evil
- **Order Axis:** Lawful ↔ Neutral ↔ Chaotic
- **Purity Axis:** Pure ↔ Neutral ↔ Corrupt

Each entity carries all three alignment readings simultaneously (e.g., Lawful Good Corrupt), enabling nuanced combinations like "Corrupt Good". Purity states modulate how strongly moral/order choices manifest in behavior and rolls.

### Core Outlook Families
- `Materialistic` – wealth, craft, inheritance duty
- `Spiritual` – faith, ritual, devotion cycles
- `Warlike` – offense, conquest, martial honor
- `Peaceful` – caretaking, diplomacy, healing
- `Expansionist` – settlement growth, frontier pushes
- `Isolationist` – fortification, inward prosperity
- `Scholarly` – knowledge, research, arcana
- `Mercantile` – trade, markets, logistics
- `Agrarian` – food security, land stewardship
- `Artisan` – aesthetics, culture, festivals

### Outlook Axes
Outlooks function as independent ideological axes similar to alignments, with spectra such as:
- **Xeno Axis:** Xenophilic ↔ Neutral ↔ Xenophobic
- **Warfare Axis:** Warlike ↔ Neutral ↔ Peaceful
- **Expansion Axis:** Expansionist ↔ Neutral ↔ Isolationist
- **Economy Axis:** Materialistic ↔ Neutral ↔ Spiritual (or other economic/spiritual pairs)
- **Culture Axis:** Scholarly ↔ Neutral ↔ Artisan (example pairing)

An entity may carry up to **three regular outlooks** simultaneously, or trade that breadth for **two fanatic outlooks** (extreme positions locked near the axis endpoints). Fanatic outlooks impose stronger behavioral biases and heavier initiative modifiers. Aggregate entities (villages, guilds, companies, bands, armies) follow the same rules: their collective outlook slots represent dominant cultural ideals or strategic doctrines, derived from member contributions and leadership influence.

Purity plus the tri-axis alignment readings and the active outlook set define the weighting tables referenced in event resolution rolls and village cultural behavior.

- **Aggregate Stat Derivation:** Collective entities compute their key stats by sampling top-performing members rather than simple averages, emphasizing leadership and specialist impact. Numbers below are illustrative and should be parameterized/tuned per domain.
- **Army Perception:** Sample the upper scout cohort (e.g., top percentile slice) to represent reconnaissance acuity.
- **Village Wisdom:** Drawn from ruling elites or council (e.g., top governance skill holders) to reflect decision-making quality.
- **Band Initiative:** Controlled by leader or co-leader average initiative scores, cascading to member urgency.
- **Trading Guild Logistics:** Weighted by highest logistics-skilled artisans/clerks to simulate operational expertise.
Sampling windows are configurable per stat (percentile, fixed count, role-weighted), enabling nuanced aggregation and preventing noise from low-skill populations. Tune these thresholds via data assets so designers can iterate without code changes.

---

## Exploits

- **Miracle Spam:** Overusing low-cost miracles could trivialize alignment drift—needs diminishing returns or prayer cost scaling (Severity: Medium).  
- **Resource Hoarding:** Player-built bottlenecks might starve AI settlements—consider NPC fallback trade or smuggling (Severity: Medium).

---

## Tests

- [ ] Simulated 60-minute sandbox run maintains ≥3 active villages.
- [ ] Alignment extremes trigger appropriate village state transitions.
- [ ] Player miracle interventions correctly adjust worship mana metrics without destabilizing simulation.
- [ ] Performance stays within target entity budgets at peak population.
- [ ] High-initiative villages pursue expansion actions despite low surplus, while low-initiative analogues defer plans.
- [ ] Personality variants (lawful vs chaotic) exhibit distinct initiative frequency/amplitude when exposed to identical event sequences.

---

## Performance

- **Complexity:** O(n) per village for aggregation; O(m) per villager for alignment sampling (aim for linear passes).  
- **Max Entities:** Target 500 villagers, 10 villages, 20 active bands.  
- **Update Freq:** Evaluate cohesion/alignment every 0.5s simulated; expensive recalculations (expansion planning) every 5s.

---

## Visual Representation

### System Diagram
```
[World Resources] → sustain → [Villagers]
[Villagers] → cluster into → [Villages]
[Villages] ↔ share → [Alignment Outlooks]
[Villages] → spawn → [Bands]
[Player Miracles] → influence → [Villages & Alignment]
```

### Data Flow
```
Resource Nodes → Harvest → Storehouses → Village Cohesion → Alignment-directed Surplus Spend → Worship Mana → Registry/Telemetry
```

---

## Iteration Plan

- **v1.0 (MVP):** Villagers cluster into villages, manage resources, track worship mana as the miracle fuel.  
- **v2.0:** Add alignment variants affecting expansion logic and band behavior.
- **v3.0:** Integrate diplomacy, inter-village conflict, and multiplayer hooks.

---

## Open Questions

### ✅ **ANSWERED:**
1. ✅ **Village founding threshold:** Villagers split when resources in their area are "reasonably exploited" 
   - <DESIGN QUESTION: What % depletion = "reasonably exploited"? 60%? 75%? 90%?>
   - <DESIGN QUESTION: Does this check raw nodes remaining, harvest rate decline, or storehouse throughput?>

3. ✅ **Initiative system:** Events trigger rolls that can increase or decrease initiative, with rationale on both sides
   - <DESIGN QUESTION: What's the roll formula? d20 + modifiers vs difficulty?>
   - <DESIGN QUESTION: Example rationales needed - what argues for +initiative vs -initiative per event type?>

5. ✅ **Tech tier progression:** Advancement requires unlocking a set number of research milestones per level
   - <DESIGN QUESTION: How many milestones per tier? Fixed (e.g., 5) or scaling (tier 1 = 3, tier 10 = 8)?>
   - <DESIGN QUESTION: Are milestones domain-specific (military, civic, arcane) or generic research points?>

6. ✅ **Resource crisis threshold:** 10% stockpile triggers desperate state (diplomacy/conflict responses)
   - <DESIGN QUESTION: Is this per-resource or aggregate? (10% food = desperate even if 100% wood?)>
   - <DESIGN QUESTION: What actions unlock at desperate state? Raid priority? Surrender offers?>

### ❌ **STILL OPEN:**
7. How should tribute tiers map to miracle family unlocks and unique village-specific bonuses?
8. How are prayer requests prioritized/queued when multiple crises occur simultaneously?
9. How do shared or competing prayers distribute credit and alignment influence across multiple deities without double-counting?
10. How do we surface villager desires/prayer state in UI without overwhelming the player?

---

## References

- `Docs/TruthSources_Inventory.md#villagers` for current villager truth sources.
- `Docs/TruthSources_Inventory.md#storehouses` for resource storage flows.
- `Docs/Concepts/Villagers/Village_Villager_Alignment.md` for deeper alignment exploration.
- `Docs/Concepts/Core/Prayer_Power.md` for prayer economy interactions.

---

## Related Documentation

- Pending Bands registry concept: `Docs/Concepts/Villagers/Bands_Vision.md`.
- Resource loops: `Docs/Concepts/Resources/` (legacy material pending refresh).

---

**For Implementers:** Align future truth source work (Village, Alignment, Bands) with the state machine and metrics above.
**For Designers:** Use Player Interaction and Iteration Plan sections to scope sandbox milestones before layering campaign/skirmish variations.
