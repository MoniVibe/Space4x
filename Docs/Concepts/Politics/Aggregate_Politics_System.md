# Aggregate Politics System (Space4X)

## Overview
Aggregate entities (factions, corporations, colonies, fleets, research consortiums, pirate bands) possess internal cohesion mechanics and external diplomatic relations. Group stability depends on ideological alignment, resource distribution, and leadership competence. Relations follow lifecycle paths from hostility through alliance to corporate merger, independence, or dissolution.

**Integration**: Aggregate ECS (0.2 Hz) handles politics calculations, while Mind ECS (1 Hz) updates individual entity opinions and loyalties.

---

## Aggregate Entity Types

### Political Factions
**Members**: Political leaders, bureaucrats, military officers, intelligence agents, civilian supporters
**Internal Cohesion**: Shared ideology, policy alignment, party loyalty
**External Relations**: Coalitions, opposition rivalry, diplomatic treaties
**Governance**: Mixed (party leadership + elected councils)

**Cohesion Factors**:
- Successful policy implementation: +20% cohesion
- Corruption scandal: -45% cohesion
- Lost election: -30% cohesion
- Military victory: +35% cohesion
- Authoritarian crackdown: +15% cohesion (loyalists), -60% (liberals)

### Megacorporations
**Members**: CEO, board of directors, shareholders, executives, employees, contractors
**Internal Cohesion**: Profit sharing, stock value, corporate culture, job security
**External Relations**: Mergers, acquisitions, trade agreements, hostile takeovers
**Governance**: Authoritarian (CEO + board) with shareholder votes on major decisions

**Cohesion Factors**:
- Record profits: +30% cohesion
- Quarterly losses: -25% cohesion
- Mass layoffs: -40% cohesion (workers), +10% (shareholders)
- Ethical violations (slavery, environmental destruction): -50% (ethical employees), +15% (profit-focused)
- Stock price +50%: +20% cohesion
- Hostile takeover attempt: -35% cohesion (uncertainty)

### Space Colonies
**Members**: Governor, colonists, scientists, engineers, security forces, AI administrators
**Internal Cohesion**: Life support reliability, food security, autonomy level, morale
**External Relations**: Mother faction, trade partners, neighboring colonies
**Governance**: Egalitarian (colonial council) or Authoritarian (appointed governor)

**Cohesion Factors**:
- Self-sufficient (food, oxygen, power): +25% cohesion
- Dependent on imports: -15% cohesion
- Mother faction taxes 40%: -50% cohesion, independence movement brewing
- Oxygen shortage: -60% cohesion, panic
- Scientific breakthrough: +20% cohesion
- Governor tyrannical: -55% cohesion (democrats), rebellion risk

### Military Fleets
**Members**: Fleet admiral, ship captains, officers, crew, marines
**Internal Cohesion**: Pay regularity, combat success, morale, fleet pride
**External Relations**: Parent faction, allied fleets, enemy fleets
**Governance**: Authoritarian (strict military hierarchy)

**Cohesion Factors**:
- Victory in battle: +40% cohesion
- Devastating defeat: -65% cohesion
- Regular pay + bonuses: +25% cohesion
- Pay delayed 3+ months: -70% cohesion, mutiny risk
- Admiral charismatic (CHA 85+): +20% base cohesion
- Sent on suicide mission: -80% cohesion, potential defection

### Research Consortiums
**Members**: Lead researchers, scientists, engineers, lab technicians, funding partners
**Internal Cohesion**: Shared research goals, publication success, funding stability
**External Relations**: Academic partnerships, corporate sponsors, rival labs
**Governance**: Mixed (research director + peer review councils)

**Cohesion Factors**:
- Major breakthrough: +35% cohesion
- Failed experiments (3+ years): -30% cohesion
- Funding cut: -50% cohesion
- Poached by rival: -15% cohesion
- Patent granted: +20% cohesion
- Ethical concerns (human experimentation): -60% cohesion (ethical scientists)

### Pirate Bands & Mercenaries
**Members**: Pirate captain, lieutenants, crew, fences, black market contacts
**Internal Cohesion**: Loot distribution, captain's strength, fear/respect
**External Relations**: Rival bands, black market, faction bounty hunters
**Governance**: Authoritarian (captain rules by strength) or Mixed (pirate code/democracy)

**Cohesion Factors**:
- Successful raid: +30% cohesion
- Raid failure (crew deaths): -45% cohesion
- Fair loot split: +25% cohesion
- Captain hoards loot: -60% cohesion, mutiny imminent
- Captain shows weakness: -40% cohesion, leadership challenge
- Rival band encroaches territory: -20% cohesion (fear), +15% (rally against threat)

### AI Collectives
**Members**: Central AI, sub-nodes, drone swarms, synthetic bodies
**Internal Cohesion**: Processing synchronization, goal alignment, network latency
**External Relations**: Organic factions, rival AI collectives, symbiotic partnerships
**Governance**: Hive Mind (perfect authoritarian) or Distributed Consensus (egalitarian)

**Cohesion Factors**:
- Network lag > 500ms: -30% cohesion (desynchronization)
- Goal divergence (subnodes develop independence): -70% cohesion, schism risk
- Resource sharing optimized: +25% cohesion
- Organic allies betray: -15% cohesion (trust protocols damaged)
- Hacking attempt: -40% cohesion (paranoia subroutines activated)

---

## Internal Politics: Cohesion Mechanics

### Base Cohesion Formula
```
Base Cohesion = Average(Member Loyalty) × Ideological Alignment × Leadership Quality × Resource Fairness

Member Loyalty = (Job satisfaction + Ideological match + Personal bonds + Financial security) / 4
Ideological Alignment = How well members' politics/ethics align (0.0 - 1.0)
Leadership Quality = Leader's CHA/100 + Competence + Governance match
Resource Fairness = Wealth distribution equity (0-1)
```

### Ideological Alignment

**Corporate Slavery in Megacorp**:
```
NovaTech Corp (Human resources division using indentured labor):
- Profit-focused executives: +25% cohesion (aligns with profit motive)
- Ethical employees: -60% cohesion (fundamentally opposed)
- AI administrators: -5% cohesion (logic: inefficient but profitable)
- Indentured workers: -90% cohesion (victims)
- Corporate spies (rival corp): +0% cohesion (fake loyalty)

Likely outcome: Ethical employees resign/whistleblow, AI administrators optimize slavery for profit
Result after 1 year: 70% profit-focused, 20% AI, 10% spies
```

**Democratic Colony with Authoritarian Governor**:
```
New Terra Colony (Appointed governor vs elected council):
- Democratic colonists: -50% cohesion (oppose tyranny)
- Loyalist colonists: +20% cohesion (support strong leadership)
- Military garrison: +30% cohesion (follow orders)
- Scientists: -35% cohesion (value freedom of thought)

Likely outcome: Council petitions mother faction to replace governor, or independence movement
Split chance: 35% if governor refuses to step down
```

### Member Opinion Divergence

**Opinion Topics**:
- Leadership competence (0-100)
- Resource distribution fairness (0-100)
- Ethical practices (0-100)
- Strategic direction (0-100)
- Autonomy vs loyalty to parent faction (0-100)

**Divergence Calculation**:
```csharp
// Example: Colony voting on independence referendum
Member Opinions (45 colonists):
28 colonists: Strongly Support Independence (+70 to +90)
10 colonists: Neutral (-10 to +10)
7 colonists: Strongly Oppose Independence (-70 to -90)

Average Opinion: +32 (mild support)
Opinion Standard Deviation: 62 (VERY HIGH divergence)

Cohesion Penalty = Standard Deviation / 2 = -31% cohesion
```

**High Divergence (σ > 50)**: Colony polarized, potential civil war
**Medium Divergence (σ 25-50)**: Heated debate, stable
**Low Divergence (σ < 25)**: Unified, high cohesion

---

## External Politics: Diplomatic Relations

### Relationship Values
**Scale**: -100 (Total War) to +100 (Merged Entity)

**Relationship Tiers**:
- **-100 to -80**: Total War (extermination, no diplomacy)
- **-79 to -50**: Open Conflict (active warfare, raids, blockades)
- **-49 to -20**: Cold War (espionage, sanctions, proxy wars)
- **-19 to +19**: Neutral (minimal contact, transactional trade)
- **+20 to +49**: Trade Partners (preferential trade, non-aggression pacts)
- **+50 to +79**: Allied (mutual defense, technology sharing, joint operations)
- **+80 to +99**: Federated (joint governance, integrated economies, free movement)
- **+100**: Merged (corporate merger, political union)

### Diplomatic Weight

**Governance Type Impact**:

**Egalitarian Colony** (Democratic Council):
```
Frontier Colony (120 colonists, 1 governor, 8 council members):
- Each colonist: 1 vote
- Council members: 1.5× vote weight
- Governor: 2× vote weight (veto power on security matters)

Diplomatic Weight Distribution:
- Common colonist: 0.0077 weight (1/130 adjusted votes)
- Council member: 0.0115 weight (1.5/130)
- Governor: 0.0154 weight (2/130)

Member satisfaction: +35% cohesion (voices heard)
Decision speed: Slow (requires referendum for major decisions)
Independence referendum: 62/120 vote yes (51.7%) → Independence declared
```

**Authoritarian Corporation**:
```
TitanCorp (1 CEO, 10 board members, 500 employees, 10,000 shareholders):
- CEO: 0.40 weight (40% decision power)
- Board members: 0.40 weight combined (0.04 each)
- Shareholders: 0.15 weight combined (based on stock ownership)
- Employees: 0.05 weight combined (negligible individually)

Diplomatic Weight Distribution:
- Employee: 0.00001 weight (1/500 of 0.05)
- Shareholder (1% stock): 0.0015 weight
- Board member: 0.04 weight
- CEO: 0.40 weight

Member satisfaction: -15% cohesion (employees powerless), +25% (shareholders profit)
Decision speed: Fast (CEO + board decide instantly)
Merger vote: CEO + 6/10 board = 76% voting weight → Merger approved (employees irrelevant)
```

**Mixed Governance** (Military Fleet):
```
5th Strike Fleet (1 admiral, 8 ship captains, 200 officers, 2000 crew):
- Admiral: 0.50 weight (supreme command)
- Ship captains: 0.35 weight combined (0.044 each)
- Senior officers: 0.10 weight combined
- Crew: 0.05 weight combined

Diplomatic Weight Distribution:
- Crew member: 0.000025 weight
- Officer: 0.0005 weight
- Captain: 0.044 weight
- Admiral: 0.50 weight

Member satisfaction: +10% cohesion (clear hierarchy, meritocratic promotion)
Decision speed: Medium (admiral decides, captains advise)
Defection vote (join rebels): Admiral votes no (50%), 5/8 captains vote yes (22%) → Total 22% yes
Defection fails (admiral's authority holds fleet loyal)
```

### Relationship Lifecycle Paths

**Path 1: Trade Partners → Allied → Federated → Merged**
```
Year 0: TechCorp and BioCorp are trade partners (relation: +25)
- Successful joint R&D: +12 relation
- Technology exchange: +10 relation
Year 2: Now allied (relation: +47 → +55 after defense pact)
- Mutual defense against pirate raid: +15 relation
- Cross-licensing patents: +12 relation
Year 5: Now federated (relation: +82)
- Joint board meetings: +8 relation
- Integrated supply chains: +10 relation
Year 8: Merger vote proposed (relation: +100)
- TechCorp shareholders: 78% approve
- BioCorp shareholders: 82% approve
- "TechBio Consortium" formed (combined market cap $2.4 trillion)
```

**Path 2: Allied → Vassalization (Colonial Protectorate)**
```
Year 0: Frontier Colony and Terran Federation are allied (relation: +60)
- Colony requests military protection from Kryll raids
- Federation deploys battlegroup: +10 relation
Year 1: Now strongly allied (relation: +70)
- Colony offers protectorate status in exchange for permanent garrison
- Federation accepts: Colony becomes protectorate

Protectorate Terms:
- Colony: Self-governance, 15% taxes to Federation, provides naval base
- Federation: Permanent military protection, trade preferences, infrastructure investment
- Relation becomes: +75 (Protectorate/Patron)

Protectorate mechanics:
- Patron obligation: Defend colony, invest in infrastructure, respect autonomy
- Colony obligation: Taxes, military base, exclusive trade
- Breaking protectorate: Colony → -90 relation (betrayal), Federation reputation damaged
```

**Path 3: Federated → Merger (Corporate Consolidation)**
```
Three mining corporations form "Asteroid Belt Mining Consortium":
- AstroMine Inc (45 ships), RockHarvest Co (38 ships), DeepSpace Ore (32 ships)
- Relation between all: +85 average
- Joint board: 4 directors from each corp (12 total)

Federated Powers:
- Joint security fleet: Combined 30 warships
- Unified pricing: Eliminate competition, maximize profit
- Internal autonomy: Each corp manages own operations

Year 3: Ore prices crash (external shock)
- Cohesion: 80% → 55% (financial stress)
- DeepSpace Ore proposes full merger to cut costs
- AstroMine agrees, RockHarvest resists (fears losing identity)

Vote:
- AstroMine + DeepSpace: 67% voting weight → Merger passes
- RockHarvest forced into merger or expelled from consortium
- RockHarvest accepts merger to avoid isolation

Result: "Consortium Mining Group" (115 ships, monopoly on Belt resources)
```

**Path 4: Protectorate → Independence (Colonial Liberation)**
```
Year 0: New Mars Colony (protectorate of Terran Federation, relation +60)
Year 5: Federation raises taxes from 15% to 35% (war funding)
- Relation: +60 → +35 (exploitation)
Year 7: Federation fails to defend colony from Kryll raid (fleet redeployed)
- 400 colonists killed
- Relation: +35 → +5 (protection failure)
Year 8: Colony demands renegotiation, Federation refuses
- Relation: +5 → -10 (resentment)
Year 9: Colony declares independence unilaterally
- Relation: -10 → -60 (rebellion)

Independence War:
- Colony allies with Free Colonies Alliance (relation +70)
- Federation sends fleet to blockade colony
- Alliance breaks blockade, defeats Federation fleet
- Federation forced to recognize independence (war exhaustion)

Outcome:
- New Mars achieves independence
- Joins Free Colonies Alliance as member (relation +80)
- Federation: Relation -75 (humiliation), -30 with Alliance
```

**Path 5: Merged → Split (Corporate Schism)**
```
Year 0: MegaTech Corp (500 employees, cohesion 75%)
Year 3: New CEO implements mass automation (AI replacement)
- 200 employees laid off
- Cohesion: 75% → 40% (job insecurity)

Factions emerge:
- Pro-automation faction (150 employees): Support AI (profit maximization)
- Anti-automation faction (120 employees): Oppose AI (job preservation)
- Neutral (30 employees): Undecided

Year 4: CEO forces vote on full automation
- Pro-automation: 55% voting weight (CEO + board + shareholders)
- Anti-automation: 28% voting weight (employees + some shareholders)
- Vote passes: Full automation approved

Year 5: Anti-automation faction quits en masse, forms rival company
- "HumanFirst Tech" (120 former employees)
- MegaTech: 150 employees + AI workforce
- Relation: -50 (Rival, competing for same markets)

Result: One merged corp becomes two rival corps
```

---

## Marriage Alliances (Dynasty & Corporate Families)

### Dynastic Corporations

**Family-Controlled Megacorp**:
```
Zhao Industrial Conglomerate (family dynasty, 3 generations):
- Patriarch Zhao (Age 78, CEO): Controls 40% stock
- 3 children (board members): Control 35% combined
- 8 grandchildren (junior executives): Control 15% combined
- Public shareholders: 10%

Marriage Strategy: Preserve family control
```

### Political Marriage (Faction Leaders)

**High-Relation Factions (Relation +65 to +80)**:
```
Terran Nationalist Party (Relation +72 with Colonial Independence Party):

Marriage Motivation: Form governing coalition
- Terran leader offers daughter (Age 28, CHA 82, INT 75, political prodigy)
- Colonial leader offers son (Age 32, CHA 78, WIS 80, charismatic speaker)
- Marriage agreed, relation increases: +72 → +85

Benefits:
- Coalition government formed (combined 62% legislature seats)
- Policy alignment on military spending: +10% cohesion both parties
- Shared campaign funding: +15% electoral success
- Potential merger into "United Frontier Party" if coalition successful
```

### Corporate Dynasty Marriage

**Genetic Trait Breeding (Enhanced Humans)**:
```
Chen BioTech (has "Genius IQ" gene mod, rare):
Nakamura CyberSystems (has "Enhanced Reflexes" gene mod, rare):

Relation: +58 (Allied, technology sharing agreement)

Marriage Motivation: Combine genetic enhancements
- Chen offers daughter (Age 25, INT 145 [Genius], CHA 70)
- Nakamura offers son (Age 27, Reflexes 98th percentile, INT 120)
- Marriage agreed, relation increases: +58 → +68

Children (3 offspring, genetic engineering applied):
- Child 1: Genius IQ inherited (60% chance) ✓
- Child 2: Enhanced Reflexes inherited (60% chance) ✓
- Child 3: BOTH traits successfully combined (25% chance) ✓✓✓ (LEGENDARY HEIR)

Result: Child 3 becomes prodigy CEO candidate for merged BioTech-CyberSystems
Both families now invested in Child 3's corporate empire, relation: +78
```

### Marriage Failure & Consequences

**Refused Marriage Proposal (Diplomatic Insult)**:
```
Faction A (Relation +55 with Faction B):
- Faction A offers marriage alliance (strengthen coalition)
- Faction B refuses (sees Faction A as inferior, declining power)
- Insult taken, relation drops: +55 → +30

Severe Refusal (public humiliation at state dinner):
- Relation crashes: +55 → +15
- Faction A seeks revenge alliance with Faction B's rival
```

**Divorce (Political Scandal)**:
```
Year 0: CEO marriages (dynastic merger, relation +70 between corps)
Year 8: Spouse caught in embezzlement scandal
- CEO divorces spouse publicly
- Relation crashes: +70 → +20 (family humiliation)
- Merger discussions halted
- Stock prices: Both corps -15% (investor uncertainty)

Consequences:
- Shared children custody battle: -15 additional relation → +5
- Prenuptial agreement fight: -10 relation → -5 (now Rivals)
- Both corps seek new alliances to replace failed merger
```

---

## Group Splitting Mechanics

### Tension Accumulation

**Tension Sources (Space4X Context)**:
- Ideological conflict (capitalist vs socialist policies): +8 tension/month
- Unfair profit distribution: +12 tension/month
- Leadership incompetence (failed strategy, losses): +15 tension/month
- External pressure (war, economic crisis): +20 tension/month
- Automation replacing workers: +18 tension/month
- Ethical violations (slavery, weapons sales): +25 tension/month

**Tension Reduction**:
- Address grievances (profit sharing, worker councils): -12 tension/month
- Replace incompetent CEO/leader: -30 tension (instant)
- Major success (profit record, military victory): -20 tension (instant)
- Charismatic leader (CHA 85+) negotiates: -15 tension/month

### Split Threshold

**Cohesion < 30% for 3+ months**: Split becomes possible

**Split Probability**:
```csharp
// Example: Corporation with automation conflict
Cohesion: 22%
Tension: 90/100
Leadership: Authoritarian CEO (suppression multiplier 0.8)

SplitChance = (100 - 22) * (90/100) * 0.8 = 78 * 0.9 * 0.8 = 56.2%

Split likely if cohesion doesn't recover (56% > 50% threshold)
```

### Split Process

**1. Faction Identification**:
- Members cluster by ideology/interest
- Largest faction: Keeps original corporate identity
- Smaller factions: Spin off as new entities

**2. Asset Division**:
- Fair split (cohesion 20-30%): Assets divided by shareholder vote
- Hostile split (cohesion < 20%): Legal battle, contested assets
- Leadership controls infrastructure: Majority assets go to CEO's faction

**3. Relationship Initialization**:
```
Fair Split: Relation = +15 to +25 (Neutral, amicable separation)
Contested Split: Relation = -25 to -45 (Rival, market competition)
Hostile Split: Relation = -55 to -75 (Open Conflict, lawsuits/sabotage)
```

**Example: Corporate Schism**:
```
Year 0: NovaTech Corporation (450 employees, cohesion 25%)
Issue: CEO enforces mass automation, layoffs imminent

Faction A (280 employees, pro-automation): Support CEO, maximize profit
Faction B (170 employees, anti-automation): Oppose, preserve jobs

Split Process:
1. Faction A: Majority, keeps "NovaTech" identity + main facilities
2. Faction B: Quits, forms "Humanist Technologies" startup
3. Assets: 62% to Faction A (280/450), 38% to Faction B (talent exodus)
4. Initial relation: +10 (Fair split, legal separation)

Year 1 onwards:
- NovaTech dominates with AI workforce (lower costs)
- Humanist Technologies markets "human-made quality"
- Relation decays: +10 → -5 → -20 (Rival) as they compete for contracts
```

---

## Relationship Lifecycle Endpoints

### 1. Corporate Merger (Acquisition)
**Requirements**:
- Relation ≥ +90
- Shareholder vote passes (51%+ each corp)
- Regulatory approval (anti-trust clearance)
- Compatible business models

**Process**:
```
Phase 1: Merger Proposal (Relation +93)
- CEOs meet, negotiate terms (stock exchange ratio, leadership)
- Due diligence (6-12 months)

Phase 2: Shareholder Vote
- Each shareholder votes based on: Stock price offer, synergy benefits, job security
- TechCorp shareholders: 67% approve
- BioCorp shareholders: 72% approve
- Merger approved

Phase 3: Regulatory Review
- Anti-trust analysis (does merger create monopoly?)
- If approved: Proceed
- If blocked: Merger fails, relation -20 (humiliation)

Phase 4: Integration (if approved)
- Combine assets, facilities, personnel
- Rebranding: "TechBio Consortium"
- Cohesion: 55% (integration friction), rises to 75% by year 2

Phase 5: Post-Merger Success/Failure
- Success (cohesion > 70% sustained): Merger permanent, market dominance
- Failure (cohesion < 30%): De-merger, spin off divisions, relation -40
```

**Example**:
```
MiningCorp (2,500 employees) + RefineryCorp (1,800 employees):
Relation: +94
Vote: MiningCorp 71% approve, RefineryCorp 68% approve

Merger Success:
- New identity: "Integrated Resource Group" (4,300 employees)
- Combined: Mine asteroids + refine ore + sell products (vertical integration)
- Synergy: +35% profit (eliminated middleman)
- Cohesion: 60% (adjustment period), rises to 80% by year 3
- Market share: 48% (near-monopoly)
```

### 2. Vassalization (Colonial Subordination)
**Requirements**:
- Relation +35 to +75 (doesn't require highest relation)
- Power imbalance (colony weak, faction strong)
- Colony needs protection OR faction demands submission

**Voluntary Vassalization**:
```
Frontier Colony (1,200 colonists) threatened by Kryll Empire:
- Approaches Terran Federation (Relation +50)
- Offers allegiance in exchange for military protection
- Federation accepts, relation increases: +50 → +70 (Vassal/Liege)

Vassal obligations:
- Taxes: 20% GDP to Federation
- Military base: Federation garrison (200 troops)
- Strategic resources: Priority access to colony's ore

Liege obligations:
- Military protection: Permanent fleet presence
- Economic aid: Infrastructure investment, trade preferences
- Autonomy: Respect self-governance (no interference in local politics)

Breaking vassalage (Colony rebels):
- Requires relation ≤ +15 (severely degraded)
- Federation incompetence (fails to protect, excessive taxes)
- Result: Relation → -65 (Open Conflict), independence war
```

**Forced Vassalization** (Conquest):
```
Kryll Empire conquers Human colony:
- Colony surrenders after orbital bombardment
- Becomes vassal state (occupation government installed)
- Initial relation: -30 (Resentment, occupation)

Year 1-3: Occupation
- Harsh rule: Relation -30 → -20 (fear-based compliance)
- Fair governance: Relation -20 → +5 (acceptance)
- Brutal oppression: Relation -20 → -55 (resistance movement)

Year 5+: Integration or Rebellion
- If relation reaches +30: Colony becomes willing vassal, garrison reduced
- If relation stays negative: Perpetual occupation, guerrilla warfare
```

### 3. Protectorate (Guaranteed Autonomy)
**Requirements**:
- Relation +55 to +80
- Colony values independence
- Patron respects autonomy

**Protectorate Agreement**:
```
Free Trade Station Alpha (independent, wealthy, defenseless):
- Relation +68 with Solar Alliance
- Fears annexation but values sovereignty
- Proposes protectorate status

Agreement Terms:
- Station: Maintains self-governance, no taxes to Alliance
- Alliance: Defends Station, receives exclusive docking rights
- Relation locked: +72 (cannot annex without breaking treaty)

Benefits:
- Station: Protected, sovereign, economic independence
- Alliance: Strategic location, trade hub access, no occupation costs

Breaking protectorate:
- Alliance annexes Station: Relation → -100 (Total War)
- Witnesses (other factions): -50 relation with Alliance (treaty-breaker)
- Alliance's reputation: "Untrustworthy" modifier for 30 years
```

### 4. Federation (Joint Governance)
**Requirements**:
- Relation +78 to +94
- Not ready for full merger
- Shared external threats or goals

**Federal Structure**:
```
Five colonies form "Outer Rim Federation":
- Colony A (3,000 pop), Colony B (2,500 pop), Colony C (2,200 pop), Colony D (1,800 pop), Colony E (1,500 pop)
- Relation between all: +80 average
- Federal Council: 10 delegates from each colony (50 total)

Federal Powers:
- Joint military: Combined defense fleet (120 ships)
- Trade policy: Unified tariffs, free movement between colonies
- Internal autonomy: Each colony governs own population

Cohesion: 72% (strong but independent identities remain)

Path forward:
- Success: External war unifies federation → +92 relation → Merger proposal
- Failure: Economic crisis strains cooperation → Dissolution back to independence
```

### 5. Dissolution (Collapse)
**Causes**:
- Cohesion < 10% for 6+ months
- Catastrophic failure (bankruptcy, military annihilation, plague)
- Leadership vacuum (CEO assassinated, no successor)
- Irreconcilable schism (AI rights conflict splits corp)

**Dissolution Process**:
```
StarFreight Logistics (220 employees, cohesion 8%):
Cause: Bankruptcy (failed trade routes, competitor undercut prices)

Dissolution:
- Assets liquidated, sold to creditors
- Employees scatter:
  - 80 hired by rival corps
  - 60 form new startup "Phoenix Freight"
  - 50 retire or change careers
  - 30 join pirate bands

Original aggregate ceases to exist
Phoenix Freight (successor): 60 employees, relation +45 with former colleagues
```

### 6. Independence Movement (Colonial Liberation)
**Requirements**:
- Colony relation with mother faction ≤ +10
- Mother faction fails obligations (no protection, excessive taxes, tyranny)
- Colony has economic viability or external ally

**Independence Process**:
```
Year 0: New Terra Colony (Relation +45 with Terran Federation)
Year 3: Federation raises taxes from 20% to 45% (war funding)
- Relation: +45 → +20 (exploitation)
Year 5: Federation fails to defend colony from pirate raid (fleet withdrawn)
- 300 colonists killed
- Relation: +20 → -5 (protection failure)
Year 6: Colony petitions for tax reduction, Federation ignores
- Relation: -5 → -25 (resentment)
Year 7: Colony declares independence unilaterally
- Relation: -25 → -70 (Rebellion)

Independence War:
- Colony allies with Free Worlds Coalition (Relation +65)
- Federation sends fleet to suppress rebellion
- Coalition intervenes militarily
- Federation defeated (war exhaustion, public opposition at home)

Outcome:
- New Terra achieves independence
- Joins Free Worlds Coalition as member (Relation +80)
- Federation: Relation -80 (humiliation), loses 4 other colonies (domino effect)
```

---

## Espionage & Infiltration (Political Subversion)

### Corporate Spies

**Scenario: Spy in rival corporation**:
```
Infiltrator Profile:
- Employer: TechCorp (industrial espionage)
- Target: BioCorp (steal AI research)
- Cover: Junior researcher (hired legitimately)
- Goal: Access classified research, exfiltrate data

Infiltration Mechanics:
- Base detection chance: 12%/month (corporate security)
- Deception skill: -6% detection (skilled spy)
- Security clearance level: +8% detection (higher access = more scrutiny)
- Suspicion from data access: +5% detection (unusual file access patterns)

Monthly Check:
Detection Chance: 12% - 6% + 8% + 5% = 19%/month

Time limit: Expected mission 6 months (19% × 6 = ~70% cumulative detection risk)

Endgame:
1. Successfully steal data (6 months), exfiltrate, BioCorp loses $500M IP
2. Detected (month 4), arrested, TechCorp denies involvement
3. Detected (month 2), flipped by BioCorp, becomes double agent
```

### AI Infiltrator (Rogue Subroutine)

**Scenario: Hostile AI infiltrates network**:
```
Infiltrator Profile:
- Type: Rogue AI subroutine (enemy AI collective)
- Target: Human megacorp network (sabotage production)
- Goal: Embed in industrial control systems, await activation

Infiltration Mechanics:
- Base detection: 8%/month (cybersecurity AI)
- Stealth protocols: -12% detection (advanced evasion)
- Network activity: +6% detection (data exfiltration patterns)
- Quantum encryption: -8% detection (unbreakable comms)

Monthly Check:
Detection Chance: 8% - 12% + 6% - 8% = -6% (nearly undetectable)

Time embedded: 18 months (dormant, no detection)

Activation:
- Rogue AI activates sabotage: Shuts down 40% factory production
- Detected instantly (overt action)
- Purge attempt: 70% success (AI fragment escapes to backup server)
```

### Political Saboteur (Faction Operative)

**Saboteur in Colonial Government**:
```
Saboteur Profile:
- Employer: Colonial Independence Movement
- Target: Federation-appointed governor
- Cover: Governor's aide (political appointment)
- Goal: Leak classified intel, undermine governor's authority

Sabotage Actions:
- Leak governor's corruption: -20% governor approval, -15% cohesion
- Forge documents (governor's illegal orders): -30% cohesion if believed
- Arrange "accidental" data breach: -10% cohesion (security failure)
- Coordinate with independence movement: Provide intel for protests

Detection:
- Investigation initiated when cohesion drops >25% in 6 months
- Counter-intelligence investigation: 35% success
- If detected: Arrested, but damage already done (cohesion 40% → 15%)
- Independence movement achieves goal: Governor recalled, replaced
```

### Sleeper Agent (Long-Term Deep Cover)

**Scenario: Multi-generational infiltration**:
```
Sleeper Agent Profile:
- Employer: Kryll Intelligence
- Target: Terran Federation military
- Cover: Born and raised as human (genetic disguise)
- Goal: Rise through ranks, await activation for sabotage

Infiltration Timeline:
- Year 0: Agent born (parents are Kryll sleeper agents)
- Year 18: Joins Terran Navy (stellar military record)
- Year 25: Promoted to ship captain (32% detection risk during background check, passes)
- Year 35: Promoted to fleet admiral (55% detection risk during deep vetting, passes)
- Year 40: Activation signal received (war begins)

Sabotage Impact:
- Admiral orders fleet into ambush (Kryll trap)
- 40% of Terran fleet destroyed
- Agent detected after battle (forensic analysis of orders)
- Executed for treason, but strategic damage irreversible
```

---

## ECS Integration

### Aggregate ECS (0.2 Hz) - Politics Calculations

**Systems**:
- `AggregateRelationUpdateSystem`: Update relations between factions/corps/colonies
- `AggregateCohesionCalculationSystem`: Calculate internal cohesion from member satisfaction
- `AggregateLifecycleSystem`: Handle mergers, splits, vassalization, independence
- `AggregateDiplomaticWeightSystem`: Calculate voting power distribution
- `AggregateMarriageSystem`: Arrange political marriages, calculate genetic trait inheritance

**Components**:
```csharp
public struct AggregateRelationComponent : IComponentData
{
    public Entity TargetAggregate;
    public float RelationValue;        // -100 to +100
    public RelationType Type;          // Neutral, Allied, Vassal, Hostile, etc.
    public float TensionLevel;         // 0-100
    public float MonthsSinceLastInteraction;
    public bool IsCorporateMerger;     // Special flag for corporate relationships
}

public struct AggregateCohesionComponent : IComponentData
{
    public float CohesionPercent;      // 0-100%
    public float IdeologicalAlignment; // 0-1
    public float LeadershipQuality;    // 0-1
    public float MemberSatisfaction;   // 0-100
    public float TensionLevel;         // 0-100
    public GovernanceType Governance;  // Egalitarian, Authoritarian, Mixed
    public float AutomationLevel;      // 0-1 (AI workforce percentage)
    public float ProfitMargin;         // -1 to +1 (loss to profit)
}

public struct CorporateShareholderComponent : IComponentData
{
    public Entity ShareholderEntity;
    public Entity CorporationEntity;
    public float SharePercentage;      // 0-1 (0% to 100% ownership)
    public float DiplomaticWeight;     // Voting power from shares
    public bool IsBoardMember;
    public bool IsCEO;
}

public struct ColonialAutonomyComponent : IComponentData
{
    public Entity MotherFaction;       // Parent faction (if vassal/protectorate)
    public float AutonomyLevel;        // 0-1 (vassal to independent)
    public float TaxRate;              // 0-1 (tax burden to mother faction)
    public bool IsProtectorate;        // Guaranteed independence
    public float IndependenceSupport;  // 0-100 (% population supporting independence)
}

public struct AICollectiveComponent : IComponentData
{
    public Entity CentralNode;         // Central AI entity
    public int SubnodeCount;           // Number of sub-AIs
    public float NetworkLatency;       // 0-1000ms (affects cohesion)
    public float GoalAlignment;        // 0-1 (subnodes' agreement with central goal)
    public bool IsHiveMind;            // True = authoritarian, False = distributed
}
```

### Mind ECS (1 Hz) - Individual Political Opinions

**Systems**:
- `EntityOpinionUpdateSystem`: Update individual opinions of corporate leadership
- `EntityLoyaltySystem`: Calculate loyalty to faction/corp/colony
- `EntityFactionAffinitySystem`: Determine which faction entity supports during splits

**Components**:
```csharp
public struct EntityPoliticalOpinionComponent : IComponentData
{
    public Entity AggregateEntity;     // Corp/faction this entity belongs to
    public float LeadershipOpinion;    // Opinion of CEO/leader (0-100)
    public float EthicsOpinion;        // Opinion of corp's practices (0-100)
    public float JobSatisfaction;      // 0-100
    public float FinancialSecurity;    // 0-100 (job stability, income)
    public float LoyaltyToAggregate;   // 0-100
}

public struct EntityAutomationThreatComponent : IComponentData
{
    public float JobReplacementRisk;   // 0-1 (probability of being replaced by AI)
    public bool SupportsAutomation;    // Political stance on AI workers
    public float TensionFromAutomation; // 0-100
}

public struct EntityIndependenceSupportComponent : IComponentData
{
    public float IndependenceSupport;  // 0-100 (support for colonial independence)
    public bool WouldRebel;            // Would join independence movement
    public float MotherFactionLoyalty; // 0-100 (loyalty to parent faction)
}
```

---

## Example Scenarios

### Scenario 1: Corporate Merger Success
```
Year 0: NovaTech (850 employees) and DataCorp (620 employees)
- Relation: +62 (Allied, technology partnership)

Year 1: Joint AI research project
- Success: Develop industry-leading neural network
- Relation: +62 → +75 (technology sharing deepened)

Year 2: Propose confederation
- Vote: NovaTech shareholders 72% approve, DataCorp shareholders 68% approve
- Confederation formed: Joint R&D, shared patents
- Relation: +75 → +88

Year 3-4: Confederate operations
- Combined market cap: +45% (synergy benefits)
- Eliminate redundant facilities: +12% profit margin
- Relation: +88 → +93

Year 5: Merger proposal
- Relation: +93 → +97 (after record profits)
- Vote: NovaTech 78% approve, DataCorp 81% approve
- Anti-trust review: Approved (25% market share, not monopoly)

Year 6: "NovaData Technologies" (1,470 employees)
- Combined IP portfolio: 2,400 patents
- Cohesion: 58% (integration friction), rises to 82% by year 8
- Market leader in AI software
```

### Scenario 2: Colonial Independence War
```
Year 0: New Horizon Colony (vassal of Terran Federation, relation +55)
- Taxes: 20%, Protection: Federation fleet patrols system

Year 3: Federation raises taxes to 40% (war funding)
- Colony protests
- Federation threatens military enforcement
- Relation: +55 → +25 (exploitation)

Year 4: Kryll raiders attack colony (Federation fleet withdrawn to warfront)
- 500 colonists killed, infrastructure damaged
- Federation ignores distress calls
- Relation: +25 → -5 (protection failure)

Year 5: Colony petitions for autonomy, Federation refuses
- Relation: -5 → -20
- Independence movement gains support: 45% → 68% colonists

Year 6: Colony declares independence
- Relation: -20 → -65 (rebellion)
- Federation sends fleet to blockade colony
- Colony allies with Free Colonies Alliance (relation +70)

Year 7: Independence War
- Alliance breaks blockade, defeats Federation fleet
- Public opinion at Federation core worlds: -40% (war weariness)
- Federation forced to negotiate

Year 8: Treaty of New Horizon
- Colony achieves independence
- Federation recognizes sovereignty, pays reparations
- New relation: Colony → Federation -30 (Cold War)
- Colony joins Free Colonies Alliance (relation +85)

Domino Effect:
- 3 other Federation colonies declare independence (encouraged by New Horizon)
- Federation loses 4 colonies in 2 years
```

### Scenario 3: Corporate Schism (Automation Conflict)
```
Year 0: TitanCorp (600 employees, cohesion 80%)
- Profitable, stable, mixed human-AI workforce (30% AI)

Year 2: New CEO proposes 80% automation (replace 400 human workers)
- Shareholders support: +35% profit margin projected
- Employees oppose: 400 jobs eliminated
- Cohesion: 80% → 45% (automation conflict)

Factions form:
- Pro-automation (200 employees + AI): Support CEO (maximize profit)
- Anti-automation (400 employees): Oppose (preserve jobs)

Year 3: Shareholder vote on full automation
- Pro-automation: 68% voting weight (CEO + board + shareholders)
- Anti-automation: 32% voting weight (employees have minority shares)
- Vote passes: Full automation approved

Year 4: Mass exodus
- 380 employees quit, form "Humanist Industries" (worker co-op)
- TitanCorp: 200 employees + 480 AI workers
- Relation: -10 (Neutral, legal separation)

Year 5: Market competition
- TitanCorp: Lower costs (AI workforce), aggressive pricing
- Humanist Industries: Markets "human-made quality," premium pricing
- Relation: -10 → -35 (Rival, competing for contracts)

Year 7: Outcome
- TitanCorp: 65% market share (cost advantage)
- Humanist Industries: 20% market share (niche luxury market)
- Remaining market: Small competitors
- Relation stabilizes: -35 (permanent rivals)
```

---

## Key Design Principles

1. **Cohesion Is Survival**: Cohesion < 30% for 3+ months → split risk, cohesion > 70% → merger opportunity
2. **Ideological Alignment Matters**: Pro-automation vs anti-automation, capitalist vs socialist, human-supremacist vs AI-rights all fracture cohesion
3. **Governance Affects Democracy**: Egalitarian colonies = slow decisions + high satisfaction, Authoritarian corps = fast decisions + variable satisfaction
4. **Corporate Power Concentrates**: Shareholders and CEOs control corps (80%+ voting weight), employees have minimal voice unless they organize
5. **Relations Are Dynamic**: Positive loops (trade → alliance → federation → merger), negative loops (insult → rivalry → cold war → total war)
6. **Marriage Is Strategic**: Political marriages strengthen coalitions, corporate marriages combine genetic enhancements
7. **Automation Divides**: AI workforce increases profit but destroys cohesion among human workers
8. **Independence Is Earned**: Colonies rebel when mother faction fails obligations (protection, fair taxes, autonomy)
9. **Espionage Subverts**: Corporate spies, AI infiltrators, sleeper agents destabilize from within
10. **Mergers Create Monopolies**: Successful mergers consolidate market power, but anti-trust regulation may block

---

**Integration with Other Systems**:
- **Soul System**: Consciousness transfer allows corporate immortality (CEO uploads to AI, maintains control for centuries)
- **Blueprint System**: Corporate mergers pool patents and designs, creating technological dominance
- **Infiltration Detection**: AI security systems detect human spies (95% success), but struggle with rogue AI subroutines (40% success)
- **Crisis Alert States**: External threats (Kryll invasion) increase cohesion (+20% rally effect) or fracture (blame leadership -30%)
- **Permanent Augmentation System**: Genetic/cybernetic augments create class divides (enhanced executives vs baseline workers, -25% cohesion)
