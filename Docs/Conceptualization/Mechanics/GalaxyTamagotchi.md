# Galaxy Tamagotchi (Space4X 2 TPS Sim)

**Definition**
Galaxy tamagotchi is the Space4X simulation running at roughly 2 ticks per second, intended for long-horizon, observable state evolution with low-frequency updates.

**Goals**
- Maintain a stable ~2 TPS cadence for the core sim.
- Keep time/rewind deterministic and ScenarioRunner-driven.
- Make state changes easy to observe and reason about at low cadence.

**Invariants**
- PureDOTS time/rewind is canonical.
- ScenarioRunner drives time; no game-local time systems.
- Time control semantics follow `Docs/Conceptualization/Mechanics/TimeControl.md`.

**Where TPS is applied**
- `space4x/Assets/Scripts/Space4x/SimServer/Space4XSimServerSettings.cs` (default TPS target).
- `space4x/Assets/Scripts/Space4x/SimServer/Space4XSimServerBootstrapSystem.cs` (applies time scale).
- PureDOTS runtime config asset location: see `space4x/Assets/Data/README.md`.

**Operational checklist**
- ScenarioRunner wiring matches `Docs/PureDOTS_ScenarioRunner_Wiring.md`.
- Time integration follows `Docs/PureDOTS_TimeIntegration.md`.
- Sim server target is 2 TPS and applied at bootstrap.

**Non-goals**
- No per-scene or ad hoc time overrides.
- No MonoBehaviour forcing of ECS systems into the world.
