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

**Current State (as of February 6, 2026)**
- Sim server targets ~2 TPS; time/rewind is PureDOTS-canonical and ScenarioRunner-driven.
- Empire directives and directive-to-goal mapping exist (directive buffers + goal resolver).
- Persistence plumbing exists (save slot bridge + sim server persistence systems).
- Headless diagnostics and perf gate coverage are strong for regression protection.

**Vision (mutable)**
- A living galaxy you can "raise" at 2 TPS: low-frequency but high-legibility state changes.
- Orders are the primary language: empire -> faction -> fleet -> captain -> execution.
- Long-horizon autonomy: fleets honor doctrine, morale, logistics, and risk while carrying orders.
- Deterministic rewind and traceable causality are normal workflow, not special tooling.
- Every order has observable feedback: who issued it, what changed, why it changed.

**Order Lifecycle Schema (MVP)**
- Issue: Empire directives created and persisted per faction.
- Translate: Directives map into faction goals (priority + scope).
- Expand: Goals generate concrete orders (captain/fleet/mission queues).
- Execute: Orders drive AI systems and produce measurable world changes.
- Report: Outcomes feed telemetry, contact ledgers, and persistence snapshots.

**Key Gaps**
- Order lifecycle is not end-to-end yet (issue -> report is incomplete).
- 2 TPS is applied but not fully "felt" (observability/feedback needs to be richer).
- Order outcome explanations are thin (why a goal failed is not always visible).

**Near-term Milestones (mutable)**
- Define and document the order queue schema used by captain/fleet execution.
- Add "order outcome" telemetry (success/fail + reason + tick span).
- Expose 2 TPS state deltas in a compact "tick summary" for headless and UI.
- Wire persistence snapshots to include order history and outcomes.
