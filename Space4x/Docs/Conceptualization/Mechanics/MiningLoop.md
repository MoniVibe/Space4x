# Mechanic: Mining Loop

## Overview

**Status**: Concept  
**Complexity**: Moderate  
**Category**: Economy

**One-line description**: Carriers extract raw resources from celestial deposits and feed the empire’s first production chain.

## Core Concept

Mining is the foundational loop that bankrolls every other directive. Carriers prospect, deploy extraction rigs, and maintain throughput from resource-rich bodies back to staging stations. Because the initial game state is serialized for player-driven modification, deposit definitions, carrier loadouts, and rig efficiencies must be data-driven and easy to extend.

## How It Works

### Basic Rules

1. Identify a deposit (asteroid, moon seam, orbital debris belt) within carrier range.
2. Assign a carrier to deploy appropriate extraction equipment and begin harvesting at a rate governed by deposit richness, rig quality, and carrier staffing.
3. Store extracted ore in carrier holds or linked drop-off drones until a haul directive transfers it to a station or refinery.

### Parameters and Variables

| Parameter | Default Value | Range | Effect |
|-----------|---------------|-------|--------|
| DepositRichness | 1.0 | 0.1-5.0 | Multiplies base extraction per tick.
| HarvestNodeCount | 3 | 1-10 | Number of attachment points for concurrent mining vessels.
| RegenerationRate | 0.0 | 0-0.5 | Richness regeneration per tick (type-specific; 0 for non-regenerating deposits).
| RigEfficiency | 1.0 | 0.2-2.0 | Captures equipment tier and maintenance state.
| RigType | Laser | Laser/Drone/Drill | Each rig type has different extraction rates and batch behavior.
| HazardLevel | 0.0 | 0-1 | Drives attrition risk and required support modules.
| ExtractionTick | 5s | 1s-60s | Interval for yield and hazard evaluation. Matches real-time and sim time (with time controls).
| CarrierHoldCapacity | 1000 | 200-10000 | Dictates when the haul loop must pick up goods.

### Edge Cases

- **Depletion**: Deposits decline via linear drain on richness value; when richness < threshold (0.1), carriers trigger suspend state and request reassignment.
  - **Regeneration**: Certain deposit types regenerate richness over time when left idle. Type-specific (e.g., gas clouds regenerate, solid ore veins don't).
- **Hazards**: High hazard deposits require escort or specialized modules.
  - **Hazard Types**: Radiation zones, asteroid impacts, pirate ambushes, anomalies, space fauna, unstable deposits.
  - **Scaling**: Hazard frequency/severity scales with deposit value (richer = more dangerous).
  - **Event-Driven**: Hazards trigger as events during extraction cycles rather than constant state.
  - **Component Degradation**: All components (mining rigs, hull, engines, shields, etc.) can degrade.
  - **Repair**: Components may be repaired to an extent outside stations/colony orbitals (field repairs with limited effectiveness).
  - **Experience**: Experienced crews gain resistance to certain hazard types over time.
  - **Mitigation**: Hazards mitigated via shields, armor, speed, specialized modules, or escorts.
- **Overcrowding**: Multiple carriers on the same deposit compete for harvest nodes.
  - **Harvest Nodes**: Deposits have limited attachment points (harvest nodes) for mining vessels to dock and extract.
  - **Node Competition**: Each carrier must attach to a node; excess carriers wait in queue or seek alternate deposits.
  - **Per-Node Yield**: Each node provides extraction independently; carriers mine at full rate if attached to a node.

## Player Interaction

### Player Decisions

- Choosing which deposits to activate first to fuel construction timelines.
- Balancing rig quality against maintenance and up-front costs.
- Scheduling escorts or countermeasures for hazardous sites.

### Skill Expression

Experienced players anticipate depletion curves, rotate carriers before downtime, and layer exploration intel to locate richer seams faster than rivals.

### Feedback to Player

- Visual: Deposit overlays indicating richness, remaining yield, and hazard state.
- Numerical: Carrier dashboards showing extraction per tick and time-to-fill for holds.
- Audio: Drills, alarms, or hazard warnings cueing intervention needs.

## Balance and Tuning

### Balance Goals

- Mining output must reliably bootstrap the economy without trivialising later resource hunts.
- Hazards introduce meaningful risk so combat and support loops remain relevant around mining sites.
- Versatile risk: mining encounters range from trivial to high threat depending on deposit location and resource type.

### Tuning Knobs

1. **Richness Distribution**: Adjust galaxy generation curves to control early scarcity vs abundance.
2. **Hazard Scaling**: Increase attrition or required escort strength in contested regions.
3. **Rig Upkeep**: Drift maintenance costs to gate runaway carrier fleets.

### Known Issues

- TBD once prototype data reveals throughput imbalances.

## Integration with Other Systems

| System/Mechanic | Type of Interaction | Priority |
|-----------------|---------------------|----------|
| Haul Loop | Consumes mined resources and clears holds | High |
| Combat Loop | Protects mining carriers and contests deposits | High |
| Exploration Loop | Locates new deposits and hazards | High |

### Emergent Possibilities

- Hazard zones that spawn pirate interest, forcing combat choices to secure premium ores.
- Logistics bottlenecks that push players to build forward refineries on-the-fly.

## Implementation Notes

### Technical Approach

- Represent deposits as serialized entities with richness curves so players can mod initial placements.
- Use DOTS dynamic buffers on carriers to track active rigs, extraction rates, and hazard exposure for Burst-friendly simulation.
- Mining systems should run before hauling logistics each tick so resource availability updates feeding downstream commands.
- **Harvest Nodes**: Each deposit entity has buffer of `HarvestNode` elements (position, attached carrier, extraction state).
  - Carriers query spatial grid for deposits, then request node attachment.
  - Node assignment uses deterministic ordering (entity index) to prevent race conditions.
- **Deposit Regeneration**: Regenerating deposits accumulate richness over time when node count < max capacity.
  - Type-specific regeneration rates stored in deposit blob data.
- **Rig Systems**: Each rig type (Laser, Drone, Drill) implements different extraction logic via separate systems or parameterized jobs.
  - Lasers: Continuous beam, high energy cost, minimal material waste.
  - Drones: Batch collection, lower energy, requires drone maintenance.
  - Drills: Physical contact, wear-and-tear on equipment, high throughput.
- **Component Degradation**: Track component health separately; integrate with repair systems (field repairs vs station overhauls).
- **Crew Experience**: Hazard resistance stored as crew skill levels; query during hazard evaluation to modify damage/morale impact.

### Performance Considerations

- Batch deposit evaluations per sector to keep the one-million-entity target feasible.
- Reuse alignment and morale data to influence hazard response without extra components.

### Testing Strategy

1. Unit tests for extraction rate decay and depletion handling.
2. Simulation tests verifying multi-carrier diminishing returns.
3. Stress tests measuring performance when thousands of carriers mine simultaneously.

## Examples

### Example Scenario 1

**Setup**: Carrier squad finds a rich asteroid belt with moderate hazards.  
**Action**: Deploy high-tier rigs and assign a combat escort to suppress pirate spawns.  
**Result**: Output spikes early, but escort upkeep shifts priorities once hazards escalate.

### Example Scenario 2

**Setup**: Player-modified start seeds sparse deposits but grants advanced rigs.  
**Action**: Single carrier rotates between deposits, timing hauls precisely.  
**Result**: Compact operation remains competitive through efficiency rather than volume.

## References and Inspiration

- **Sins of a Solar Empire**: Long-haul mining that demands defence.  
- **Factorio**: Resource depletion driving expansion.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-11-02 | Expanded hazards, rig types, component degradation, crew experience | Answered mechanics questions |
| 2025-10-31 | Initial draft | Captured foundational mining loop vision |

---

*Last Updated: November 2, 2025*  
*Document Owner: Design Team*
