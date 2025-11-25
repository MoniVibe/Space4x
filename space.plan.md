<!-- e01b47b3-9bdf-4d1c-90d1-e12f62f20836 80e506b2-5bed-459a-8445-92ce692ae316 -->
# Demo Readiness Closure Plan

## 1. Burst & System Quality Audit

- Review all newly added systems (`Space4XRefitScenarioSystem`, `Space4XRefitScenarioActionProcessor`, `FacilityProximitySystem`, `ModuleCatalogBootstrapSystem`, `Space4XModuleRatingAggregationSystem`).
- Ensure `[BurstCompile]` attributes where appropriate and no managed objects are used per ECS best practices.
- Confirm scheduling is in the correct system groups (Initialization, Simulation, FixedStep) and `state.RequireForUpdate` guards exist.
- Validate that buffer lookups/lifetimes are correct (update before use, no captured TimeState references).

## 2. Catalog Assets & Authoring Validation

- Use the editor script `Tools/Space4X/Create Refit Catalog Assets` to generate the ScriptableObject assets under `Assets/Data/Catalogs/`.
- Verify the baker outputs (`ModuleCatalogSingleton`, `HullCatalogSingleton`, `RefitRepairTuningSingleton`) produce identical data to the hard-coded bootstrap.
- Document how to regenerate/modify the catalogs for designers.

## 3. Scenario Loader & Action Processor Testing

- Implement unit/integration tests:
- JSON parsing and spawn logic (carrier entity count, modules installed correctly).
- Action processing: degrade, repair, move, refit (using deterministic data).
- Run the actual scenario via ScenarioRunner (batchmode) and capture logs.
- Verify telemetry outputs (offense/defense ratings, refit/repair counts, CSV/JSON export).

## 4. Telemetry & HUD Wiring

- Ensure `ModuleRatingAggregate` feeds stats into `Space4XRegistrySnapshot` or telemetry metrics.
- Surface minimal HUD/debug output (offense/defense/utility, power balance) using existing systems or debug logs.
- Confirm `Space4XRegistryBridgeSystem` picks up module hull snapshots if required.

## 5. Documentation & TODO Updates

- Update `Docs/TODO/AgentB_Modules_Degradation.md` and `Docs/Progress.md` with current status.
- Add instructions for running the scenario (`Unity -batchmode ... --scenario Assets/Scenarios/space4x_demo_refit.json`).
- Document the sequence of degrade -> repair -> refit and expected telemetry.

## 6. Final Demo Verification

- Run in-editor PlayMode to ensure no errors and systems behave correctly.
- Run ScenarioRunner command to produce CSV/JSON output and validate against expectations.
- Capture a short screen recording or log excerpt showing offense rating improves after refit and power balance remains non-negative.

## 7. Prefab Maker — Data-Driven Prefab Generation

### 7.1 Editor Tool Implementation

- Create `PrefabMakerWindow` editor window (`Tools/Space4X/Prefab Maker`) with three tabs:
  - **Batch Generate**: Generate/update prefabs from catalogs
  - **Adopt/Repair**: Scan existing prefabs, add missing components/sockets, fix naming/paths
  - **Validate**: Run lints and export report
- Implement dry-run mode with deterministic diff output (console + JSON)
- Add filters (by family, size, updated only)
- Provide clear error messages for invalid items

### 7.2 Authoring Components

- Create `HullIdAuthoring` component: `{ FixedString32Bytes Id }`
- Create `ModuleIdAuthoring` component: `{ FixedString32Bytes Id }`
- Create `HullSocketAuthoring` component: spawns child empties with naming pattern `Socket_<MountType>_<Size>_<Index>`
- Create `MountRequirementAuthoring` component: `{ MountType, MountSize }`
- Create `StationIdAuthoring` component: `{ FixedString32Bytes Id }`
- Create optional `StyleTokensAuthoring`: `{ byte palette, byte roughness, byte pattern }`
- All components must bake cleanly to ECS (IDs + style tokens only)

### 7.3 Prefab Generation Logic

- **Hull Prefabs**: Generate under `Assets/Prefabs/Space4X/Hulls/<HullId>.prefab`
  - Add `HullIdAuthoring` with catalog ID
  - Add `HullSocketAuthoring` that creates child transforms for each catalog slot
  - Socket naming: `Socket_<MountType>_<Size>_<Index>` (e.g., `Socket_Weapon_M_01`)
  - Include local orientation axes for attachments
  - Optional `StyleTokensAuthoring`
  - Use primitive placeholder meshes (Capsule/Cube)

- **Module Prefabs**: Generate under `Assets/Prefabs/Space4X/Modules/<ModuleId>.prefab`
  - Add `ModuleIdAuthoring` with catalog ID
  - Add `MountRequirementAuthoring` matching catalog entry
  - Optional `StyleTokensAuthoring`
  - Use primitive placeholder meshes

- **Station Prefabs**: Generate under `Assets/Prefabs/Space4X/Stations/<StationId>.prefab`
  - Add `StationIdAuthoring`
  - Optional `RefitFacilityTag`, `FacilityZone` (placeholder sphere)
  - Use primitive placeholder meshes

- **FX/HUD Stubs**: Generate under `Assets/Prefabs/Space4X/FX/<EffectId>.prefab`
  - Minimal primitive + billboard
  - No gameplay logic

### 7.4 Binding Blob Generation

- Create/update `Space4XPresentationBinding` blob asset at `Assets/Space4X/Bindings/Space4XPresentationBinding.asset`
- Build from current prefabs every run:
  - `ModuleId → prefab ref + style`
  - `HullId → prefab ref + socket map`
  - `EffectId → fx prefab ref`
- Support two sets: `GrayboxMinimal` and `GrayboxFancy` (switchable at runtime)
- Ensure idempotency: re-running with unchanged catalogs produces no diffs (same GUIDs/contents)

### 7.5 Validation & Linting

- **Socket layout parity**: Hull sockets match catalog slots (type/size/count)
- **Module fit**: Each module prefab's `MountRequirement` consistent with catalog entry
- **Orphan check**: IDs in catalogs must have prefabs (or be flagged)
- **Path & naming**: Enforce folder layout and PascalCase/kebab-case rules
- **Idempotency**: Re-run yields zero asset diffs (except timestamp-only changes)
- **Binding integrity**: Every binding entry points to existing prefab; no duplicates
- Optional: Power/mass sanity checks (from catalog into prefab meta)

### 7.6 CLI & CI Integration

- Add CLI entry point: `-executeMethod Space4X.PrefabMaker.Run --dryRun`
- Generate JSON summary per run: items created/updated, hashes, warnings
- Support batchmode execution for CI validation

### 7.7 Tests & Acceptance Criteria

- **PrefabMaker_Batch_Idempotent**: Run generator twice; compare content hashes → equal
- **PrefabMaker_HullSockets_MatchCatalog**: Every hull has expected sockets (type/size/count)
- **PrefabMaker_ModuleFit_Valid**: All modules' mount data validate against catalog
- **Binding_Blob_Parity**: Binding entries equal prefab set; unknown IDs fail fast with clear error
- **Graybox_Swap_Optionality**: Swapping `GrayboxMinimal` ↔ `GrayboxFancy` changes look without gameplay code changes

### Definition of Done

- Tool generates all hulls/modules/stations/FX listed in current catalogs
- Binding Blob builds; runtime can spawn by ID without any art assets present
- All tests green; dry-run + JSON report available; re-run is idempotent
- CLI entry point works for CI

### To-dos

- [x] Assess current Space4x codebase status
- [x] Draft next-phase plan & placeholder presentation
- [x] Add module/hull/tuning schemas + facility tags
- [x] Implement catalog/tuning authoring Scriptables & blobs
- [x] Add FacilityProximity + update refit gating
- [x] Implement refit/repair logic + telemetry
- [x] Add refit scenario assets + action handlers
- [x] Add tests + docs updates
- [ ] Implement Prefab Maker editor window
- [ ] Create authoring components (HullId, ModuleId, HullSocket, MountRequirement, StationId, StyleTokens)
- [ ] Implement prefab generation logic (hulls, modules, stations, FX)
- [ ] Create binding blob system
- [ ] Add validation/linting system
- [ ] Add CLI entry point
- [ ] Create tests (idempotency, sockets, module fit, binding parity, graybox swap)

