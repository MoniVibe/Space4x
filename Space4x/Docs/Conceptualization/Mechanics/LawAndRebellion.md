# Mechanic: Laws, Compliance & Rebellion Escalation

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: Governance / Social

**One-line description**: Empires legislate based on outlooks and alignment; dissent escalates from protests to sabotage and civil war when compliance fails.

## Core Concept

Empires, factions, and guilds enforce laws aligned with their doctrines (e.g., slavery legality, trade embargoes, population control). Entities evaluate compliance using the alignment and morale systems. When compliance drops, rebellion escalates through discrete stages, giving both rulers and rebels opportunities to respond.

## Law Framework

- **Doctrine-Based Laws**: Each empire sets policies derived from median outlooks (e.g., authoritarian empires legalize slavery, pacifists ban it).
- **Enforcement Tools**: Security forces, propaganda networks, legal courts, or oppressive surveillance affect compliance difficulty.
- **Flexibility**: Leaders may pass reforms; doing so shifts morale across different outlook groups.
- **Player Authority**: Players who own colonies can configure laws and policies directly; scope will expand as control systems are detailed.

## Compliance Evaluation

1. **Alignment Check**: Compare entity alignment/outlook with law expectations (cf. `Docs/TODO/Alignment.md`).
2. **Morale Modifier**: Low morale amplifies dissent signals; high morale buffers compliance.
3. **Task Alignment**: If the entity’s duties directly contradict personal ethics (e.g., lawful good on a slaver ship), compliance plummets unless flagged as `SpyRole`.

## Rebellion Escalation Stages

| Stage | Description | Triggers | Ruler Response |
|-------|-------------|----------|----------------|
| 1. Unrest | Petitions, protests, cultural pushback | Minor compliance breaches | Diplomacy, concessions, propaganda |
| 2. Resistance | Strikes, sabotage, targeted dissent | Sustained low compliance, harsh crackdowns | Negotiations, targeted arrests |
| 3. Insurgency | Coordinated sabotage, open defiance | Failed mediation, high variance morale | Deploy security forces, reforms |
| 4. Civil War | Full-scale conflict, territorial control shifts | Collapse of authority, external backing | Military intervention, partition |

Rebels evolve tactics based on empire response; heavy-handed crackdowns accelerate escalation.

## Integration Touchpoints

- **Morale Dynamics**: Rebellion stages depend on morale variance and task alignment.
- **Combat Loop**: Civil wars spawn factional fleets; battles obey combat mechanics with morale multipliers.
- **Diplomacy**: External empires may support rebels or enforce peace accords.
- **Entity Hierarchy**: Ownership layers determine which assets switch allegiance during rebellion stages.

## Tuning Guidance

- Provide levers for rulers (reforms, amnesty, policy tweaks) to de-escalate before civil war.
- Ensure rebels can succeed without brute force if they win hearts-and-minds (morale swings).
- Model corruption: corrupt regimes delay acknowledging unrest, causing sudden escalation.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Established law enforcement and rebellion stages |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
