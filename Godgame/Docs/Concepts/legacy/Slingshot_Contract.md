### Purpose
Define hand slingshot behavior: aim, charge, release, and filtering.

### Contracts (APIs, events, invariants)
- Requires: `Hand.Holding != null`. Error if not.
- API: `BeginAim(ctx)`, `UpdateAim(dt)`, `Release(ctx)`, `Cancel()`.
- Ray: from hand to cursor; charge curve maps time→speed.
- Hit filtering: layer mask `Hand.SlingshotMask` and `IThrowableReceiver` only.

### Priority rules
- Routed by RMB. Preempted by UI and ModalTool; preempts siphon when Holding.
- Cancel if `Holding == null` at any phase.

### Do / Don’t
- Do: Use router probe for aim seed; clamp charge; idempotent updates.
- Don’t: Instantiate per frame; don’t throw without cargo.

### Acceptance tests
- No object in hand → BeginAim throws structured error and returns false.
- Charge 0.5 s → speed matches curve value; release spawns projectile or applies impulse.
- Filter denies hits outside mask; Cancel leaves state unchanged.

