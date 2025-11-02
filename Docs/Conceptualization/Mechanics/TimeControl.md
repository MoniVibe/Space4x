# Mechanic: Time Control & Rewind

## Overview

**Status**: Concept  
**Complexity**: Moderate  
**Category**: Systems / UX

**One-line description**: The simulation runs on a central PureDOTS time tick with pause, slow motion, fast-forward, and rewind controls to help players manage large empires.

## Core Concept

Time advances through a deterministic central tick aligned with the Unity/PureDOTS foundation. Players (and AI debugging tools) can manipulate pacing to inspect states, issue orders, and correct mistakes.

## Control Modes

- **Pause**: Freezes simulation, letting players inspect entities, queue orders, and review intel.
- **Slow Motion**: Runs at fractional speed for precise command timing during crises.
- **Fast Forward**: Speeds through low-intensity periods to reach key milestones quickly.
- **Rewind**: Steps backward along stored timeline checkpoints, enabling do-overs or forensic analysis.

## Implementation Notes

- Central tick stores deltas in time-state buffers; rewind replays or restores snapshots.
- Tick modifiers must stay deterministic for multiplayer or replays.
- Events and situations register their own keyframes so rewinding preserves consistent outcomes.

## Integration Touchpoints

- **Situations**: Rewind interacts with ongoing situations—rewinding before escalation allows alternative choices.
- **Combat Loop**: Slow-mo assists with tactical engagements; fast-forward accelerates travel between battles.
- **Tech Progression**: Diffusion timers adjust with pacing to avoid exploits (rewind may incur stability penalties).
- **AI Commanders**: AI decisions must re-evaluate after rewind to prevent desync.

## Tuning Guidance

- Limit rewind depth or charge a resource to avoid trivializing risk.
- Provide clear visual feedback for current time mode and upcoming checkpoints.
- Allow hotkeys/controller mappings for quick time control adjustments.

## Revision History

| Date | Change | Reason |
|------|--------|--------|
| 2025-10-31 | Initial draft | Defined time control and rewind system |

---

*Last Updated: October 31, 2025*  
*Document Owner: Design Team*
