### Priority
- Numeric priority. Higher wins. Reference `Interaction_Priority.md`.

### CanHandle(ctx)
- Conditions in terms of a shared context probe. No side effects.

### OnRmb(phase)
- Started: what is locked or initialized.
- Performed: time-based work, idempotent per tick.
- Canceled: commit/rollback.

### Masks & Raycasts
- Required layers/tags and how to obtain hits (prefer router probe).

### Side-effects
- Allowed state changes and forbidden ones.

### Tests
- Table-driven: context → should win.
- Phase tests: Started/Performed/Canceled sequencing.

