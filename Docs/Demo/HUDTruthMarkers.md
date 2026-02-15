# HUD Truth Markers

## Must Show (for sim-truth footage)
- Tick/time
- Side0 alive count
- Side1 alive count

## Nice To Show (when available)
- Determinism digest (`space4x.battle.determinism.digest`)
- Combat totals: shots fired / shots hit

## Footage Acceptance Criteria
- No fake units in view (no fabricated combatants for visuals).
- Render proxies derive from simulation entities.
- HUD values correspond to live sim state at capture time.

## Quick Operator Rule
- If must-show markers are missing, do not record the final take.
