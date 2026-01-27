# Scheduler & Queueing Concepts

## Goals
- Provide deterministic, service-wide scheduling and queueing utilities for long-running tasks (research, education, production, narrative countdowns).
- Ensure tasks integrate with rewind, registries, and analytics while leveraging Burst-friendly job execution.

## Core Components
- `ServiceSchedulerConfig`: singleton defining tick cadence (per service or shared), max tasks per frame, and catch-up behaviour.
- `ScheduledTask` buffer:
  ```csharp
  public struct ScheduledTask : IBufferElementData
  {
      public ServiceId Service;
      public TaskType Type;
      public Entity Subject;
      public float RemainingTime; // seconds or ticks
      public ushort Priority;
      public TaskStateFlags Flags;
  }
  ```
- `TaskQueueRegistry`: deterministic registry storing pending tasks per service, exposing sorted buffers by priority and submission tick.

## Systems
- `TaskSubmissionSystem`: services enqueue tasks with metadata (priority, prerequisites). Uses pooled buffers and command writers.
- `TaskSchedulerSystem`:
  - Runs after submissions within a dedicated `SchedulerSystemGroup`.
  - Decrements `RemainingTime` using service cadence; selects ready tasks based on priority, prerequisites, and resource availability.
  - Emits completion events via `EventSystemConcepts.md`.
- `TaskExecutionAdapters`: service-specific systems respond to completion events (e.g., finalize research, advance education tier, finish ship construction).
- `QueueInstrumentationSystem`: logs queue depth, wait times, throughput to telemetry.

## Integration
- **Production Chains**: queue production orders with skill/facility gates; scheduler ensures parallel recipes respect worker availability.
- **Education/Tech**: long-term tasks (semesters, research projects) use scheduler ticks; can pause/resume based on narrative situations or resource shortages.
- **Narrative Situations**: countdown timers for crises or opportunities; scheduler emits warning events at thresholds.
- **Military**: mobilization queues, training durations, logistics deployments.
- **Analytics**: use telemetry service to track average wait times and backlog per service.

## Authoring & Baking
- Task definitions authored through `TaskProfile` ScriptableObjects linking to service enums, base durations, and prerequisites.
- Bakers compile profiles into blob assets consumed by submission systems.

## Performance
- Store tasks in SoA/AoSoA buffers; update in parallel via `IJobChunk` or `IJobParallelFor`.
- Use stable sorting (priority, submission tick) to keep determinism.
- Allow services to request per-task worker thread counts for heavy jobs.

## Rewind & Persistence
- Record task state deltas in history buffers; on rewind, restore queues and remaining time.
- Ensure scheduler deterministic ordering across record/playback by basing decisions solely on sorted buffers and explicit priority values.

## Testing
- Add edit-mode tests covering task submission, priority ordering, and completion under deterministic seeds.
- Add playmode tests verifying integration (e.g., production orders completing in expected ticks after pausing/resuming).
