using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation.Camera
{
    /// <summary>
    /// Space4X camera state singleton used by interaction systems.
    /// </summary>
    public struct Space4XCameraState : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public float ZoomDistance;
        public float3 FocusPoint;
    }
}

