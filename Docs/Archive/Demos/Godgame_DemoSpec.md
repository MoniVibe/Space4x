# Godgame Demo Specification

## Overview

Single scene demo showcasing deterministic villager loops, construction, and time control with rewind validation.

**Note**: Godgame is a sibling project. This document serves as a placeholder specification. Implementation details will be added when Godgame demo work begins.

## Demo Slices

### 1. Villager Loop

**Setup**: Idle → Navigate → Gather → Deliver

**Demonstrates**:
- Conservation counter in HUD
- State machine transitions
- Resource delivery

**Metrics**:
- Villager count
- Active jobs
- Items gathered
- Delivery count

### 2. Construction Ghost → Build

**Setup**: Construction ghost → build completion

**Demonstrates**:
- Tickets withdraw from storehouse
- On completion emits effect + telemetry bump
- Build progress tracking

**Metrics**:
- Build progress %
- Tickets consumed
- Builds completed

### 3. Time Demo

**Setup**: Record 5s; Rewind 2s; resim to same totals

**Demonstrates**:
- Rewind determinism
- Byte-equal check logged
- State restoration

**Metrics**:
- Rewind duration
- State match (byte-equal)
- Resim totals

### 4. Biome/Placeholder (Optional)

**Setup**: Swap palette to show environment hooks

**Demonstrates**:
- Visual palette swapping
- Environment system integration

## Hotkeys

| Key | Action |
|-----|--------|
| `P` | Pause/Play |
| `[` | Step back |
| `]` | Step forward |
| `1` | Speed ×0.5 |
| `2` | Speed ×1 |
| `3` | Speed ×2 |
| `B` | Swap Minimal/Fancy binding |
| `G` | Spawn ghost |
| `R` | Trigger rewind sequence |

## HUD Layout

### Left Panel (Game State)

- Villagers count
- Jobs active
- Storehouse inventory
- Build progress

### Right Panel (System Metrics)

- Tick
- FPS
- fixed_tick_ms
- Snapshot bytes
- ECB playback ms

## Acceptance Criteria

### Presentation-Driven

- Removing the PresentationBridge → sim runs; counters still tick
- Visuals swap without code changes

### Rewind Determinism

- Rewind run = same totals (log "deterministic OK")
- Byte-equal state verification
- Full cycle test passes

## Component Requirements

*To be specified when Godgame demo implementation begins.*

## System Dependencies

*To be specified when Godgame demo implementation begins.*

## Known-Good Scenarios

- `villager_loop_small.json` - 10 villagers, 1 storehouse, 2 nodes
- `construction_ghost.json` - 1 ghost, cost 100
- `time_rewind_smoke.json` - Scripted input for demo

## Talk Track (2-3 minutes)

"Here's the deterministic loop—input→sim→present. Watch as we rewind the last 2 seconds and replay to the exact same counters. Construction consumes tickets, and we can swap visuals without touching code."

