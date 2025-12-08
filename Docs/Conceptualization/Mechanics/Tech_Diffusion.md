# Tech Diffusion

**Status:** Implemented  
**Category:** Progression  
**Classification:** `shared-core-with-variants`  
**Created:** 2025-12-07  
**Last Updated:** 2025-12-07  
**Owner:** Space4X Design

---

## Feature Overview

**High-Level Fantasy:** Breakthroughs spread through the fleet over time—research sparks, then diffuses to carriers and colonies.  
**Purpose:** Replace instant unlocks with paced rollout; create strategic timing choices for refits and deployments.  
**Player Impact:** Players plan around staggered access; early adopters take risks, late adopters get stability.

---

## Scope & Non-Goals

### In Scope (v1)
- Diffusion curve per tech (lead time, decay, saturation); supports prioritization.  
- Carrier/colony adoption checkpoints that gate module availability and efficiency buffs.  
- Hooks for alignment/morale impacts (new tech anxiety vs. enthusiasm).

### Out of Scope (v1)
- Global research tree redesign; uses existing tree as source.  
- Tech theft/espionage mechanics.  
- Presentation beyond progress indicators.

---

## Shared vs Game-Specific Classification

- **Shared core:** Diffusion math (curves, saturation), adoption thresholds, telemetry.  
- **Variants (Space4X):** Which techs diffuse, gating to carrier modules/crew morale, UI pacing, narrative hooks.  
- **No UI** in core; rollout visuals belong to Space4X layer.

---

## Systems & Flow (Condensed)

1) Research completes → seed diffusion wave with priority.  
2) Tick diffusion per region/fleet → compute adoption probability by distance/priority.  
3) Unlock modules/bonuses when adoption threshold reached; update registries.  
4) Apply morale modifiers for early/late adoption; feed telemetry for balancing.

---

## Integration & Dependencies (Key)

- **Module System:** Controls when tiers are usable; affects degradation curves.  
- **Alignment:** Early adoption risk vs. crew comfort.  
- **Mining/Logistics:** Throughput changes alter economic pacing.  
- **AI:** Fleet AI considers adoption state before committing to risky missions.

---

## Open Questions / Next Pass

- Do we need regional diffusion dampening for frontier vs. core?  
- Should diffusion speed be influenced by player-controlled relays?  
