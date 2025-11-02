### Purpose
Single source defining Divine Hand state machine and legality of throws.

### Contracts (APIs, events, invariants)
- Public API: `HandState Current`, `bool CanThrowNow(RmbContext? ctx)` answers “can we throw now?” deterministically.
- Events: `OnHandTypeChanged(ResourceType?)`, `OnHandAmountChanged(int current,int capacity)`, `OnStateChanged(HandState from, HandState to)`.
- Invariants: Only one held type at a time; timers advance only while active; no side-effects in state queries.

### Priority rules
- Input routing via `RMBtruthsource.md`. Hand states must not bypass router. UI always wins.
- Throw is gated by: holding cargo, aim valid, cooldown elapsed, masks permit hit.

### Do / Don’t
- Do: Centralize transitions in one component; validate guards before mutating.
- Don’t: Call `Input.GetMouseButton`; don’t mutate amounts in `Started`.

### Acceptance tests
- Given Empty → Holding{Wood} on pickup; guard prevents cross-type.
- Dragging ends if release or higher-priority handler wins; state returns to Holding.
- SlingshotAim requires Holding and aim hit on allowed mask; `CanThrowNow` true only after min charge.
- Dumping runs while RMB Performed over Storehouse; zeroes hand on Canceled.

### States & Transitions
- Empty → Holding{type} [guard: pickup succeeds]
- Holding{type} → Dragging [guard: drag handler wins]
- Holding{type} → SlingshotAim [guard: slingshot enabled + aim valid]
- Holding{type} → Dumping [guard: storehouse under cursor]
- Dragging → Holding{type} [guard: release or cancel]
- SlingshotAim → Holding{type} [guard: cancel]
- SlingshotAim → Empty/Holding{type} [guard: release; apply throw result]
- Dumping → Holding{type}/Empty [guard: transfer result]

### Shared timers
- `cooldownAfterThrowSeconds`
- `minChargeSeconds`, `maxChargeSeconds`
- `hysteresisFrames` for handler tie-breaks

### Guards (centralized)
- `HasCargo(type?)`, `HasCharge`, `CooldownElapsed`, `AimHitValid(LayerMask)`

