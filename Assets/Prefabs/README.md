# Space4X Prefab Directory

This directory contains all prefabs for the Space4X demo, organized by category.

## Directory Structure

```
Prefabs/
├── Systems/          # Camera, Input, Bulk setup prefabs
├── Vessels/         # Mining vessels and other ships
├── Carriers/        # Carrier ships
├── Asteroids/       # Asteroid entities (if standalone)
├── Colonies/        # Colony entities (if standalone)
└── Fleets/          # Fleet entities (if standalone)
```

## Prefab Creation

See `PureDOTS/Docs/TODO/Space4X_PrefabChecklist.md` for step-by-step instructions on creating each prefab.

## Quick Reference

### Systems Prefabs
- `Space4XCamera.prefab` - Camera controller with input (runtime, not baked)
- `MiningDemoSetup.prefab` - Bulk authoring for carriers, vessels, asteroids
- `RegistrySetup.prefab` - Bulk authoring for colonies, fleets, routes, anomalies

### Vessel Prefabs
- `MiningVessel.prefab` - Mining ship that extracts resources from asteroids
- `CombatVessel.prefab` - Combat ship (future expansion)

### Carrier Prefabs
- `Carrier.prefab` - Carrier ship that patrols and receives resources
- `Carrier_Large.prefab` - Larger carrier variant (optional)

### Asteroid Prefabs
- `Asteroid_Minerals.prefab` - Asteroid containing minerals
- `Asteroid_RareMetals.prefab` - Asteroid containing rare metals
- `Asteroid_EnergyCrystals.prefab` - Asteroid containing energy crystals
- `Asteroid_OrganicMatter.prefab` - Asteroid containing organic matter

**Note:** Asteroids are typically created via `Space4XMiningDemoAuthoring` bulk authoring rather than standalone prefabs.

### Registry Prefabs (Optional Standalone)
- `Colony.prefab` - Colony entity (if standalone authoring exists)
- `Fleet.prefab` - Fleet entity (if standalone authoring exists)
- `LogisticsRoute.prefab` - Logistics route (via SampleRegistryAuthoring)
- `Anomaly.prefab` - Anomaly entity (via SampleRegistryAuthoring)

**Note:** Registry entities are typically created via `Space4XSampleRegistryAuthoring` bulk authoring.

## Key Differences from Godgame

- **Visual Representation:** Carriers, mining vessels, and asteroids use `PlaceholderVisualAuthoring` with primitive meshes (Capsule, Cylinder, Sphere). Registry entities (colonies, fleets, routes, anomalies) remain data-only.
- **Bulk Authoring:** Many entities are created via bulk authoring components (`MiningDemoAuthoring`, `SampleRegistryAuthoring`) rather than individual prefabs.
- **Gameplay Logic:** All gameplay logic is handled by ECS systems; prefabs define initial data/configuration and visual representation.

## Notes

- **Visual Components:** Carriers, vessels, and asteroids require:
  - `PlaceholderVisualAuthoring` component
  - `MeshFilter` with appropriate primitive mesh
  - `MeshRenderer` with URP/Lit material
- All prefabs should use `TransformUsageFlags.Dynamic` for runtime entities
- ID consistency is critical: Carrier IDs, Colony IDs, Fleet IDs must match across related prefabs
- Resource types must match the `ResourceType` enum exactly
- Spatial indexing is automatic via `SpatialIndexedTag` component
- Transform positions are typically set by spawn systems, not prefab defaults
- Visual colors should match `MiningVisualSettings` defaults or be customized per prefab variant

## Resource Types

Space4X uses these resource types:
- **Minerals** - Common construction material
- **RareMetals** - Advanced components  
- **EnergyCrystals** - Power generation
- **OrganicMatter** - Life support, research

See `Space4x/Assets/Data/README.md` for more details on data assets.


