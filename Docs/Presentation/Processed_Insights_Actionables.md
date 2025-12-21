# Processed Insights → Actionables (Space4X)

## Presentation-only scale
- LOD/culling must not change flight/orbit sim state.
- PresentationLayerConfig controls perceived scale; sim stays fixed-step.

## Distance rendering policy
- Distant fleets/ships/asteroids reduce to icons/impostors via LOD variants.
- Projectiles and impacts should batch into shared buffers where possible.
- Starfields/nebulae should use instancing and procedural shader variation.

## Cache + invalidation (strategic layer)
- Cache fleet knowledge, hub reachability, threat volumes, resource flows.
- Invalidate on capture, hub build/destroy, treaty changes, new colony.

## Travel as cost fields
- Nebulae/storms are volumes that modify travel cost, damage risk, sensor occlusion.
- Navigation uses cost fields, not hard lanes.

## Immediate presentation hooks
- Ensure PresentationLayer tagging on all renderable entities.
- Drive RenderKey.LOD from camera distance and layer multipliers.
- Keep cull distances presentation-only and layer-scaled.

## BAR/Recoil-derived constraints
- Keep a hard sim/presentation boundary (no camera/UI influence on sim).
- Batch and incrementally update expensive systems (pathing/LOS/terrain).
- Use dirty-region invalidation for nav/visibility; avoid global recompute.

## Ship interior micro-worlds (Space4X)
- Treat interiors as SimInterior (always-on) + PresentInterior (streamed) micro-worlds.
- Stream interior SubScenes by interest (boarding, inspect, cinematic) and keep meta sections resident.
- Use room/portal graphs or DOTS occlusion; toggle visibility via enableables.

## Biodeck/biosculpting patterns (Space4X)
- Biodeck grids are patch-first and bound to modules; no per-plant entity swarms.
- Biosculpt edits are command-buffered and deterministic.
- Climate/biome updates are dirty-only with hysteresis.
- Hero plants are presentation-only when boarding/inspecting.

## Content registry + presentation contracts
- Assets never hang off sim entities; sim stores stable IDs + small overrides only.
- Presentation resolves IDs via catalogs/registry + streaming; editor writes patches/commands only.
- No UnityEngine.Object pointers or AssetDatabase GUIDs in runtime sim state.
- Avoid SharedComponentData for skins/profiles (chunk fragmentation).
 - Use RegistryIdentity + PresentationContentRegistryAsset as the shared spine.

## Rendering and transforms
- RenderKey/RenderCatalog remain the fast path; registry binds to indices/handles.
- Prefer prefab instantiation/pooling over RenderMeshUtility.AddComponents in bulk.
- Use Parent/Child + LocalTransform; PostTransformMatrix only for non-uniform scale.

## Runtime content refs and streaming
- Use WeakObjectReference/UntypedWeakReferenceId for runtime swaps.
- Use EntitySceneReference + SceneSystem.LoadSceneAsync for interiors/modules.
- Use Scene Sections to load only what’s needed; keep section 0 meta resident.

## In-game editor rules
- All edits are command streams + patches (ECB for structural changes).
- Persist patches keyed by StableId + component diffs + content IDs.
