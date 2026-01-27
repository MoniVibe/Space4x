using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// Assigns AI importance levels and update cadence based on entity type/role.
    /// Level 0 = hero/cinematic, Level 3 = background noise.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct AIImportanceSystem : ISystem
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

            // Assign importance to entities that need it but don't have it yet
            // This is a simple implementation - can be extended based on entity archetypes

            // Entities with PathRequest but no AIImportance get default importance
            foreach (var (pathRequest, entity) in
                SystemAPI.Query<RefRO<PathRequest>>()
                .WithNone<AIImportance>()
                .WithEntityAccess())
            {
                // Default to Normal importance (Level 2)
                ecb.AddComponent(entity, AIImportance.Normal());

                // Set update cadence based on importance
                uint cadence = GetCadenceForImportance(2);
                uint entityHash = (uint)entity.Index;
                ecb.AddComponent(entity, UpdateCadence.CreateWithRandomPhase(cadence, entityHash));
            }

            // Update cadence for entities with AIImportance but no UpdateCadence
            foreach (var (importance, entity) in
                SystemAPI.Query<RefRO<AIImportance>>()
                .WithNone<UpdateCadence>()
                .WithEntityAccess())
            {
                uint cadence = GetCadenceForImportance(importance.ValueRO.Level);
                uint entityHash = (uint)entity.Index;
                ecb.AddComponent(entity, UpdateCadence.CreateWithRandomPhase(cadence, entityHash));
            }

            // Update cadence for entities whose importance changed
            foreach (var (importance, cadence, entity) in
                SystemAPI.Query<RefRO<AIImportance>, RefRW<UpdateCadence>>()
                .WithEntityAccess())
            {
                uint expectedCadence = GetCadenceForImportance(importance.ValueRO.Level);
                if (cadence.ValueRO.UpdateCadenceValue != expectedCadence)
                {
                    uint entityHash = (uint)entity.Index;
                    cadence.ValueRW = UpdateCadence.CreateWithRandomPhase(expectedCadence, entityHash);
                }
            }

            ecb.Playback(state.EntityManager);
        }

        [BurstCompile]
        private static uint GetCadenceForImportance(byte importanceLevel)
        {
            return importanceLevel switch
            {
                0 => 1,   // Hero - every tick
                1 => 5,   // Important - every 5 ticks
                2 => 10,  // Normal - every 10 ticks
                3 => 20,  // Background - every 20 ticks
                _ => 10   // Default to normal
            };
        }
    }
}

