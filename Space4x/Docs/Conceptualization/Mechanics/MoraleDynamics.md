# Mechanic: Morale & Outlook Dynamics

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: Social / Psychology

**One-line description**: Individual morale reactions stem from alignment and outlooks, then aggregate upward to guilds, fleets, factions, and empires.

## Core Concept

Morale is a per-entity state influenced by every major loop—combat outcomes, logistics, migration, doctrine compliance, and environment modifiers. Each entity rolls morale adjustments according to its alignment triplet and ethical outlooks; aggregate entities average those values with weighting modifiers to decide stability, cohesion, and rebellion risk. Because everything is intertwined, small shocks can cascade through connected organizations.

## Individual Morale Calculation

1. **Baseline**: Start from a neutral value derived from alignment (Lawfulness, Altruism, Integrity) and doctrine expectations.
2. **Task Alignment**: Evaluate how well the entity’s current assignment, surroundings, or orders align with its outlooks.
   - Peaceful xenophiles thrive in cooperative or humanitarian tasks, but suffer heavy penalties if forced into atrocities unless flagged as `SpyRole`/`DoubleAgent`.
   - Fanatic xenophobes gain morale when enforcing exclusionary policies but rebel when ordered to embrace outsiders.
   - Neutrals (near-zero alignments/outlooks) roll with 50/50 variance when the moral framing is ambiguous, preserving emergent stories.
3. **Event Modifiers**: Apply situational impacts (migration, combat loss, supply shortage) scaled by the alignment-task mismatch. Misaligned tasks amplify negatives; aligned tasks soften the blow.
4. **Context Overlays**: Environmental factors (planet modifiers, shipboard comfort, facility tier) add persistent buffs/debuffs.
5. **Clamp & Drift**: Morale values saturate within [-1, +1] and drift slowly back toward baseline during calm periods.

## Aggregation Rules

- **Weighted Average**: Aggregates (crews, guilds, fleets, factions, empires) blend member morale using influence weights defined in `Docs/TODO/Alignment.md` (rank, experience, fanaticism).
- **Variance Tracking**: Store variance in addition to mean. High variance indicates a polarized group, increasing mutiny or secession chances even if average morale seems acceptable.
- **Inheritance**: Aggregates propagate their morale to parent organizations with decay factors (e.g., a disgruntled guild influences its empire less than its immediate fleet).

## Event Hooks

- **Migration**: When colonists disembark from mobile fleets, roll morale for departing and remaining members separately. Large departures trigger morale shocks and potential splinter factions.
- **Combat**: Victory grants morale boosts when tactics match doctrine; pyrrhic wins that violate ethics still penalize lawful or altruistic crews.
- **Logistics**: Supply shortages hit materialist or expansionist outlooks harder; steady logistics boosts morale for pragmatic factions.
- **Policy Shifts**: Empire decrees adjust morale differently per outlook (e.g., egalitarian policies soothe xenophiles but anger fanatical xenophobes).

## Integration Touchpoints

- **Alignment Compliance**: Morale is a key input for deciding whether doctrine breaches escalate to mutiny (see `Docs/TODO/Alignment.md`).
- **Combat Loop**: Cohesion thresholds use morale as a multiplier when determining formation breaks.
- **Construction Loop**: Crew morale modifies fatigue curves, influencing construction throughput.
- **Entity Hierarchy**: Morale informs when individuals or sub-groups declare independence or reassign asset ownership.

## Tuning Guidance

- Keep per-event morale adjustments modest so stacking events matters more than single spikes.
- Provide designer-accessible tables mapping outlook combinations to modifier strengths (e.g., Peaceful Xenophile = -0.3 morale on forced migration).
- Allow tech or doctrine perks to dampen or amplify specific morale channels, enabling strategic customization.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Defined morale propagation tied to alignment/outlooks |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
