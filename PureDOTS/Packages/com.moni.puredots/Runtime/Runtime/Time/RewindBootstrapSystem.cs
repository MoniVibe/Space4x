using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.Time
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct RewindBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RewindConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            if (SystemAPI.HasSingleton<RewindState>())
            {
                state.Enabled = false;
                return;
            }

            var config = SystemAPI.GetSingleton<RewindConfig>();

            var e = em.CreateEntity();
            em.AddComponentData(e, new RewindState
            {
                Mode = config.InitialMode,
                TargetTick = 0,
                TickDuration = config.TickDuration,
                MaxHistoryTicks = config.MaxHistoryTicks,
                PendingStepTicks = 0
            });
            em.AddComponentData(e, new RewindLegacyState
            {
                PlaybackSpeed = 1f,
                CurrentTick = 0,
                StartTick = 0,
                PlaybackTick = 0,
                PlaybackTicksPerSecond = config.TickDuration > 0f ? 1f / config.TickDuration : 60f,
                ScrubDirection = 0,
                ScrubSpeedMultiplier = 1f,
                RewindWindowTicks = 0,
                ActiveTrack = default
            });

            state.Enabled = false;
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
    }
}
