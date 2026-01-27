# PureDOTS Camera Rig Contract

This document defines the stable contract for camera implementation in PureDOTS-based games.

## Overview

The PureDOTS camera system provides a clean separation between:
- **Game-specific camera logic** (input interpretation, game rules, behaviors)
- **Framework camera infrastructure** (state management, application to Unity Camera)

## Architecture

```
Game Project (Space4X/Godgame)
    ↓ (owns input & game flavor)
CameraRigService.Publish(state)
    ↓ (single source of truth)
CameraRigApplier (LateUpdate)
    ↓ (only mutates Camera.main)
Unity Camera Transform
```

## Core Components

### CameraRigState
The contract struct that represents canonical camera state (transform is derived):

```csharp
public struct CameraRigState
{
    public Vector3 Focus;          // World-space pivot/target (authoritative)
    public float Pitch;            // Pitch angle in degrees
    public float Yaw;              // Yaw angle in degrees
    public float Roll;             // Roll angle in degrees (optional)
    public float Distance;         // Orbit radius (0 = look-in-place / free-fly)
    public CameraRigMode Mode;     // Orbit vs FreeFly (UX/input hint)
    public bool PerspectiveMode;   // true=perspective, false=orthographic
    public float FieldOfView;      // FOV in degrees (0 = default)
    public CameraRigType RigType;  // Which game rig owns this
}
```

### CameraRigService
The central authority for camera state:

```csharp
public static class CameraRigService
{
    public static void Publish(CameraRigState state);  // ONLY WAY to change rig state
    public static CameraRigState Current { get; }      // Current authoritative state
    public static bool HasState { get; }               // True if any state published
    public static event Action<CameraRigState> CameraStateChanged;  // For telemetry
}
```

### CameraRigApplier
The single component that applies rig state to Unity Cameras:

```csharp
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(10000)]  // Runs in LateUpdate after everything else
public sealed class CameraRigApplier : MonoBehaviour
{
    // Automatically reads from CameraRigService and applies to Camera.main
}
```

### BW2StyleCameraController
A reusable camera implementation providing B&W2-style controls:

- **LMB + Drag**: Pan across terrain
- **MMB + Drag**: Orbit around pivot
- **Scroll Wheel**: Zoom in/out
- **Terrain Clamping**: Prevents going through ground

## Usage Patterns

### Pattern 1: Custom Game Camera

```csharp
public class MyGameCamera : MonoBehaviour
{
    private Vector3 _focus;
    private float _yaw, _pitch;
    private float _distance;

    void Update()
    {
        // 1. Read game-specific input
        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector2 lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        // 2. Apply game-specific camera logic
        _focus += transform.right * moveInput.x * speed * Time.deltaTime;
        _yaw += lookInput.x * sensitivity;
        _pitch = Mathf.Clamp(_pitch - lookInput.y * sensitivity, -80f, 80f);

        // 3. Create and publish canonical CameraRigState
        var rigState = new CameraRigState
        {
            Focus = _focus,
            Pitch = _pitch,
            Yaw = _yaw,
            Roll = 0f,
            Distance = _distance,
            Mode = CameraRigMode.Orbit,
            PerspectiveMode = true,
            FieldOfView = 60f,
            RigType = CameraRigType.Space4X  // or Godgame
        };

        CameraRigService.Publish(rigState);
        // CameraRigApplier derives/apply transform in LateUpdate
    }
}
```

### Pattern 2: Using BW2StyleCameraController

```csharp
public static class CameraSetup
{
    public static void SetupBw2Camera(GameObject cameraRig)
    {
        // Add required components
        cameraRig.AddComponent<Camera>();
        var controller = cameraRig.AddComponent<BW2StyleCameraController>();
        cameraRig.AddComponent<CameraRigApplier>();
        cameraRig.AddComponent<BW2CameraInputBridge>();

        // Configure for your game
        controller.groundMask = LayerMask.GetMask("Terrain");
        controller.panScale = 1.0f;
        controller.orbitYawSensitivity = 0.25f;
        // ... other settings

        // Done! BW2StyleCameraController handles input and publishes state
    }
}
```

## Assembly Separation

The camera system is split into separate assemblies to enforce separation:

- **PureDOTS.Runtime**: Deterministic simulation, AI, rewind (no camera dependencies)
- **PureDOTS.Camera**: Presentation camera code (depends on Runtime)

This prevents accidental coupling between simulation and presentation code.

## Important Notes

### Frame Time vs Simulation Time
- Camera code uses `Time.deltaTime` (frame time)
- Not part of deterministic simulation/rewind
- Safe for smooth camera movement and presentation

### Single Source of Truth
- Only `CameraRigService.Publish()` can change camera state
- Only `CameraRigApplier` can mutate `Camera.main`
- Prevents conflicts between multiple camera rigs

### Single Publisher (Recommended)
- Exactly **one** active camera controller should call `CameraRigService.Publish()` per frame.
- Other systems (selection/control groups) should emit requests (e.g. `PureDOTS.Input.CameraRequestEvent`) and let the active camera rig consume them.

### Game vs Framework Responsibilities
- **Game code**: Input interpretation, game-specific behaviors, camera bounds
- **Framework**: State management, atomic application to Unity Camera

## Migration Guide

When implementing cameras in Space4X/Godgame:

1. **Create game-specific camera controller**
2. **Read game input and apply game rules**
3. **Compute CameraRigState**
4. **Call CameraRigService.Publish(state)**
5. **Attach CameraRigApplier to camera GameObject**

The framework handles the rest automatically.
