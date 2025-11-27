# Space 4X - Core Design Pillars

Core pillars are the fundamental design principles that guide all feature and mechanic decisions. Every addition to the game should support at least one pillar without undermining others.

## Pillar 1: Carrier-First Command

### Description
The player exists as a strategic intelligence issuing orders to carriers, stations, and construction projects—never directly controlling individual units. All gameplay flows through carrier task forces that execute directives autonomously. This creates a command-and-control fantasy where players orchestrate fleets rather than micromanage ships.

### Design Implications
- All player actions are expressed as orders assigned to carriers or facilities
- Carriers are the primary unit of player interaction, not individual vessels or crew
- UI and command systems prioritize fleet-level decisions over unit-level control
- Starting modes configure carrier availability and capabilities, not unit counts
- Player agency comes from strategic positioning and resource allocation, not tactical unit control

### Examples in Practice
- Mining loop: Player assigns mining orders to carriers, which deploy mining vessels autonomously
- Combat: Player commands carrier formations, not individual fighters
- Construction: Player orders station builds, carriers handle logistics and deployment
- Exploration: Carriers dispatch scout vessels based on exploration orders

---

## Pillar 2: Interdependent Loops

### Description
Core gameplay loops (mining, hauling, exploration, combat) are deeply interconnected. No loop exists in isolation—mining requires hauling to stations, exploration reveals threats requiring combat, combat consumes resources requiring mining. Players must balance all loops simultaneously, creating strategic depth through system interaction rather than isolated mechanics.

### Design Implications
- Each loop feeds into and depends on others
- Resource flows between loops create natural strategic tension
- Pivoting between loops should be fluid, not penalized
- Systems must communicate state clearly so players understand interdependencies
- Balance changes to one loop ripple through others, requiring holistic tuning

### Examples in Practice
- Mining → Hauling → Station Construction → Carrier Production → Mining (resource cycle)
- Exploration → Threat Discovery → Combat → Resource Loss → Mining Recovery (threat-response cycle)
- Tech Diffusion → Module Upgrades → Refit Requirements → Facility Proximity → Logistics Planning (progression cycle)
- Compliance Breaches → Mutiny/Desertion → Crew Loss → Reduced Efficiency → Resource Shortage (social cycle)

---

## Pillar 3: Living Galaxy

### Description
The galaxy is a dynamic, reactive environment that responds to player actions and evolves independently. Threats escalate, resources deplete, factions adapt, and anomalies emerge. The simulation runs continuously, creating emergent narratives and forcing players to adapt rather than follow scripted paths.

### Design Implications
- Systems must support emergent behavior, not just scripted events
- AI factions and threats operate independently of player actions
- Resource depletion, tech diffusion, and compliance create natural pacing
- Events cascade from system interactions, not predetermined triggers
- Player actions have lasting consequences that reshape the galaxy state

### Examples in Practice
- Mining depletes deposits, forcing exploration for new sources
- Tech diffusion creates temporal advantages that shift power balances
- Compliance breaches trigger mutiny/desertion events that cascade into resource shortages
- Threat ecosystems escalate based on player expansion and time passage
- Anomalies emerge from system interactions, not scripted spawns

---

## Balancing Pillars

These pillars work together to create a strategic command experience where players orchestrate carrier fleets across a living galaxy through interdependent systems. The tension comes from balancing immediate tactical needs (combat, resource shortages) with long-term strategic goals (expansion, tech progression, empire stability).

### Potential Conflicts
- **Carrier-First vs. Interdependent Loops**: Players may want direct control when loops break down (e.g., mining vessel stuck). Resolution: Provide clear feedback and autonomous recovery systems, not manual override.
- **Interdependent Loops vs. Living Galaxy**: Complex interdependencies can create unpredictable cascades that feel unfair. Resolution: Provide clear telemetry and early warning systems so players can anticipate and adapt.
- **Living Galaxy vs. Carrier-First**: Dynamic threats may require rapid response that feels at odds with strategic command. Resolution: Design threats with appropriate warning times and escalation curves that match strategic pacing.

### Resolution Strategies
- Prioritize player agency through information and strategic options, not direct control
- Use telemetry and registry systems to surface system state clearly
- Design cascades with player-visible causes and recoverable consequences
- Balance automation with strategic decision points where player input matters

---

## Pillar Health Check

When designing a feature, ask:
1. Which pillar(s) does this feature support?
2. Does it undermine any pillar?
3. If it conflicts with a pillar, is the trade-off worth it?
4. Can the feature be redesigned to better align with the pillars?

---

*Last Updated: October 31, 2025*












