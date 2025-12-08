# Space4X Demo 0 Scene Setup

## Overview
This document describes how to set up the Demo0 scene for Space4X using the prefabs and authoring scripts created for Demo 0.

## Steps

1. **Create the main scene** (`Demo0.unity`):
   - Open Unity and create a new scene
   - Save as `Assets/Scenes/Demo/Demo0.unity`

2. **Add Bootstrap Config**:
   - Create empty GameObject named "BootstrapConfig"
   - Add `PureDotsConfigAuthoring` component (if available)
   - Add `SpatialPartitionAuthoring` component (if available)

3. **Create SubScene for DOTS entities**:
   - Create empty GameObject named "SimulationSubScene"
   - Add `SubScene` component to it
   - Create a child GameObject hierarchy:
     - Space Backdrop: Instance of `S4X_SpaceBackdrop` prefab at position (0, 0, 0)
     - Asteroid: Instance of `S4X_Asteroid_Mineral` prefab at position (0, 0, 0)
     - Carrier: Instance of `S4X_Carrier` prefab at position (-10, 0, 0)
     - Mining Vessel: Instance of `S4X_MiningVessel` prefab at position (-5, 0, 0)
   - Select all child GameObjects
   - Use **GameObject → Convert To Entity → Convert and Save As SubScene**
   - Save SubScene as `Assets/Scenes/Demo/Demo0.SubScene.unity`
   - Assign the SubScene asset to the SubScene component

4. **Add Camera**:
   - Instance of `S4X_CameraRig` prefab or create Main Camera at position (0, 10, -20) looking at (0, 0, 0)

5. **Add Debug Overlay (Optional)**:
   - Instance of `DebugOverlayCanvas` prefab
   - Configure text fields to reference DebugOverlayReader component

6. **Add to Build Settings**:
   - File → Build Settings → Add Open Scenes

## Prefabs Location
- `Assets/Prefabs/Space4X/Demo/S4X_SpaceBackdrop.prefab`
- `Assets/Prefabs/Space4X/Demo/S4X_Asteroid_Mineral.prefab`
- `Assets/Prefabs/Space4X/Demo/S4X_Carrier.prefab`
- `Assets/Prefabs/Space4X/Demo/S4X_MiningVessel.prefab`
- `Assets/Prefabs/Space4X/Demo/S4X_CameraRig.prefab`
- `Assets/Prefabs/Shared/Debug/DebugOverlayCanvas.prefab` (optional)


