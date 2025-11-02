### Purpose
Single source for storehouse inventory and intake behavior.

### Contracts (APIs, events, invariants)
- API: `int Add(ResourceType,int)`, `int Remove(ResourceType,int)`, `int Space(ResourceType)`, totals accessors.
- Intake collider spec: front intake trigger; tap-dump on RMB.
- UI events: `OnTotalsChanged(ResourceType,int)`, `OnCapacityChanged`.
- Failure modes: full, type mismatch, blocked intake.

### Priority rules
- `Interaction_Priority.md`: StorehouseDump outranks PileSiphon.

### Do / Don’t
- Do: Validate capacity; emit events once per change; serialize totals.
- Don’t: Accept negative or cross-type.

### Acceptance tests
- Add within space → accepted; full → rejects and returns remainder; UI events fire once.

