using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Assigns random phase offsets to entities for staggered updates.
    /// Ensures entities don't all update on the same tick, spreading work across multiple ticks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(NavPerformanceBudgetSystem))]
    public partial struct NavStaggerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            // Assign phase offsets to entities that need navigation updates but don't have UpdateCadence yet
            // This includes entities with PathRequest but no UpdateCadence
            foreach (var (pathRequest, entity) in
                SystemAPI.Query<RefRO<PathRequest>>()
                .WithNone<UpdateCadence>()
                .WithEntityAccess())
            {
                // Generate phase offset based on entity hash
                uint entityHash = (uint)entity.Index;
                uint cadence = 5; // Default: update every 5 ticks
                uint phaseOffset = entityHash % cadence;

                ecb.AddComponent(entity, UpdateCadence.CreateWithRandomPhase(cadence, entityHash));
            }

            // Also assign to entities with GroupNavComponent (groups need staggered updates)
            foreach (var (groupNav, entity) in
                SystemAPI.Query<RefRO<GroupNavComponent>>()
                .WithNone<UpdateCadence>()
                .WithEntityAccess())
            {
                uint entityHash = (uint)entity.Index;
                uint cadence = 10; // Groups update less frequently
                ecb.AddComponent(entity, UpdateCadence.CreateWithRandomPhase(cadence, entityHash));
            }

            ecb.Playback(state.EntityManager);
        }
    }
}

