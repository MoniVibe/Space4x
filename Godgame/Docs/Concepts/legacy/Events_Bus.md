### Purpose
Catalog of all events, payloads, emitters, and listeners with versioning policy.

### Contracts (APIs, events, invariants)
- Event list: name → payload schema → who emits → who listens.
- Versioning: additive only; deprecate with grace period; no breaking changes.
- No stringly one-offs: central registry only.

### Priority rules
- UI-critical events not throttled; telemetry behind flags.

### Do / Don’t
- Do: Strongly-typed channels; document threading model.
- Don’t: Emit from constructors or `Awake` before subscriptions exist.

### Acceptance tests
- Registry enumerates without duplicates; sample emit/listen roundtrips.

