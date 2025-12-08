# Mining Loop

**Status:** Implemented  
**Category:** Economy  
**Classification:** `shared-core-with-variants`  
**Created:** 2025-12-07  
**Last Updated:** 2025-12-07  
**Owner:** Space4X Design

---

## Feature Overview

**High-Level Fantasy:** Carriers run industrial mining sorties, strip asteroids/fields efficiently, and keep the haul flowing without micromanagement.  
**Purpose:** Provide a reliable early/mid-game economy backbone and a pacing lever for progression/tech diffusion.  
**Player Impact:** Players schedule sorties, tune risk vs. yield, and juggle cargo/escort to keep production stable while fleets stay mobile.

---

## Scope & Non-Goals

### In Scope (v1)
- Mining job acquisition from registered nodes (asteroids/fields) with carrier assignment rules.
- Yield generation, chunking, and transfer into haulers/cargo holds with degradation/overfill handling.
- Risk hooks (piracy/hostile events) and efficiency modifiers (crew alignment, module quality, tech).

### Out of Scope (v1)
- Planetary drilling and refinery mini-games.
- Crew-level skill trees beyond alignment modifiers.
- Presentation/VFX; handled in game layer only.

---

## Shared vs Game-Specific Classification

- **Shared core (PureDOTS):** Mining session schema, yield tick logic, degradation/overfill rules, registry integration, telemetry hooks.  
- **Variants (Space4X):** Carrier job selection, escort risk rules, cargo routing, faction-specific yields and anomalies.  
- **No UI/presentation** in core; all UI lives in Space4X layer.

---

## Systems & Flow (Condensed)

1) Discover eligible nodes → score by distance/risk/yield.  
2) Assign carrier + hauler → spawn mining session (`MiningSession`, `MiningYieldBuffer`).  
3) Tick extraction → apply module/crew modifiers → emit resource chunks.  
4) Transfer to cargo; handle overfill/degradation; log telemetry.  
5) On completion/interrupt → update registry, free carrier, trigger routing.

---

## Integration & Dependencies (Key)

- **Registry:** Uses resource node registry + logistics routes.  
- **Modules:** Mining module quality influences rate/stability; ties to Module System doc.  
- **Alignment:** Crew alignment modifies risk discipline and uptime.  
- **Tech:** Tech diffusion unlocks module tiers and efficiency modifiers.  
- **AI:** Fleet AI respects mining jobs as economic objectives.

---

## Open Questions / Next Pass

- Target sync cadence between mining tick and logistics updates (keep under 3 ms/frame bus cost).  
- Do we need adjustable risk appetites per carrier role (core/escort)?  
- Where to surface scarcity/overheat warnings in UI without spamming?  
