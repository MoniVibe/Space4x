### Purpose
Define what persists and back-compat guarantees for save files (v1).

### Contracts (APIs, events, invariants)
- Persist: piles {type, amount, position}, villagers {state, job}, storehouse {totals}, hand {held type/amount}.
- Keys and versions; upgrade notes to v2.

### Priority rules
- Backward-compatible reads; forward-compatible write with version tag.

### Do / Don’t
- Do: Validate on load; repair or reject with clear errors.
- Don’t: Serialize transient visuals.

### Acceptance tests
- Save→Load idempotence; v1 file loads after upgrades via path.

