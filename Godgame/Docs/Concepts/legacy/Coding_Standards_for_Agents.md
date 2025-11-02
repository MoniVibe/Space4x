### Purpose
Operational coding standards for gameplay agents and handlers.

### Contracts (APIs, events, invariants)
- No `GetMouseButton`; use Input System actions.
- No side effects in `Started`; use `Performed` for work.
- Prefer dependency injection over `FindObjectOfType`.
- Public APIs require XML summaries.

### Priority rules
- Interfaces stable; evolve via new endpoints, not breaking changes.

### Do / Don’t
- Do: Fail fast with actionable, verbose errors; maintain environment hygiene.
- Don’t: Change versions inside fixes; avoid ad-hoc globals.

### Acceptance tests
- Static checks find banned APIs; build lints pass on public summaries.

