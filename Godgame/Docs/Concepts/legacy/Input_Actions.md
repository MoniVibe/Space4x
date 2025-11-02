### Purpose
Canonical action maps, bindings, and interaction contracts.

### Contracts (APIs, events, invariants)
- Action maps: `UI`, `Gameplay`. Single `RightClick` action in `UI` or shared.
- Interactions: Press(behavior=1), Hold, Release. Deadzones documented per axis.
- Tools subscribe via `PlayerInput` or injected `InputActionReference`.

### Priority rules
- UI precedence; no legacy Input API usage.

### Do / Don’t
- Do: Centralize action references; disable legacy Input.
- Don’t: Read `Mouse.current` directly.

### Acceptance tests
- RightClick triggers Started/Performed/Canceled; UI module blocks when over UI.

