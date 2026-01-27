# Pattern Bible: Emergent Narrative & Gameplay Patterns

**Last Updated**: 2025-11-29
**Purpose**: Catalog of emergent patterns that arise from behavioral systems (alignment, cohesion, moral conflict, grudges, etc.)

This document captures patterns **before implementation**. Each pattern describes preconditions, effects, and narrative hooks in plain English. When ready to wire into the engine, lift patterns from here into actual system definitions.

---

## Pattern Structure

Each pattern includes:
- **Name**: Memorable, TV trope-style
- **Scope**: Individual / Aggregate / Region / Cross-Aggregate
- **Preconditions**: Plain English conditions (not code)
- **Gameplay Effects**: Concrete mechanical outcomes
- **Narrative Hook**: One-liner description
- **Priority**: Core / Nice-to-have / Wild experiment
- **Related Systems**: Which existing systems enable this

---

# DUAL LEADERSHIP PATTERNS

## The Devoted Castellan

**Scope**: Individual (OperationalLeader)

**Preconditions**:
- OperationalLeader with loyalty to SymbolicLeader >150
- Low ambition (craven + forgiving traits)
- Offered promotion to lead new aggregate
- CommandFriction with current SymbolicLeader is low (<0.3)

**Gameplay Effects**:
- Refuses promotion
- Loyalty to SymbolicLeader +20
- Aggregate cohesion +0.1
- Reputation tag "Devoted Steward" (+10)
- If SymbolicLeader dies, may stay with aggregate until death (martyr/saint status)

**Narrative Hook**: "A loyal steward who would rather die than leave their lord."

**Priority**: Core

**Related Systems**: Dual Leadership, Loyalty, Promotion, Reputation

---

## Heroic Mutiny

**Scope**: Aggregate

**Preconditions**:
- Dual leadership with high CommandFriction (>0.7)
- SymbolicLeader issues morally reprehensible order (conflict >0.7)
- OperationalLeader has Good alignment (>50)
- Crew loyalty to SymbolicLeader low (<70)
- Cohesion <0.3

**Gameplay Effects**:
- Splintering triggered
- OperationalLeader becomes SymbolicLeader of new aggregate
- 60-80% of crew joins new aggregate (those aligned with OperationalLeader)
- Original aggregate remains with loyalists
- New aggregate gains "Reformed" or "Breakaway" tag
- Regional reputation for new aggregate starts at 0 (clean slate)

**Narrative Hook**: "The steward who mutinied to save everyone's souls."

**Priority**: Core

**Related Systems**: Dual Leadership, Moral Conflict, Splintering, Alignment

---

## The Glory Hound & The Professional

**Scope**: Aggregate (dual leadership dynamic)

**Preconditions**:
- SymbolicLeader: Heroic + Bold outlook/traits
- OperationalLeader: Scholarly + Pragmatic outlook/traits
- Alignment distance moderate (0.3-0.5)
- CommandFriction oscillating (0.4-0.6)

**Gameplay Effects**:
- SymbolicLeader proposes risky high-reward actions
- OperationalLeader hesitates, proposes conservative alternatives
- 50% of crew aligns with each leader (oscillating loyalty)
- Aggregate initiative fluctuates wildly (-10% to +10% based on who "wins" each decision)
- Occasional brilliant successes (when both agree: +30% efficiency)
- Occasional catastrophic failures (when SymbolicLeader overrides: -20% efficiency)

**Narrative Hook**: "The hotshot captain and the by-the-book shipmaster, always at odds but occasionally brilliant together."

**Priority**: Nice-to-have

**Related Systems**: Dual Leadership, CommandFriction, Initiative, Voting

---

## The Tyrant's Reckoning

**Scope**: Aggregate

**Preconditions**:
- SymbolicLeader: Evil + Authoritarian (Moral <-50, Order >50)
- OperationalLeader: Good + Mutinous (Moral >50, Outlook = Mutinous)
- CommandFriction >0.8 for 500+ ticks
- SymbolicLeader issues series of brutal orders (3+ in 300 ticks)
- Cohesion <0.2

**Gameplay Effects**:
- OperationalLeader + crew stage violent coup
- SymbolicLeader killed or imprisoned
- OperationalLeader becomes SymbolicLeader (government type shifts to Democratic)
- All members with Evil alignment <-30 purged or flee
- New aggregate gains "Revolutionary" tag
- High morale (+50) but low resources (−30%) from chaos

**Narrative Hook**: "The shipmaster who killed the tyrant and freed the crew."

**Priority**: Core

**Related Systems**: Dual Leadership, Moral Conflict, Splintering, Alignment, Grudges

---

## The Academic & The Zealot

**Scope**: Aggregate (dual leadership dynamic)

**Preconditions**:
- SymbolicLeader: Scholarly + Methodical (Order >50)
- OperationalLeader: Fanatic + Devout (Purity >60, Outlook = Fanatic)
- Alignment distance moderate on Purity axis (>30 difference)
- CommandFriction moderate (0.4-0.6)

**Gameplay Effects**:
- SymbolicLeader focuses on efficient resource use, research
- OperationalLeader prioritizes ideological purity, rituals
- Resource allocation conflicts frequent
- When aligned on goal: +20% research speed, +15% morale
- When misaligned: -10% efficiency, +5% stress
- Occasional "conversion" events where one partially shifts toward the other's outlook

**Narrative Hook**: "The pragmatic scholar and the zealous believer, locked in eternal debate."

**Priority**: Nice-to-have

**Related Systems**: Dual Leadership, Alignment, Outlook, Resource Allocation

---

## Synchronized Excellence

**Scope**: Aggregate (dual leadership synergy)

**Preconditions**:
- CommandFriction <0.2
- Both leaders have high AlignmentStrength (>0.8)
- Partnership duration >5000 ticks
- Aggregate cohesion >0.8

**Gameplay Effects**:
- Unlock "Special Maneuvers" ability set
- Initiative +15%
- Cohesion +0.1
- Efficiency bonus on all tasks (+20%)
- Rare "Perfect Execution" events (critical success chance +10%)

**Narrative Hook**: "Two leaders, one mind—unstoppable when they move together."

**Priority**: Nice-to-have

**Related Systems**: Dual Leadership, CommandFriction, Cohesion, Special Abilities

---

# GRUDGE & VENGEANCE PATTERNS

## The Blood Feud

**Scope**: Cross-Aggregate (families/factions)

**Preconditions**:
- Two aggregates with multiple members holding inherited grudges
- Grudge intensity >75 (Vendetta level) on both sides
- At least one member per aggregate with VengefulScore >60
- Proximity <50m or shared territory

**Gameplay Effects**:
- Automatic hostility when aggregates encounter each other
- No diplomatic options available (locked out of peace treaties)
- Combat resolution always results in escalation (no retreat/surrender)
- Each combat event increases grudge intensity for survivors (+10)
- Feud ends only when one aggregate is destroyed or all vengeful members die
- Regional reputation affected ("Feuding Clans" tag)

**Narrative Hook**: "A vendetta passed down through generations, consuming all who inherit it."

**Priority**: Core

**Related Systems**: Grudges, VengefulScore, Inheritance, Diplomacy

---

## Forgiveness Arc

**Scope**: Individual or Aggregate

**Preconditions**:
- Entity with high ForgivingScore (>60)
- Has active grudge intensity 40-70 (Serious, not Vendetta)
- Offender performs reparative action or shows remorse
- At least 1000 ticks since offense

**Gameplay Effects**:
- Grudge intensity drops rapidly (−20 immediately, then normal decay)
- Loyalty to offender's aggregate can be restored (if previously members)
- Morale +15 (emotional relief)
- Reputation tag "Merciful" (+5)
- Enables "Reconciliation" event with offender
- May inspire other forgiving members to let go of grudges (cascade effect)

**Narrative Hook**: "The warrior who laid down their weapon and forgave the unforgivable."

**Priority**: Nice-to-have

**Related Systems**: Grudges, VengefulScore, Morale, Reputation

---

## The Vendetta Spiral

**Scope**: Cross-Aggregate

**Preconditions**:
- Two aggregates in ongoing conflict
- Members on both sides with VengefulScore >40
- Each combat/offense triggers retaliatory grudges
- Cycle repeats 3+ times

**Gameplay Effects**:
- Grudge intensity escalates with each cycle (+15 per cycle)
- Both aggregates lose resources to conflict (−10% per cycle)
- Morale decreases (−5 per cycle) from endless war
- Third parties avoid both aggregates (regional reputation −20)
- Escalation threshold: if grudge intensity >90, triggers "Total War" state
- Only intervention by high-authority aggregate or total destruction ends it

**Narrative Hook**: "An eye for an eye, until everyone is blind."

**Priority**: Core

**Related Systems**: Grudges, VengefulScore, Combat, Escalation

---

# COHESION & SPLINTERING PATTERNS

## The Fracturing Band

**Scope**: Aggregate

**Preconditions**:
- Aggregate cohesion <0.2 for 100+ ticks
- At least 2 distinct alignment factions (distance >60 between factions)
- Multiple members with high initiative (>0.6)
- Dissent >50%

**Gameplay Effects**:
- Splintering event triggers
- Aggregate splits into 2-3 new aggregates based on alignment clusters
- Each new aggregate recalculates alignment (weighted average of members)
- Members with highest initiative + alignment match become new SymbolicLeaders
- Original aggregate dissolves
- New aggregates may become hostile to each other based on alignment distance

**Narrative Hook**: "The band that could not hold together, splintering into warring factions."

**Priority**: Core

**Related Systems**: Cohesion, Splintering, Alignment, Initiative

---

## The Unbreakable Brotherhood

**Scope**: Aggregate

**Preconditions**:
- Cohesion >0.9 for 1000+ ticks
- All members have loyalty >150
- Aggregate has survived 3+ major crises together
- Alignment variance <20 on all axes

**Gameplay Effects**:
- Cohesion locked at minimum 0.8 (cannot drop below)
- Moral conflict effects reduced by 50% (strong conviction)
- Splintering disabled
- Initiative +10% (unified purpose)
- When one member is attacked, all members retaliate (+30% combat effectiveness)
- Reputation tag "Unbreakable" (+15)

**Narrative Hook**: "Brothers and sisters in arms, forged in fire and bound by unbreakable loyalty."

**Priority**: Nice-to-have

**Related Systems**: Cohesion, Loyalty, Aggregate Identity, Combat

---

## The Reluctant Alliance

**Scope**: Aggregate (post-merge)

**Preconditions**:
- Two aggregates merge due to external threat
- Alignment distance moderate (0.4-0.6)
- Initial cohesion <0.5
- Members retain loyalty to original aggregates (dual loyalty)

**Gameplay Effects**:
- Merged aggregate has low initial cohesion (0.3-0.4)
- Factions within aggregate compete for influence
- Decision-making slow (hesitation +30%)
- If external threat resolved: 60% chance of re-splintering into original aggregates
- If external threat persists: cohesion gradually increases (+0.05 per 500 ticks)
- Potential to become stable alliance or fracture

**Narrative Hook**: "Enemies forced to work together, barely holding it together."

**Priority**: Nice-to-have

**Related Systems**: Merging, Cohesion, Loyalty, External Threats

---

# MORAL CONFLICT PATTERNS

## The Crisis of Conscience

**Scope**: Individual (OperationalLeader or senior member)

**Preconditions**:
- Good alignment (>50) and Lawful (>40)
- Ordered to commit war crime or severe moral violation
- MoralConflict level >0.8 (Severe)
- Loyalty to issuing leader <100

**Gameplay Effects**:
- 300 tick hesitation
- Morale −50
- 50% chance of refusal
- If refusal: Loyalty to issuing leader −30, crew aligns with refuser (+20 loyalty)
- If compliance: Grudge created (intensity 80), morale −50, alignment shifts Evil (+10)
- Aggregate cohesion −0.2
- May trigger "Disobedience" or "Moral Stand" event

**Narrative Hook**: "The officer who refused to follow orders and saved their soul."

**Priority**: Core

**Related Systems**: Moral Conflict, Alignment, Loyalty, Refusal

---

## The Slide into Darkness

**Scope**: Individual

**Preconditions**:
- Entity with Good alignment (>20) at start
- Repeatedly obeys morally compromising orders (5+ times)
- Each compliance shifts alignment Evil (cumulative −50 over time)
- Low AlignmentStrength (<0.3) makes them susceptible

**Gameplay Effects**:
- Alignment gradually shifts Evil (−10 per severe compliance)
- VengefulScore increases (+5 per event)
- BoldScore may increase (+3 per event, becoming more aggressive)
- Outlook shifts from Loyalist/Neutral to Opportunist or Fanatic
- Morale permanently reduced (−20)
- May eventually become antagonist to former allies if alignment crosses threshold

**Narrative Hook**: "The good soldier who obeyed every order, until there was nothing good left."

**Priority**: Nice-to-have

**Related Systems**: Moral Conflict, Alignment Drift, Outlook Shift, Behavior Change

---

## The Lawful Rebel

**Scope**: Individual

**Preconditions**:
- Lawful alignment (>60) and Good alignment (>40)
- Aggregate or faction engages in systematic lawlessness or corruption
- Witness 3+ violations of rules/ethics within 500 ticks
- AlignmentStrength >0.7 (strong conviction)

**Gameplay Effects**:
- Entity publicly denounces aggregate/faction
- Loyalty to aggregate −50
- Initiates "Reform Demand" or leaves to form new aggregate
- Other Lawful members may join (cascade effect)
- If reforms ignored: becomes opposition leader
- If reforms accepted: cohesion restored, corrupt members purged

**Narrative Hook**: "The rule-follower who became a rebel to uphold the law."

**Priority**: Nice-to-have

**Related Systems**: Moral Conflict, Alignment, Loyalty, Splintering

---

# INITIATIVE & AUTONOMY PATTERNS

## The Proactive Hero

**Scope**: Individual

**Preconditions**:
- High initiative (>0.7)
- Good alignment (>50) and Bold traits (>40)
- Aggregate in crisis (low morale, high stress, or under attack)
- SymbolicLeader absent, incapacitated, or ineffective

**Gameplay Effects**:
- Entity acts autonomously to address crisis
- Temporarily assumes leadership role (if successful)
- 70% chance of positive outcome (+30 morale, crisis resolved)
- 30% chance of negative outcome (makes things worse, −20 morale)
- If successful: Promoted to SymbolicLeader or gains "Hero" reputation tag (+20)
- If failed: Demoted or shamed (−15 reputation)

**Narrative Hook**: "The nobody who stepped up when the leader fell."

**Priority**: Nice-to-have

**Related Systems**: Initiative, Leadership Election, Crisis Response, Heroism

---

## The Scheming Opportunist

**Scope**: Individual

**Preconditions**:
- High initiative (>0.6)
- Opportunist outlook
- Neutral or Evil alignment
- Low loyalty to SymbolicLeader (<70)
- Access to resources or influence

**Gameplay Effects**:
- Entity pursues self-interest over aggregate goals
- Hoards resources (aggregate wealth −10%, entity wealth +20%)
- Undermines rivals (creates professional grudges)
- If caught: Loyalty of others to entity −30, may be expelled
- If successful: Gains power/wealth, may challenge for leadership
- 40% chance of triggering internal conflict

**Narrative Hook**: "The ambitious schemer who sees every crisis as an opportunity."

**Priority**: Wild experiment

**Related Systems**: Initiative, Outlook, Resource Hoarding, Internal Conflict

---

## The Paralyzed Collective

**Scope**: Aggregate

**Preconditions**:
- All members have low initiative (<0.3)
- Craven traits widespread (>60% members)
- Low morale (<30)
- Facing external threat or crisis

**Gameplay Effects**:
- Aggregate unable to act autonomously (decision paralysis)
- All actions delayed +50%
- Vulnerable to external aggression (−30% combat effectiveness)
- May be absorbed by more assertive aggregate
- Requires external intervention or new SymbolicLeader with high initiative
- If crisis resolved by outsider: aggregate becomes dependent (tributary/vassal)

**Narrative Hook**: "The band frozen in fear, waiting for someone to save them."

**Priority**: Nice-to-have

**Related Systems**: Initiative, Morale, External Threats, Dependency

---

# ALIGNMENT SHIFT PATTERNS

## The Corruption Cascade

**Scope**: Aggregate

**Preconditions**:
- Aggregate leadership shifts Evil (SymbolicLeader Moral <-40)
- Low cohesion (<0.4)
- Weak AlignmentStrength among members (<0.4 average)
- Resource scarcity or high stress

**Gameplay Effects**:
- Members' alignment gradually shifts to match leadership (−5 Moral per 200 ticks)
- Outlook shifts from Loyalist/Neutral to Opportunist
- Good members (<40) either leave (splintering) or comply (alignment shifts)
- Aggregate reputation decreases regionally (−20)
- May attract Evil-aligned recruits (+10% recruitment from Evil population)
- End state: Aggregate becomes Evil-aligned faction

**Narrative Hook**: "The good company that slowly became the villain."

**Priority**: Nice-to-have

**Related Systems**: Alignment Drift, Leadership Influence, Cohesion, Recruitment

---

## The Redemption Arc

**Scope**: Individual or Aggregate

**Preconditions**:
- Evil alignment (Moral <-40)
- Major traumatic event (e.g., witness consequences of own actions, lose loved one)
- Encounter with Good-aligned mentor or aggregate
- AlignmentStrength low enough to shift (<0.5)

**Gameplay Effects**:
- Alignment begins shifting Good (+10 Moral per 500 ticks if conditions persist)
- Grudges from past actions remain but decay faster
- May seek atonement (perform reparative actions)
- If full redemption (Moral >20): Gain "Redeemed" tag, morale +30
- If redemption fails (revert to Evil): Become more extreme (Moral −20, VengefulScore +30)
- Regional reputation slow to change (lags behind actual alignment by 1000 ticks)

**Narrative Hook**: "The villain who sought redemption and found a new path."

**Priority**: Wild experiment

**Related Systems**: Alignment Drift, Traumatic Events, Mentorship, Reputation

---

## The Fanatic Conversion

**Scope**: Individual or Small Aggregate

**Preconditions**:
- Neutral alignment (Moral −20 to +20)
- Exposed to Fanatic-led aggregate with high Purity (>70)
- Low AlignmentStrength (<0.3)
- Vulnerable state (low morale, recent loss, seeking purpose)

**Gameplay Effects**:
- Alignment rapidly shifts to match Fanatic aggregate (±30 per axis over 300 ticks)
- Outlook becomes Fanatic
- AlignmentStrength increases to 0.8+ (strong conviction)
- Loyalty to converting aggregate increases to 150+
- Difficult to reverse (AlignmentStrength resists further shifts)
- May become zealot (willing to die for cause, morale immune to danger)

**Narrative Hook**: "The lost soul who found certainty in fanaticism."

**Priority**: Nice-to-have

**Related Systems**: Alignment Drift, Outlook Shift, Conversion, Fanaticism

---

# LOYALTY & BETRAYAL PATTERNS

## The Traitor's Gambit

**Scope**: Individual (spy or disillusioned member)

**Preconditions**:
- Member of Aggregate A with loyalty <30
- High grudge against SymbolicLeader (intensity >60)
- Opportunist or Mutinous outlook
- Aggregate B offers incentive (wealth, power, revenge)

**Gameplay Effects**:
- Entity leaks information to Aggregate B
- Suspicion increases within Aggregate A (+0.1 per 100 ticks)
- If caught: Executed or imprisoned, aggregate morale +10 (traitor removed)
- If successful: Joins Aggregate B, gains wealth/status
- Aggregate A suffers from leaked intel (−20% effectiveness in conflict with B)
- Regional reputation for entity: "Traitor" tag (−30)

**Narrative Hook**: "The betrayer who sold out their comrades for personal gain."

**Priority**: Core

**Related Systems**: Loyalty, Spy Role, Grudges, Suspicion, Betrayal

---

## The Defector's Dilemma

**Scope**: Individual

**Preconditions**:
- Member of Aggregate A with moderate loyalty (60-90)
- Alignment distance with Aggregate A >50
- Encounters Aggregate B with alignment match >0.8
- Aggregate A issues morally conflicting order

**Gameplay Effects**:
- Entity faces choice: stay loyal or defect
- If defects: Joins Aggregate B, morale +20 (alignment match), loyalty to B starts at 80
- If stays: Loyalty to A −20, morale −15, grudge intensity against A +20
- Aggregate A views defection as betrayal (grudge against entity +40)
- Aggregate B may welcome defector or distrust them (50/50 based on Aggregate B outlook)

**Narrative Hook**: "The soldier torn between loyalty and conscience."

**Priority**: Core

**Related Systems**: Loyalty, Alignment, Defection, Moral Conflict

---

## The Oathkeeper

**Scope**: Individual

**Preconditions**:
- Lawful alignment (>60)
- Loyalty to aggregate >180 (max loyalty)
- Has made explicit oath/vow to aggregate or leader
- Aggregate faces existential threat or SymbolicLeader in danger

**Gameplay Effects**:
- Entity prioritizes oath above all else (including self-preservation)
- Will not flee or surrender even if rational to do so
- Combat effectiveness +30% when defending oath subject
- If oath subject (leader/aggregate) destroyed: Entity either:
  - Dies defending them (martyrdom, becomes legend: +50 reputation posthumously)
  - Survives but becomes "Broken Oath" (morale permanently −50, may become wanderer)
- Reputation tag "Oathkeeper" (+25)

**Narrative Hook**: "The knight who kept their oath to the bitter end."

**Priority**: Nice-to-have

**Related Systems**: Loyalty, Lawful Alignment, Oaths, Martyrdom

---

# REPUTATION & INFLUENCE PATTERNS

## The Living Legend

**Scope**: Individual

**Preconditions**:
- High reputation (>150)
- Completed 5+ heroic actions or critical successes
- High AlignmentStrength (>0.8)
- Survived 3+ life-threatening situations

**Gameplay Effects**:
- Inspires allies (morale +20 within 30m radius)
- Intimidates enemies (enemy morale −15 when entity present)
- Recruitment bonus (+30% when entity is member)
- Leadership election automatic (if no other legend present)
- If dies: Becomes martyr/saint, aggregate morale +50 then −30 from grief
- Regional reputation spreads (influence radius 100m)

**Narrative Hook**: "The hero whose very presence turns the tide of battle."

**Priority**: Nice-to-have

**Related Systems**: Reputation, Heroism, Morale, Leadership

---

## The Fallen Hero

**Scope**: Individual

**Preconditions**:
- Previously "Living Legend" status (reputation >150)
- Commits severe moral violation or betrayal
- Witnessed by allies or becomes public knowledge

**Gameplay Effects**:
- Reputation crashes (−100 immediately)
- Morale of former allies −40 (disillusionment)
- Loyalty to entity drops to 0 for all members
- May be exiled or executed by former aggregate
- Enemies gain morale +20 ("even their heroes are corrupt")
- If redeems self (very difficult): May regain some reputation over time (+5 per 500 ticks)

**Narrative Hook**: "The hero who fell from grace and became everything they fought against."

**Priority**: Wild experiment

**Related Systems**: Reputation, Betrayal, Morale, Redemption

---

## The Infamous Villain

**Scope**: Individual

**Preconditions**:
- Evil alignment (Moral <-60)
- Committed 5+ war crimes or atrocities
- High reputation (but negative: <-150)
- Survived multiple attempts on life

**Gameplay Effects**:
- Feared by enemies (enemy morale −25)
- Attracts Evil-aligned followers (+40% recruitment from Evil population)
- Hunted by Good-aligned aggregates (bounty placed)
- May inspire "hero" to rise and oppose them (generates nemesis)
- If killed: Killer becomes "Hero" (reputation +50)
- Regional reputation: "Terror" (all Good entities hostile on sight)

**Narrative Hook**: "The monster whose name alone strikes fear into the hearts of the innocent."

**Priority**: Wild experiment

**Related Systems**: Reputation, Evil Alignment, War Crimes, Nemesis Generation

---

# GUILD & FACTION PATTERNS

## The Schism

**Scope**: Guild/Faction

**Preconditions**:
- Guild/faction with 10+ members
- Two distinct philosophical factions (alignment distance >60)
- Low cohesion (<0.3)
- Doctrinal dispute or leadership crisis

**Gameplay Effects**:
- Guild/faction splits into two new factions
- Each faction claims legitimacy ("True" vs "Reformed")
- Members choose sides based on alignment match
- 20% of members remain neutral, may join neither
- New factions become rivals (grudge intensity starts at 40)
- Regional influence splits proportionally to membership

**Narrative Hook**: "The church that split in two, each claiming to be the true faith."

**Priority**: Core

**Related Systems**: Guilds, Splintering, Alignment, Doctrinal Disputes

---

## The Guild Merger

**Scope**: Cross-Guild

**Preconditions**:
- Two guilds with similar specializations
- Alignment distance <30
- Both facing external threat or economic pressure
- Leadership mutual respect (low CommandFriction if dual leadership exists)

**Gameplay Effects**:
- Guilds merge into single larger guild
- Combined membership (10-50 members)
- New governance structure (election or negotiated)
- Collaboration bonuses (+15% efficiency in shared domain)
- Regional influence increases (+20%)
- May create tensions if members resist integration (cohesion starts at 0.5)

**Narrative Hook**: "Two guilds became one, stronger together than apart."

**Priority**: Nice-to-have

**Related Systems**: Guilds, Merging, Collaboration, External Threats

---

## The Radical Takeover

**Scope**: Guild/Faction

**Preconditions**:
- Guild/faction with moderate alignment
- Subset of members with extreme alignment (>70 on any axis) and Fanatic outlook
- Low AlignmentStrength among moderate members (<0.4)
- Crisis or external threat weakens moderate leadership

**Gameplay Effects**:
- Radicals seize leadership (coup or democratic takeover)
- Guild/faction alignment shifts rapidly to radical position (±50 per axis over 200 ticks)
- Moderate members either:
  - Comply (alignment shifts to match radicals)
  - Leave (splintering, form moderate opposition)
  - Are purged (expelled or imprisoned)
- Guild/faction becomes aggressive or isolationist (depending on radical ideology)
- Regional reputation shifts dramatically

**Narrative Hook**: "The guild seized by radicals, transformed into something unrecognizable."

**Priority**: Nice-to-have

**Related Systems**: Guilds, Radicalization, Alignment Drift, Coup

---

# ENVIRONMENTAL & EXTERNAL PATTERNS

## The Siege Mentality

**Scope**: Aggregate

**Preconditions**:
- Aggregate under sustained external threat (500+ ticks)
- Low resources (<30%)
- High stress (>0.7)
- Isolated from allies

**Gameplay Effects**:
- Cohesion increases temporarily (+0.2, survival instinct)
- Alignment shifts Lawful (+10 Order, need for discipline)
- Outlook shifts to Loyalist (rally around leadership)
- Moral conflict thresholds increase (desperate times, desperate measures)
- Efficiency decreases (−20%, stressed and exhausted)
- If siege lifts: Cohesion crash (−0.3, release of tension)
- If siege succeeds (aggregate destroyed): Survivors scatter, traumatized

**Narrative Hook**: "The besieged fortress, holding together out of sheer necessity."

**Priority**: Nice-to-have

**Related Systems**: External Threats, Stress, Cohesion, Resource Scarcity

---

## The Diaspora

**Scope**: Cross-Aggregate (post-destruction)

**Preconditions**:
- Aggregate destroyed by external force
- Survivors scattered (3-10 entities)
- High loyalty to destroyed aggregate (>120)
- Strong cultural/ideological identity (AlignmentStrength >0.7)

**Gameplay Effects**:
- Survivors seek to rebuild aggregate elsewhere
- Loyalty to original aggregate persists (becomes "in exile" status)
- Reputation tag "Refugee" or "Exile"
- May join other aggregates but retain dual loyalty
- 30% chance of founding new aggregate within 1000 ticks
- If successful: New aggregate named after original ("New [Name]" or "[Name] Reborn")
- Inherited grudge against destroyers (intensity 90, Vendetta)

**Narrative Hook**: "The scattered remnants, dreaming of home and plotting revenge."

**Priority**: Wild experiment

**Related Systems**: Aggregate Destruction, Survivors, Loyalty, Refugees

---

## The Golden Age

**Scope**: Aggregate or Region

**Preconditions**:
- High cohesion (>0.85)
- High morale (>80)
- High resources (>80%)
- Low stress (<0.2)
- No major conflicts for 2000+ ticks

**Gameplay Effects**:
- Efficiency +30% on all tasks
- Research/innovation rate +40%
- Population growth +20%
- Reputation spreads regionally (+30 influence radius 100m)
- Attracts recruits and migrants (+50% immigration)
- Cultural flourishing (unlock special cultural traits/traditions)
- Vulnerable to complacency (if Golden Age lasts >5000 ticks, initiative −10%)

**Narrative Hook**: "The shining city on the hill, a beacon of prosperity and hope."

**Priority**: Wild experiment

**Related Systems**: Cohesion, Morale, Resources, Cultural Development

---

# CROSS-AGGREGATE DYNAMICS

## The Proxy War

**Scope**: Cross-Aggregate (4+ aggregates)

**Preconditions**:
- Two major factions (A and B) with grudge intensity >60
- Two minor aggregates (C and D) with loyalties to A and B respectively
- Major factions reluctant to engage directly (reputation concerns or resource constraints)

**Gameplay Effects**:
- Major factions manipulate minor aggregates into conflict (supply, intel, incentives)
- C and D engage in combat (proxy war)
- Major factions gain/lose based on proxy outcomes without direct cost
- If proxy wins: Major faction gains regional influence (+15%)
- If proxy loses: Major faction loses influence (−10%) and may need to intervene directly
- Minor aggregates suffer casualties and resource drain (−30%)
- Regional instability increases

**Narrative Hook**: "The great powers fought through pawns, unwilling to dirty their own hands."

**Priority**: Wild experiment

**Related Systems**: Factions, Grudges, Proxy Conflicts, Regional Influence

---

## The Alliance of Necessity

**Scope**: Cross-Aggregate

**Preconditions**:
- Two or more aggregates with alignment distance >50 (natural enemies)
- Facing common existential threat (larger aggregate, environmental disaster)
- Pragmatic outlooks (Opportunist or Scholarly)

**Gameplay Effects**:
- Temporary alliance formed (treaty: mutual defense)
- Cohesion within alliance low (<0.4)
- Frequent internal disputes (decision-making delayed +20%)
- If threat defeated: 70% chance alliance dissolves immediately
- If threat persists: Alliance may stabilize (cohesion +0.05 per 300 ticks)
- Post-threat: May become permanent alliance or revert to hostility

**Narrative Hook**: "Strange bedfellows, united by fear and necessity."

**Priority**: Nice-to-have

**Related Systems**: Alliances, External Threats, Diplomacy, Temporary Truces

---

## The Domino Effect

**Scope**: Regional (multiple aggregates)

**Preconditions**:
- One aggregate suffers major crisis (splintering, defeat, resource collapse)
- Neighboring aggregates have economic or social ties to crisis aggregate
- Regional cohesion already moderate (<0.6)

**Gameplay Effects**:
- Crisis spreads to connected aggregates (cascade effect)
- Each connected aggregate suffers:
  - Morale −15
  - Resources −10%
  - Cohesion −0.1
- May trigger additional splintering or conflicts
- Regional instability increases
- If one aggregate stabilizes: Can help others recover (+10 morale to connected aggregates)
- If all fall: Regional collapse (chaos state, requires external intervention)

**Narrative Hook**: "One domino fell, and the whole region tumbled after."

**Priority**: Wild experiment

**Related Systems**: Regional Dynamics, Economic Ties, Cascade Effects, Stability

---

# INDIVIDUAL JOURNEY PATTERNS

## The Nobody to Hero

**Scope**: Individual

**Preconditions**:
- Low initial reputation (<20)
- Bold traits (>40) and Good alignment (>40)
- Aggregate in crisis or under threat
- High initiative (>0.6)

**Gameplay Effects**:
- Entity performs heroic action autonomously
- If successful: Reputation +30, morale boost to aggregate (+20)
- Promoted to OperationalLeader or SymbolicLeader role
- May trigger "Inspiration" cascade (other low-reputation members attempt heroic actions)
- If failed: Reputation −10, but respected for trying (+5 loyalty from Bold members)

**Narrative Hook**: "The nobody who became somebody when it mattered most."

**Priority**: Nice-to-have

**Related Systems**: Reputation, Heroism, Initiative, Leadership Election

---

## The Fall from Power

**Scope**: Individual (SymbolicLeader)

**Preconditions**:
- SymbolicLeader with declining performance (failed 3+ major decisions)
- Low loyalty from members (<70 average)
- Rival with high reputation (>100) present in aggregate
- Democratic or Meritocratic governance

**Gameplay Effects**:
- Leadership challenge triggered
- Aggregate votes between current leader and rival
- If challenger wins: Current leader demoted to normal member or exiled
- If current leader wins: Authority restored but grudge created with challenger
- Cohesion drops during transition (−0.15)
- If exiled: Former leader may found rival aggregate

**Narrative Hook**: "The king who lost their throne to a worthier successor."

**Priority**: Nice-to-have

**Related Systems**: Leadership, Voting, Rivalry, Demotion

---

## The Lone Wanderer

**Scope**: Individual

**Preconditions**:
- Entity leaves or expelled from aggregate
- No other aggregate within 50m
- Low loyalty to all known aggregates (<40)
- Neutral or Opportunist outlook

**Gameplay Effects**:
- Entity operates independently (no aggregate bonuses)
- Autonomy increases (initiative +0.2)
- Morale fluctuates based on survival success
- May encounter other aggregates:
  - Recruit to aggregate (if alignment match >0.7)
  - Attacked as outsider (if territorial aggregate)
  - Ignored (if neutral aggregate)
- 40% chance of founding new aggregate if encounters 2+ other wanderers

**Narrative Hook**: "The wanderer with no home, seeking a place to belong."

**Priority**: Wild experiment

**Related Systems**: Independence, Wandering, Recruitment, Aggregate Formation

---

# KNOWLEDGE & LEARNING PATTERNS

## The Breakthrough

**Scope**: Individual or Guild

**Preconditions**:
- Scholarly outlook
- High skill in relevant domain (>80)
- Access to research resources
- Low stress (<0.3, focused work)

**Gameplay Effects**:
- Discover new knowledge/technique/spell signature
- Reputation +20
- Guild gains specialization bonus (+15% in domain)
- Knowledge spreads to other Scholarly members (teaching cascade)
- May trigger "Intellectual Rivalry" with other guilds (grudge intensity +20)

**Narrative Hook**: "The scholar who unlocked secrets others deemed impossible."

**Priority**: Nice-to-have

**Related Systems**: Knowledge, Skills, Research, Guild Specialization

---

## The Master-Apprentice Bond

**Scope**: Individual (pair)

**Preconditions**:
- Master with high skill (>90) and Scholarly outlook
- Apprentice with low skill (<30) but high initiative (>0.5)
- Both in same aggregate or guild
- Partnership duration >1000 ticks

**Gameplay Effects**:
- Apprentice skill increases rapidly (+10 per 300 ticks, vs normal +5)
- Apprentice alignment gradually shifts toward Master (±5 per 500 ticks)
- Loyalty between pair increases to 150+
- If Master dies: Apprentice gains "Legacy" trait (inherits portion of Master's reputation)
- If Apprentice betrays: Master suffers morale −30, grudge intensity 90

**Narrative Hook**: "The student who learned at the feet of the master."

**Priority**: Nice-to-have

**Related Systems**: Knowledge Transmission, Skills, Mentorship, Loyalty

---

## The Forbidden Knowledge

**Scope**: Individual or Guild

**Preconditions**:
- Discover knowledge with moral/ethical implications
- Low Purity (<20) or Opportunist outlook
- High curiosity (Scholarly) but low AlignmentStrength (<0.4)

**Gameplay Effects**:
- Choice: Use forbidden knowledge or suppress it
- If used: Power increase (+30% effectiveness in domain) but alignment shifts Evil/Corrupt (−20)
- If suppressed: Reputation +10 (ethical choice) but rivals may discover it
- If knowledge spreads: Regional instability (other entities seek it)
- May trigger "Inquisition" from Lawful/Pure factions (persecution)

**Narrative Hook**: "The researcher who found dark secrets and paid the price."

**Priority**: Wild experiment

**Related Systems**: Knowledge, Alignment Drift, Ethics, Persecution

---

# PLAYER INTERVENTION PATTERNS

## The Divine Sniper

**Scope**: Individual + Player Intervention

**Preconditions**:
- Manual aim enabled (tech unlock or miracle power)
- Entity with ranged weapon
- Player attention focused on entity
- Critical moment (boss fight, siege, decisive battle)
- Rewind/time control available

**Gameplay Effects**:
- Time slows to 10% speed (or full pause with modifier key)
- Trajectory visualization renders (color-coded by hit probability)
- Player aims precisely at cursor position
- Rewind available for iteration (try multiple aims)
- On success: Satisfying "planned shot" dopamine hit
- On repeated success: Player feels like tactical genius or omniscient god
- Miracle point cost (Godgame) or cooldown (Space4X)

**Narrative Hook**: "The god-guided arrow that never misses, the sniper shot calculated across infinite timelines."

**Priority**: Core (major feature pillar)

**Related Systems**: Manual Aim, Rewind, Time Control, Projectiles, Miracles, Tech Progression

---

## The Bullet-Time Savior

**Scope**: Individual + Player Intervention

**Preconditions**:
- Friendly unit about to be hit by enemy projectile
- Player has "Divine Influence" unlocked (Godgame) or "Point Defense Override" (Space4X)
- Time control active

**Gameplay Effects**:
- Time freezes, incoming projectile visible mid-flight
- Player aims own unit's weapon to intercept enemy projectile
- Successful interception destroys both projectiles
- Saved unit morale +30 ("Divine Protection" witnessed)
- Reputation +10 ("Miracle Worker" tag)
- High skill requirement (narrow timing window)

**Narrative Hook**: "The impossible shot that saved a life, witnessed by all as divine intervention."

**Priority**: Nice-to-have

**Related Systems**: Manual Aim, Projectile Collision, Time Stop, Morale, Reputation

---

## The Architect of Fate

**Scope**: Multi-Entity + Player Intervention (Advanced)

**Preconditions**:
- Player has high-tier tech (Space4X "Tactical Omniscience") or miracle (Godgame "Hand of Fate")
- Multiple entities in combat (5+ on each side)
- Rewind available with long history buffer (60+ seconds)
- Player willing to invest time iterating

**Gameplay Effects**:
- Player enters "Tactical Planning Mode":
  - Time fully paused
  - Can aim 3-5 entities simultaneously
  - Stagger fire timings
  - Predict cascading outcomes
- Execute plan, observe results
- If suboptimal: Rewind and adjust
- Iterate until desired outcome achieved
- Final execution feels like "perfect plan coming together"
- High skill ceiling (planning complexity)

**Narrative Hook**: "The commander who saw all possible futures and chose the one where they won."

**Priority**: Wild experiment

**Related Systems**: Manual Aim, Multi-Target, Rewind, Time Stop, Tactical UI

---

## The Glorious End (Last Stand)

**Scope**: Individual + Cultural Trait (Space4X)

**Preconditions**:
- Pilot with warrior culture (CulturalLastStandTrait: Honorbound, Berserker, Zealot, etc.)
- Strike craft critically damaged (<25% hull)
- Enemy capital ship within ramming range (500m)
- Pilot passes decision roll (high alignment strength + low survival instinct)
- Outlook: Fanatic, Bold, or Loyalist

**Gameplay Effects**:
- Pilot crashes dying craft into enemy capital ship (kamikaze boarding)
- Explosive impact damage (kinetic + fuel + ordnance detonation)
- Directional damage based on impact location (Bridge, Engines, Weapons, Hangar)
- Pilot survival roll (20-40% chance based on armor, angle, luck)
- If survives: Pilot inside enemy ship, can:
  - Join existing boarding party (strength in numbers)
  - Solo sabotage (stealth, disable critical systems)
  - Rampage (maximum carnage, glory death)
  - Lay low (survival mode, hide until capture/rescue)
- Ship security response escalates (detection → manhunt → lockdown)
- Resolution outcomes:
  - **Recovered**: Ship captured by allies, pilot extracted (+30 reputation, +20 faction morale)
  - **Captured**: Ransomed, executed, recruited (defection), or released
  - **Killed**: Martyrdom (+50 posthumous reputation, inspiration buff to allies)
  - **Trapped**: Ship escapes, pilot in prolonged limbo (weeks/months)

**Narrative Hook**: "The warrior who chose death inside the enemy's heart over life in retreat."

**Priority**: Core (cultural identity pillar for Space4X)

**Related Systems**: Last Stand, Directional Damage, Boarding, Crew Capture, Alignment, Loyalty, Reputation, Cultural Traits, Interior Combat

---

# MODULAR HULL & CUSTOMIZATION PATTERNS

## The Q-Ship Ambush

**Scope**: Individual Vessel + Tactical Deception (Space4X)

**Preconditions**:
- Hauler hull configured as Q-Ship (hidden weapons, decoy cargo bays)
- Chaotic or Opportunist faction (willing to use deception)
- Encounters enemy raider/pirate expecting easy prey
- Weapons powered down to avoid detection

**Gameplay Effects**:
- Initial scan shows hauler signature (low threat)
- Enemy closes to boarding/attack range
- Q-Ship powers up weapons (power spike visible on sensors)
- Hidden hardpoints deploy (railguns, missile launchers)
- First volley catches enemy unprepared (−20% defense, surprise penalty)
- Enemy morale −30 ("Ambushed!" debuff)
- If Q-Ship wins: Reputation +20 as pirate hunter
- If Q-Ship loses: Cargo lost anyway, module investment wasted

**Narrative Hook**: "The merchant that was really a warship in disguise."

**Priority**: Nice-to-have

**Related Systems**: Modular Hull, Power Management, Sensor Detection, Deception, Surprise

---

## The Desperate Refit

**Scope**: Individual Vessel + Crisis Response

**Preconditions**:
- Vessel docked at friendly station/carrier
- Crew suffered heavy losses or module damage
- Emergency situation (invasion, siege, critical mission)
- Player or AI commander willing to sacrifice specialization for survival

**Gameplay Effects**:
- Emergency module swap performed (e.g., remove cargo bays, install weapons)
- Refit time: 300-1000 ticks depending on module size
- Crew morale fluctuates based on refit purpose:
  - Defensive modules (shields, armor): Morale +10 (safety)
  - Offensive modules (weapons): Morale varies (Bold +10, Craven −10)
  - Cargo removal for combat: Morale −5 (loss of profit)
- New configuration may violate mass/power limits (emergency overload)
- After crisis: Revert to original configuration or keep new loadout

**Narrative Hook**: "The freighter turned warship when survival demanded it."

**Priority**: Core

**Related Systems**: Modular Hull, Refitting, Emergency Response, Module Swapping

---

## The Stripped Carrier

**Scope**: Individual Vessel + Extreme Specialization

**Preconditions**:
- Carrier hull configured with maximum hangars, minimal weapons
- Faction doctrine emphasizes strike craft superiority
- Carrier has escort vessels for protection
- High crew skill in fighter operations (>70)

**Gameplay Effects**:
- 2-3x normal fighter capacity (240+ fighters on large carrier)
- Extremely vulnerable if escorts destroyed (no defense)
- Fast fighter launch rate (+50% deployment speed)
- If caught alone: 80% chance of destruction
- If properly escorted: Overwhelming strike craft superiority
- Doctrine risk: "All eggs in one basket" vulnerability

**Narrative Hook**: "The glass cannon carrier that ruled the skies—until it didn't."

**Priority**: Nice-to-have

**Related Systems**: Modular Hull, Doctrine, Escort Tactics, Vulnerability

---

## The Overloaded Gambit

**Scope**: Individual Vessel + Risk/Reward

**Preconditions**:
- Vessel configured beyond mass/power limits (120-150% capacity)
- Critical mission (must succeed despite risks)
- Crew Bold or Fanatic (willing to take risk)
- No alternative vessels available

**Gameplay Effects**:
- Speed reduced by 50-75% (mass overload)
- Power brownouts (systems randomly shut down)
- Module stress damage over time (10% efficiency loss per 500 ticks)
- If mission successful before breakdown: Heroic victory (+30 reputation)
- If vessel breaks down mid-mission: Catastrophic failure (stranded, vulnerable)
- Crew morale oscillates: Bold +20 ("glorious risk"), Craven −30 ("death trap")
- Post-mission: Emergency repairs required (1000+ ticks downtime)

**Narrative Hook**: "The ship loaded beyond reason, a ticking time bomb chasing glory."

**Priority**: Nice-to-have

**Related Systems**: Modular Hull, Overload Mechanics, Risk Management, Module Stress

---

## The Alignment-Locked Shame

**Scope**: Individual + Alignment Restriction (Evil Factions)

**Preconditions**:
- Evil faction vessel with slave pens installed
- Vessel captured by Good faction forces
- Good faction crew attempts to use ship

**Gameplay Effects**:
- Slave pen modules cannot be removed (permanent hull modification)
- Good alignment crew suffers morale −40 ("Tainted Ship" debuff)
- Options:
  - Scuttle ship (destroy rather than use)
  - Convert slave pens to cargo (requires 1000 tick refit, moral conflict)
  - Accept morale penalty and use ship as-is
- If used: Regional reputation −20 ("Using slaver vessels")
- If scuttled: Reputation +10 ("Moral High Ground")

**Narrative Hook**: "The captured ship with a dark past that good crews refused to sail."

**Priority**: Wild experiment

**Related Systems**: Modular Hull, Alignment Gates, Moral Conflict, Capture, Refitting

---

## The Frankenstein Refit

**Scope**: Individual Vessel + Chaotic Factions

**Preconditions**:
- Chaotic faction with access to salvaged/captured modules
- Mismatched module types from different factions
- Jury-rigged power systems (overclocked reactors)
- Crew with high maintenance skill but low standards

**Gameplay Effects**:
- Vessel has modules from 3+ different tech trees/factions
- Power efficiency −20% (incompatible systems)
- Mass distribution unbalanced (−10% maneuverability)
- Random module failures (5% chance per 500 ticks)
- Unique strengths: May have module combinations normally impossible
- If successful in combat: "Mad Genius" reputation (+15)
- If catastrophic failure: Explosion risk (20% on critical damage)

**Narrative Hook**: "The patchwork ship held together with duct tape and prayer."

**Priority**: Wild experiment

**Related Systems**: Modular Hull, Salvage, Tech Mixing, Jury-Rigging, Chaotic Alignment

---

# ENVIRONMENTAL QUESTS & LOOT VECTORS

## The Haunted Consequence

**Scope**: Regional + Environmental Corruption

**Preconditions**:
- Major battle or massacre leaves blood-soaked ground (50+ deaths)
- Cemetery or battlefield abandoned without proper burial rites
- Corruption accumulation reaches 0.7+ intensity
- Village within 200m of corruption epicenter

**Gameplay Effects**:
- Benevolent spirits spawn seeking closure (return heirlooms, fulfill promises)
- Malevolent haunts spawn if spirits ignored (attack villagers, spread fear)
- Village morale −20 ("Haunted Grounds" debuff)
- Priests/shamans can commune with spirits, learn skills from dead villagers
- Physical classes (warriors) ineffective vs. ethereal (0.2x damage)
- If resolved: Corruption reduces to 0.2, spirits grant blessings
- If ignored: Spirits turn malevolent, corruption spreads to 1.0

**Narrative Hook**: "The dead remember, and they will not rest until justice is done."

**Priority**: Core

**Related Systems**: Environmental Corruption, Class Effectiveness, Spirit Communion, Knowledge Transmission

---

## The Deforestation Reckoning

**Scope**: Environmental + Resource Exploitation

**Preconditions**:
- Village logs >70% of nearby forest (deforestation corruption 0.9+)
- Forest spirits awakened by destruction
- No druids or shamans in village to negotiate

**Gameplay Effects**:
- Corrupted treants spawn (5-10 entities)
- Forest spirit declares war on village
- Harvest yields −30% (forest curse)
- Combat resolution: Treants defeated but corruption persists (0.6)
- Negotiation resolution: Stop logging, replant 100 trees, gain blessing (+10% yields)
- If ignored: Forest encroaches on village, buildings damaged by roots
- Long-term: Reforestation removes corruption over 2000 ticks

**Narrative Hook**: "The forest remembers every tree felled, and nature demands balance."

**Priority**: Nice-to-have

**Related Systems**: Resource Exploitation, Environmental Corruption, Negotiation, Alignment

---

## The Demonic Bargain

**Scope**: Individual + Alignment Shift

**Preconditions**:
- Evil or Opportunist villager (Moral < −30 or AlignmentStrength < 0.4)
- Discovers demon portal in mine/abandoned ruin
- Demon offers forbidden knowledge (necromancy, dark magic, power)
- Villager must choose: accept bargain or report to authorities

**Gameplay Effects**:
- If accepted:
  - Gain forbidden knowledge (+30% effectiveness in domain)
  - Alignment shift toward Evil (Moral −20, Purity −20)
  - Corruption spreads (0.01 per tick)
  - Price: Serve demon for 3000 ticks, spread corruption
  - Knowledge contagious (spreads to guild members)
- If reported:
  - Village sends exorcism team (priest/paladin)
  - Reputation +10 ("Resisted Temptation")
  - Demon portal closed
- Long-term consequences:
  - Accepted: Village becomes evil, or demon invasion
  - Reported: Village safe but necromancer resentful

**Narrative Hook**: "Power beyond imagination, for a price the soul cannot afford."

**Priority**: Core

**Related Systems**: Demonic Bargains, Forbidden Knowledge, Alignment Drift, Guild Curriculum

---

## The Undead Labor Economy

**Scope**: Aggregate + Alignment-Gated Economy

**Preconditions**:
- Necromancer in village (Evil alignment, Moral < −50)
- Undead enslaved through necromancy (10+ undead)
- Village accepts undead workers (Evil or Opportunist aggregate)

**Gameplay Effects**:
- Undead perform labor (mining, hauling, refining) at 80% efficiency
- No food/rest required (infinite stamina)
- Village production +40% (ore, wood, stone)
- Morale impact:
  - Evil villagers: +10 ("Efficient Labor")
  - Neutral villagers: −10 ("Unsettling")
  - Good villagers: −30 ("Abomination"), may leave
- Obedience decay: Undead revolt if necromancer skill drops or dies
- High-level undead (liches) can research (+20% research speed)
- Council participation: Liches serve as advisors (Evil aggregates only)
- Regional reputation: −20 ("Necropolis"), Good factions hostile

**Narrative Hook**: "The dead labor ceaselessly, a workforce without complaint or cost."

**Priority**: Nice-to-have

**Related Systems**: Necromancer Enslavement, Labor Economy, Alignment Gates, Morale

---

## The Border Encroachment

**Scope**: Territorial + Village Defense

**Preconditions**:
- Village influence radius not patrolled (peacekeepers < 3)
- Encroachment pressure reaches 0.7+ (no border security for 500+ ticks)
- Corruption spawns at village borders (darkness vs. light)

**Gameplay Effects**:
- Threats spawn at border zones (haunts, wildlife, bandits)
- Village morale −15 ("Unsafe Borders")
- Peacekeepers patrol borders, fight wilderness (light vs. darkness)
- Each patrol reduces encroachment pressure by 0.1
- If borders secured (light level > 0.8): Pressure reduces passively
- Village expansion:
  - Lawful/Good: Build walls, slow expansion (60% resources to defense)
  - Chaotic/Evil: Aggressive expansion, low defenses (20% to defense)
- Encroachment ignored: Threats enter village, attack population

**Narrative Hook**: "The darkness presses in, and only vigilance holds it at bay."

**Priority**: Core

**Related Systems**: Territorial Influence, Peacekeeper Patrols, Village Expansion, Alignment

---

## The Ancestral Communion

**Scope**: Individual + Knowledge Transmission

**Preconditions**:
- Shaman in village (high spiritual skill >70)
- Dead villager persists as spirit with retained skills
- Spirit willing to teach (Good alignment or payment offered)
- Shaman attempts communion

**Gameplay Effects**:
- Communion success based on shaman skill (50-90% chance)
- If successful:
  - Spirit teaches retained skills (carpentry, herbalism, combat)
  - Shaman learns at 2x speed (ancestral guidance)
  - Spirit shares secrets (hidden treasures, warnings, lore)
  - Spirit alignment influences shaman (±5 per 500 ticks)
- If failed:
  - Spirit offended, may turn malevolent
  - Shaman suffers morale −10 ("Failed Communion")
- Good spirits willing to teach freely
- Evil spirits demand payment (sacrifice, dark deed)
- Neutral spirits barter (treasure for knowledge)

**Narrative Hook**: "The dead speak, and the wise listen to their whispers."

**Priority**: Nice-to-have

**Related Systems**: Spirit Communion, Knowledge Transmission, Class Effectiveness, Alignment

---

## The Derelict Jackpot (Space4X)

**Scope**: Individual Vessel + Loot Vector (Space4X)

**Preconditions**:
- Captain discovers ancient derelict on scanners
- Derelict has infestation (rogue robots, aliens, void horrors)
- Valuable cargo + ancient tech blueprint inside
- Captain must decide: salvage or avoid

**Gameplay Effects**:
- Boarding party composition matters:
  - Engineers: Disable robots (75% success)
  - Marines: Fight aliens (60% success)
  - Scientists: Understand void horrors (40% success)
- Success tiers:
  - Full clearance: Cargo + tech + derelict hull salvage
  - Partial: Cargo only, crew casualties
  - Failure: Crew lost, derelict escapes
- Rewards:
  - Tech blueprints: Jump drive upgrade, weapon tech
  - Cargo: 500-2000 tons (value: 5000-20000 credits)
  - Reputation: +15-30 ("Derelict Raider")
- Risks:
  - Infestation spreads to ship (quarantine failure)
  - Cursed tech (malfunctions, crew possession)
  - Void corruption (crew morale −20, alignment shift Evil)

**Narrative Hook**: "The ancient hulk holds riches beyond measure, and horrors to match."

**Priority**: Nice-to-have

**Related Systems**: Space Encounters, Boarding Parties, Loot Tables, Tech Progression (Space4X)

---

## The Wormhole Gambit (Space4X)

**Scope**: Individual + Risk/Reward (Space4X)

**Preconditions**:
- Captain discovers unstable wormhole
- Wormhole destination unknown (could be anywhere, or nowhere)
- Resources/time constraints force decision (pursue fleeing enemy, escape danger)

**Gameplay Effects**:
- Jump through wormhole: Random destination
- Outcomes:
  - **Lucky**: Shortcut to destination (+1000km travel saved)
  - **Unlucky**: Stranded in unknown sector (must find way back)
  - **Jackpot**: Ancient precursor system (ruins, tech, resources)
  - **Disaster**: Hostile alien territory, immediate combat
  - **Temporal**: Time dilation (crew ages, but arrive early/late)
- Bold captains more likely to risk (+40% acceptance)
- Lawful captains refuse (too unpredictable)
- Opportunist captains gamble if high reward potential

**Narrative Hook**: "The wormhole beckoned, a gateway to fortune or oblivion."

**Priority**: Wild experiment

**Related Systems**: Space Anomalies, Captain Personality, Risk Management (Space4X)

---

## The Grafting Horror

**Scope**: Individual + Evil Experimentation

**Preconditions**:
- Evil-aligned necromancer/surgeon (Moral < −40)
- Access to unwilling subjects (kidnapped villagers)
- Hidden lab (outside village borders or underground)
- Surgical skill >60

**Gameplay Effects**:
- Kidnapping triggers missing persons investigation (3+ missing = detection risk)
- Grafting progress (0-1 over 800 ticks)
- Outcomes:
  - Success (40%): Subject gains extra limb (3rd arm, tentacle), alignment shift Evil −15
  - Partial (30%): Dysfunctional graft (50% effectiveness), chronic pain morale −20
  - Failure (20%): Subject dies, becomes patchwork material
  - Catastrophic (10%): Subject transforms into abomination, attacks experimenter
- Counter-quest triggers when lab discovered
- Good-aligned rescuers horrified (morale −30 "Witnessed Abomination")

**Narrative Hook**: "The surgeon who played god and created monsters in the dark."

**Priority**: Nice-to-have

**Related Systems**: Evil Experimentation, Limb Grafting, Counter-Quests, Kidnapping

---

## The Undead Patchwork

**Scope**: Individual + Necromancy (Evil)

**Preconditions**:
- Necromancer with high skill (>70)
- Access to 3-8 corpses (battlefields, cemeteries, kidnapped)
- Evil alignment (Moral < −50)
- 1500 ticks assembly + reanimation time

**Gameplay Effects**:
- Patchwork types:
  - **Brute**: 4 arms, 2.5x strength
  - **Assassin**: Extra legs, spider-climb, stealth
  - **Scholar**: Multiple heads, enhanced intellect (research bonus)
  - **Abomination**: Random mix, unstable (30% revolt risk)
- Corruption spreads from creation site (0.02 per tick)
- Unstable patchworks revolt if creator dies or skill drops
- Good villagers horrified if discovered (morale −40)
- Counter-quest: Destroy patchworks, rescue kidnapped victims

**Narrative Hook**: "The corpse stitched from many, serving none but its dark master."

**Priority**: Nice-to-have

**Related Systems**: Necromancy, Undead Labor, Counter-Quests, Corruption

---

## The Breeding Catastrophe

**Scope**: Regional + Chaos Experimentation

**Preconditions**:
- Evil/Chaotic breeder (Moral < −30 or Order < −40)
- Breeding facility (hidden pens, 7+ generations for instability)
- Access to base creatures (wolves, spiders, monsters)

**Gameplay Effects**:
- Generation progression:
  - Gen 1-3: Mild enhancements (2x size, +20% damage)
  - Gen 4-6: Significant mutations (new abilities)
  - Gen 7+: Unstable abominations (50% containment breach risk)
- **Containment Breach**:
  - Horrors escape into wilderness (30% at Gen 7+)
  - Attack nearby villagers (threat level scales with generation)
  - Reproduce in wild (permanent regional threat)
- Counter-quest: Exterminate breeding stock, burn facility, hunt escapees
- If uncontained: Permanent wildlife threat (basilisks, chimeras, dire wolves)

**Narrative Hook**: "The breeder who twisted nature until it broke free and devoured him."

**Priority**: Wild experiment

**Related Systems**: Breeding Programs, Containment, Wildlife Threats, Counter-Quests

---

## The Band of Unlikely Heroes

**Scope**: Cross-Aggregate + Counter-Quest

**Preconditions**:
- Adventuring band discovers evil scheme (15% chance per exploration)
- Band has Bold leader or Good alignment (Moral > 30)
- Band strength > threat level × 0.8 (not hopelessly outmatched)
- No time to fetch village reinforcements

**Gameplay Effects**:
- Band decides to intervene independently (guild/village unaware)
- Combat: Band vs. evil actors (necromancers, cultists, horrors)
- **Outcomes**:
  - **Near Miss Victory**: Band wins, minimal casualties, reputation +35 ("Unlikely Heroes")
  - **Pyrrhic Victory**: Band wins, 40% casualties, reputation +20 ("Brave Attempt")
  - **Failure**: Band wiped out, evil scheme continues, reputation posthumous +10
- Village rewards survivors (500-2000 gold, honorary titles)
- Inspires other bands to act (regional morale +10)

**Narrative Hook**: "The band that stumbled upon evil and chose to fight, not flee."

**Priority**: Core

**Related Systems**: Band Initiative, Counter-Quests, Independent Action, Reputation

---

## The Guild Schism

**Scope**: Aggregate + Internal Conflict

**Preconditions**:
- Guild member engages in evil experimentation (demon summoning, patchworks)
- Junior member discovers scheme (random encounter in guildhall)
- Guild has mixed alignments (Good, Neutral, Evil members)
- Ritual 70%+ complete (critical decision point)

**Gameplay Effects**:
- Guild splits by alignment:
  - Good faction demands intervention (40-60% of guild)
  - Evil faction supports experimenter (10-30% of guild)
  - Neutral faction follows guild master (20-40% of guild)
- Internal battle if guild master chooses intervention
- **Resolution**:
  - Evil faction defeated: Expulsion/execution, guild morale −15, reputation −10 ("Internal Corruption")
  - Evil faction wins: Good members leave, guild becomes evil-aligned
  - Stalemate: Guild splinters into 2 separate guilds
- Corruption remains at 0.4-0.8 depending on outcome
- Guild master may resign in shame (new election required)

**Narrative Hook**: "The guild that tore itself apart over the darkness within."

**Priority**: Nice-to-have

**Related Systems**: Guild Dynamics, Alignment Conflict, Counter-Quests, Internal Battles

---

## The Last-Second Miracle

**Scope**: Player Intervention + Apocalyptic Crisis

**Preconditions**:
- DemonLord summoning 95%+ complete (minutes remaining)
- All NPC counter-quests failed (villages destroyed, heroes dead)
- Player has Divine Intervention miracle (Divine Sniper, time stop)
- World-ending scenario imminent

**Gameplay Effects**:
- Player activates miracle (time freezes)
- Manually aim at ritual leader's vital point
- Perfect shot required (headshot, heart strike)
- **Success**:
  - Ritual collapses at 99% (Near Miss tier)
  - DemonLord manifestation prevented
  - World saved, but 4+ villages destroyed, 10,000+ dead
  - Player reputation +100 ("Savior of the World")
  - Survivors form deity cult (worship player)
- **Failure**:
  - DemonLord manifests (Tier 5 Apocalypse)
  - Game-ending scenario (must rewind)
- High skill ceiling (precise aim under pressure)

**Narrative Hook**: "At the final second, a divine arrow ended the nightmare."

**Priority**: Core (major player intervention moment)

**Related Systems**: Divine Sniper, Player Miracles, Time Stop, Apocalyptic Events

---

## The Escalating Threat

**Scope**: Regional + Outcome Progression

**Preconditions**:
- Evil scheme (demon summoning, entity breach) reaches critical mass
- Intervention timing determines outcome tier
- Ritual progress 0-100% (time pressure)

**Gameplay Effects**:
- **Tier 1: Near Miss (0-40% progress)**: Easy victory, minimal casualties, corruption 0.2
- **Tier 2: Partial Success (40-70% progress)**: Costly victory, 30% casualties, corruption 0.5
- **Tier 3: Pyrrhic Victory (70-95% progress)**: Devastating battle, 60% casualties, corruption 0.8
- **Tier 4: Failure (95-99% progress)**: Partial invasion, regional devastation, corruption 1.0
- **Tier 5: Apocalypse (100% progress)**: Full invasion, 90% population death, world-ending
- Each tier increases difficulty 2x
- Time remaining shown to player (urgency indicator)
- Intervention rewards scale with tier (higher tier = higher reputation if successful)

**Narrative Hook**: "Every second counts when the world hangs in the balance."

**Priority**: Core

**Related Systems**: Counter-Quests, Urgency, Outcome Escalation, Time Pressure

---

# LOST-TECH & KNOWLEDGE DISCOVERY PATTERNS

## The Scout's Fortune

**Scope**: Individual + Economic Loop

**Preconditions**:
- Scout explores ruins (exploration range, 15% discovery chance per 500 ticks)
- Ruins contain valuable knowledge (complexity 0.7+, value 800-3000 gold)
- Scout has intelligence >60 (affects appraisal accuracy)
- Wealthy patrons seeking knowledge

**Gameplay Effects**:
- Scout discovers ruin, appraises value based on intelligence
- Lists knowledge on marketplace (asking price 50-100% of true value)
- Patron purchases listing (varies by patron type: Noble, Merchant, MageOrder)
- Scout receives payment (500-3000 gold windfall)
- Scout reputation +15 ("Knowledge Broker")
- High appraisal accuracy (INT 90+): Scout gets fair price
- Low appraisal accuracy (INT 40): Scout undervalues (sells 800 gold worth 3000)

**Narrative Hook**: "The scout who stumbled upon forgotten riches and changed their fortune."

**Priority**: Core

**Related Systems**: Ruin Discovery, Knowledge Marketplace, Scout Behavior, Economic Loops

---

## The Patron's Monopoly

**Scope**: Aggregate + Knowledge Control

**Preconditions**:
- Wealthy patron (Noble, MageOrder) purchases exclusive ruin rights
- Knowledge extracted by hired band (magic ritual, advanced tech)
- Patron implements knowledge as secret (not baseline, inner circle only)
- Rival factions aware of monopoly

**Gameplay Effects**:
- Patron hoards knowledge (not shared with aggregate baseline)
- Only trusted members granted access (loyalty >70, Evil alignment for dark knowledge)
- Patron reputation +30 ("Keeper of Secrets") or −20 ("Hoarder") based on alignment
- Rival guilds attempt espionage (10% chance per 1000 ticks)
- If espionage succeeds: Knowledge leaked, monopoly broken, patron reputation −40
- Knowledge monopoly grants power advantage (+20% effectiveness in domain)

**Narrative Hook**: "The patron who locked away ancient knowledge, guarding it jealously from the world."

**Priority**: Nice-to-have

**Related Systems**: Knowledge Monopoly, Espionage, Patron Behavior, Secret Knowledge

---

## The Long Expedition

**Scope**: Cross-Aggregate + Time Investment

**Preconditions**:
- Band hired for complex knowledge extraction (complexity 0.9, 3500 tick duration)
- Ruin requires high intelligence/wisdom (INT 90, WIS 80)
- Band has skilled members (archmage, scholar, sage)
- Extended time away from home (months)

**Gameplay Effects**:
- Band travels to ruin, begins extraction (3500 ticks = ~58 minutes real-time)
- Periodic morale checks (every 500 ticks):
  - High morale: Extraction continues
  - Low morale (<30): 20% chance band abandons quest
- Extraction complete: Band receives 1500-3000 gold payment
- Band reputation +25 ("Knowledge Seekers")
- Band members gain intelligence/wisdom XP (+10-20)
- Patron receives exclusive knowledge (magic ritual, ancient engineering)

**Narrative Hook**: "The expedition that spent months in dusty ruins, deciphering secrets lost to time."

**Priority**: Core

**Related Systems**: Extraction Quests, Band Behavior, Knowledge Extraction, Time Mechanics

---

## The Corrupt Quartermaster

**Scope**: Individual + Leadership Betrayal

**Preconditions**:
- Band with quartermaster (LootShareFairness <0.4, corrupt)
- Band earns significant loot (2000+ gold from quest)
- Quartermaster takes unfair share (30% vs. fair 1/N split)
- Band morale drops below 20

**Gameplay Effects**:
- Quartermaster distributes loot unfairly (takes 600, distributes 1400 among 7 members)
- Band morale −20 ("Corrupt Leadership")
- Mutiny risk (10% chance when morale <20)
- **If mutiny succeeds**:
  - Band kills quartermaster, reputation −30 ("Traitor Executed")
  - Elects new fair quartermaster (LootShareFairness 0.9)
  - Next loot fairly distributed, morale recovers +30
- **If mutiny fails**:
  - Quartermaster executes ringleaders (−2 band members)
  - Band morale −40, reputation −50 ("Tyrant's Band")
  - High defection risk (50% chance members leave)

**Narrative Hook**: "The quartermaster who stole from the band and paid the ultimate price."

**Priority**: Nice-to-have

**Related Systems**: Quartermaster Role, Loot Distribution, Mutiny, Morale

---

## The Cultural Diffusion

**Scope**: Aggregate + Knowledge Adoption

**Preconditions**:
- Patron implements knowledge into aggregate baseline (cultural practice, method)
- Knowledge grants bonus (+15% disease resistance, +20% farm yields)
- Members adopt slowly over time (1% per 100 ticks = 10000 ticks to 100%)

**Gameplay Effects**:
- **Tick 0**: Knowledge implemented, 0% adoption, 0% bonus
- **Tick 2500**: 25% adoption, +3.75% bonus (partial benefit)
- **Tick 5000**: 50% adoption, +7.5% bonus (half benefit)
- **Tick 10000**: 100% adoption, +15% bonus (full benefit)
- Adoption visible in aggregate culture (handwashing → hygiene reputation)
- Neighboring aggregates may observe and adopt (cultural spread)
- At 100% adoption: Practice becomes permanent cultural norm (persists in ruins if aggregate falls)

**Narrative Hook**: "The knowledge that slowly spread through the village, becoming the way of life."

**Priority**: Core

**Related Systems**: Knowledge Adoption, Cultural Practices, Aggregate Baseline, Time Progression

---

## The Shipmaster's Efficiency (Space4X)

**Scope**: Individual + Crew Management (Space4X)

**Preconditions**:
- Ship with skilled shipmaster (LogisticsSkill 90, EngineeringSkill 85)
- Captain-Shipmaster alignment: Aligned (LeadershipDynamic: Cooperation)
- Ship under operational stress (combat, long voyage)

**Gameplay Effects**:
- **Crew Efficiency**: +15% crew performance (weapons reload faster, repairs faster)
- **Maintenance Costs**: −30% repair costs (engineering skill 85)
- **Replacement Recruitment**: Recruits crew at stations 2x faster (500 ticks vs. 1000)
- **Cooperation Bonus**: +10% ship-wide performance (captain and shipmaster aligned)
- **Morale Bonus**: +20 crew morale (competent leadership)
- If shipmaster dies or leaves: Ship operates at 85% efficiency until replacement found

**Narrative Hook**: "The shipmaster who kept the crew running like clockwork, even in the darkest hours."

**Priority**: Core (Space4X)

**Related Systems**: Shipmaster Role, Crew Management, Leadership Dynamic, Space4X Logistics

---

## The Encrypted Ritual

**Scope**: Individual + Puzzle Solving

**Preconditions**:
- Band discovers encrypted knowledge (magic ritual, ancient tech)
- Requires special key/ritual to unlock (divination spell, artifact)
- Band has archmage with divination (INT 95, WIS 85, divination skill 70)
- Extraction time: 3500 ticks base + 1000 ticks decryption

**Gameplay Effects**:
- Band cannot extract knowledge without key
- **Option 1**: Archmage casts divination spell (500 mana, 1000 ticks, 70% success)
  - Success: Encryption unlocked, extraction proceeds
  - Failure: Must try again or seek artifact key
- **Option 2**: Find artifact key (separate quest, 2000 ticks side quest)
  - Artifact in nearby dungeon/ruin
  - Key immediately unlocks encryption
- If decrypted: Band gains exclusive access (patron pays 2x normal rate)
- If failed: Band abandons quest, patron seeks different band

**Narrative Hook**: "The ritual locked behind ancient wards, yielding only to the wise and persistent."

**Priority**: Nice-to-have

**Related Systems**: Encryption, Puzzle Solving, Magic Rituals, Quest Complexity

---

## The Captain-Shipmaster Rivalry (Space4X)

**Scope**: Individual + Leadership Conflict (Space4X)

**Preconditions**:
- Captain (Bold, Moral +40) and Shipmaster (Craven, Moral −20) misaligned
- LeadershipDynamic: Rivalry (cooperation penalty −15%)
- Ship operates at reduced efficiency (85%)
- Crew confused by conflicting orders

**Gameplay Effects**:
- **Efficiency Penalty**: Ship operates at 85% (−15% from rivalry)
- **Crew Morale**: −10 (conflicting leadership)
- **Resolution Options**:
  - **Fire Shipmaster**: 500 ticks downtime, find replacement, morale −10
  - **Shipmaster Resigns**: No efficiency bonuses, ship operates baseline
  - **Alignment Shift**: Shipmaster adopts captain's outlook (1000 ticks, becomes Bold)
  - **Escalation**: Shipmaster sabotages captain (assassination attempt 5% chance per 1000 ticks)
- If rivalry escalates to mutiny: Ship splits, civil conflict
- If resolved (alignment shift): Ship operates at 110% (aligned cooperation bonus)

**Narrative Hook**: "The ship torn between two visions, until one leader prevailed."

**Priority**: Nice-to-have (Space4X)

**Related Systems**: Leadership Dynamic, Rivalry, Shipmaster Role, Crew Management

---

# META PATTERNS

## The Cycle of Empires

**Scope**: Regional (multi-generational)

**Preconditions**:
- Region with 5+ major aggregates
- Generational time (5000+ ticks)
- Resource dynamics (boom-bust cycles)

**Gameplay Effects**:
- Dominant aggregate emerges (highest resources, influence)
- Dominance period (1000-2000 ticks)
- Decline triggers (overextension, internal strife, rival rise)
- New dominant aggregate rises from former subordinate
- Cycle repeats every 3000-5000 ticks
- Regional culture/alignment shifts with each dominant aggregate

**Narrative Hook**: "Empires rise and fall like tides—eternal, inevitable."

**Priority**: Wild experiment

**Related Systems**: Regional Dynamics, Resources, Multi-Generational, Influence

---

## The Hero's Journey

**Scope**: Individual (narrative arc)

**Preconditions**:
- Entity begins as low-reputation member
- Faces series of escalating challenges (3-5 major events)
- Survives and succeeds through combination of skill, initiative, and luck

**Gameplay Effects**:
- Arc progression:
  1. Ordinary member (reputation <20)
  2. Call to action (crisis event)
  3. Trial (combat, moral conflict, or challenge)
  4. Transformation (alignment/outlook shift, skill increase)
  5. Return (assumes leadership or high status, reputation >100)
- Each stage increases reputation +15-30
- Final reputation >150 ("Living Legend" status)
- Inspires others (morale +30 to aggregate)

**Narrative Hook**: "The hero's journey, from humble beginnings to legendary deeds."

**Priority**: Wild experiment

**Related Systems**: Reputation, Events, Leadership, Transformation

---

# IMPULSE & KNOCKBACK COMBAT PATTERNS

## The Heavy Impact

**Scope**: Individual (combat)

**Preconditions**:
- Attacker with high Physical (70+) and Strength (80+)
- Heavy weapon (warhammer 8kg, greataxe 6kg)
- Target with lower mass (<70kg) or low parry skill (<50)

**Gameplay Effects**:
- Hit impulse 800-1200+ (formula: (Physical * 2 + Strength * 3) * WeaponMultiplier + WeaponMass * 10)
- Knockback distance 1-3m depending on target mass and skill
- Parry stamina cost 80-120 (impulse / 10, skill reduces up to 70%)
- Target may be knocked into walls/hazards for extra damage (10% of impulse)
- Heavy weapons create space in melee (tactical repositioning)

**Narrative Hook**: "The warhammer strike sends the rogue flying through the air."

**Priority**: Core

**Related Systems**: Combat, Stamina, Physics, Skill Progression

---

## The Rogue's Gamble

**Scope**: Individual (combat)

**Preconditions**:
- Rogue class with high dodge skill (70+) but low stamina pool (150 max)
- Facing warrior with heavy weapon (1000+ impulse)
- Multiple hits incoming (warrior 3s cooldown = ~180 ticks)

**Gameplay Effects**:
- Dodge attempts: 90% success at master level, 20 stamina per dodge
- Parry attempts: 85 stamina cost for warhammer hit (high cost)
- Over 8-10 warrior swings: rogue depletes stamina from 150 → 0
- When stamina exhausted: forced to take hit (200 damage, 1.6m knockback)
- Rogue must land fast counterattacks (dual-wield daggers) between dodges
- Victory condition: warrior HP depleted before rogue stamina runs out

**Narrative Hook**: "The rogue dances between hammer blows, stamina draining with each desperate dodge."

**Priority**: Core

**Related Systems**: Combat, Stamina, Class Asymmetry, Skill Progression

---

## The Mage's Retreat

**Scope**: Individual (combat)

**Preconditions**:
- Mage class with teleport ability (50 mana cost)
- High mana pool (500+) but low physical defense
- Facing melee combatant with high impulse

**Gameplay Effects**:
- Shield absorbs damage but mage knocked back 2-3m (creates distance)
- Teleport costs 50 mana, blinks 3m away from hit
- Mage kites with ranged spells (fireball 100 damage, 400 impulse)
- Over 10+ exchanges: warrior closes gap, mage teleports, repeats
- Mage wins by attrition (mana pool lasts longer than warrior can chase)
- Focus finisher: Time Stop (0.8 focus, freeze 1s) → Arcane Blast (800 damage)

**Narrative Hook**: "The mage blinks away from each swing, hurling fire from safe distance."

**Priority**: Core

**Related Systems**: Combat, Mana, Teleport, Class Asymmetry, Focus System

---

## The Warrior's Fortitude

**Scope**: Individual (combat)

**Preconditions**:
- Warrior class with master-level skill (70+)
- High armor (0.5-0.7 reduction) and defensive stance active
- Target mass 90kg+ (heavy build)
- Facing high impulse attacks (800-1200)

**Gameplay Effects**:
- Shrug-off response: 90% knockback reduction at master level
- Damage reduction: 50% (armor + skill mitigation)
- Knockback: 1155 impulse → only 0.28m movement (vs. 1.66m for rogue)
- Parry stamina cost: 34 stamina (vs. 85 for rogue, skill reduces 70%)
- Counter-strike bonus: +50 damage after tanking hit
- Warrior outlasts opponents through superior mitigation

**Narrative Hook**: "The warrior stands unmoved, the hammer blow barely shifting their stance."

**Priority**: Core

**Related Systems**: Combat, Armor, Skill Progression, Defensive Stance

---

## The Stamina War

**Scope**: Individual (combat encounter)

**Preconditions**:
- Prolonged melee combat (8+ exchanges, 1000+ ticks)
- High impulse attacks requiring parries (800+ impulse)
- Defender with limited stamina pool (100-200)

**Gameplay Effects**:
- Stamina depletion timeline:
  - Tick 0: 150 stamina (full)
  - Tick 180: 130 stamina (1 dodge)
  - Tick 360: 110 stamina (2 dodges)
  - Tick 540: 25 stamina (4 dodges)
  - Tick 720: 0 stamina (exhausted after 5th parry)
- Once exhausted: forced to take hits (no dodge/parry option)
- Defender either flees (disengagement 15% success) or dies
- Winner determined by stamina economy, not just raw damage

**Narrative Hook**: "Exhausted, the rogue can no longer lift their arms to parry."

**Priority**: Core

**Related Systems**: Stamina, Combat, Prolonged Engagement

---

## The Focus Finisher

**Scope**: Individual (decisive moment)

**Preconditions**:
- Entity with focus bar charged (0.5-1.0 focus available)
- Master-level skill (70+) unlocking flare-up abilities
- Combat at critical moment (low HP, time pressure, or opportunity)

**Gameplay Effects**:
- Rogue: Shadow Step (0.5 focus, teleport behind enemy, backstab 3x damage)
- Warrior: Destroyer Strike (1.0 focus, 3x impulse = 3465, 5-10m knockback)
- Mage: Time Stop (0.8 focus, freeze 1s, free spell cast)
- Focus recharges slowly (0.01 per tick = 50-100 ticks to full)
- Flare-up abilities turn tide of battle (decisive tactical advantage)
- Used at climactic moments (enemy at 30% HP, player cornered, etc.)

**Narrative Hook**: "With a flash of focus energy, the warrior's final strike shatters reality."

**Priority**: Core

**Related Systems**: Focus System, Combat, Abilities, Master Skills

---

## The Master's Mitigation

**Scope**: Individual (skill progression)

**Preconditions**:
- Entity reaches master level (skill 70+) in combat skill
- Has faced high impulse attacks (800+) at lower skill levels
- Skill progression from novice (0-30) → adept (30-70) → master (70-100)

**Gameplay Effects**:
- Knockback reduction progression:
  - Novice (skill 20): 1155 impulse → 1.66m knockback
  - Adept (skill 50): 1155 impulse → 1.15m knockback (30% reduction)
  - Master (skill 90): 1155 impulse → 0.71m knockback (70% reduction)
- Stamina cost reduction:
  - Novice: 115 stamina to parry warhammer
  - Adept: 69 stamina (40% reduction)
  - Master: 34 stamina (70% reduction)
- Impulse absorption: 50% at master level (armor-independent skill)
- Enables "anime fights" where masters handle hits that flatten novices

**Narrative Hook**: "The master stands where a novice would fall—skill conquers force."

**Priority**: Core

**Related Systems**: Skill Progression, Combat, Physics, Mitigation

---

## The Knockback Chain

**Scope**: Individual (environmental interaction)

**Preconditions**:
- Combat in confined space (boarding action, narrow corridor, room with walls)
- High impulse attack (1000+) causing significant knockback (2-5m)
- Walls, hazards, or other entities within knockback distance

**Gameplay Effects**:
- Primary knockback: 1155 impulse → 1.66m movement
- If wall within range: collision causes extra damage (10% of impulse = 115 damage)
- If hazard within range: entity knocked into fire/pit/trap
- Chain knockback: entity A knocked into entity B, both take damage
- Space4X boarding: knockback into bulkheads, through doorways (instant kill if 5m+)
- Tactical positioning: warriors use walls to amplify damage (corner enemies)

**Narrative Hook**: "The hammer blow sends the marine crashing through the bulkhead door."

**Priority**: Nice-to-have

**Related Systems**: Combat, Physics, Environmental Hazards, Space4X Boarding

---

## The Anime Duel

**Scope**: Individual (epic encounter)

**Preconditions**:
- Both combatants at master level (skill 70+)
- High-stakes 1v1 combat (duel, climax, boss fight)
- Multiple exchanges (10+ attacks, 1500+ ticks)
- Focus abilities available (0.5-1.0 focus bar)

**Gameplay Effects**:
- Master rogue vs. master warrior:
  - Warrior swings 8 times over 1260 ticks (3s cooldown)
  - Rogue dodges 5 times (90% success, 100 stamina spent)
  - Rogue parries 3 times (255 stamina spent, total 355 → exhausted)
  - Rogue uses Shadow Step (0.5 focus, teleports behind, backstab)
  - Warrior uses Destroyer Strike (1.0 focus, 3465 impulse, 10m knockback)
  - Outcome: warrior wins after rogue stamina depletes
- Master mage vs. master warrior:
  - Mage teleports 6 times (300 mana spent)
  - Warrior closes gap each time (movement + charge)
  - Mage uses Time Stop (0.8 focus, freeze 1s, Arcane Blast 800 damage)
  - Outcome: mage wins by kiting + finisher
- Spectacular visual moments (Shadow Step, Destroyer Strike, Time Stop)
- Skill-based outcome (not stat-check, strategy + resource management)

**Narrative Hook**: "Two masters clash, each blow reshaping the battlefield—a dance of death."

**Priority**: Nice-to-have

**Related Systems**: Combat, Focus System, Master Skills, Epic Encounters

---

# IMPLEMENTATION NOTES

## Priority Definitions

**Core**: Essential patterns that should be implemented first. These are foundational to gameplay.

**Nice-to-have**: Enriching patterns that add depth and narrative variety. Implement after core patterns are stable.

**Wild experiment**: Ambitious or complex patterns that may require significant system extensions. Implement only if time/resources allow.

## System Coverage

Most patterns leverage these existing systems:
- ✅ Alignment (tri-axis)
- ✅ Outlooks
- ✅ Behavior traits (Vengeful, Bold)
- ✅ Cohesion
- ✅ Moral Conflict
- ✅ Grudges
- ✅ Loyalty & Belonging
- ✅ Initiative
- ✅ Voting & Consensus
- ✅ Splintering & Merging
- ✅ Reputation
- ✅ Dual Leadership (new)

Some patterns require minor extensions:
- Spy Role (suspicion tracking)
- Oaths (explicit vow tracking)
- Traumatic Events (event triggers)
- Knowledge/Research (skill progression extension)
- Regional Dynamics (multi-aggregate simulation)

## Lifting from Bible to Engine

When ready to implement a pattern:

1. **Identify pattern** from Bible
2. **Map preconditions** to component queries
3. **Define triggers** (event systems or periodic checks)
4. **Implement effects** as component mutations
5. **Test in isolation** (unit tests)
6. **Test in simulation** (integration tests)
7. **Tune parameters** (thresholds, probabilities, etc.)
8. **Add narrative hooks** (UI tooltips, event descriptions)

---

**Total Patterns Captured**: 92+

**Categories**: Dual Leadership (7), Grudges (3), Cohesion (3), Moral Conflict (3), Initiative (3), Alignment Shift (3), Loyalty (3), Reputation (3), Guilds (3), Environmental (3), Cross-Aggregate (3), Individual Journeys (3), Knowledge (3), Player Intervention (4), Modular Hull & Customization (6), Environmental Quests & Loot Vectors (15), Lost-Tech & Knowledge Discovery (8), Impulse & Knockback Combat (9), Meta (2)

**Next**: Add patterns as discovered during design/playtesting. Keep Bible living document.
