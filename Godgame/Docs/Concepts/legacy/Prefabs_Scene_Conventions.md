### Purpose
Conventions for prefabs and scene objects to ensure consistency.

### Contracts (APIs, events, invariants)
- Root components required; `visualRoot` child; colliders/nav setup.
- Naming and variant policy; pooling rules; serialization expectations.

### Priority rules
- Scenes must not override core interaction layers or masks.

### Do / Don’t
- Do: Keep root transforms clean; pool hot-spawned prefabs.
- Don’t: Add gameplay scripts to `visualRoot`.

### Acceptance tests
- Audit passes: roots, naming, layers, pooling markers present.

