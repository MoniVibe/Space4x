using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Time
{
    /// <summary>
    /// Seeds a default TimeState singleton if none exists so GetSingleton<TimeState>() is always safe.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial struct TimeBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // If TimeState already exists, nothing to do.
            if (SystemAPI.TryGetSingleton<TimeState>(out _))
            {
                state.Enabled = false;
                return;
            }

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new TimeState
            {
                Tick = 0,
                DeltaTime = 1f / 60f,
                DeltaSeconds = 1f / 60f,
                ElapsedTime = 0f,
                WorldSeconds = 0f,
                IsPaused = false,
                FixedDeltaTime = 1f / 60f,
                CurrentSpeedMultiplier = 1f
            });

            // One-shot bootstrap.
            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // No runtime work.
        }
    }
}
