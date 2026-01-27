using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;

namespace PureDOTS.Systems.Relations
{
    /// <summary>
    /// System that processes relation events and updates relations.
    /// Processes RelationEvent buffers and creates RecordInteractionRequest components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Runtime.Systems.Social.RelationInteractionSystem))]
    public partial struct RelationUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var ecbSingleton = SystemAPI.GetSingletonRW<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.ValueRW.CreateCommandBuffer(state.WorldUnmanaged);

            // Process relation events from buffers
            foreach (var (events, entity) in SystemAPI.Query<DynamicBuffer<RelationEvent>>().WithEntityAccess())
            {
                for (int i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    
                    // Create RecordInteractionRequest for processing
                    var requestEntity = ecb.CreateEntity();
                    ecb.AddComponent(requestEntity, new RecordInteractionRequest
                    {
                        EntityA = evt.SourceEntity,
                        EntityB = evt.TargetEntity,
                        Outcome = evt.Outcome,
                        IntensityChange = evt.RelationDelta,
                        TrustChange = 0, // Will be calculated
                        IsMutual = true
                    });
                }
                
                // Clear processed events
                events.Clear();
            }

        }
    }
}

