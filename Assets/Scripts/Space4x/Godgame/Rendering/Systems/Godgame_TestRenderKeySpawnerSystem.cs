#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using PureDOTS.Runtime.Core;
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
        private bool _spawned;

        public void OnCreate(ref SystemState state)
        {
            _spawned = false;
            if (state.WorldUnmanaged.Name != "Game World")
            {
                state.Enabled = false;
                return;
            }
#if UNITY_EDITOR
            if (RuntimeMode.IsHeadless)
            {
                state.Enabled = false;
                return;
            }
            if (!Application.isPlaying)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<GodgameRenderCatalogSingleton>();
#else
            state.Enabled = false;
#endif
        }

        public void OnUpdate(ref SystemState state)
        {
#if UNITY_EDITOR
            if (RuntimeMode.IsHeadless)
            {
                state.Enabled = false;
                return;
            }

            if (_spawned)
            {
                state.Enabled = false;
                return;
            }

            if (state.WorldUnmanaged.Name != "Game World")
            {
                return;
            }

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
            _spawned = true;
            state.Enabled = false;
#else
            state.Enabled = false;
#endif
        }

        public void OnDestroy(ref SystemState state) { }
    }
}

#endif
