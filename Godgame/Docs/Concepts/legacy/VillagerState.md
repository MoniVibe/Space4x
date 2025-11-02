### Purpose
Finite state machine spec for villagers: exact states, transitions, and guards.

### Contracts (APIs, events, invariants)
- States: Idle, Navigate, Gather, Carry, Deliver, Interrupted.
- Events: `OnStateChanged(from,to)`. Guard methods: `HasPath`, `HasCapacity`, `HasResource`.

### Priority rules
- Interrupts preempt current state; resume policy defined by job.

### Do / Don’t
- Do: Declare transitions centrally; validate guards before switching.
- Don’t: Do side effects in guard checks.

### Acceptance tests
- Gather with capacity → Carry; path blocked → Interrupted; delivery succeeds → Idle.

