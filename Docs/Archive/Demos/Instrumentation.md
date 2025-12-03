# Demo Instrumentation & Reports

## Overview

The demo instrumentation system captures key metrics per slice, writes JSON/CSV reports, and stores artifacts for validation and analysis.

## Reporter System

### DemoReporter Component

Attached by `DemoBootstrap` to track metrics:

```csharp
public struct DemoReporter : IComponentData
{
    public FixedString64Bytes GameName;
    public FixedString64Bytes ScenarioName;
    public uint StartTick;
    public uint EndTick;
    public BlobAssetReference<DemoMetricsBlob> MetricsBlob;
}
```

### Metrics Collection

**Per-Slice Metrics**:

**Combat Duel**:
- `damage_total` - Total damage dealt
- `hits` - Number of hits
- `crit%` - Critical hit percentage
- `modules_destroyed{}` - Breakdown by module type
- `hull_HP` - Final hull health

**Mining Loop**:
- `throughput/s` - Resources per second
- `station_stock` - Station inventory
- `vessels_active` - Active mining vessels
- `resources_mined_total` - Total resources extracted

**Compliance Demo**:
- `sanctions_triggered` - Number of sanctions
- `reputation_delta` - Reputation change
- `safe_zone_violations` - Violation count

**Carrier Ops**:
- `modules_destroyed` - Modules destroyed
- `repair_time` - Total repair time
- `refit_count` - Number of refits
- `crew_buffs` - Crew modifier effects

**System Metrics** (all slices):
- `fixed_tick_ms` - Fixed step duration
- `snapshot_kb` - Snapshot ring buffer usage
- `alloc_bytes` - Memory allocation
- `tick` - Final simulation tick
- `fps` - Average frame rate

## Report Formats

### JSON Report

**Structure**:
```json
{
  "game": "Space4X",
  "scenario": "combat_duel_weapons.json",
  "timestamp": "2025-01-15T10:30:00Z",
  "seed": 12345,
  "bindings": "Fancy",
  "metrics": {
    "damage_total": 1250.5,
    "hits": 45,
    "crit_percent": 12.5,
    "modules_destroyed": {
      "engine": 1,
      "bridge": 0
    },
    "fixed_tick_ms": 8.5,
    "snapshot_kb": 256
  },
  "determinism": {
    "rewind_test_passed": true,
    "fps_variants": [30, 60, 120],
    "state_match": true
  }
}
```

**Location**: `Reports/<game>/<scenario>/<timestamp>_metrics.json`

### CSV Report

**Structure**:
```csv
metric,value,unit
damage_total,1250.5,float
hits,45,count
crit_percent,12.5,percent
fixed_tick_ms,8.5,milliseconds
snapshot_kb,256,kilobytes
```

**Location**: `Reports/<game>/<scenario>/<timestamp>_metrics.csv`

### Time Series CSV (Optional)

For detailed analysis:

```csv
tick,damage_total,hits,fixed_tick_ms
0,0,0,8.5
10,50,2,8.3
20,150,5,8.6
...
```

**Location**: `Reports/<game>/<scenario>/<timestamp>_timeseries.csv`

## Screenshots

### Capture Points

- **Scenario Start**: T=0s, before any actions
- **Scenario End**: T=end, final state
- **Key Events**: Optional captures at significant moments

### Screenshot Format

- PNG format
- Filename: `<timestamp>_<event>_<bindings>.png`
- Example: `20250115_103000_start_Fancy.png`

**Location**: `Reports/<game>/<scenario>/<timestamp>_screenshots/`

## Artifact Storage

### Directory Structure

```
Reports/
├── Space4X/
│   ├── combat_duel_weapons/
│   │   ├── 20250115_103000_metrics.json
│   │   ├── 20250115_103000_metrics.csv
│   │   ├── 20250115_103000_timeseries.csv
│   │   └── 20250115_103000_screenshots/
│   │       ├── start_Fancy.png
│   │       └── end_Fancy.png
│   └── mining_loop/
│       └── ...
└── Godgame/
    └── ...
```

### Artifact Naming

- Format: `<timestamp>_<type>_<variant>.<ext>`
- Timestamp: `YYYYMMDD_HHMMSS`
- Type: `metrics`, `timeseries`, `screenshot`
- Variant: `Minimal`, `Fancy`, `start`, `end`

## Telemetry Hooks

### Integration Points

**Mining Telemetry**:
```csharp
// From Space4XMiningTelemetrySystem
TelemetryMetric metric = new TelemetryMetric
{
    Key = "space4x.mining.oreInHold",
    Value = oreInHold,
    Tick = currentTick
};
```

**Combat Telemetry**:
```csharp
// From Space4XFleetInterceptTelemetrySystem
TelemetryMetric metric = new TelemetryMetric
{
    Key = "space4x.intercept.attempts",
    Value = interceptAttempts,
    Tick = currentTick
};
```

**Compliance Telemetry**:
```csharp
// From Space4XComplianceTelemetrySystem
TelemetryMetric metric = new TelemetryMetric
{
    Key = "space4x.compliance.sanctions",
    Value = sanctionsTriggered,
    Tick = currentTick
};
```

### Reporter Reads Telemetry

```csharp
// DemoReporterSystem reads from TelemetryStream
var telemetryStream = SystemAPI.GetSingleton<TelemetryStream>();
var metrics = telemetryStream.Metrics;

foreach (var metric in metrics)
{
    reporter.AccumulateMetric(metric.Key, metric.Value);
}
```

## Report Generation

### On Scenario End

1. Collect final metrics from telemetry
2. Aggregate per-slice metrics
3. Write JSON report
4. Write CSV report (if enabled)
5. Capture end screenshot
6. Validate determinism (if rewind used)

### CLI Report Generation

```bash
-executeMethod Demos.Build.Run --game=Space4X --scenario=combat_duel_weapons.json --report Reports/Space4X/combat_duel_weapons/latest.json
```

## Validation Reports

### Determinism Report

After rewind test:
```json
{
  "determinism": {
    "rewind_test_passed": true,
    "fps_variants": [30, 60, 120],
    "state_match": true,
    "byte_equal_check": true,
    "metrics_match": true
  }
}
```

### Binding Swap Report

After Minimal↔Fancy swap:
```json
{
  "binding_swap": {
    "swap_successful": true,
    "visuals_changed": true,
    "metrics_identical": true,
    "no_exceptions": true
  }
}
```

## Report Consumption

### CI Integration

CI systems can:
1. Parse JSON reports
2. Assert metrics meet thresholds
3. Compare against baseline
4. Generate trend reports

### Local Analysis

Developers can:
1. Load JSON/CSV in analysis tools
2. Compare runs across bindings
3. Validate determinism
4. Track performance trends

## Implementation Notes

- Reporter runs in `PresentationSystemGroup` (non-Burst)
- Metrics accumulated during simulation
- Reports written at scenario end
- Screenshots captured via Unity API
- Artifacts stored relative to project root

