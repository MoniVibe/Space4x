using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

//#define SPACE4X_DEBUG_CUBES

namespace Space4X.Demo
{
#if SPACE4X_DEBUG_CUBES
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XDebugCubeOrbitSystem : ISystem
    {
        private int _debugFrames;

        public void OnCreate(ref SystemState state)
        {
            _debugFrames = 120;
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            float angularSpeed = 0.5f;

            foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>()
                                               .WithAll<DebugOrbitTag>())
            {
                var lt = transform.ValueRO;
                float3 pos = lt.Position;

                // Orbit around the origin on XZ
                float2 pos2 = new float2(pos.x, pos.z);
                float r = math.length(pos2);
                if (r < 0.0001f)
                    continue;

                float angle = math.atan2(pos.z, pos.x);
                angle += angularSpeed * dt;

                float2 newPos2 = new float2(math.cos(angle) * r, math.sin(angle) * r);
                lt.Position = new float3(newPos2.x, pos.y, newPos2.y);

                // Optionally, spin them in place too
                lt.Rotation = math.mul(
                    lt.Rotation,
                    quaternion.AxisAngle(math.up(), angularSpeed * dt));

                transform.ValueRW = lt;
            }
        }
    }
#endif
}
