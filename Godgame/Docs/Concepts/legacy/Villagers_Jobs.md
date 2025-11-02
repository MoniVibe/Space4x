### Purpose
Authoritative job graph and ownership rules.

### Contracts (APIs, events, invariants)
- Graph: Gather → Deliver → Idle. Hooks for pathing, interrupts.
- Ownership: resources carried virtually; single write path to storehouse.
- Events catalog: `JobAssigned`, `JobProgress`, `JobCompleted`, `JobInterrupted`.

### Priority rules
- Player interactions take precedence in shared targets; villagers yield.

### Do / Don’t
- Do: Emit storehouse updates only via Storehouse API; cancel on higher-priority claims.
- Don’t: Directly modify piles; duplicate routing logic.

### Acceptance tests
- Job completes under normal conditions; interrupts pause and resume correctly; totals consistent.

