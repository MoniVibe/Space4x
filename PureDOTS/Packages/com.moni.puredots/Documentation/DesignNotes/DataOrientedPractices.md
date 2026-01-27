# Data-Oriented Practices Checklist

- **Structure of Arrays (SoA/AoSoA)**: split hot component data (positions, state enums, timers) into contiguous arrays or subcomponents; keep cold/presentation data on parallel archetypes to minimize cache misses. Consider AoSoA layouts for frequently vectorized data (e.g., climate grids) when Burst-friendly.
- **Threading & Jobs**: default to Burst-compiled jobs (`IJobChunk`, `IJobEntity`, `IJobParallelFor`) with explicit dependency management. Document when systems must run on the main thread and why (e.g., graphics API calls).
- **Hot/Cold Archetype Separation**: maintain simulation-critical archetypes lean; attach diagnostics, presentation, or analytics components to companion entities or enable flags instead of mixing them into hot chunks.
- **Command Buffers & Pools**: use pooled `EntityCommandBuffer`, `NativeList`, and request queues to defer structural changes and avoid per-frame allocations.
- **Job-Friendly Data Access**: expose service queries through Burst-compatible structs (`ref readonly NativeArray`, `BlobAssetReference`) instead of managed collections.
- **Deterministic Schedules**: control system order via groups, avoid `WithoutBurst` fallback paths in hot loops, and ensure parallel jobs write to deterministic buffers using `ParallelWriter` and canonical sorting.
- **Tooling Hooks**: provide debug overlays and telemetry in cold systems that read shared buffers without disturbing hot archetypes.
