# Mechanic: Fleet Coordination & Reinforcements

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: AI / Combat

**One-line description**: When multiple fleets converge, the highest-ranking captain assumes admiralty, orchestrating doctrine-driven plans while individual captains execute according to their outlooks; reinforcements warp in using tactics tied to behavior and tech level.

## Command Hierarchy

- Highest-ranking captain becomes admiral, setting overall engagement stance based on alignment/outlook.
- Lawful admirals enforce coordinated plans and formations; chaotic admirals allow greater autonomy.
- Subordinate captains follow assigned objectives but may diverge if chaotic or if task urgency demands.

## Task Execution

- Fleets retain their mission goals; admiral directives prioritize tasks by urgency (defend haulers, strike targets, rescue allies).
- Coordination leverages service traits—experienced fleets handle complex maneuvers.

## Reinforcement Tactics

- Warp-in behavior depends on captain outlook and tech level:
  - **Chaotic/Warlike**: Attempt flanking or close-range drop-ins, even atop enemy formations.
  - **Lawful/Defensive**: Warp in at standoff range, forming protective screens.
- Warp tech tier sets precision: low-tech arrivals scatter unpredictably, high-tech jumps land pinpoint with desired orientation.

## Disengagement Logic

- Fleets try to disengage when outmatched or low on supplies, prioritizing jumps or retreats.
- If engines, reactors, or fuel reserves are compromised, they fight defensively or pursue negotiation/parlay where outlook permits.

## Integration Touchpoints

- **Combat Loop**: Influences battle setups, entry vectors, and target priorities.
- **Vessel Movement AI**: Reinforcement paths reuse stance and formation logic.
- **Mission Board**: Coordinated fleets can accept chained combat contracts.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Documented fleet coordination and reinforcement behavior |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
