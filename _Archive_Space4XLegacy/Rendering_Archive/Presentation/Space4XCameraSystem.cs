using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct Space4XCameraSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraInput>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var input = SystemAPI.GetSingleton<CameraInput>();
            var dt = SystemAPI.Time.DeltaTime;

            // This system would normally move a camera entity or write to a camera transform
            // For now, we'll just log if there's input to verify it's working
            
            if (math.lengthsq(input.Pan) > 0.01f || math.abs(input.Zoom) > 0.01f)
            {
                // Logic to move camera entity would go here
                // e.g. query for a CameraTag entity and update its LocalTransform
            }
        }
    }
}
