#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Mathematics.math;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Village
{
    /// <summary>
    /// Moves villagers back and forth between their home and work positions
    /// using simple placeholder logic:
    /// Phase 0: going to work
    /// Phase 1: going home
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [MovedFrom(true, "PureDOTS.Demo.Village", null, "VillagerWalkLoopSystem")]
    public partial struct VillagerWalkLoopSystem : ISystem
    {
        const float Speed         = 2f;   // units / second
        const float ArrivalRadius = 0.1f; // snap radius

        public void OnCreate(ref SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            UnityEngine.Debug.Log($"[VillagerWalkLoopSystem] OnCreate in world: {state.WorldUnmanaged.Name}");
            // Only run if there are villagers in this world
            state.RequireForUpdate<VillagerTag>();
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

            RunNonBursted(ref state);
        }

        [BurstDiscard]
        private void RunNonBursted(ref SystemState state)
        {
            // This is the sim delta time from Entities, NOT UnityEngine.Time.deltaTime.
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (transform,
                          home,
                          work,
                          vState) in SystemAPI
                          .Query<RefRW<LocalTransform>,
                                 RefRO<VillagerHome>,
                                 RefRO<VillagerWork>,
                                 RefRW<VillagerState>>()
                          .WithAll<VillagerTag>())
            {
                float3 current = transform.ValueRO.Position;

                // Phase: 0 = going to work, 1 = going home
                bool goingToWork = (vState.ValueRO.Phase == 0);
                float3 target    = goingToWork ? work.ValueRO.Position
                                               : home.ValueRO.Position;

                float3 toTarget = target - current;
                float distSq    = lengthsq(toTarget);

                // If close enough: snap + flip phase
                if (distSq < ArrivalRadius * ArrivalRadius)
                {
                    transform.ValueRW.Position = target;
                    vState.ValueRW.Phase       = (byte)(goingToWork ? 1 : 0);
                    continue;
                }

                float dist    = sqrt(distSq);
                float3 dir    = toTarget / dist;
                float maxStep = Speed * dt;
                float step    = min(maxStep, dist);

                transform.ValueRW.Position = current + dir * step;
            }
        }

        public void OnDestroy(ref SystemState state) { }
    }
}
#endif
