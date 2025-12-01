# Demo Preflight Validation

## Overview

The preflight validation pipeline ensures demo builds are ready for distribution. It validates prefab generation, determinism, budgets, and binding swaps before building.

## Preflight Entry Point

### CLI Method

```csharp
public static class Demos
{
    public static class Preflight
    {
        public static void Run()
        {
            // Parse --game=Godgame|Space4X
            // Run all validation steps
            // Log pass/fail
            // Exit with error code if failures
        }
    }
}
```

### Execution

```bash
-executeMethod Demos.Preflight.Run --game=Space4X
```

## Validation Steps

### 1. Prefab Maker Validation

**Purpose**: Ensure prefabs are generated correctly and idempotently.

**Steps**:
1. Run Prefab Maker in Minimal mode
2. Validate prefab generation:
   - Check prefab count matches expected
   - Verify prefab components are correct
   - Assert no missing references
3. Write idempotency JSON:
   - Hash of generated prefabs
   - Component counts per prefab
   - Reference checksums
4. Re-run Prefab Maker
5. Compare idempotency JSON (must match)

**Output**:
```json
{
  "prefab_maker": {
    "status": "pass",
    "prefab_count": 42,
    "idempotency_match": true,
    "checksum": "abc123..."
  }
}
```

**Failure Criteria**:
- Prefab count mismatch
- Missing components
- Idempotency mismatch

### 2. Determinism Dry-Runs

**Purpose**: Validate scenarios produce identical results at different frame rates.

**Steps**:
1. Load scenario
2. Run at 30Hz, capture final state
3. Run at 60Hz, capture final state
4. Run at 120Hz, capture final state
5. Compare states (byte-equal check)
6. Log pass/fail

**Scenarios Tested**:
- Short versions of known-good scenarios
- Focus on critical paths (combat, mining, compliance)

**Output**:
```json
{
  "determinism": {
    "status": "pass",
    "scenarios_tested": 4,
    "fps_variants": [30, 60, 120],
    "state_match": true,
    "byte_equal": true
  }
}
```

**Failure Criteria**:
- State mismatch between fps variants
- Non-deterministic behavior detected

### 3. Budget Assertions

**Purpose**: Ensure performance budgets are met.

**Fixed Tick Budget**:
- Assert `fixed_tick_ms ≤ target` (e.g., 16.67ms for 60Hz)
- Measure across entire scenario run
- Log max/average fixed_tick_ms

**Snapshot Ring Budget**:
- Assert snapshot ring usage within limits
- Check ring buffer capacity
- Verify no ring overflow

**Output**:
```json
{
  "budgets": {
    "status": "pass",
    "fixed_tick_ms": {
      "max": 12.5,
      "average": 8.3,
      "target": 16.67,
      "within_budget": true
    },
    "snapshot_ring": {
      "usage_kb": 256,
      "capacity_kb": 1024,
      "within_budget": true
    }
  }
}
```

**Failure Criteria**:
- `fixed_tick_ms > target`
- Snapshot ring overflow
- Memory allocation exceeds limits

### 4. Binding Swap Validation

**Purpose**: Ensure Minimal↔Fancy binding swap works without exceptions.

**Steps**:
1. Load scenario with Minimal bindings
2. Run for 10 seconds, capture metrics
3. Swap to Fancy bindings
4. Assert no exceptions
5. Run for 10 seconds, capture metrics
6. Compare metrics (must be identical)
7. Swap back to Minimal
8. Assert no exceptions

**Output**:
```json
{
  "binding_swap": {
    "status": "pass",
    "minimal_to_fancy": {
      "swap_successful": true,
      "no_exceptions": true,
      "metrics_match": true
    },
    "fancy_to_minimal": {
      "swap_successful": true,
      "no_exceptions": true,
      "metrics_match": true
    }
  }
}
```

**Failure Criteria**:
- Exception during swap
- Metrics mismatch after swap
- Visual bindings not applied

## Preflight Report

### Combined Output

```json
{
  "preflight": {
    "game": "Space4X",
    "timestamp": "2025-01-15T10:30:00Z",
    "status": "pass",
    "steps": {
      "prefab_maker": { ... },
      "determinism": { ... },
      "budgets": { ... },
      "binding_swap": { ... }
    }
  }
}
```

### Report Location

`Reports/<game>/preflight/<timestamp>_preflight.json`

## Integration with Build

### Pre-Build Hook

```bash
# Run preflight before build
-executeMethod Demos.Preflight.Run --game=Space4X
# If pass, proceed with build
-executeMethod Demos.Build.Run --game=Space4X --scenario=combat_duel_weapons.json
```

### CI Integration

CI pipeline:
1. Run preflight validation
2. Assert all steps pass
3. If pass, proceed to build
4. If fail, report failures and stop

## Failure Handling

### Partial Failures

If one step fails:
- Log failure details
- Continue other steps (for diagnostics)
- Exit with error code
- Report all failures

### Recovery Actions

Common failures and fixes:
- **Prefab Maker**: Re-run, check asset references
- **Determinism**: Check RNG seed, verify systems are deterministic
- **Budgets**: Optimize systems, reduce entity count
- **Binding Swap**: Check binding assets, verify presentation system

## Implementation Notes

- Preflight runs in Editor (not build)
- Uses same systems as runtime (determinism validation)
- Can run headless (batchmode)
- Reports stored for trend analysis
- Exit codes: 0=pass, 1=fail

