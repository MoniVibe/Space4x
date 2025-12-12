using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using RenderFlags = PureDOTS.Rendering.RenderFlags;
using RenderKey = PureDOTS.Rendering.RenderKey;

namespace Godgame.Rendering.Systems
{
    /// <summary>
    /// Minimal RenderKey spawner so Godgame scenes always produce a RenderKey for sanity checks.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Godgame_TestRenderKeySpawnerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!Application.isPlaying)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<GodgameRenderCatalogSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            const int count = 5;

            for (int i = 0; i < count; i++)
            {
                var e = em.CreateEntity();
                var angle = math.radians(i * (360f / count));
                var pos = new float3(math.cos(angle) * 6f, 0.5f, math.sin(angle) * 6f);

                em.AddComponentData(e, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1.5f));
                em.AddComponentData(e, new RenderKey
                {
                    ArchetypeId = GodgameRenderKeys.VillageCenter,
                    LOD = 0
                });
                em.AddComponentData(e, new RenderFlags
                {
                    Visible = 1,
                    ShadowCaster = 1,
                    HighlightMask = 0
                });
            }

            Debug.Log("[Godgame_TestRenderKeySpawnerSystem] Spawned demo RenderKey entities.");
            state.Enabled = false;
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
