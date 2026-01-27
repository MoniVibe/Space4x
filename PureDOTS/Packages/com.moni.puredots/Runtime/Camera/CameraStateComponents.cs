using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// Camera configuration (sensitivity, limits, behavior settings).
    /// </summary>
    public struct CameraConfig : IComponentData
    {
        public float OrbitYawSensitivity;
        public float OrbitPitchSensitivity;
        public float2 PitchClamp;           // Min/max pitch in degrees
        public float PanScale;
        public float ZoomSpeed;
        public float MinDistance;
        public float MaxDistance;
        public float TerrainClearance;      // Minimum height above terrain
        public float CollisionBuffer;       // Safety margin for collision
        public float CloseOrbitSensitivity; // Sensitivity multiplier when close
        public float FarOrbitSensitivity;   // Sensitivity multiplier when far
        public float SmoothingDamping;       // Optional smoothing (0 = no smoothing)
    }

    /// <summary>
    /// Authoritative camera state computed by DOTS systems.
    /// Single-writer: only CameraSystem writes this.
    /// </summary>
    public struct CameraState : IComponentData
    {
        public uint LastUpdateTick;
        public byte PlayerId;               // Player identifier (0 = default/single-player)
        public float3 TargetPosition;       // World-space camera position
        public float3 TargetForward;         // Camera forward direction
        public float3 TargetUp;              // Camera up direction
        public float3 PivotPosition;         // World-space orbit pivot (if orbiting)
        public float Distance;               // Distance from pivot (zoom level)
        public float Pitch;                  // Vertical rotation angle (degrees)
        public float Yaw;                    // Horizontal rotation angle (degrees)
        public float FOV;                    // Field of view (degrees)
        public byte IsOrbiting;              // Flag: currently orbiting (MMB held)
        public byte IsPanning;               // Flag: currently panning (LMB held)
        public float PanPlaneHeight;        // Height of pan plane during drag
    }

    /// <summary>
    /// Camera tag for identifying camera entities.
    /// </summary>
    public struct CameraTag : IComponentData { }
}

