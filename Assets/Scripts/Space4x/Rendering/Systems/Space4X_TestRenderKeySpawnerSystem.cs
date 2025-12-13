#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using PureDOTS.Runtime.Core;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// One-shot RenderKey spawner to validate catalog → BRG path.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4X_TestRenderKeySpawnerSystem : ISystem
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
        }

        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless || !Application.isPlaying)
            {
                state.Enabled = false;
                return;
            }

            if (_spawned)
            {
                state.Enabled = false;
                return;
            }

            // Only spawn inside the canonical Game World to avoid Default World noise.
            if (state.WorldUnmanaged.Name != "Game World")
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int count = 0;

            // Spawn a formation of carriers
            for (int x = -2; x <= 2; x++)
            {
                for (int z = 5; z <= 15; z += 5)
                {
                    var e = ecb.CreateEntity();
                    var position = new float3(x * 5f, 5f, z);

                    ecb.AddComponent(e, LocalTransform.FromPositionRotationScale(
                        position,
                        quaternion.identity,
                        10f));

                    ecb.AddComponent(e, new RenderKey
                    {
                        ArchetypeId = Space4XRenderKeys.Carrier,
                        LOD = 0
                    });

                    ecb.AddComponent(e, new RenderFlags
                    {
                        Visible = 1,
                        ShadowCaster = 1,
                        HighlightMask = 0
                    });

                    count++;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            _spawned = true;
            state.Enabled = false;
#if UNITY_EDITOR
            LogSpawnMessage(count);
#endif
        }
        public void OnDestroy(ref SystemState state) { }

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogSpawnMessage(int count)
        {
            Debug.Log($"[Space4X_TestRenderKeySpawnerSystem] Spawned {count} debug RenderKey entities in formation.");
        }
#endif
    }
}

#endif
