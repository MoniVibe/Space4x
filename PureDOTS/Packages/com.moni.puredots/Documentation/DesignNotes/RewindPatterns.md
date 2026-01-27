# Rewind Patterns

_Updated: 2025-10-25_

This note captures the approved rewind strategies for Pure DOTS systems. All new systems must adopt one (or a combination) of these approaches so record/playback stays deterministic across the full simulation stack.

## Snapshot Cadence
- Use fixed tick cadence derived from `HistorySettings` (default: every 5 seconds, critical systems: 1 second).
- Store compact snapshots in dynamic buffers (`HistorySample<T>` or specialised structs) with tick + payload.
- Prefer summarised data (min/avg/max) for large grids; `GridHistorySample` exists for environment blobs, recording `TerrainVersion` to detect invalidated snapshots.

## Command Replay
- Systems driven by explicit inputs should record commands instead of full state when the state can be deterministically rebuilt.
- Persist commands in chronological order, and include enough context to rebuild in a single pass.
- Divine Hand + miracles share `InteractionHistorySample` to capture state machine transitions, resource payloads, and flags.

## Deterministic Rebuild
- When the runtime state can be derived from a deterministic transform, only record the seed + version data.
- Sort entity sets before playback using `Entity.Index`/`Entity.Version` to guarantee stable iteration (utility helper pending implementation in `TimeAware.cs`).
- For spatial or registry rebuilds, record the version plus any non-deterministic deltas (e.g., manual overrides).

## Implementation Guidelines
- Each system participating in rewind should implement `Record`, `Playback`, and `CatchUp` routines, exposed either through partial `ISystem` sections or extension helpers.
- Update buffers in `HistorySystemGroup` only; gameplay systems should queue samples and flush during their `Record` stage.
- Use `LastRecordedTick` to avoid duplicate writes within the same tick.
- When terrain or grid versions change, increment `TerrainVersion` and push a `GridHistorySample` even if data is unchanged (so playback detects version hops).

## Shared History Structures
- `GridHistorySample`: for environment grids, stores the snapshot tick, terrain version, and statistical summary of sampled values.
- `InteractionHistorySample`: single source of truth for hand interactions; downstream systems replay against this rather than duplicating bespoke history.
- Extend this catalogue when adding cross-system data to avoid fragmented history formats.

Keep this document aligned with `HistoryComponents.cs` and update both when adding new rewind-friendly structures.
