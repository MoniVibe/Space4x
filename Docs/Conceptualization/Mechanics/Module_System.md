# Module System

**Status:** Implemented  
**Category:** Equipment  
**Classification:** `shared-core-with-variants`  
**Created:** 2025-12-07  
**Last Updated:** 2025-12-07  
**Owner:** Space4X Design

---

## Feature Overview

**High-Level Fantasy:** Carriers refit like modular capital ships—swap bays, rigs, and support packages to pivot roles quickly.  
**Purpose:** Enable fast role changes (mining, hauling, strike) without new hulls, and provide degradation/maintenance as a pacing lever.  
**Player Impact:** Players trade uptime vs. performance; choose loadouts that balance mining throughput, cargo, escorts, and risk mitigation.

---

## Scope & Non-Goals

### In Scope (v1)
- Slot/size rules for carrier hardpoints; validation of compatible modules.
- Degradation, repair, and refit time costs; partial failure hooks.  
- Configurable stat deltas (yield rate, cargo, speed, detection, alignment impact).

### Out of Scope (v1)
- Cosmetic-only modules (pure VFX).  
- Cross-faction black-market modules (future hook only).  
- Player-authored modules; only configured catalog.

---

## Shared vs Game-Specific Classification

- **Shared core (PureDOTS):** Module schema, degradation/repair math, slot validation, telemetry.  
- **Variants (Space4X):** Carrier-specific slot map, faction module catalog, alignment modifiers, refit UI/flow, economic costs.  
- **Presentation:** All UI/VFX/audio live in game layer.

---

## Systems & Flow (Condensed)

1) Choose loadout → validate slot/size → schedule refit with time/cost.  
2) Apply stat deltas and degradation timers to carrier sim state.  
3) Tick degradation; route failures to downtime/repair queue.  
4) Telemetry records uptime, MTBF, and refit frequency for balancing.

---

## Integration & Dependencies (Key)

- **Mining Loop:** Module quality gates extraction stability/throughput.  
- **Alignment:** Risky/black-ops modules shift crew compliance.  
- **Logistics:** Refit downtime blocks haul schedules; hooks to route planner.  
- **Tech Diffusion:** Unlocks new module tiers; controls degradation curves.

---

## Open Questions / Next Pass

- How aggressive should degradation be vs. tech diffusion pace?  
- Do we expose preconfigured loadout templates per role to reduce micromanagement?  
