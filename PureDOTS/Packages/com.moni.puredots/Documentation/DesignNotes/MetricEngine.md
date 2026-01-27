# Metric Engine & Analytics Concepts

## Goals
- Provide a shared event-to-metric pipeline supporting both games (Godgame, Space4x) for population, economy, combat, industrial, and narrative analytics.
- Maintain deterministic, incremental aggregations without per-frame recomputation.
- Support scoped metrics (Individual → Band → Village → Colony → Planet → System → Sector → Galaxy) with configurable cadences.

## Architecture Overview
1. **Events** (ground truth):
   - Append-only logs of births, deaths, production ticks, trades, facility upgrades, contracts, combat outcomes.
   - Each event carries `timestamp`, `scope_type`, `scope_id`, involved entity ids, quantity, unit, and payload (as struct/blob).
2. **State**:
   - ECS components reflecting current world (Population, Wealth, Inventory, FacilityStatus, BandMembership).
   - Updated each tick from systems (registries, production, AI).
3. **Metrics**:
   - Derived views computed incrementally from events/state.
   - Stored as time-bucketed values for different cadences (per tick, minute, hour, day, week).

## Metric Registry
- Central registry asset (`MetricRegistry` ScriptableObject) listing:
  - `metric_id`, `scope_type`, `unit`, `cadence`, `formula`, `dependencies`.
  - Example entries:
    ```yaml
    - id: galaxy_value
      scope: Galaxy
      unit: credits
      cadence: per_minute
      formula: sum(entity.market_value)
    - id: birth_rate_per_1k
      scope: Village
      unit: births_per_1k_pop_per_year
      cadence: daily
      formula: 1000 * births(365d) / avg(population, 365d)
    - id: gdp_per_capita
      scope: Colony
      unit: credits_per_person_per_year
      cadence: weekly
      formula: gdp_real(365d) / avg(population, 365d)
    - id: band_avg_will
      scope: Band
      unit: rating
      cadence: per_tick
      formula: avg(member.will)
    ```
- Provides help text, unit metadata, and ensures a single definition per metric.

## Aggregation DAG
- Build dependency graph among metrics (e.g., `gdp_per_capita` depends on `gdp_real` and `population`).
- Topologically order metrics per cadence to avoid circular dependencies.
- DAG nodes reference formulas implemented as deterministic evaluation jobs using state/event aggregations.

## Scope Hierarchy
- Maintain persistent scope tree (Individual→Band/Facility→Village→Colony→Planet→System→Sector→Galaxy).
- Events tagged with `scope_type`, `scope_id`.
- Bottom-up rollups by traversing ancestry; supports partial updates by dirty flags.

## Incremental Aggregation Techniques
- Running sums/counts for averages (update on entity join/leave or stat change).
- Variance/standard deviation via Welford’s algorithm (store `mean`, `m2`, `count`).
- Cumulative counters (e.g., `total_births`), store snapshots per cadence and compute deltas.
- Rolling windows:
  - Ring buffers per scope/metric (e.g., 365 daily buckets).
  - Optionally exponential moving averages for lightweight trends (store single value + alpha).
- Dirty flags: mark aggregates dirty when inputs change; recompute lazily.

## Cadence Scheduling
- Scheduler keyed by cadence (per tick/per minute/hourly/daily/weekly).
- For each cadence when due:
  1. Retrieve metric list in topological order.
  2. For each metric and scope, compute value using incremental data and dependencies.
  3. Write to metric fact table (time series).
- Avoid computing heavy metrics every frame; rely on cadenced tasks.

## Storage Layout
- **Event Log** (`events` table):
  - `(timestamp, event_type, scope_type, scope_id, actor_id, quantity, unit, payload)`.
  - Indexed by `(scope_type, scope_id, timestamp)` for replay/debugging.
- **Metric Facts** (`metrics` table):
  - `(ts_bucket, cadence, scope_type, scope_id, metric_id, value, version)`.
  - Unique index to allow rewrites when late events adjust history.
- **Running Aggregates** (`aggregates` table or component):
  - `(scope_type, scope_id, metric_id, sum, count, mean, m2, last_updated)`.
  - Stored in DOTS-friendly containers (SoA arrays/buffers) for runtime use.

## Example Calculations
- **Galaxy Total Value**:
  - Maintain per entity `market_value`. On change:
    ```
    delta = new_value - old_value;
    aggregates[entity, market_value].sum += delta;
    foreach ancestor scope (facility→village→…→galaxy):
        aggregates[scope, market_value].sum += delta;
    ```
  - Per minute, record metric: `metrics[galaxy_value, galaxy_id] = aggregate sum`.
- **Village Birth Rate**:
  - Log `birth` events with village scope.
  - Maintain ring buffer `births_by_day[village]` and `population_by_day[village]`.
  - Daily job: compute `births_365` (sum last 365), `pop_avg_365` (mean last 365).
  - Metric = `1000 * births_365 / max(1, pop_avg_365)`.
- **Colony GDP per Capita**:
  - On production event: value-added = output − input (using price index).
  - Aggregate weekly. Weekly job rolls 52-week window, deflates using price index metric.
  - `gdp_per_capita = gdp_real_365 / max(1, avg_population_365)`.
- **Band Average Will**:
  - Running sum/count on member changes.
  - Per tick metric read (no recompute).

## Handling Rolling Windows
- Exact windows: fixed-size ring buffer or queue per metric/scope.
- EMA: store previous value and apply `ema = alpha * x + (1 - alpha) * ema`.
- Choose based on fidelity vs memory/perf requirements; annotate in registry.

## Data Integrity
- Distinguish zero vs. unknown (null) values.
- Normalize currency/unit conversions before aggregation (PPP, exchange rates).
- Manage late events via watermarks (allow N-day delay). Recompute affected buckets and bump `version` in metric facts.
- Ensure rollups are idempotent; compute from child buckets rather than accumulating twice.

## Metric Engine Runtime
Pseudo-code:
```csharp
void RunMetricCadence(Cadence cadence, TimeBucket bucket)
{
    var metricsToRun = registry.GetByCadence(cadence);
    foreach (var metric in metricsToRun.TopologicalOrder())
    {
        foreach (var scope in ScopeProvider.GetScopes(metric.ScopeType))
        {
            var value = MetricEvaluator.Compute(metric, scope, bucket, aggregates, dependencies);
            MetricWriter.Upsert(scope, metric.Id, bucket, cadence, value);
        }
    }
}
```
- `MetricEvaluator` pulls from running aggregates, event buffers, or dependency metrics.
- Use Burst-friendly evaluators where possible; for complex formulas, precompile expression trees or use generated code.

## UI & Debugging
- Expose metric definitions, units, last inputs in UI (e.g., tooltip: `BirthRate = 12 births / avgPop 842`).
- Provide drill-down (galaxy→sector→colony→facility) with sparkline charts.
- Maintain `last_updated` timestamps, flag stale metrics.
- Offer developer tools to replay events and verify metric pipelines.

## Integration Hooks
- **Systems**: production chains, industrial sectors, population, economy update running aggregates through events.
- **Event System**: metrics can subscribe to specific event types via event brokers.
- **Scheduler**: reuse `SchedulerAndQueueing` to schedule metric cadences.
- **Telemetry/Analytics**: metrics feed dashboards or CI regression checks.
- **Narrative**: triggers when metrics cross thresholds (famine, boom).

## Testing
- Unit tests for metric formulas (simulated events, check outputs).
- Integration tests verifying DAG ordering and dependency resolution.
- Determinism tests with record/playback of events.
- Performance tests for high-frequency metrics (per tick) and high-scope counts.
