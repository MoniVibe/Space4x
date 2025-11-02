### Purpose
Contracts for aggregate resource piles and storehouse.

### Contracts (APIs, events, invariants)
- Pile API: `Initialize(ResourceType,PileConfig)`, `int Add(int)`, `int Take(int)`, `int Amount {get;}`.
- Storehouse API: `int Add(ResourceType,int)`, `int Remove(ResourceType,int)`, `int Space(ResourceType)`.
- Merge rules: same type, within `mergeRadius`, cap by `maxUnitsPerPile`.
- Size curve drives visuals; serialization keys: `{type, amount, position}`.

### Priority rules
- Interaction precedence per `Interaction_Priority.md`. StorehouseDump > PileSiphon.

### Do / Don’t
- Do: Clamp per-frame transfers; pool piles; emit UI events on change.
- Don’t: Negative amounts; duplicate raycasts.

### Acceptance tests
- Add to near-cap pile → overflow spawns new pile.
- Siphon rate frame-independent; totals identical at 30 vs 120 FPS.

