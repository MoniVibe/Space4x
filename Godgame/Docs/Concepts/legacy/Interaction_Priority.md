### Purpose
Global order for interaction handlers. Used by RMB router and LMB tools.

### Contracts (APIs, events, invariants)
- Order (top wins): UI > ModalTool > StorehouseDump > PileSiphon > Drag > GroundDrip > Fallback.
- Same list consumed by router and tools; keep single source.

### Priority rules
- Tie-breaks: keep current winner until CanHandle=false for N frames.
- Cooldown: optional 0.1 s after cancel.

### Do / Don’t
- Do: Reference this file for handler priorities; keep numbers aligned.
- Don’t: Define ad-hoc local priority tables.

### Acceptance tests
- Table-driven: each context selects expected handler; ties respect hysteresis.

