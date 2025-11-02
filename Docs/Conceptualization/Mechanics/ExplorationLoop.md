# Mechanic: Exploration Loop

## Overview

**Status**: Concept  
**Complexity**: Moderate  
**Category**: Exploration

**One-line description**: Scouting carriers chart unknown space, reveal resource opportunities, and feed strategic intel into doctrine-driven decision making.

## Core Concept

Exploration is the third-priority loop, enabling the bodiless commander to expand knowledge of the galaxy. Carriers equipped with sensors and survey drones uncover deposits, threat loci, and colonizable worlds. Because starting configurations are serialized for players to alter, exploration parameters (sensor ranges, anomaly tables, fog logic) must be data-first and easily extended.

## How It Works

### Basic Rules

1. Dispatch exploration carriers toward uncharted sectors or anomalies.
2. Execute survey actions that reduce fog of war, catalog entities, and flag hazards or opportunities.
3. Feed discoveries into mining, combat, and hauling planners, updating route selection and strategic objectives.

### Parameters and Variables

| Parameter | Default Value | Range | Effect |
|-----------|---------------|-------|--------|
| SensorRange | 2 sectors | 1-5 sectors | Determines detection radius per tick.
| SurveyTime | 30s | 10-120s | Duration required to fully scan a point of interest.
| DiscoveryYield | 1.0 | 0-3.0 | Weight applied to deposit richness or anomaly insight.
| DataDecay | 0.0 | 0-1 | How fast intel becomes stale.
| RiskLevel | 0.1 | 0-1 | Probability of triggering hostile encounters during surveys.

### Edge Cases

- **Data Spoilage**: High DataDecay sectors require recurring patrols to keep intel current.
- **Ambushes**: Elevated RiskLevel spawns event hooks for pirates or anomalies, tying back to combat requirements.
- **False Positives**: Poor sensor quality produces noisy data, prompting further confirmation before committing mining fleets.

## Player Interaction

### Player Decisions

- Prioritizing sectors that balance near-term resource needs with long-term expansion prospects.
- Assigning sensor packages or escorts to exploration carriers depending on risk appetite.
- Timing resurvey efforts to avoid acting on stale intel.

### Skill Expression

Skilled players layer exploration sweeps to minimize redundant coverage, chain discoveries into efficient mining expansions, and intentionally bait rivals with decoy intel when doctrines allow.

### Feedback to Player

- Visual: Fog-of-war gradations and overlays highlighting survey completeness.
- Numerical: Intel panels listing deposit prospects, threat assessments, and data freshness timers.
- Audio: Pings for successful discoveries or anomaly warnings.

## Balance and Tuning

### Balance Goals

- Exploration should meaningfully gate access to high-value mining sites without stalling early progression.
- Riskier sectors must offer enough upside to justify escort investment or doctrine shifts.

### Tuning Knobs

1. **Sensor Efficiency**: Adjust base range and accuracy to modulate scouting pace.
2. **Discovery Distribution**: Shape how frequently premium deposits or anomalies appear.
3. **Risk Scaling**: Tie encounter frequency to faction presence or time since last patrol.

### Known Issues

- TBD pending prototype of fog and intel decay systems.

## Integration with Other Systems

| System/Mechanic | Type of Interaction | Priority |
|-----------------|---------------------|----------|
| Mining Loop | Supplies deposit intel and hazard forecasts | High |
| Combat Loop | Triggers ambushes and informs threat response | High |
| Haul Loop | Optimizes route planning and station placement | Medium |

### Emergent Possibilities

- Discoveries that unlock narrative threads or doctrine tensions (e.g., hidden colonies affecting alignment buffers).
- Sector control mechanics where frequent surveys extend logistical bonuses to hauling.

## Implementation Notes

### Technical Approach

- Store fog and intel layers in serialized maps so player-modified starts can predefine explored regions.
- Reuse spatial partition profiles for exploration sweeps to keep scanning costs bounded.
- Log discoveries into registry telemetry for debugging and downstream UI without locking us into a specific UX.

### Performance Considerations

- Batch sensor checks per sector and limit high-resolution scans to active points of interest.
- Use deterministic random seeds for anomalies to keep multiplayer or replay states consistent.

### Testing Strategy

1. Unit tests for sensor range calculations and intel decay.
2. Scenario tests ensuring mining AI respects freshly discovered deposits.
3. Stress tests with simultaneous exploration fleets to validate overhead under million-entity simulations.

## Examples

### Example Scenario 1

**Setup**: Core sectors explored; frontier remains fogged.  
**Action**: Player dispatches exploration carriers with mid-tier sensors and escorts.  
**Result**: New high-richness deposit discovered alongside elevated pirate risk, informing combat deployments.

### Example Scenario 2

**Setup**: Player-modified start grants advanced sensors but minimal escorts.  
**Action**: Rapid surveys map vast territory but trigger frequent ambushes.  
**Result**: Player must pivot to reinforce combat loop sooner than expected.

## References and Inspiration

- **Stellaris**: Science ship exploration gating expansion.
- **Distant Worlds**: Continuous scanning with persistent fog considerations.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Documented exploration loop vision |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
