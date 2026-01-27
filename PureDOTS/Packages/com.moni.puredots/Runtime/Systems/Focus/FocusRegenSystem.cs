using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Focus;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Focus
{
    /// <summary>
    /// Regenerates focus over time based on BaseRegenRate.
    /// Applies drain from active abilities.
    /// Runs early in FocusSystemGroup.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FocusSystemGroup))]
    public partial struct FocusRegenSystem : ISystem
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
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            new FocusRegenJob
            {
                DeltaTime = deltaTime,
                CurrentTick = currentTick
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct FocusRegenJob : IJobEntity
        {
            public float DeltaTime;
            public uint CurrentTick;

            void Execute(ref EntityFocus focus)
            {
                // Skip if in coma
                if (focus.IsInComa)
                {
                    return;
                }

                // Calculate net focus change
                float netRate = focus.BaseRegenRate - focus.TotalDrainRate;
                float focusChange = netRate * DeltaTime;

                // Apply change
                focus.CurrentFocus += focusChange;

                // Clamp to valid range
                if (focus.CurrentFocus < 0f)
                {
                    focus.CurrentFocus = 0f;
                }
                else if (focus.CurrentFocus > focus.MaxFocus)
                {
                    focus.CurrentFocus = focus.MaxFocus;
                }

                focus.LastUpdateTick = CurrentTick;
            }
        }
    }

    /// <summary>
    /// System group for focus-related systems.
    /// Runs in SimulationSystemGroup.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FocusSystemGroup : ComponentSystemGroup { }
}

