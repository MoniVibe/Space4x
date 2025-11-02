### Purpose
Canonical hints, errors, and SFX event names to avoid inconsistent strings.

### Contracts (APIs, events, invariants)
- Hints: Ground, Pile same-type, Storehouse intake.
- Errors: Type mismatch, capacity full, invalid target.
- SFX events: accept, deny, drip, siphon, dump.

### Priority rules
- UI copy aligns with `Terminology_Glossary.md`.

### Do / Don’t
- Do: Centralize keys; localize via one system.
- Don’t: Inline hard-coded strings.

### Acceptance tests
- All handlers source copy from this registry; deny cases fire correct SFX.

