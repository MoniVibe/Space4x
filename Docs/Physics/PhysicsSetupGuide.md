# Space4X Physics Setup Guide

## Quick Test: Ship Hits Asteroid

This guide walks through setting up a simple collision test to verify the physics integration is working.

## Prerequisites

1. **Physics Systems Running**: The following systems should be active:
   - `PhysicsBodyBootstrapSystem` (InitializationSystemGroup)
   - `PhysicsSyncSystem` (PhysicsPreSyncSystemGroup)
   - `PhysicsEventSystem` (PhysicsPostEventSystemGroup)
   - `Space4XCollisionResponseSystem` (SimulationSystemGroup, after PhysicsPostEventSystemGroup)

2. **Physics World**: Unity Physics/Havok should be running automatically. Check the scene hierarchy for a Physics Step GameObject (usually auto-created).

3. **PhysicsConfig**: The `PhysicsConfig` singleton should exist (created by `CoreSingletonBootstrapSystem`). Verify it has `EnableSpace4XPhysics = 1`.

## Setup Steps

### Step 1: Add Physics to Asteroid Prefab

1. Open your asteroid prefab (or create a test asteroid GameObject)
2. Add `Space4XVesselPhysicsAuthoring` component:
   - **Collider Type**: Sphere
   - **Radius**: 2-3 (large enough to see)
   - **Layer**: Asteroid
   - **Raises Collision Events**: ✓ (checked)
   - **Is Trigger**: ✗ (unchecked - we want real collisions)
   - **Priority**: 100

### Step 2: Add Physics to Miner/Carrier Prefab

1. Open your miner or carrier prefab
2. Add `Space4XVesselPhysicsAuthoring` component:
   - **Collider Type**: Sphere
   - **Radius**: 1
   - **Layer**: Ship (or Miner if using miner)
   - **Raises Collision Events**: ✓ (checked)
   - **Is Trigger**: ✗ (unchecked)
   - **Priority**: 150

### Step 3: Position Entities for Collision

1. Place asteroid at position (0, 0, 0)
2. Place miner/carrier at position (5, 0, 0) - close enough to collide
3. Ensure both entities have movement systems that will bring them together

### Step 4: Verify Physics Bodies Created

When you hit Play:

1. `PhysicsBodyBootstrapSystem` should detect entities with `RequiresPhysics` and `SpacePhysicsBody`
2. It will add Unity Physics components (`PhysicsCollider`, `PhysicsVelocity`, `PhysicsMass`)
3. Check the Unity Console for any bootstrap errors

### Step 5: Watch for Collision Logs

When collisions occur, you should see logs like:

```
[Space4XCollision] Entity 42:1 (Ship) hit 15:0 (Asteroid) at (2.5, 0, 0) normal=(1, 0, 0) impulse=0 tick=1234
```

These logs come from `Space4XCollisionResponseSystem.LogCollisionsDebug()` and are **always enabled** for testing.

## Troubleshooting

### No Collision Logs Appearing

1. **Check PhysicsConfig**: Verify `EnableSpace4XPhysics = 1`
   ```csharp
   // In Unity Console or debugger:
   var config = SystemAPI.GetSingleton<PhysicsConfig>();
   Debug.Log($"Space4X Physics Enabled: {config.IsSpace4XPhysicsEnabled}");
   ```

2. **Check Physics Bodies Exist**: Entities should have:
   - `SpacePhysicsBody` component
   - `RequiresPhysics` component
   - `PhysicsCollider` component (added by bootstrap)
   - `SpaceCollisionEvent` buffer (added by authoring)

3. **Check Collision Layers**: Verify layers are set correctly:
   - Ship layer should collide with Asteroid layer
   - Check `Space4XPhysicsLayers.GetCollidesWithMask()` for layer compatibility

4. **Check Entity Movement**: Entities must actually move and come into contact. If they're stationary, no collisions will occur.

5. **Check Physics Step**: Verify Unity Physics is running. Look for `PhysicsStep` or `PhysicsWorldSingleton` in the world.

### Collision Events Not Being Created

1. **Check PhysicsEventSystem**: Verify it's running after physics step
2. **Check Rewind State**: Collisions are skipped during `RewindMode.Playback`
3. **Check Settle Frames**: First frame after rewind may skip collisions

### Physics Bodies Not Being Created

1. **Check Bootstrap System**: `PhysicsBodyBootstrapSystem` should run in `InitializationSystemGroup`
2. **Check Authoring**: Entities need `RequiresPhysics` + `SpacePhysicsBody` components
3. **Check ECB**: Bootstrap uses ECB - verify it's playing back correctly

## Next Steps

Once collisions are logging correctly:

1. **Add Gameplay Responses**: Implement damage, mining triggers, etc. in `HandleCollision()` method
2. **Add Visual Feedback**: Spawn particles, play sounds on collision
3. **Add Collision Avoidance**: Use collision events to steer ships away from asteroids
4. **Test Rewind**: Verify collisions work correctly during rewind/playback

## Debug Commands

Enable detailed physics logging:

```csharp
// In Unity Console or a debug system:
var config = SystemAPI.GetSingleton<PhysicsConfig>();
config.LogCollisions = 1;  // Enable detailed logging
SystemAPI.SetSingleton(config);
```

This enables additional logging in `Space4XCollisionDebugSystem` (separate from the always-on debug logs).













