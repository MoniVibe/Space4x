# Movement Authoring Guide

This guide explains how to set up movement policy authoring for entities in PureDOTS, Space4X, and Godgame projects.

## Overview

Movement policy tags define how entities move through 3D space:
- **GroundMovementTag** - Constrains to terrain surface (Godgame villagers, ground units)
- **FlyingMovementTag** - 2.5D movement with altitude control (Godgame flying creatures)
- **SpaceMovementTag** - Full 6DoF movement (Space4X ships, projectiles)

## Adding Movement Authoring to Prefabs

### Ground Units (Godgame)

1. Select your villager/ground unit prefab
2. Add `GroundMovementAuthoring` component
3. Configure:
   - **Align To Surface**: Whether to align rotation to terrain normal (true) or keep upright (false)
   - **Max Slope Degrees**: Maximum slope angle the entity can traverse (0-90 degrees)
   - **Height Offset**: Offset from terrain surface (for hovering slightly above ground)

**Example**: Villager prefab with `GroundMovementAuthoring`:
- Align To Surface: `true`
- Max Slope Degrees: `45`
- Height Offset: `0`

### Flying Units (Godgame)

1. Select your flying creature/drone prefab
2. Add `FlyingMovementAuthoring` component
3. Configure:
   - **Min Altitude**: Minimum altitude above terrain (e.g., 5 units)
   - **Max Altitude**: Maximum altitude above terrain (e.g., 50 units)
   - **Preferred Altitude**: Preferred cruising altitude (e.g., 20 units)
   - **Altitude Change Rate**: Rate of altitude change (units per second)

**Example**: Dragon prefab with `FlyingMovementAuthoring`:
- Min Altitude: `10`
- Max Altitude: `100`
- Preferred Altitude: `30`
- Altitude Change Rate: `5`

### Space Units (Space4X)

1. Select your ship/projectile prefab
2. Add `SpaceMovementAuthoring` component
3. No configuration needed (full 6DoF, no constraints)

**Example**: Carrier prefab with `SpaceMovementAuthoring` (no config fields)

## Required Singleton Authoring in SubScenes

### Time System Setup

**Required**: `PureDotsConfigAuthoring` must be present in your SubScene.

1. In your SubScene (not the main scene), create a GameObject
2. Add `PureDotsConfigAuthoring` component
3. Assign a `PureDotsRuntimeConfig` asset that contains:
   - `TimeSettingsConfig` (fixed delta time, default speed, pause on start)
   - `HistorySettingsConfig` (rewind/history settings)
   - `PoolingSettingsConfig` (memory pooling)
   - `ThreadingSettingsConfig` (worker threads)

**Location**: Create the config asset via `Assets > Create > PureDOTS > Runtime Config`

**Baking Result**: This bakes `TimeState`, `TickTimeState`, `TimeSettingsConfig`, `HistorySettings`, and other runtime configs as singletons.

### Spatial Grid Setup

**Required**: `SpatialPartitionAuthoring` must be present in your SubScene.

1. In your SubScene, create a GameObject
2. Add `SpatialPartitionAuthoring` component
3. Assign a `SpatialPartitionProfile` asset with 3D-aware settings:
   - **Bounds**: Set world bounds (e.g., Center: (0, 0, 0), Extent: (512, 128, 512))
   - **Cell Size**: Grid cell size (e.g., 8 units)
   - **Manual Cell Counts**: Set to 3D grid (e.g., X: 64, Y: 16, Z: 64)
   - **Lock Y Axis To One**: Set to `false` for 3D spatial queries

**Location**: Create the profile asset via `Assets > Create > PureDOTS > Spatial Partition Profile`

**Baking Result**: This bakes `SpatialGridConfig`, `SpatialGridState`, and spatial grid buffers as singletons.

## Verification Checklist

After baking your SubScene, verify in Play mode:

### 1. Entity Inspection
1. Enter Play mode
2. Open **Window > Entities > Hierarchy**
3. Select a baked entity (e.g., villager, ship)
4. In the Inspector, verify:
   - ✅ Correct `*MovementTag` is present (GroundMovementTag, FlyingMovementTag, or SpaceMovementTag)
   - ✅ Correct `*MovementConfig` is present (if applicable)
   - ✅ Config values match your authoring fields

### 2. Singleton Verification
In the Entities Hierarchy, verify singletons exist:
- ✅ `TimeState` singleton entity
- ✅ `TickTimeState` singleton entity
- ✅ `SpatialGridConfig` singleton entity
- ✅ `SpatialGridState` singleton entity

### 3. Debug System
Enable `MovementPolicyDebugSystem` to see movement tag counts:
- Ground units: Green (logged as "Ground")
- Flying units: Yellow (logged as "Flying")
- Space units: Blue (logged as "Space")
- Untagged: Gray (logged as "Untagged")

## Common Issues

### "TimeState singleton missing"
**Solution**: Add `PureDotsConfigAuthoring` to your SubScene with a valid `PureDotsRuntimeConfig` asset.

### "SpatialGridConfig singleton missing"
**Solution**: Add `SpatialPartitionAuthoring` to your SubScene with a valid `SpatialPartitionProfile` asset.

### Entities have no movement tag
**Solution**: Add the appropriate `*MovementAuthoring` component to your prefab and rebake the SubScene.

### Movement systems not running
**Solution**: Verify entities have the correct movement tag and that movement systems are enabled in your world.

## Project-Specific Notes

### Godgame
- **Villagers**: Use `GroundMovementAuthoring` (default: align to surface, max slope 45°)
- **Flying Creatures**: Use `FlyingMovementAuthoring` with appropriate altitude limits
- **Ground Movement Adapter**: Processes entities with `GroundMovementTag` or excludes `FlyingMovementTag`/`SpaceMovementTag`

### Space4X
- **Ships/Carriers**: Use `SpaceMovementAuthoring` for full 6DoF
- **Projectiles**: Automatically get `SpaceMovementTag` when spawned by `Space4XProjectileSpawnerSystem`
- **Movement Adapter**: Only processes entities with `SpaceMovementTag`

## Best Practices

1. **Always add movement authoring** to prefabs before placing them in SubScenes
2. **Use appropriate tags** - Don't mix ground and space movement on the same entity
3. **Configure spatial grid** with proper 3D cell counts (Y dimension > 1) for space games
4. **Verify baking** by checking Entities Hierarchy after entering Play mode
5. **Use debug system** to verify movement tag distribution matches expectations

