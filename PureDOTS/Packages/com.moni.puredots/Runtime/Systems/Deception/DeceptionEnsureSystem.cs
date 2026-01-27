using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Deception;
using PureDOTS.Runtime.Interrupts;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Deception
{
    /// <summary>
    /// Structural-apply helper: ensures observers have DisguiseDiscovery buffer and Interrupt buffer.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DeceptionEnsureSystem : ISystem
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
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind) || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<DeceptionObserverConfig>>()
                         .WithEntityAccess())
            {
                if (!state.EntityManager.HasBuffer<DisguiseDiscovery>(entity))
                {
                    ecb.AddBuffer<DisguiseDiscovery>(entity);
                }
                if (!state.EntityManager.HasBuffer<Interrupt>(entity))
                {
                    ecb.AddBuffer<Interrupt>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}





