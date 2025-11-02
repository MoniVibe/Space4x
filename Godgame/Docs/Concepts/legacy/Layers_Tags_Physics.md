### Purpose
Canonical layers, tags, collision matrix, and raycast masks.

### Contracts (APIs, events, invariants)
- Layer registry: Piles, Storehouse, Ground, Villager, HandOnly, UI.
- Collision matrix: declare allowed pairs; include link/screenshot if needed.
- Raycast masks by feature: RMB router InteractionMask, SlingshotMask, SiphonMask.

### Priority rules
- No ad-hoc layers; all masks declared here.

### Do / Don’t
- Do: Validate masks at startup; fail fast with actionable error.
- Don’t: Inline numeric layer constants.

### Acceptance tests
- Linter validates existence and matrix expectations; router masks equal truth list.

