using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Gates Simulation + FixedStep groups based on TickTimeState and RewindState.
    /// Simulation is halted during rewind playback and while paused; fixed-step only runs while actively playing.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(RewindCoordinatorSystem))]
    public partial struct SimulationTickGateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TickTimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewind))
            {
                return;
            }

            var simulationGroup = state.World.GetExistingSystemManaged<SimulationSystemGroup>();
            if (simulationGroup != null)
            {
                bool allowSimulation = (rewind.Mode == RewindMode.Record && tick.IsPlaying) ||
                                       rewind.Mode == RewindMode.CatchUp;
                if (simulationGroup.Enabled != allowSimulation)
                {
                    simulationGroup.Enabled = allowSimulation;
                }
            }

            var fixedGroup = state.World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedGroup != null)
            {
                bool allowFixed = rewind.Mode == RewindMode.Record && tick.IsPlaying;
                if (fixedGroup.Enabled != allowFixed)
                {
                    fixedGroup.Enabled = allowFixed;
                }
            }
        }
    }
}
