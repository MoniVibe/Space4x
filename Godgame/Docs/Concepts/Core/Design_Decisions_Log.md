# Design Decisions Log

**Purpose:** Track major design decisions as they're made to maintain vision consistency.

**Status:** Living Document  
**Created:** 2025-11-02  
**Last Updated:** 2025-11-02

---

## Decision Entry Format

```
Date: YYYY-MM-DD
Decision: [Short title]
Context: [What question was this answering?]
Answer: [The decision made]
Implications: [What this affects]
Follow-up Questions: [New questions this raises]
PureDOTS Notes: [Technical implementation considerations]
```

---

## Decisions

### 2025-11-02: Miracle Activation Method

**Context:** How do players cast miracles? (From Miracle_System_Vision.md Open Question #2)

**Answer:** Black & White 2 style button/menu dispensation
- Click miracle button → configure parameters → click target
- Side-to-side mouse shake to cancel active selection
- No complex gesture recognition (beyond cancel shake)
- "Maybe added dispensation methods" - open for future expansion

**Implications:**
- UI needs miracle button panel
- Parameter selection UI required (intensity slider, mode toggles)
- Input system needs shake detection
- Simpler than gesture recognition, faster to implement

**Follow-up Questions:**
- ✅ **ANSWERED 2025-11-02:** Radial menus as added dispensation method
- ✅ **ANSWERED 2025-11-02:** Miracle UI placement:
  - Favorite miracles: Atop worship sites (temples, shrines)
  - All miracles: Bottom bar
- <DESIGN QUESTION: Does shake cancel work during hand pickup/carry states too?>
- <DESIGN QUESTION: How many "favorite" slots per worship site? 3? 5? Based on building tier?>
- <DESIGN QUESTION: Can player customize which miracles are favorites?>

**PureDOTS Notes:**
```
Technical Implementation:
- HandInputRouterSystem already handles RMB priorities
- Add MiracleInputHandler similar to existing handlers:
  - PileSiphonRmbHandler.cs
  - StorehouseDumpRmbHandler.cs
  - ObjectGrabRmbHandler.cs

- Shake detection:
  - Track mouse delta per frame
  - Count direction changes within 0.5s window
  - Trigger: ≥3 direction reversals
  - Clear active miracle selection state

- UI System:
  - Miracle panel rendering (PresentationSystemGroup)
  - Parameter UI (intensity slider, mode buttons)
  - Target preview (show AoE circle at cursor)
  
- Input flow:
  1. Player clicks miracle button (UI)
  2. MiracleSelectionState component created (singleton)
  3. HandInputRouter checks MiracleSelectionState priority
  4. Click target → spawn miracle effect entity
  5. Clear MiracleSelectionState

- Rewind considerations:
  - UI state doesn't need snapshot (presentation only)
  - Miracle effect entities need TimeAware components
  - Mana cost deducted in deterministic system
```

**Related Concepts:**
- `Docs/Concepts/Miracles/Miracle_System_Vision.md`
- `Docs/Concepts/Interaction/RMB_Priority.md`

---

### 2025-11-02: Village Founding Threshold

**Context:** What triggers villagers to split and found new settlements? (From Sandbox_Autonomous_Villages.md Open Question #1)

**Answer:** Villagers split when resources in their area are "reasonably exploited"

**Implications:**
- Villages don't expand infinitely in place
- Resource depletion drives settlement spread
- Map will naturally fill with settlements over time

**Follow-up Questions:**
- ✅ **ANSWERED 2025-11-02:** 75% depletion = "reasonably exploited"
- ✅ **ANSWERED 2025-11-02:** Check node count in spatial grid cells
- <DESIGN QUESTION: Node count threshold - how many cells around village? 1 cell radius? 3 cells?>
- <DESIGN QUESTION: Does 75% mean "75% of nodes are depleted" or "nodes are at 75% capacity"?>
- <DESIGN QUESTION: Can players prevent splitting (e.g., miracle to boost local resources)?>

**PureDOTS Notes:**
```
Technical Implementation:
- Spatial grid query: "Find resource nodes within radius"
  - Use SpatialGridState + RegistryHelper
  - Query ResourceRegistry for node entities
  
- Depletion calculation options:
  Option A: Count depleted nodes vs total
    ResourceNodes.Where(n => n.RemainingAmount < threshold).Count() / TotalNodes
  
  Option B: Average remaining %
    ResourceNodes.Average(n => n.RemainingAmount / n.MaxAmount)
  
  Option C: Harvest rate tracking
    Compare recent harvest rates (from history buffers)
    If declining below threshold → exploited

- Village splitting logic:
  VillageExpansionSystem (new):
  - Run in GameplaySystemGroup after ResourceSystemGroup
  - Query villages with OverpopulationTag + DepletedResourcesTag
  - Find suitable founding location (SpatialQuery for resources)
  - Spawn VillageFoundingEvent
  - Select subset of villagers to migrate
  
- Components needed:
  VillageResourceMetrics : IComponentData {
    float AverageNodeDepletion;    // 0-1
    float HarvestRateDecline;      // % change
    int NodesInRadius;
    int DepletedNodes;
  }
  
  VillageFoundingEvent : IComponentData {
    Entity SourceVillage;
    float3 TargetLocation;
    int MigratingVillagerCount;
  }

- Performance:
  - Run every 5-10 seconds (not per frame)
  - Cache resource queries in village registry
  - Leverage existing SpatialRegistryMetadata
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md`
- PureDOTS: `ResourceRegistrySystem.cs`, `SpatialSystemGroup`

---

### 2025-11-02: Resource Crisis Threshold

**Context:** What resource level triggers desperate village behavior? (From Sandbox_Autonomous_Villages.md Open Question #6)

**Answer:** 10% stockpile triggers desperate state

**Implications:**
- Clear threshold for AI behavior change
- Villages become aggressive/diplomatic when low
- Player can see crisis coming (stockpile meters)

**Follow-up Questions:**
- ✅ **ANSWERED 2025-11-02:** 10% food specifically (per-resource, not aggregate)
- ✅ **ANSWERED 2025-11-02:** Desperate actions:
  - Send distress prayers
  - Accept unfavorable trades
  - Raiding
- <DESIGN QUESTION: Does desperate state affect morale/initiative?>
- <DESIGN QUESTION: Can villages recover from desperate without player help?>
- <DESIGN QUESTION: Do other resources (wood, ore) have different thresholds or just food?>
- <DESIGN QUESTION: Priority order of desperate actions? Prayer first, then trade, then raid?>

**PureDOTS Notes:**
```
Technical Implementation:
- Storehouse system already tracks inventory
- Add VillageResourceState component:
  
  VillageResourceState : IComponentData {
    float FoodPercent;         // 0-1
    float WoodPercent;         // 0-1
    float OrePercent;          // 0-1
    float AggregatePercent;    // Average or weighted
    byte State;                // Abundant, Stable, Low, Desperate
  }

- DesperateThreshold constant: 0.1f (10%)
- State transitions:
  > 0.75 = Abundant
  0.5 - 0.75 = Stable
  0.25 - 0.5 = Low
  < 0.1 = Desperate

- Systems affected:
  VillageAISystem - prioritize resource gathering
  DiplomacySystem - send trade requests
  BandFormationSystem - authorize raiding
  PrayerSystem - escalate urgency

- Telemetry:
  godgame.village.desperate.count
  godgame.village.resource.aggregate.avg
  
- UI feedback:
  Storehouse color coding (green/yellow/red)
  Village crisis icon
  Prayer urgency indicator
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md`
- `Docs/TruthSources_Inventory.md#storehouses`
- PureDOTS: `StorehouseSystems.cs`, `StorehouseRegistrySystem.cs`

---

### 2025-11-02: Initiative Roll System

**Context:** How do events affect entity/village initiative? (From Sandbox_Autonomous_Villages.md Open Question #3)

**Answer:** Events trigger rolls that can increase or decrease initiative, with rationale on both sides

**Implications:**
- Initiative is dynamic, not static
- Same event can have different outcomes per entity
- Personality/alignment affects roll results
- Creates emergent behavior variation

**Follow-up Questions:**
- ✅ **ANSWERED 2025-11-02:** Roll formula is d20
- ✅ **ANSWERED 2025-11-02:** Initiative persists but changes (no decay over time)
- ✅ **ANSWERED 2025-11-02:** Example rationale: "Materialist needs initiative to clear out and exploit a mine"
- <DESIGN QUESTION: d20 roll mechanics:
  - Roll vs DC (difficulty class)?
  - Roll + modifiers vs threshold?
  - What determines the DC per event?>
- <DESIGN QUESTION: More rationale examples needed for other outlooks/events>
- <DESIGN QUESTION: Can initiative go negative? What happens?>
- <DESIGN QUESTION: Initiative range: 0-20 (matching d20) or normalized 0-1?>

**PureDOTS Notes:**
```
Technical Implementation:
- Event-driven system (not per-frame)
- Use Unity.Mathematics.Random for determinism

Initiative roll pattern:
  OnEvent(Entity entity, EventType event) {
    // Get entity personality/alignment
    var alignment = GetComponent<EntityAlignment>(entity);
    var personality = GetComponent<PersonalityTraits>(entity);
    
    // Roll with bias
    var random = GetSeededRandom(entity, currentTick);
    var roll = random.NextFloat(0, 1);
    
    // Calculate modifiers
    float positiveModifier = CalculatePositiveRationale(event, alignment);
    float negativeModifier = CalculateNegativeRationale(event, alignment);
    
    // Net change
    float netChange = (roll * positiveModifier) - ((1-roll) * negativeModifier);
    
    // Apply to initiative
    initiative.Value = math.clamp(initiative.Value + netChange, minInitiative, maxInitiative);
  }

Components needed:
  InitiativeState : IComponentData {
    float Value;           // 0-1 normalized
    float TrendVelocity;   // Rate of change
    uint LastEventTick;
  }
  
  InitiativeModifiers : IComponentData {
    float LawfulDamping;   // Reduces volatility
    float ChaoticAmplify;  // Increases swings
    float StoicResistance; // Flattens negative spikes
  }

Event rationale tables (ScriptableObject):
  EventRationaleData {
    EventType type;
    AlignmentAxis[] positiveAxes;   // Which alignments favor +initiative
    AlignmentAxis[] negativeAxes;   // Which favor -initiative
    float basePositiveMod;
    float baseNegativeMod;
  }

Example:
  Event: Villager Death
  Positive rationale: Warlike, Vengeful = vengeance motivation
  Negative rationale: Peaceful, Stoic = grief/depression
  
  Event: Victory
  Positive: Warlike, Expansionist = momentum
  Negative: Peaceful, Isolationist = fatigue/desire to consolidate

Systems:
  InitiativeEventSystem (new) in GameplaySystemGroup
  - Consumes event buffers (DeathEvent, VictoryEvent, MiracleEvent)
  - Runs roll logic
  - Updates InitiativeState
  - Rewind-safe (reads from history on playback)
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md`
- `Docs/Concepts/Progression/Alignment_System.md`
- `Docs/Concepts/Meta/Generalized_Alignment_Framework.md`

---

### 2025-11-02: Tech Tier Advancement

**Context:** How do villages progress through tech tiers? (From Sandbox_Autonomous_Villages.md Open Question #5)

**Answer:** Advancement requires unlocking a set number of research milestones per level

**Implications:**
- Tech progression is milestone-based, not time-based
- Villages can stall at tier without research
- Research buildings/staffing becomes critical
- Clear progression feedback (X/Y milestones)

**Follow-up Questions:**
- ✅ **ANSWERED 2025-11-02:** Different milestone numbers per tier (scaling)
- ✅ **ANSWERED 2025-11-02:** Both domain-specific AND generic points
- ✅ **ANSWERED 2025-11-02:** Villages CAN regress tiers
- <DESIGN QUESTION: Specific milestone counts per tier?
  - Tier 1 = 3 milestones? Tier 10 = 8? Tier 20 = 15?
  - Linear scaling (tier × 0.5) or exponential?>
- <DESIGN QUESTION: Ratio of domain vs generic points?
  - 50/50? 70% domain, 30% generic?
  - Do generic points count toward any domain or unlock special techs?>
- <DESIGN QUESTION: Regression triggers:
  - Lose researchers below threshold?
  - Facility destruction?
  - Time-based decay without research activity?>
- <DESIGN QUESTION: Can villages skip tiers or must progress sequentially?>

**PureDOTS Notes:**
```
Technical Implementation:
- Research as accumulation system
- Milestone = threshold met in research domain

Components:
  VillageTechState : IComponentData {
    byte CurrentTier;           // 1-20
    ushort TotalMilestones;     // Lifetime count
    ushort MilestonesThisTier;  // Progress to next tier
    ushort MilestonesRequired;  // Target for advancement
  }
  
  ResearchProgress : IComponentData {
    float MilitaryPoints;
    float CivicPoints;
    float ArcanePoints;
    // etc. per domain
  }
  
  ResearchMilestone : IBufferElementData {
    FixedString32Bytes MilestoneId;
    byte Domain;      // Military, Civic, Arcane
    uint UnlockedTick;
  }

Advancement logic:
  TechAdvancementSystem (new) in GameplaySystemGroup:
  - Query villages with research facilities
  - Accumulate points from staffed researchers
  - Check milestone thresholds
  - Unlock milestone when domain points >= threshold
  - Count milestones this tier
  - If count >= required → advance tier, reset counter

Milestone threshold formula options:
  Option A: Fixed per domain
    Military: 100 points per milestone
    Civic: 150 points per milestone
    
  Option B: Scaling with tier
    Points needed = baseCost * tierMultiplier
    Tier 5 military: 100 * 1.5 = 150
    Tier 10 military: 100 * 2.0 = 200

Research rate:
  Points per tick = Σ(Researcher.Education * Researcher.Wisdom) / facilityCount
  
Buildings:
  School: +1 point/tick per researcher
  University: +3 points/tick per researcher
  Academy: +10 points/tick per researcher

Telemetry:
  godgame.village.tech.tier.avg
  godgame.village.research.rate.avg
  godgame.village.milestones.total

Visual feedback:
  Tech tier affects building visuals (tier unlock gates)
  Research progress bar in village UI
  Milestone unlock celebration VFX
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md#technology-progression`
- PureDOTS: Need new `TechAdvancementSystem.cs`

---

### 2025-11-02: Miracle UI Layout & Radial Menus

**Context:** Where does miracle UI live and what are "added dispensation methods"? (Follow-up from miracle activation decision)

**Answer:** 
- **Radial menus** as added dispensation method
- **Favorite miracles:** Displayed atop worship sites (temples, shrines)
- **All miracles:** Available in bottom bar

**Implications:**
- Contextual UI reduces clutter
- Worship sites become interactive miracle shortcuts
- Player can quick-cast favorites without opening full menu
- Bottom bar always accessible as fallback

**Follow-up Questions:**
- <DESIGN QUESTION: How many "favorite" slots per worship site? 3? 5? Based on building tier?>
- <DESIGN QUESTION: Can player customize which miracles are favorites?>
- <DESIGN QUESTION: Do radial menus open on right-click or dedicated key?>
- <DESIGN QUESTION: Radial menu shows what? Just favorites? Category filters?>

**PureDOTS Notes:**
```
Technical Implementation:
- Radial menu UI (PresentationSystemGroup)
  - Track cursor position on activation
  - Display circular menu of miracle icons
  - Sector selection (8 directions? 12 directions?)
  - Smooth rotation/selection feedback

- Worship site miracle shortcuts:
  - Component: WorshipSiteMiracleSlots : IBufferElementData {
      MiracleType favoriteSlot1;
      MiracleType favoriteSlot2;
      MiracleType favoriteSlot3;
    }
  - Click/hover worship site → show miracle icons floating above
  - Click icon → activate that miracle
  
- Bottom bar:
  - Persistent miracle panel
  - Scrollable if miracle count > UI space
  - Category tabs (Weather, Healing, Destruction, etc.)
  
- Input priority:
  - Radial menu doesn't conflict with RMB handlers
  - Use dedicated key (middle mouse? Q key?)
  - Or: Hold RMB without object under cursor
  
- Data flow:
  WorshipSite entity → Query favorite miracle IDs
  → Render UI at world position (3D → screen space)
  → Click detection → Activate miracle
```

**Related Concepts:**
- `Docs/Concepts/Miracles/Miracle_System_Vision.md`
- `Docs/Concepts/Buildings/` (worship site mechanics)

---

### 2025-11-02: Resource Depletion Specifics

**Context:** Refining "reasonably exploited" threshold for village founding

**Answer:**
- **75% depletion** = reasonably exploited
- **Check:** Node count in spatial grid cells

**Implications:**
- Villages expand when local resources hit 75% depleted
- Spatial grid cells define "area" (not arbitrary radius)
- Clear numeric threshold for AI decisions

**Follow-up Questions:**
- <DESIGN QUESTION: Node count threshold - how many cells around village? 1 cell radius? 3 cells?>
- <DESIGN QUESTION: Does 75% mean "75% of nodes are depleted" or "nodes are at 75% capacity"?>
  - Interpretation A: 75 out of 100 nodes are empty
  - Interpretation B: Average node is at 25% remaining resources
- <DESIGN QUESTION: Different thresholds per resource type? (Wood vs ore vs food?)>

**PureDOTS Notes:**
```
Technical Implementation:
- Depletion calculation (clarified):
  
  Option A: Count-based (likely)
    cellNodes = SpatialQuery.GetNodesInCells(villageCell, radiusCells);
    depletedCount = cellNodes.Count(n => n.RemainingAmount < threshold);
    depletionPercent = depletedCount / cellNodes.Count();
    if (depletionPercent >= 0.75f) → foundNewVillage();
  
  Option B: Capacity-based
    avgRemaining = cellNodes.Average(n => n.RemainingAmount / n.MaxAmount);
    if (avgRemaining <= 0.25f) → foundNewVillage(); // 75% depleted
  
- Spatial grid cells:
  - Reuse existing SpatialGridState
  - Village occupies center cell
  - Check surrounding cells (Moore neighborhood?)
  - Radius tunable: 1 cell (3×3 grid), 2 cells (5×5), etc.
  
- Component update:
  VillageResourceMetrics : IComponentData {
    float NodeDepletionPercent;  // 0-1
    int CellRadius;              // How many cells to check
    int NodesInArea;
    int DepletedNodes;
    uint LastCheckTick;
  }

- System:
  VillageResourceMonitoringSystem (new)
  - Runs every 10 seconds (not per frame)
  - Query villages
  - Spatial query for nodes in cell radius
  - Calculate depletion %
  - Tag OverExploitedTag if >= 75%
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md`
- PureDOTS: `SpatialSystemGroup`, `ResourceRegistrySystem`

---

### 2025-11-02: Desperate State Details

**Context:** Refining 10% resource crisis threshold behavior

**Answer:**
- **10% food specifically** (per-resource, not aggregate)
- **Actions triggered:**
  1. Send distress prayers
  2. Accept unfavorable trades
  3. Raiding

**Implications:**
- Food is critical resource (population survival)
- Other resources (wood, ore) less urgent
- Escalating desperation: prayer → trade → violence
- Prayer system becomes crisis communication channel

**Follow-up Questions:**
- <DESIGN QUESTION: Priority order enforced? (Try prayer first, then trade, then raid?)>
- <DESIGN QUESTION: How long before escalating to next action? (1 day prayer, then trade, then raid?)>
- <DESIGN QUESTION: Do other resources (wood, ore) have different thresholds or just food?>
- <DESIGN QUESTION: Can villages do multiple desperate actions simultaneously?>

**PureDOTS Notes:**
```
Technical Implementation:
- Food-specific crisis:
  VillageResourceState : IComponentData {
    float FoodPercent;          // Primary crisis trigger
    float WoodPercent;          // Secondary (construction)
    float OrePercent;           // Tertiary (military)
    byte CrisisState;           // None, FoodCrisis, etc.
  }

- Crisis action cascade:
  DesperateActionState : IComponentData {
    byte CurrentAction;         // 0=None, 1=Prayer, 2=Trade, 3=Raid
    uint ActionStartTick;
    uint ActionFailedCount;     // Escalation trigger
  }
  
- System flow:
  DesperateVillageSystem (new):
  1. Detect 10% food
  2. Spawn PrayerRequest (entity with urgent flag)
  3. Wait for fulfillment (timeout: 1 day?)
  4. If unfulfilled → spawn TradeRequest (accept any terms)
  5. If no trade takers → spawn RaidPlan targeting nearest village
  
- Prayer urgency:
  PrayerRequest : IComponentData {
    PrayerType type;            // Food, Aid, Protection
    float Urgency;              // 0-1 (10% food = 0.9 urgency)
    uint TimeoutTick;
    Entity RequestingVillage;
  }
  
- Unfavorable trade:
  TradeOffer : IComponentData {
    ResourceType offer;
    int offerAmount;
    ResourceType request;
    int requestAmount;
    float UnfavorableRatio;     // 0.1-0.5 (desperate accepts bad deals)
  }

- Raiding:
  RaidPlan : IComponentData {
    Entity TargetVillage;
    ResourceType targetResource;  // Steal food
    int BandSize;
    byte Aggression;              // High in desperate state
  }
  
- Telemetry:
  godgame.village.crisis.food.count
  godgame.village.desperate.prayers.count
  godgame.village.desperate.raids.count
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md#worship-miracle-system`
- `Docs/Concepts/Villagers/Bands_Vision.md`
- PureDOTS: `StorehouseSystems.cs`, prayer system (TBD)

---

### 2025-11-02: Initiative d20 System & Persistence

**Context:** Clarifying initiative roll mechanics

**Answer:**
- **Roll formula:** d20
- **Initiative persists** but changes (no automatic decay)
- **Example rationale:** "Materialist needs initiative to clear out and exploit a mine"

**Implications:**
- Familiar d20 mechanic (D&D style)
- Initiative is state, not temporary buff
- Outlook determines what needs initiative
- Materialists use initiative for economic expansion

**Follow-up Questions:**
- <DESIGN QUESTION: d20 roll vs what? DC? Opposed roll? Threshold?>
- <DESIGN QUESTION: Initiative range: 0-20 (matching d20) or normalized 0-1?>
- <DESIGN QUESTION: More rationale examples:
  - Warlike needs initiative for → ?
  - Scholarly needs initiative for → ?
  - Spiritual needs initiative for → ?>
- <DESIGN QUESTION: Can initiative go below 0? What happens at 0 initiative?>

**PureDOTS Notes:**
```
Technical Implementation:
- d20 roll pattern:
  
  OnEvent(Entity entity, EventType event) {
    var random = GetSeededRandom(entity, currentTick);
    int roll = random.NextInt(1, 21);  // d20
    
    // Get modifiers
    var outlook = GetComponent<EntityOutlook>(entity);
    var alignment = GetComponent<EntityAlignment>(entity);
    
    // Calculate DC based on event
    int baseDC = GetEventDC(event);  // e.g., mine exploitation = DC 12
    int modifiedDC = baseDC + GetAlignmentMod(alignment);
    
    // Check if initiative needed
    bool needsInitiative = CheckRationale(outlook, event);
    
    if (needsInitiative && roll >= modifiedDC) {
      // Success: grant initiative bonus
      initiative.Value += CalculateBonus(roll - modifiedDC);
    } else if (needsInitiative && roll < modifiedDC) {
      // Failure: lose initiative
      initiative.Value -= CalculatePenalty(modifiedDC - roll);
    }
    
    // Clamp but don't decay
    initiative.Value = math.clamp(initiative.Value, minInit, maxInit);
  }

- Initiative persistence:
  InitiativeState : IComponentData {
    float Value;               // Current initiative (persists)
    float HistoricalPeak;      // Highest ever reached
    uint LastModifiedTick;     // When last changed
  }
  
  // No decay system - initiative only changes from events

- Rationale table (example):
  EventType: MineDiscovered
  Outlooks that need initiative:
    - Materialistic: "exploit for wealth" (DC 10)
    - Warlike: "secure strategic resource" (DC 12)
    - Expansionist: "extend control" (DC 11)
  
  Outlooks that DON'T need initiative:
    - Isolationist: "ignore, not our concern"
    - Spiritual: "thank gods, carry on"
    
  EventType: VillagerDeath
  Outlooks that need initiative:
    - Warlike + High Patriotism: "vengeance" (DC 14)
    - Materialistic + Family: "assume duties" (DC 10)
  
  Outlooks that DON'T:
    - Peaceful: "mourn peacefully"
    - Stoic: "accept fate"

- Data-driven rationales:
  EventRationaleData : ScriptableObject {
    EventType eventType;
    OutlookRequirement[] requiresInitiative;  // Which outlooks roll
    OutlookRequirement[] ignoresEvent;        // Which outlooks skip
    int baseDC;
  }
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md#system-dynamics`
- `Docs/Concepts/Progression/Alignment_System.md`

---

### 2025-11-02: Tech Regression & Dual Point System

**Context:** Refining tech tier advancement mechanics

**Answer:**
- **Villages CAN regress** tiers
- **Both domain-specific AND generic points**
- **Different milestone numbers per tier** (scaling)

**Implications:**
- Tech is not permanent (villages can decline)
- Dual research currency (specialization + general progress)
- Higher tiers require more milestones (exponential difficulty)
- Villages can specialize or generalize

**Follow-up Questions:**
- <DESIGN QUESTION: Specific milestone formula?
  - Linear: Tier × 2 (Tier 1 = 2, Tier 10 = 20)?
  - Square root: √Tier × 5 (Tier 1 = 5, Tier 4 = 10, Tier 9 = 15)?
  - Fibonacci sequence?>
- <DESIGN QUESTION: Ratio of domain vs generic points?
  - Need 70% domain + 30% generic to advance?
  - Or: Need X domain milestones AND Y generic milestones?>
- <DESIGN QUESTION: Regression triggers:
  - Facility destruction lowers tier?
  - Lose all researchers = immediate regression?
  - Time-based decay (no research for N days)?
  - Only regress 1 tier at a time or can collapse multiple tiers?>
- <DESIGN QUESTION: Do milestones persist after regression?
  - Return to Tier 5 from Tier 7 → keep Tier 1-5 milestones?
  - Or full reset?>

**PureDOTS Notes:**
```
Technical Implementation:
- Dual point system:
  
  ResearchProgress : IComponentData {
    // Domain-specific
    float MilitaryPoints;
    float CivicPoints;
    float ArcanePoints;
    float LogisticsPoints;
    
    // Generic
    float GenericPoints;
    
    // Tracking
    ushort MilitaryMilestones;
    ushort CivicMilestones;
    ushort ArcaneMilestones;
    ushort GenericMilestones;
  }

- Advancement formula (example):
  Tier 1 → 2: Requires 3 total milestones (2 domain + 1 generic)
  Tier 5 → 6: Requires 8 milestones (6 domain + 2 generic)
  Tier 10 → 11: Requires 15 milestones (11 domain + 4 generic)
  Tier 19 → 20: Requires 30 milestones (22 domain + 8 generic)
  
  Formula: milestonesRequired = Ceiling(tier * 1.5)
           domainRequired = Ceiling(milestonesRequired * 0.7)
           genericRequired = milestonesRequired - domainRequired

- Regression system:
  TechRegressionSystem (new):
  - Triggers:
    1. All research facilities destroyed
    2. Researcher count below minimum threshold
    3. Optional: Time decay (e.g., 30 days no research)
  
  - Behavior:
    Check VillageTechState each evaluation period (daily?)
    If trigger met:
      - Regress 1 tier
      - Keep milestones (knowledge persists)
      - Require fewer milestones to climb back
  
  - Recovery bonus:
    "Recovering" villages advance faster if they've been there before
    
- Component:
  VillageTechState : IComponentData {
    byte CurrentTier;
    byte HistoricalPeakTier;      // Never decreases
    ushort CurrentMilestones;     // Progress this tier
    ushort RequiredMilestones;    // Target to advance
    byte RegressionRisk;          // 0-100 (based on triggers)
  }

- Generic vs domain points:
  Generic points can be:
    - Applied to any domain (player choice)
    - Unlock cross-domain techs (hybrid buildings)
    - Prerequisite for apex tiers (Tier 18+ needs generalist knowledge)
  
  Domain points:
    - Unlock domain-specific milestones
    - Grant specialized bonuses
    - Example: Military milestones unlock better weapons
```

**Related Concepts:**
- `Docs/Concepts/Core/Sandbox_Autonomous_Villages.md#technology-progression`
- PureDOTS: Need `TechAdvancementSystem.cs`, `TechRegressionSystem.cs`

---

## Summary of Session 2025-11-02

**Decisions Made:** 10 (5 initial + 5 refinements)
**Open Questions Resolved:** ~15
**New Questions Raised:** ~20

**Next Priority Questions:**
1. Resource depletion threshold percentages
2. Initiative roll formula specifics
3. Milestone counts per tech tier
4. Prayer/mana economy scaling

---

**For Implementers:** Reference this log before starting new features to ensure alignment with decisions.  
**For Designers:** Add entries as decisions are made; link to relevant concept docs.

