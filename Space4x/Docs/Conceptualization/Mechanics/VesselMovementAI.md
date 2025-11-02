# Mechanic: Vessel Movement & Formation AI

## Overview

**Status**: Concept  
**Complexity**: Complex  
**Category**: AI / Navigation

**One-line description**: Movement paths depend on stance, class, and alignment—aggressive routes punch through, defensive routes detour, formations reflect individual and aggregate outlooks.

## Route Planning

- **Stance-Based**: Aggressive vessels take direct routes through hazards; defensive stances plot safer paths around threats.
- **Class Sensitivity**: Haulers and civilian carriers favor secure corridors; war fleets tolerate higher risk, evaluating threat levels and ignoring trivial dangers.
- **Threat Assessment**: Fleets continuously assess encountered threats and choose to reroute, hold, or press on based on stance and tolerance.
- **Child Vessel Tethering**: Fighters, escorts, and drones remain within tether ranges unless explicit patrol orders are assigned; larger child vessels (corvettes, gunboats) can roam on independent patrol loops.

## Formation Behavior

- Formation tightness stems from individual and aggregate outlooks/alignment.
- **Lawful**: Maintain disciplined formations; chaotic members default to structure but may deviate.
- **Chaotic**: Break formation frequently; aggregate cohesion moderates deviation.
- Formation adjustments occur when stance changes (aggressive tightens for strike runs, defensive widens for coverage).
- Child vessels adopt escort formations tied to role: PD drones orbit in panes (lawful) or loose swarms (chaotic); strike wings form sorties; logistics shuttles queue in staggered lines.

## In-Transit Responses

- Facing threats mid-route: aggressive fleets engage or push through; defensive fleets reroute or request escorts.
- Vessels can broadcast assistance requests via mission board or command interface automation policies.
- Child vessels auto-return when ammunition, fuel, or hull integrity dip below thresholds—values tuned by outlook/alignment. They also recall on stance change, jump prep, or direct orders.
- Scout drones expand sensor radius proportional to captain perception; stationary recon missions increase detection layers.
- Target prioritization mirrors captain outlook: lawful defenders prioritize screening friendly assets, warlike lawful strike the highest-threat hulls or weapons systems, chaotic craft pick dynamic targets (nearest or weakest) unless overridden by the carrier.

## Integration Touchpoints

- **AI Commanders**: Movement decisions align with captain outlook and service traits.
- **Fleet Composition**: Docking capacity and escort slots influence route selections.
- **Logistics Warfare**: Safe-route planning critical for haulers to avoid supply disruption.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Captured vessel movement behaviors based on stance, class, and alignment |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
