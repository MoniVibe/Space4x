using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Camera
{
    /// <summary>
    /// Stateless snapshot of the authoritative ECS camera rig for HUD/telemetry consumption.
    /// </summary>
    public struct CameraRigTelemetry : IComponentData
    {
        public byte PlayerId;
        public uint LastTick;
        public float3 Position;
        public float3 Forward;
        public float3 Up;
        public float Distance;
        public float Pitch;
        public float Yaw;
        public float Shake;
    }
}
