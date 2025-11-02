### Purpose
Product + UX contract for time control. Split policy (what) from mechanics (how).

### Contracts (APIs, events, invariants)
- Features: pause, slow/fast, step, rewind, resume, timeline branching.
- Controls (Input): Time/Pause, Time/RewindHold, Time/StepBack, Time/SpeedUp, Time/SpeedDown.
- Scope: gameplay rewinds; audio and post FX do not by default.
- Authority: time system owns simulation rate; individual systems read but do not author time.

### Priority rules
- Routing priority: UI > Modal tools > Time controls > RMB/LMB.

### Do / Don’t
- Do: Reflect state in TimeHUD (tick, speed, branch id, memory).
- Don’t: Let subsystems adjust Time.timeScale; use TimeEngine API.

### Acceptance tests
- UX: Pause/resume toggle; rewind hold ramps rate; step back decrements one tick; speed multipliers apply discretely.

### Defaults
- Fixed dt: 1/60 s. Snapshot cadence: every 30 ticks. Budget: 256 MB rolling.
- Snapshots LZ4-compressed; delta encode large arrays. Branch: keep last 3 branches.
- Rewind rate curve: -1× at 0.0 s → -8× at 1.5 s hold.

