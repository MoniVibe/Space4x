### Purpose
Classification of systems and their rewind obligations.

### Contracts (APIs, events, invariants)
- Pure-sim: deterministic from state + commands. Snapshot state root only.
- Record/Replay: non-deterministic; record outputs per tick (e.g., ragdolls if enabled).
- Visual-only: not rewound; re-tween from state on resume.

### Domain checklist
- Villagers, Storehouse, Piles, Hand, Construction, Combat:
  - State root path
  - What to snapshot
  - What to recompute on load
  - Events to suppress during rewind

### Priority rules
- Suppress gameplay events during rewind replay.

### Do / Don’t
- Do: Keep state roots compact and serializable.
- Don’t: Emit side-effects during Load.

### Acceptance tests
- No double-deliver; totals match after rewind sequences; conservation holds for piles/storehouse.

