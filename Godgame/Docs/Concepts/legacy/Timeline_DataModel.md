### Purpose
Storage model for rewind/branch timeline and GC policy.

### Contracts (APIs, events, invariants)
- Model: snapshots every N ticks + command log for gaps.
- Branching: each rewind-and-act creates a new branch id; maintain head pointer.
- Retention: keep last X minutes or Y MB; GC strategy FIFO by branch priority.
- Snapshot cadence default: every 0.5 s (30 ticks at dt=1/60). Max sizes: ≤1 MB per snapshot; rolling ≤256 MB.

### Priority rules
- Prefer keeping active branch + two recent alternates.

### Do / Don’t
- Do: LZ4 compress; delta encode large arrays; checksum snapshots.
- Don’t: Store transient visuals.

### Acceptance tests
- GC retains within budget; branch switch preserves integrity; seek to tick reconstructs state.

