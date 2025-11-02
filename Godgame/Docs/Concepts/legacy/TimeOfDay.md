### Purpose
Deterministic, tick-derived time-of-day for lighting, schedules, and AI.

### Contracts (APIs, events, invariants)
- Source of truth: `TimeEngine.Tick` and `TimeSettings.ticksPerDay`.
- API (service subscribes to `TimeEngine.OnPostTick`):
  - `int DayIndex` (≥1)
  - `float DayFraction01` (0..1)
  - `int Hour24` (0..23), `float HourFloat` (0..24)
  - `bool IsDaytime`, `bool IsNighttime`
  - `event Action<int> OnNewDay`
  - `event Action<float> OnDayFractionChanged` (optional throttled)
  - `event Action OnSunrise`, `event Action OnSunset`
- Config (extend `TimeSettings`):
  - `int ticksPerDay` (default 3600 for 1-minute days at 60 Hz; tune per design)
  - `float sunriseHour` (default 6f), `float sunsetHour` (default 18f)

### Math
- `dayTick = TimeEngine.Tick % ticksPerDay`
- `DayFraction01 = (float)dayTick / ticksPerDay`
- `HourFloat = DayFraction01 * 24f`
- `IsDaytime = HourFloat ∈ [sunriseHour, sunsetHour)`

### Invariants
- Pause freezes day progression; rewind reverses it; speed changes scale it.
- No wall-clock usage. No dependency on `Time.timeScale`.

### Usage
- Lighting: drive sun rotation, ambient intensity, and exposure from `DayFraction01`.
- AI: rest/work conditions query `IsNighttime`/`IsDaytime`.
- UI: show DayIndex and clock computed from `HourFloat`.

### Migration
- Remove `World/DayNightCycle.cs` and replace with a `TimeOfDay` service that subscribes to ticks.
- Update `Villagers/BehaviorTree/Conditions.IsNightTime` to query the service instead of `Time.time`.
- Any timers impacting sim should move to tick counters or `ITimeAware`.

### Acceptance tests
- Over a simulated day: exactly one `OnSunrise`, one `OnSunset`, one `OnNewDay`.
- Pause: no events, no fraction change.
- Rewind: events reverse appropriately; `DayFraction01` matches tick-derived value.


