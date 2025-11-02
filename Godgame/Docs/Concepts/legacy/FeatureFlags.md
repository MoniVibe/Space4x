### Purpose
Gate new mechanics and provide safe rollback.

### Contracts (APIs, events, invariants)
- Config path: `Assets/Configs/FeatureFlags.asset` or Resources lookup.
- API: `bool IsEnabled(string key)`, `Enable/Disable` in editor only.
- Rollback: document revert steps per flag.

### Priority rules
- Flags evaluated early; default to safe behavior.

### Do / Don’t
- Do: Guard risky handlers and mechanics; log decisions early.
- Don’t: Ship flags that change save format without migration.

### Acceptance tests
- Flag toggles alter behavior deterministically; rollback steps succeed.

