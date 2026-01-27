#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using PureDOTS.LegacyScenario.Rendering;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Village
{
    /// <summary>
    /// Bootstrap system that creates legacy scenario village entities: homes, workplaces, and villagers.
    /// Spawns 10 homes, 10 workplaces, and 10 villagers in a strip layout.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SharedRenderBootstrap))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillageDemoBootstrapSystem")]
    public partial struct VillageScenarioBootstrapSystem : ISystem
    {
        bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            UnityEngine.Debug.Log($"[VillageScenarioBootstrapSystem] OnCreate in world: {state.WorldUnmanaged.Name}");
            state.RequireForUpdate<RenderMeshArraySingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            // Guard: Skip if world is not ready (during domain reload)
            if (!state.WorldUnmanaged.IsCreated)
                return;

#if UNITY_EDITOR
            // Guard: Skip heavy work in editor when not playing
            if (!UnityEngine.Application.isPlaying)
                return;
#endif

            if (_initialized)
                return;

            _initialized = true;

            var em  = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            const int   villagerCount = 10;
            const float spacing       = 2f;

            int villageCount = 0;
            int homeCount = 0;
            int workCount = 0;

            for (int i = 0; i < villagerCount; i++)
            {
                float x = (i - villagerCount / 2) * spacing;

                float3 homePos = new float3(x, 0f, 0f);
                float3 workPos = new float3(x, 0f, 10f);

                // Home lot
                {
                    Entity home = ecb.CreateEntity();
                    ecb.AddComponent(home, new HomeLot    { Position = homePos });
                    ecb.AddComponent(home, LocalTransform.FromPosition(homePos));
                    ecb.AddComponent(home, new VillageTag());
                    ecb.AddComponent(home, new VisualProfile { Id = VisualProfileId.DebugHome });
                    homeCount++;
                    villageCount++;
                }

                // Work lot
                {
                    Entity work = ecb.CreateEntity();
                    ecb.AddComponent(work, new WorkLot    { Position = workPos });
                    ecb.AddComponent(work, LocalTransform.FromPosition(workPos));
                    ecb.AddComponent(work, new VillageTag());
                    ecb.AddComponent(work, new VisualProfile { Id = VisualProfileId.DebugWork });
                    workCount++;
                    villageCount++;
                }

                // Villager
                {
                    Entity villager = ecb.CreateEntity();
                    ecb.AddComponent(villager, new VillagerTag());
                    ecb.AddComponent(villager, new VillagerHome  { Position = homePos });
                    ecb.AddComponent(villager, new VillagerWork  { Position = workPos });
                    ecb.AddComponent(villager, new VillagerState { Phase    = 0 });
                    ecb.AddComponent(villager, LocalTransform.FromPosition(homePos));
                    ecb.AddComponent(villager, new VisualProfile { Id = VisualProfileId.DebugVillager });
                }
            }

            ecb.Playback(em);
            ecb.Dispose();

            // Ensure visual systems can run by providing the world-level tag singleton.
            if (!SystemAPI.HasSingleton<VillageWorldTag>())
            {
                var worldTagEntity = em.CreateEntity();
                em.AddComponent<VillageWorldTag>(worldTagEntity);
            }

            UnityEngine.Debug.Log($"[VillageScenarioBootstrapSystem] World '{state.WorldUnmanaged.Name}': spawned {villagerCount} Villagers, {homeCount} Homes, {workCount} Works.");
        }
    }
}
#endif
