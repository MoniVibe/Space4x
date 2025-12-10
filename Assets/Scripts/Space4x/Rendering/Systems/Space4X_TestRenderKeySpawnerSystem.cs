using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    /// <summary>
    /// One-shot RenderKey spawner to validate catalog → BRG path.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4X_TestRenderKeySpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!Application.isPlaying)
                return;

            var em = state.EntityManager;
            int count = 0;

            // Spawn a formation of carriers
            // x: -2 to 2 (5 columns)
            // z: 5, 10, 15 (3 rows)
            for (int x = -2; x <= 2; x++)
            {
                for (int z = 5; z <= 15; z += 5)
                {
                    var e = em.CreateEntity();

                    em.AddComponentData(e, LocalTransform.FromPositionRotationScale(
                        new float3(x * 5f, 5f, z),
                        quaternion.identity,
                        10f));

                    em.AddComponentData(e, new RenderKey
                    {
                        ArchetypeId = Space4XRenderKeys.Carrier,
                        LOD = 0
                    });

                    em.AddComponentData(e, new RenderFlags
                    {
                        Visible = 1,
                        ShadowCaster = 1,
                        HighlightMask = 0
                    });

                    count++;
                }
            }

            Debug.Log($"[Space4X_TestRenderKeySpawnerSystem] Spawned {count} debug RenderKey entities in formation.");

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
    }
}
