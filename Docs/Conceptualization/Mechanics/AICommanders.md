# Mechanic: AI Commanders & Fleet Behavior

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: AI / Command

**One-line description**: Carrier captains follow orders from their owning entity, executing stepwise plans that respect provisions, crew readiness, hull integrity, and threat assessments filtered through alignment.

## Core Concept

Captains inherit directives from the player, faction, or empire they serve. Orders break into actionable steps (secure provisions, repair hull, travel, execute objective). Alignment and outlooks influence how aggressively they pursue goals, but safety thresholds (threat level vs hull status) cannot be ignored.

## Order Pipeline

1. **Receive Directive**: From player/faction/empire command queue.
2. **Pre-Flight Checks**: Ensure provisions, crew morale, and hull integrity meet minimum thresholds.
   - Alignment influences tolerance (chaotic captains may cut corners, lawful insist on full readiness).
3. **Threat Evaluation**: Assess local/route threats using exploration data and diplomacy context.
   - Captains will never engage mining or hauling if threat level exceeds defensive capability.
4. **Execution**: Perform objective with stepwise adjustments (reroute, request escort, delay for repairs).
5. **Feedback Loop**: Report success/failure and request new orders or assistance.

## Alignment Influence

- **Lawful** captains stick to doctrine, prioritizing safety and compliance.
- **Chaotic** captains take calculated risks, but still respect non-negotiable safety thresholds.
- **Good/Altruistic** captains aid allies or civilians even if it delays primary objectives.
- **Evil/Exploitative** captains take opportunities for personal or factional gain (salvage, plunder) aligned with orders.

## Autonomy & Escalation

- Captains may escalate for reinforcements if threat level spikes or resources run low.
- Doctrine policies can expand or restrict captain autonomy (e.g., “Aggressive Mining” allows riskier operations).
- Repeatedly ignored orders or impossible tasks may trigger morale hits or mutiny checks.
- Captains can advise short-to-medium-term plans: materialists propose market exploits, warlikes suggest refits or counter-tech, xenophiles recommend multi-species recruitment, xenophobes push psychological warfare. Chaotic captains may act independently when idle.

## Integration Touchpoints

- **Combat Loop**: Threat assessment guides when captains call for escorts or retreat.
- **Haul Loop**: Captains schedule haul runs only when mines/stations are secure and holds can be cleared safely.
- **Construction Loop**: Captains assigned to construction verify material availability before committing crews.
- **Morale Dynamics**: Task alignment directly affects morale; forced unethical tasks trigger dissent.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Defined captain order processing and safety behavior |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
