using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Villagers
{
    /// <summary>
    /// System that evaluates initiative and triggers autonomous life-changing decisions.
    /// When initiative threshold is met, villagers make major decisions (family, business, revenge, etc.)
    /// Based on Villager_Behavioral_Personality.md autonomous action selection.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    [UpdateAfter(typeof(VillagerInitiativeSystem))]
    public partial struct VillagerAutonomousActionSystem : ISystem
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
            if (timeState.IsPaused) return;

            var job = new EvaluateAutonomousActionsJob
            {
                CurrentTick = timeState.Tick
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        // Job cannot be Burst-compiled when using FixedString32Bytes construction from string literals (calls managed code)
        public partial struct EvaluateAutonomousActionsJob : IJobEntity
        {
            public uint CurrentTick;

            public void Execute(
                ref VillagerInitiativeState initiativeState,
                ref VillagerBehavior behavior,
                in VillagerAlignment alignment,
                in VillagerNeeds needs)
            {
                // Check if it's time for an autonomous action
                if (CurrentTick < initiativeState.NextActionTick)
                {
                    return; // Not time yet
                }

                // Initiative threshold check
                // Higher initiative = more likely to act
                // For now, use a simple probability based on initiative
                // TODO: Implement weighted action tables based on personality
                
                if (behavior.ActiveGrudgeCount > 0 && behavior.IsVengeful)
                {
                    // TODO: Replace placeholder with real grudge-driven action selection once deterministic tables are defined.
                    initiativeState.PendingAction = default;
                    behavior.LastMajorActionTick = CurrentTick;
                    return;
                }

                // TODO: Replace placeholder with data-driven action lookup (blob assets) once available.
                // For now we just clear any pending action to indicate an intent was evaluated this tick.
                initiativeState.PendingAction = default;

                behavior.LastMajorActionTick = CurrentTick;

                // TODO: Enqueue actual job/command requests based on pending action
                // This would involve:
                // - Creating job tickets for business ventures
                // - Triggering courtship/relationship systems
                // - Forming bands for revenge/adventure
                // - Opening shops/workshops
            }
        }
    }
}

