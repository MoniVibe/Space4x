### Purpose
Rules all systems must follow for deterministic simulation.

### Contracts (APIs, events, invariants)
- One tick authority: `SimTick` integer, fixed Δt only. `SimTime.Now = tick * dt`.
- Ban wall-clock: no `Time.time`, `Time.deltaTime`, `DateTime.Now` in sim code.
- Random: `DetRand(tick, entityId)` or seeded PRNG with recorded seeds per tick.
- Physics: fixed timestep locked; no auto-substeps; no continuous collision unless recorded.
- IO isolation: external inputs become timestamped commands.

### Priority rules
- Time system drives ticks; subsystems are passive and pure per tick.

### Do / Don’t
- Do: Keep sim pure functions of (state, commands, tick).
- Don’t: Emit gameplay events during Load replay.

### Acceptance tests
- Same commands → identical snapshot checksums across two runs.

