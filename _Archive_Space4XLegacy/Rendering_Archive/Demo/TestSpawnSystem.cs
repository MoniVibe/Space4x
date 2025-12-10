using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Shared.Demo;
using UnityEngine;

namespace Space4X.Demo
{
    public struct TestSpawnTag : IComponentData {}

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SharedDemoRenderBootstrap))]
    public partial class TestSpawnSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<DemoRenderReady>();
        }

        protected override void OnUpdate()
        {
            // TEMP: disable this demo spawner to keep Space4X debug orbit cubes clear
            Enabled = false;
            return;

            if (SystemAPI.HasSingleton<TestSpawnTag>()) return;

            var e = EntityManager.CreateEntity();
            EntityManager.AddComponent<TestSpawnTag>(e);

            // Position (0,0,0), Scale 2
            var transform = LocalTransform.FromPosition(float3.zero);
            transform.Scale = 2f;
            EntityManager.AddComponentData(e, transform);

            // Render component
            // Cyan color (0, 1, 1, 1)
            DemoRenderUtil.MakeRenderable(EntityManager, e, new float4(0, 1, 1, 1));

            Debug.Log("[TestSpawnSystem] Spawned test entity at (0,0,0) with scale 2 and Cyan color.");
            
            Enabled = false; // Run once
        }
    }
}
