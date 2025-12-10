using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Shared.Demo;

//#define SPACE4X_DEBUG_CUBES

namespace Space4X.Demo
{
#if SPACE4X_DEBUG_CUBES
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SharedDemoRenderBootstrap))]
    public partial struct Space4XDebugCubeSpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DemoRenderReady>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            Debug.Log("[Space4XDebugCubeSpawnerSystem] Spawning Space4X debug cubes...");

            float3[] positions =
            {
                new float3(0f, 0f, 0f),   // magenta
                new float3(5f, 0f, 0f),   // yellow
                new float3(0f, 0f, 5f),   // green
            };

            float4[] colors =
            {
                new float4(1f, 0f, 1f, 1f),
                new float4(1f, 1f, 0f, 1f),
                new float4(0f, 1f, 0f, 1f),
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var e = em.CreateEntity();

                // 1) Use the SAME render util signature, but pass our desired position/scale
                DemoRenderUtil.MakeRenderable(
                    em,
                    e,
                    positions[i],
                    new float3(1.5f, 1.5f, 1.5f),
                    colors[i]);

                // 2) Force LocalTransform to our desired position/scale
                var lt = LocalTransform.FromPositionRotationScale(
                    positions[i],
                    quaternion.identity,
                    1.5f);

                if (em.HasComponent<LocalTransform>(e))
                    em.SetComponentData(e, lt);
                else
                    em.AddComponentData(e, lt);

                em.AddComponent<DebugOrbitTag>(e);
            }

            Debug.Log("[Space4XDebugCubeSpawnerSystem] Spawned " + positions.Length + " Space4X debug cubes.");

            state.Enabled = false;
        }
    }
#endif
}
