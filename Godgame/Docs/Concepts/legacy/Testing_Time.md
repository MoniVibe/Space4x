### Purpose
Acceptance tests for time determinism, rewind, and performance.

### Contracts (APIs, events, invariants)
- Determinism: same commands → same snapshot checksum on two runs.
- Rewind/branch: record 60 s, rewind 10 s, issue new command, diverge checksums.
- Memory: 10 min run ≤ budget.
- Performance: ≤ 1 ms per snapshot at cadence, ≤ 0.2 ms per tick logging on target PC.
- Domain: villager loop no double-deliver; piles/storehouse conserve resources.

### Priority rules
- Time tests run with fixed dt and identical seeds.

### Do / Don’t
- Do: Table-driven tests; assert budgets and checksums.
- Don’t: Use wall-clock in assertions.

### Acceptance tests
- Listed above serve as pass/fail gates before enabling rewind.

