# Crew Alignment & Compliance

**Status:** Implemented  
**Category:** Social / Morale  
**Classification:** `game-specific`  
**Created:** 2025-12-07  
**Last Updated:** 2025-12-07  
**Owner:** Space4X Design

---

## Feature Overview

**High-Level Fantasy:** Crews follow captains they trust—mutiny, desertion, and compliance pressure change how carriers perform.  
**Purpose:** Add a social risk axis that interacts with loadouts, missions, and tech pacing.  
**Player Impact:** Players manage morale/leverage: pick missions and modules that keep crews loyal while pushing productivity.

---

## Scope & Non-Goals

### In Scope (v1)
- Alignment axes (discipline, loyalty, ideology) affecting uptime, risk-taking, and mutiny thresholds.  
- Event hooks for morale shocks (losses, overwork, harsh modules, hostile space).  
- Compliance gating: missions/modules require minimum alignment; failure leads to refusal/desertion.

### Out of Scope (v1)
- Detailed interpersonal relationships between individual crew.  
- Narrative chains; kept to systemic events.  
- Visual crew management UI beyond summaries.

---

## Classification Notes

- Pure gameplay layer; no agnostic core beyond basic stat modifiers.  
- Uses PureDOTS registries for crew state storage, but rules/content are Space4X-specific.  
- Presentation/UI fully game-side.

---

## Systems & Flow (Condensed)

1) Evaluate alignment state per carrier → derive compliance score.  
2) Apply modifiers to mission success chance, risk tolerance, and uptime.  
3) Trigger morale events from combat losses/overwork/refit hardships.  
4) Resolve thresholds: refusal → mission abort; mutiny/desertion → carrier lost or captured.  
5) Recovery via rest periods, leadership bonuses, or ideological alignment.

---

## Integration & Dependencies (Key)

- **Module System:** Harsh modules penalize morale; supportive modules ease fatigue.  
- **Mining Loop:** Overwork and hostile mining zones drive morale shocks.  
- **Tech Diffusion:** Ideology shifts based on tech choices; unlocks morale tools.  
- **AI/Registry:** Crew alignment stored in registries; influences fleet AI risk posture.

---

## Open Questions / Next Pass

- How quickly can morale recover without trivializing risk?  
- Do we need faction-specific ideology curves or keep uniform?  
