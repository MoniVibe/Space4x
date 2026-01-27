using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Keeps <see cref="FixedStepSimulationSystemGroup"/> aligned with the deterministic
    /// timestep defined by <see cref="TimeState"/>. The active fixed step duration is
    /// mirrored into the <see cref="GameplayFixedStep"/> singleton for gameplay systems
    /// that need direct access to the cadence.
    /// </summary>
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(TimeSettingsConfigSystem))]
    public partial class GameplayFixedStepSyncSystem : SystemBase
    {
        private FixedStepSimulationSystemGroup _fixedStepGroup;

        protected override void OnCreate()
        {
            RequireForUpdate<TimeState>();
            RequireForUpdate<TickTimeState>();
            RequireForUpdate<RewindState>();
            RequireForUpdate<GameplayFixedStep>();
            _fixedStepGroup = World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
        }

        protected override void OnUpdate()
        {
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            var fixedStep = SystemAPI.GetSingletonRW<GameplayFixedStep>();

            if (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (_fixedStepGroup == null)
            {
                _fixedStepGroup = World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
                if (_fixedStepGroup == null)
                {
                    return;
                }
            }

            var delta = math.max(tickTimeState.FixedDeltaTime, 1e-4f);
            if (math.abs(fixedStep.ValueRO.FixedDeltaTime - delta) > 1e-6f)
            {
                fixedStep.ValueRW = new GameplayFixedStep
                {
                    FixedDeltaTime = delta
                };
            }

            if (_fixedStepGroup != null && math.abs(_fixedStepGroup.Timestep - delta) > 1e-6f)
            {
                _fixedStepGroup.Timestep = delta;
            }
        }
    }
}
