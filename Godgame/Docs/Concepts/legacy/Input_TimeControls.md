### Purpose
New Input System spec for time controls.

### Contracts (APIs, events, invariants)
- Actions: RewindHold (Hold), StepBack (Press), SpeedUp (Press), SpeedDown (Press), Pause (Press).
- Interactions: RewindHold ramps rewind rate by hold duration.
- Speed multipliers: 0, 0.25, 0.5, 1, 2, 4, 8.

### Priority rules
- Routing: UI > Modal tools > Time controls > RMB/LMB.

### Do / Don’t
- Do: Bindings documented with deadzones; route to TimeEngine only.
- Don’t: Let other systems consume these actions.

### Acceptance tests
- Each action triggers expected engine call; UI precedence blocks when over UI.

