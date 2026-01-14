# Smoke Scene Render Parity Checklist

**Purpose:** Ensure presentation reflects simulation truth (render==sim) and remains scalable at 100k–1M entities.

## Pre-Flight Checks

### Catalog Setup

- [ ] `Space4XRenderCatalog_v2.asset` exists and is referenced by scene
- [ ] Theme 0 maps every required semantic key (`Space4XRenderKeys.*`) to a **non-zero** variant index (variant 0 is fallback)
- [ ] Fallback mesh/material are assigned in catalog asset
- [ ] `RenderPresentationCatalogAuthoring` or `RenderPresentationCatalogRuntimeBootstrap` is present in scene
- [ ] Catalog version increments when catalog asset changes (automatic via baker/bootstrap)

### Bootstrap Verification

- [ ] `RenderPresentationCatalog` singleton exists after world initialization
- [ ] `RenderPresentationCatalog.Blob.IsCreated == true`
- [ ] `RenderPresentationCatalog.RenderMeshArrayEntity` has `RenderMeshArray` shared component
- [ ] `RenderMeshArray` contains meshes and materials (check via editor diagnostics or runtime logs)

## Runtime Truth Checks

### Presentation System Ordering

- [ ] All presentation systems run in `PresentationSystemGroup` (after simulation)
- [ ] `PresentationRewindGuardSystem` disables presentation during catch-up rewinds
- [ ] Presentation systems read `TimeState.Tick` but do not mutate simulation components
- [ ] No presentation system writes to simulation archetypes (carriers, vessels, asteroids, etc.)
- [ ] No depth offsets / smoothing / interpolation hacks are used to “fix” render==sim (truth mode stays exact)

### Render Key Assignment

- [ ] Every gameplay entity that should render has `RenderSemanticKey` assigned
- [ ] `RenderSemanticKey` values match `Space4XRenderKeys` constants (200=Carrier, 210=Miner, etc.)
- [ ] Entities with `RenderSemanticKey` resolve to `RenderVariantKey` via `ResolveRenderVariantSystem`
- [ ] `RenderVariantKey` maps to valid variant index (0..catalog.Variants.Length-1)
- [ ] No dev-only logs from `ResolveRenderVariantSystem` reporting “fell back to Variant 0” for required keys

### Material/Mesh Assignment

- [ ] Entities with `MeshPresenter` enabled have `MaterialMeshInfo` assigned
- [ ] `MaterialMeshInfo` indices are within `RenderMeshArray` bounds
- [ ] All `MaterialMeshInfo` entities share the catalog's `RenderMeshArray` shared component
- [ ] No per-entity material instances created (check via Unity Profiler)

### Visibility & Batching

- [ ] Entities with `RenderFlags.Visible == 1` appear in scene
- [ ] Entities without `RenderSemanticKey` or with invalid variant do not render (or render fallback)
- [ ] Batch count scales sub-linearly with entity count (check via Unity Profiler)
- [ ] Material count in `RenderMeshArray` stays low (<20 materials recommended)

## Scale Validation (100k+ Entities)

### Performance Metrics

- [ ] Frame time remains stable as entity count increases
- [ ] Batch count does not explode (should be ~material_count * mesh_count, not entity_count)
- [ ] Draw call count scales with visible entities, not total entities (culling works)
- [ ] Memory usage for render components is reasonable (~bytes per entity, not KB)

### Batching Health

- [ ] Unique material count stays low (check `RenderMeshArray.MaterialReferences.Length`)
- [ ] Unique mesh count stays reasonable (check `RenderMeshArray.MeshReferences.Length`)
- [ ] No per-entity `RenderMeshArray` instances (all entities share catalog's instance)
- [ ] `RenderFilterSettings` shared component has low cardinality (ideally 1 instance)

### Truth Verification

- [ ] Visual positions match `LocalTransform.Position` (no lag/desync)
- [ ] Visual colors match `RenderTint` / `MaterialPropertyOverride` values
- [ ] Visual scales match `PresentationScale` / `LocalTransform.Scale`
- [ ] Entity counts match between simulation and presentation (no orphaned renderables)

## Common Issues & Fixes

### Issue: Entities Not Rendering

**Checklist:**
1. Verify `RenderSemanticKey` is assigned
2. Verify `MeshPresenter` is enabled
3. Verify `MaterialMeshInfo` is assigned
4. Verify `RenderMeshArray` shared component exists
5. Verify variant index is valid (check catalog Theme 0 mapping)
6. Verify `RenderFlags.Visible == 1`

**Fix:** Ensure `Space4XPresentationLifecycleSystem` runs and assigns components, or check authoring/baker setup.

### Issue: Batch Count Too High

**Checklist:**
1. Count unique materials in `RenderMeshArray` (should be <20)
2. Count unique meshes in `RenderMeshArray` (should be <50)
3. Check for per-entity `RenderMeshArray` instances (should be 1 shared instance)
4. Check `RenderFilterSettings` cardinality (should be 1)

**Fix:** Consolidate materials/meshes in catalog asset, ensure shared component usage.

### Issue: Visuals Don't Match Simulation

**Checklist:**
1. Verify presentation systems run after simulation completes
2. Verify `TimeState.Tick` is read-only in presentation
3. Verify no presentation system mutates simulation components
4. Check for rewind state issues (presentation should pause during catch-up)

**Fix:** Ensure system ordering (`[UpdateAfter(typeof(SimulationSystemGroup))]`), verify rewind guards.

### Issue: Theme 0 Fallback Not Working

**Checklist:**
1. Verify Theme 0 exists in catalog asset
2. Verify Theme 0 maps all semantic keys used in gameplay
3. Verify `DefaultThemeIndex` is set correctly in catalog blob
4. Check `ResolveRenderVariantSystem` logs for fallback usage

**Fix:** Update catalog asset Theme 0 mappings, ensure fallback mesh/material are assigned.

## Maintenance Guidelines

### When Adding New Render Types

1. Add semantic key constant to `Space4XRenderKeys`
2. Add variant entry to catalog asset `Variants` array
3. Map semantic key to variant in Theme 0 (and other active themes)
4. Ensure gameplay assigns `RenderSemanticKey` when spawning entities
5. Verify `Space4XPresentationLifecycleSystem` assigns render components

### When Modifying Catalog Asset

1. Never reorder existing variant entries (append only)
2. Always update Theme 0 mappings when adding variants
3. Ensure fallback mesh/material remain valid
4. Test that existing entities still resolve correctly

### When Adding Material Properties

1. Use instanced properties (`[MaterialProperty]` attribute) not material instances
2. Add component to entities, not materials
3. Verify Entities Graphics handles property correctly (check Profiler)

### When Debugging Batching Issues

1. Use Unity Profiler → Entities Graphics → Batches view
2. Check `RenderMeshArray` material/mesh counts
3. Verify shared component cardinality
4. Check for per-entity material instances (should be 0)

## Quick Reference

**Key Systems:**
- `ResolveRenderVariantSystem` - Maps semantic keys to variant keys
- `ApplyRenderVariantSystem` - Writes MaterialMeshInfo from variants
- `RenderMeshArrayBindSystem` - Ensures shared component binding
- `Space4XPresentationLifecycleSystem` - Assigns render components to gameplay entities

**Key Components:**
- `RenderSemanticKey` - Gameplay-assigned semantic ID
- `RenderVariantKey` - Resolved variant index
- `MaterialMeshInfo` - Index into RenderMeshArray
- `RenderMeshArray` - Shared component containing mesh/material arrays
- `RenderTint` / `MaterialPropertyOverride` - Instanced material properties

**Key Assets:**
- `space4x/Assets/Data/Space4XRenderCatalog_v2.asset` - Catalog definition
- `space4x/Assets/Scenes/TRI_Space4X_Smoke.unity` - Smoke scene

**Diagnostics:**
- `Space4XSmokePresentationCountsSystem` - Logs component counts
- `RenderSanitySystem` - Validates render pipeline health
- Unity Profiler → Entities Graphics → Batches - Batch count analysis
