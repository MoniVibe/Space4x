using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Runtime.Perception;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Interrupts
{
    /// <summary>
    /// Structural-apply helper: ensures entities that emit/consume interrupts have an Interrupt buffer.
    /// This prevents structural changes inside hot perception loops.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct InterruptBufferEnsureSystem : ISystem
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

            foreach (var (_, entity) in SystemAPI.Query<RefRO<PerceptionState>>()
                         .WithNone<Interrupt>()
                         .WithEntityAccess())
            {
                ecb.AddBuffer<Interrupt>(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<SignalPerceptionState>>()
                         .WithNone<Interrupt, PerceptionState>()
                         .WithEntityAccess())
            {
                ecb.AddBuffer<Interrupt>(entity);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<CommsReceiverConfig>>()
                         .WithNone<Interrupt>()
                         .WithEntityAccess())
            {
                ecb.AddBuffer<Interrupt>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

