### Purpose
Single source for how villagers work across AI, jobs, and inventories.

### Contracts (APIs, events, invariants)
- APIs: `AssignJob(JobType, target)`, `CancelJob()`, `OnResourceDelivered(ResourceType,int)`.
- Events: `OnJobStarted`, `OnJobCompleted`, `OnInterrupted(reason)`.
- Invariants: One job at a time; virtual carry separates from world aggregates.

### Priority rules
- Jobs honor global interaction priority when competing with player hand.

### Do / Don’t
- Do: Use storehouse as single write path for totals; respect pathing hooks.
- Don’t: Directly mutate pile amounts; avoid FindObjectOfType.

### Acceptance tests
- Gather→Deliver→Idle loop completes; ownership and interrupts handled; totals reconcile in storehouse.

