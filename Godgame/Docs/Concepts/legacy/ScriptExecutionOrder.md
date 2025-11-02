### Purpose
Explicit order for input, router probes, physics, and visuals.

### Contracts (APIs, events, invariants)
- Order list: InputSystem → ProbeContext → Router dispatch → Feature updates → Visuals.
- Physics vs visuals: raycasts before visual effects.

### Priority rules
- Router Update before per-feature visuals; UI module first.

### Do / Don’t
- Do: Configure Script Execution Order; document numbers.
- Don’t: Depend on undefined order between systems.

### Acceptance tests
- Sanity test: router wins consistent handlers under contention.

