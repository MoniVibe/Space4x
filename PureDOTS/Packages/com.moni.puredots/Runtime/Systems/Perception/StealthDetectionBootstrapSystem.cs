using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Detection;
using PureDOTS.Runtime.Perception;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Perception
{
    /// <summary>
    /// Bootstrap system that ensures entities with StealthStats get StealthModifiers and StealthProfile components.
    /// Runs once at startup or when entities are created.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PerceptionSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(ObstacleGridBootstrapSystem))]
    public partial struct StealthDetectionBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<SimulationFeatureFlags>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var features = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((features.Flags & SimulationFeatureFlags.PerceptionEnabled) == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();

            // Add StealthModifiers and StealthProfile to entities with StealthStats
            foreach (var (stats, entity) in SystemAPI.Query<RefRO<StealthStats>>()
                .WithNone<StealthModifiers, StealthProfile>()
                .WithEntityAccess())
            {
                // Add StealthModifiers with default values
                state.EntityManager.AddComponent<StealthModifiers>(entity);
                var modifiers = StealthModifiers.Default;
                modifiers.LastUpdateTick = timeState.Tick;
                state.EntityManager.SetComponentData(entity, modifiers);

                // Add StealthProfile
                state.EntityManager.AddComponent<StealthProfile>(entity);
                var profile = StealthProfile.Default;
                profile.Level = (StealthLevel)stats.ValueRO.CurrentState;
                profile.BaseRating = stats.ValueRO.BaseStealthRating;
                profile.LastUpdateTick = timeState.Tick;
                state.EntityManager.SetComponentData(entity, profile);
            }
        }
    }
}

