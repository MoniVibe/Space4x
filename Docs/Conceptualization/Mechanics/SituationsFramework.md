# Mechanic: Situations & Evolving States

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: Systems / Narrative

**One-line description**: Situations track ongoing problems or opportunities affecting individuals or aggregates, progressing through phases with branching outcomes influenced by policies, resources, and player intervention.

## Core Concept

Situations are long-running state machines representing crises, projects, or social movements. They activate when conditions arise (e.g., energy reserves at zero) and evolve through phases until resolved. Each phase offers choices and consequences, shaped by the controlling entity’s policies, outlooks, and available resources.

## Structure

- **Trigger Condition**: World-state check (resource deficit, morale threshold, discovery event) spawns a Situation instance tied to one or more entities.
- **Phases**:
  1. **Detection**: Alerts stakeholders; minimal impact but introduces tension.
  2. **Escalation**: Debuffs/penalties kick in; optional mitigation tasks appear.
  3. **Climax**: Must choose a resolution path—policies, resource allocation, diplomacy, or sacrifice.
  4. **Aftermath**: Apply outcomes; may spawn follow-up events or new situations.
- **Outcome Paths**: Determined by policies, resources, outlook alignment, and intervention success.

## Example: Energy Crisis

- **Trigger**: Colony energy reserves reach zero.
- **Phase 1**: Rolling brownouts; morale dips for tech-reliant populations.
- **Phase 2**: Essential services shut down; players can reroute power, enforce rationing, or request aid.
- **Phase 3**: Choose between emergency reactors (risk explosions), forced population relocation, or accepting economic collapse.
- **Phase 4**: Success restores stability; failure sparks rebellion, migration, or permanent debuffs.

## Integration Touchpoints

- **Dynamic Events**: Situations may spawn from or feed into event chains.
- **Law & Rebellion**: Policy selections during situations can soothe or inflame dissent.
- **Morale Dynamics**: Each phase adjusts morale baselines; resolution outcomes ripple through aggregation.
- **Tech Progression**: Certain tech unlocks new resolution options (fusion batteries, psychic mediators).
- **Economy & Corruption**: Corrupt governors might siphon relief funds, altering outcome probabilities.

## Tuning Guidance

- Ensure each situation has at least two viable resolution strategies to support varied playstyles.
- Scale phase timers and penalties by entity size and difficulty settings.
- Offer breadcrumbs so players can anticipate escalation and plan mitigations.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Defined situation framework with phase structure |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
