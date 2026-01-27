using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Interrupts
{
    /// <summary>
    /// Ensures default intent commitment components exist for entities using EntityIntent.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct IntentCommitmentBootstrapSystem : ISystem
    {
        // Keep OnCreate non-burst; burst direct-call recursion can stack overflow in headless player startup.
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        // Keep OnUpdate non-burst; burst direct-call recursion can stack overflow in headless player startup.
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
            var defaultConfig = IntentCommitmentConfig.Default;

            foreach (var (_, entity) in SystemAPI.Query<RefRO<EntityIntent>>()
                         .WithNone<IntentCommitmentConfig>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, defaultConfig);
                ecb.AddComponent(entity, new IntentCommitmentState
                {
                    LockUntilTick = 0,
                    CooldownUntilTick = 0,
                    LastIntentTick = 0
                });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<IntentCommitmentConfig>>()
                         .WithNone<IntentCommitmentState>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new IntentCommitmentState
                {
                    LockUntilTick = 0,
                    CooldownUntilTick = 0,
                    LastIntentTick = 0
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
