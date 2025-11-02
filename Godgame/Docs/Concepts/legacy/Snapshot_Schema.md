### Purpose
Exact on-disk/in-memory format of snapshots.

### Contracts (APIs, events, invariants)
- Header: version, tick, branch, checksum.
- Chunks: World meta, RNG, Entities (Archetype→array), Storehouse totals, Piles {id,type,amt,pos}, Villagers {id,state,job,target,virtualCarry}, Hand {type,amt}, etc.
- Binary layout, endianness, compression (LZ4), diffing strategy.
- Back-compat policy: additive; readers tolerate older versions.

### Priority rules
- Keep per-snapshot ≤ 1 MB; rolling budget ≤ 256 MB on Windows.

### Do / Don’t
- Do: Include physics state if physics is authoritative.
- Don’t: Store transient VFX/audio.

### Acceptance tests
- Snapshot writes/reads roundtrip; checksum matches across runs.

