# State Machine Framework Concepts

## Goals
- Provide deterministic, data-driven state machines for gameplay services (divine hand, miracles, tech research, elite governance).
- Keep state logic Burst-friendly and compatible with rewind/history capture.
- Enable authoring via blobs/ScriptableObjects so designers adjust transitions without code changes.

## Core Components
- `StateMachineDefinition` (blob):
  - `BlobArray<StateNode>` nodes with entry/exit events, timers, payload references.
  - `BlobArray<Transition>` edges with guard conditions, priority, and event hooks.
- `StateMachineComponent`:
  - `StateId CurrentState`, `StateId PendingState`, `float StateTime`, `ushort Flags`.
  - Optional `DynamicBuffer<StateMachineEvent>` for emitted events per entity.
- `StateMachineContext`:
  - Packed references to registries, services, or buffers required by guard/effect jobs.

## Update Pipeline
1. **Guard Evaluation Job**: iterates transitions for entities in parallel (`IJobChunk`), evaluates guard conditions (data-driven thresholds, event receipts) using Burst-compatible delegates or function enums.
2. **Transition Resolution**: selects highest-priority satisfied transition, writes `PendingState`, and emits queued events into pooled buffers.
3. **State Application System**: applies `PendingState`, resets timers, runs entry actions (via state effect tables), and writes history samples for rewind.

## Authoring & Baking
- `StateMachineAuthoring` ScriptableObject defines nodes, transitions, guard parameters. Baker converts to `StateMachineDefinition` blob and attaches to entities via conversion systems.
- Provide validation tools (duplicate transitions, unreachable states) and preview graphs in editor.

## Integration Patterns
- **Services**: tech, education, elites reference the framework by storing `StateMachineComponent` on service entities and running service-specific guard jobs.
- **Events**: integrate with `EventSystemConcepts.md` to publish transition events and react in other systems (e.g., analytics, narrative).
- **Hot/Cold Splits**: keep state components on hot archetypes; presentation hooks read state via cold companion entities.

## Testing
- Add edit-mode tests that instantiate state machine blobs, step through transitions with deterministic inputs, and assert expected states/events.
- Add playmode tests verifying service integration (e.g., education pipeline transitions from `Enrollment` → `Study` → `Graduation` under simulated scheduler ticks).
