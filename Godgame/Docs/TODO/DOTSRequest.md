# DOTS Requests

Central log of work items that need review/coordination on the DOTS side. Update alongside feature docs when new systems or engine hooks are required.

## Divine Hand MVP

- Review proposed Interaction systems (`RightClickProbeSystem`, `RightClickRouterSystem`, `DivineHandStateSystem`, `HandCarrySystem`, `HandSlingshotSystem`, `HandDumpSystem`) for Burst compatibility and schedule.
- Confirm resource/storehouse DOTS components (`AggregatePile`, `GodgameStorehouse`) expose the APIs and buffers needed by the hand loop.
- Validate telemetry integration against PureDOTS expectations before wiring HUD bindings.
- Pending fixes from initial review:
  - [x] Right-click probe must only enqueue handlers when targets pass affordance checks (storehouse intake, pile, ground).
  - [x] Hand component requires a dedicated cooldown duration; `DivineHandStateSystem` currently reuses `MinChargeSeconds`.
  - [x] Slingshot charge should start at 0 on entry; clamp against the live accumulator instead of a stale snapshot.
- Dedicated prompt for the DOTS agent lives in `Docs/TODO/DOTS_Prompt.md` (include when handing off).
