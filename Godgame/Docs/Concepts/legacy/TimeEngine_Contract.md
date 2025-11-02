### Purpose
API contract for the time engine used by all systems.

### Contracts (APIs, events, invariants)
- ITimeAware: `OnTick(tick)`, `Save(ref Writer)`, `Load(ref Reader)`, `OnRewindStart/End()`.
- ITimeInput: register command streams (`Enqueue(tick, cmd)`), query current branch.
- Global services: `TimeEngine.GoTo(tick)`, `StepBack(steps)`, `SetSpeed(mult)`, `Pause(bool)`.
- Events: `RewindStarted`, `RewindFinished`, `TickChanged`.

### Priority rules
- TimeEngine owns tick progression; systems must not mutate it.

### Do / Don’t
- Do: Use commands to cross subsystem boundaries; keep tick pure.
- Don’t: Leak commands from future branches.

### Acceptance tests
- GoTo/StepBack produce consistent states; Pause/Resume do not advance tick; SetSpeed scales smoothly.

