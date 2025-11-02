# PureDOTS Template Vision

## Purpose
PureDOTS is a reusable Unity Entities template meant to accelerate new simulation-heavy games while enforcing a pure DOTS, deterministic-first mindset. It offers a minimal but opinionated stack (time/rewind spine, baseline simulation loops, authoring assets, CI hooks) that teams can extend without inheriting legacy MonoBehaviour baggage. Every addition should preserve determinism, data-oriented layouts, and flexibility so multiple genres—city builders, survival sims, RTS, or hybrids—can share the same foundation.

## Design Pillars
- **Deterministic Core** – The custom `TimeState`/`RewindState` backbone, fixed-step bootstrap, and history tiers remain authoritative. New systems must respect the record/playback/catch-up routing and be safe under rewind.
- **DOTS-Only Simulation** – Systems reside in explicit groups, compiled with Burst, and avoid hybrid bridges in hot paths. Presentation hooks live in optional companion worlds or cold archetypes.
- **Flexibility by Configuration** – All subsystems (time tuning, physics backend, spatial partitioning, authoring) expose ScriptableObject configs and bakers. No hard-coded dependencies; swapping implementations mid-project should be a config change plus system registration.
- **Scalability Discipline** – The template must comfortably handle 50k–100k complex entities by default. Architectural choices, code generation, and authoring UX should assume SoA layouts, hot/cold splits, and job-friendly patterns.
- **Observability & Automation** – Built-in debugging HUDs, logging switches, and CI scripts ensure teams can profile, test, and ship deterministically.

## Core Systems Overview
1. **Time & Rewind Spine**
   - `PureDotsWorldBootstrap` constructs the world, aligns time steps, and wires custom groups.
   - `TimeTickSystem`, `RewindCoordinatorSystem`, and routing groups manage deterministic progression, pause, rewind, and catch-up phases.
   - History tiers and sample buffers define snapshot cadence; ensure new domains declare their tier and guards.
2. **Baseline Simulation Domains**
   - Minimal villager/resources loop demonstrates job assignment, gathering, deposit, and needs management without service locators.
   - Systems are split into deterministic groups (`VillagerSystemGroup`, `ResourceSystemGroup`, etc.) to illustrate scheduling best practices.
3. **Authoring & Config Assets**
   - `PureDotsRuntimeConfig`, `ResourceTypeCatalog`, and baker components translate ScriptableObject data into DOTS singletons/buffers.
   - Template scene showcases the canonical authoring flow (config GameObject, sub-scenes, prefabs).

## Modularity Expectations
- **Physics** – Entities Physics ships by default, but a physics interface layer should gate all runtime queries. Provide ScriptableObject-driven options (e.g., `PhysicsConfig` asset) that register one of:
  1. Pure Entities Physics (default)
  2. Havok-backed deterministic variant
  3. Stub/no-physics mode for non-physical prototypes  
  Swapping implementations should involve updating config + enabling/disabling corresponding system groups.
- **Spatial Partitioning** – Default spatial grid targets fast proximity queries for AI and physics broad-phase. The design must allow live replacement (grid size, quadtree, BVH, or GPU-accelerated structures) via config assets and baker flags. Systems should consume abstracted lookup components/buffers rather than hard-coded grid references.
- **Presentation Bridges** – Rendering, VFX, and UI components attach through separate archetypes or companion entities. Simulation state lives in “hot” archetypes; presentation bits stay “cold”. Bridges subscribe to deterministic events or read buffers; they must tolerate rewind (either regenerate visuals every frame or honor `PlaybackGuardTag`).
- **Input & Command Routing** – Time control commands already flow through buffers. Future input systems (player, AI directors, network replay) must stage requests into the same DOTS-friendly command buffers.

## Performance & Data Guidelines
- **Hot/Cold Archetypes** – Keep simulation-critical components on slim archetypes. Store presentation, analytics, or infrequent data on parallel archetypes or child entities. Document recommended splits per domain (e.g., `VillagerState` vs. `VillagerPresentation`).
- **Chunk Utilization Targets** – Aim for 60–80% chunk occupancy for hot archetypes. Monitor chunk fragmentation with Entities Hierarchy tools; avoid component combinations that explode archetype counts.
- **Burst & Jobs First** – Systems should default to `ISystem` + Burst jobs. Structural changes happen via `EntityCommandBuffer` (deferred to end of frame) to keep the main thread clean.
- **Threading Management** – Use explicit scheduling groups to control dependencies, avoid full sync points, and document when main-thread-only work is unavoidable. Provide samples for customizing worker counts and thread affinities via Unity’s job configuration.
- **Memory & Cache Awareness** – Encourage developers to store large arrays in `BlobAssetReference` or shared static data. Use `NativeList`/`NativeQueue` pools instead of repeated allocations.
- **Diagnostics** – Ship profiling presets (Entity Debugger filters, Performance Reporting guidelines) and encourage automated soak tests to verify 100k entity loads with rewind enabled.

## Authoring & Workflow
- **Scriptable Configs Everywhere** – Time, physics, spatial grid, resource catalogs, and gameplay parameters live in assets referenced by bakers. Runtime overrides can be staged through DOTS singletons or config buffers.
- **Template Scene & SubScenes** – Maintain a “golden” template scene demonstrating best practices (config object, sample SubScenes, and minimal conversion steps). Newly generated projects should duplicate this scene as a starting point.
- **Testing Hooks** – `PureDOTS` editor menu triggers play/edit-mode tests. Extend CI scripts to accept scenario names, stress multipliers, and soak durations.
- **Documentation Source of Truth** – `Docs/EnvironmentSetup.md`, `Docs/TestingGuidelines.md`, and this vision file must evolve together. Any new subsystem ships with a corresponding doc fragment.

## Roadmap
1. **Short Term**
   - Finalize physics abstraction (config assets, system registration patterns).
   - Implement modular spatial grid service with runtime-configurable cell sizes and alternative providers.
   - Add automated performance suite (headless playmode run that spawns 100k archetype mix, captures timings, validates determinism under rewind).
2. **Medium Term**
   - Introduce service-free registries (resource, villager, construction) using DOTS buffers + query APIs only.
   - Stand up presentation bridges (entities graphics, UI HUD) that respect hot/cold separation and rewind guards.
   - Expand authoring UX (custom inspectors, validation tooling, sample SubScenes per domain).
3. **Long Term**
   - Package optional gameplay modules (combat, economy, weather) implemented with the same modular rules.
   - Provide migration scaffolds for teams porting legacy MonoBehaviour content (conversion scripts, compatibility layers).
   - Release benchmarking reports & guidelines for different hardware tiers, keeping entity targets realistic and measurable.

## Adoption Checklist
1. Clone the template and review `Docs/` for environment setup, testing, and vision alignment.
2. Configure runtime assets (time/rewind, physics provider, spatial partitioning) to match the project’s genre.
3. Define hot/cold archetypes for new entity categories before writing systems.
4. Implement new systems within existing groups or register new groups with explicit ordering.
5. Enable automated tests/soak runs early and keep results in CI dashboards.
6. Update `PureDOTS_TODO.md` and `Docs/Progress.md` as domains evolve to maintain traceability back to this vision.

Keep every addition modular, deterministic, and data-oriented—so the template stays lean yet powerful for any future PureDOTS-powered game.
