# Headless & Presentation Progress (Space4X)

*Archived 2025-12-23 - Stale progress report (older than 3-4 days). See current HEADLESS_PROGRESS.md for latest status.*

## Canonical Scenario & Showcase Scene (Space4X)

- **Headless proof**: `space4x_mining_combat.json` (`space4x/Assets/Scenarios/space4x_mining_combat.json`) driven by the Space4X headless command in `Docs/Headless/headless_runbook.md`.
- **Showcase scene**: `Assets/Scenes/TRI_Space4X_Smoke.unity` is the canonical "Headless Progress" scene.
  - Uses the same scenario/config SubScenes as the headless run; no separate "headless-only" scene is allowed.
  - When a new behavior is proven in headless for this scenario (e.g., mining loop, carrier motion, combat events), add the minimal presentation hook here (entities, overlays, debug UI).
  - Keep RenderCatalog + PresentationLayerConfig + camera wiring identical between headless bootstrap and this scene so presenter diagnostics and smoke logs line up.

## Incremental Feature Mirroring Rules

- Headless first: new simulation features and proofs land in the `space4x_mining_combat.json` scenario and its headless run.
- Presentation follows: for each such feature, extend `TRI_Space4X_Smoke` to visualize it without changing the scenario JSON.
- Avoid forks: if you need additional visual-only helpers, add them as extra authoring/prefabs in the smoke scene, not by cloning scenarios or creating special headless scenes.

## Target Smoke Content (Vision)

- World: a few carriers operating in a sector with resource fields and hostile contacts, all spawned and controlled by headless systems for `space4x_mining_combat.json`.
- Entities: carriers, mining vessels, strike craft, asteroids/resource nodes, and enemy forces—all created by real gameplay systems, never editor-only spawn hacks.
- Behavior: carriers deploy miners to harvest resources; strike craft launch automatically when enemies are nearby; combat outcomes and mining yields must match what headless telemetry reports.
- Constraint: if an entity/interaction is not present in the headless run for this scenario, it must not be "faked" in `TRI_Space4X_Smoke` beyond neutral debug overlays.

## Parity Checklist

When adding a new feature to headless, ensure it appears in presentation:

1. **Headless first**: Implement the feature in `space4x_mining_combat.json` scenario or related systems under `space4x/Assets/Scripts/Space4x`.
2. **Prove in headless**: Run headless command from `Docs/Headless/headless_runbook.md` and verify the feature appears in telemetry/logs.
3. **Extend diagnostics**: Update `Space4XSmokeWorldCountsSystem` and `Space4XSmokePresentationCountsSystem` to track the new entity/behavior type.
4. **Wire presentation**: Ensure rendering systems (`Space4XPresentationLifecycleSystem`, etc.) map the new entities to `RenderSemanticKey` values.
5. **Update smoke scene**: Add minimal visual hooks in `TRI_Space4X_Smoke` (entities, overlays, debug UI) - no new spawners, only visualization of what headless produces.

### Expected Entity Set (Current)

- **Carriers**: Spawned by scenario JSON with `ResourceStorage`, `Fleet`, `Combat` components.
- **Mining Vessels**: Spawned by scenario JSON, deployed by carriers to harvest resources.
- **Strike Craft**: Launched automatically when enemies are nearby (via `Combat` component on carriers).
- **Resource Deposits/Asteroids**: Spawned by scenario JSON.
- **Enemies**: Hostile carriers/fleets spawned by scenario JSON.

### Parity Violations to Avoid

- ❌ Spawning fallback entities when SubScene fails to load (use `Space4XSmokeFallbackSpawnerSystem` to log errors instead).
- ❌ Creating entities in `TRI_Space4X_Smoke` that don't exist in headless runs.
- ❌ Using MonoBehaviours that create gameplay entities solely for visual effect.
- ❌ Hardcoding mining/combat behaviors that don't reflect underlying sim state.
- ✅ Only visualization: camera rigs, UI overlays, debug HUDs that read from sim components.



