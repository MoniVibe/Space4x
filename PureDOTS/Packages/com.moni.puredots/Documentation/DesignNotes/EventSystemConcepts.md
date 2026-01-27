# Event & Transition System Concepts

## 1. Deterministic Event Buffers
- Maintain per-service event buffers (`DynamicBuffer<EventRecord>`) populated through pooled command writers.
- `EventRouterSystem` runs after producing systems, copies events into global `EventStreamRegistry`, and clears producers with `Dependency.CompleteDependencyBeforeRO`.
- Include `Tick`, `Source`, `PayloadType`, `PayloadIndex` fields to keep replay/rewind deterministic; payloads live in blob-backed pools.

## 2. State Transition Graphs
- Represent long-running process states (e.g., tech research phases, elite politics) as data-driven graphs (`BlobAssetReference<StateGraph>`).
- Each node defines entry/exit conditions, events to emit, and side-effects (component flags). Graph execution jobs advance state per tick and enqueue events when transitions occur.

## 3. Event Subscription & Filtering
- Provide `EventQueryUtility` to filter streams by tick range, service, or payload type; expose Burst-friendly iterators so systems can consume without managed allocations.
- Support per-frame “hot” consumption and deferred “cold” logging via separate buffers to avoid cache pollution.

## 4. Transition Scheduling
- Align transition evaluation with service schedulers (tech, education, diplomacy) to avoid redundant evaluations. Use `UpdateInGroup` attributes to enforce ordering relative to other services.

## 5. Authoring & Debugging
- Author events/transition graphs via ScriptableObjects (`EventDefinition`, `StateGraphDefinition`) baked into blobs.
- Extend debug tooling to replay recent events, visualize current state nodes, and annotate transitions for designers.
