# Mechanic: Combat Loop

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: Combat

**One-line description**: Carrier-led engagements secure mining zones, protect logistics, and reshape territorial control through decisive fleet orders.

## Core Concept

Combat is the second-priority loop, built to safeguard mining operations and contest rival empires. Players direct carriers and attached strike wings from a bodiless command perspective, leveraging doctrine alignment, initiative, and cohesion systems already defined in alignment truth sources. The initial serialized game state lets players mod fleets, armaments, and starting threats without altering core logic.

## How It Works

### Basic Rules

1. Identify a threat or target zone (hostile fleets, pirate dens, enemy stations) and issue engagement orders to relevant carriers.
2. Carriers choose tactics based on doctrine alignment, cohesion, and risk appetite, generating commands for strike craft and escorts.
3. Engagement resolution updates territory control, attrition, and triggers follow-up events (salvage, morale shifts, compliance checks).

### Parameters and Variables

| Parameter | Default Value | Range | Effect |
|-----------|---------------|-------|--------|
| EngagementScale | Skirmish | Skirmish–Armada | Determines participating entities and simulation granularity.
| InitiativeThreshold | 0.5 | 0-1 | Minimum initiative needed to execute high-complexity maneuvers.
| CohesionFloor | 0.4 | 0-1 | Modulates formation integrity; derived from alignment systems.
| AttritionRate | 1.0 | 0.1-3.0 | Governs loss-per-tick under focused fire.
| DoctrineBias | 0 | -2 to +2 | Tweaks tactic preference per empire doctrine.

### Edge Cases

- **Mutiny**: Low cohesion combined with conflicting doctrine can trigger internal rebellion mid-battle.
- **Fog of War**: Exploration accuracy affects combat readiness; outdated intel raises surprise penalties.
- **Overkill**: Excess force risks collateral damage that impacts diplomacy or mining infrastructure.
- **Ground/Boarding**: Abstracted for now—outcomes resolved via strategic systems until detailed mechanics are introduced.

## Player Interaction

### Player Decisions

- Selecting where to deploy limited combat-ready carriers to defend mining lines or strike opportunistic targets.
- Choosing engagement posture (hit-and-run, siege, interception) aligned with doctrine and initiative constraints.
- Timing reinforcements or retreats to protect logistics throughput.

### Skill Expression

Mastery comes from leveraging carrier initiative windows, sequencing engagements to avoid attrition peaks, and exploiting enemy doctrine weaknesses captured in the alignment system.

### Feedback to Player

- Visual: Tactical overlays showing firing arcs, cohesion halos, and threat zones.
- Numerical: Engagement dashboards listing attrition rates, morale shifts, and salvage potential.
- Audio: Combat cues reflecting tactical posture (e.g., alarms when cohesion collapses).

## Balance and Tuning

### Balance Goals

- Combat should meaningfully protect or disrupt mining and hauling without becoming the only viable loop.
- Doctrine diversity and alignment influence should visibly shift engagement outcomes.
- Combat is high risk, high skill—only well-trained mercs thrive long-term, evoking Kenshi-style danger.

### Tuning Knobs

1. **Initiative Bias**: Adjust how alignment triplets convert to action readiness.
2. **Attrition Curves**: Modulate time-to-kill and repair costs.
3. **Reward Scaling**: Control salvage/resource windfalls so combat complements but does not replace mining income.

### Known Issues

- TBD pending prototype combat telemetry.

## Integration with Other Systems

| System/Mechanic | Type of Interaction | Priority |
|-----------------|---------------------|----------|
| Mining Loop | Provides targets and defence obligations | High |
| Haul Loop | Requires escort and route security | High |
| Alignment & Doctrine (Docs/TODO/Alignment.md) | Governs tactics, mutiny, compliance | Critical |

### Emergent Possibilities

- Combat victories unlocking temporary mining bonuses through captured infrastructure.
- Doctrine conflicts creating narrative events when fleets refuse orders mid-battle.

## Implementation Notes

### Technical Approach

- Use existing alignment components (AlignmentTriplet, Cohesion, Initiative) as core combat inputs.
- Structure combat tick systems to scale with millions of entities by batching engagements per sector and reusing spatial partition data.
- Store engagement outcomes in serialized logs so players can mod follow-up states.

### Performance Considerations

- Limit per-frame physics detail for large armadas; rely on aggregate damage models when entity counts spike.
- Integrate with spatial grid services to cull inactive combatants.

### Testing Strategy

1. Unit tests for initiative gating and tactic selection.
2. Scenario tests verifying mutiny triggers under doctrine misalignment.
3. Stress tests with thousands of carriers to validate performance under target entity counts.

## Examples

### Example Scenario 1

**Setup**: Mining fleet under pirate harassment.  
**Action**: Escort carriers intercept, leverage high cohesion to hold formation.  
**Result**: Pirates routed, mining uptime restored, minor salvage recovered.

### Example Scenario 2

**Setup**: Player-modified start spawns aggressive rival empire nearby.  
**Action**: Defensive posture with hit-and-run tactics exploits rival’s low cohesion.  
**Result**: Stalemate that buys time to reinforce stations and expand mining elsewhere.

## References and Inspiration

- **Homeworld**: Carrier-centric fleet command with emphasis on formation management.
- **Endless Space**: Doctrine-driven combat outcomes.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Captured carrier-forward combat vision |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
