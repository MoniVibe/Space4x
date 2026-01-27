using PureDOTS.Runtime;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct MiningLoopSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MiningLoopState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Gate on scenario initialization
            if (SystemAPI.TryGetSingleton<ScenarioState>(out var scenario) && !scenario.IsInitialized)
            {
                return;
            }

            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (loopStateRW, harvesterConfig, loopConfig) in SystemAPI
                         .Query<RefRW<MiningLoopState>, RefRO<HarvesterConfig>, RefRO<MiningLoopConfig>>())
            {
                ref var loopState = ref loopStateRW.ValueRW;
                var config = harvesterConfig.ValueRO;
                var loop = loopConfig.ValueRO;

                switch (loopState.Phase)
                {
                    case MiningLoopPhase.Idle:
                        loopState.Phase = MiningLoopPhase.TravellingToHarvest;
                        loopState.PhaseTimer = math.max(0.1f, config.HarvestRadiusMeters / math.max(0.1f, loop.TravelSpeedMetersPerSecond));
                        break;

                    case MiningLoopPhase.TravellingToHarvest:
                        loopState.PhaseTimer -= deltaTime;
                        if (loopState.PhaseTimer <= 0f)
                        {
                            loopState.Phase = MiningLoopPhase.Harvesting;
                            loopState.PhaseTimer = 0f;
                        }
                        break;

                    case MiningLoopPhase.Harvesting:
                        loopState.CurrentCargo = math.min(loop.MaxCargo, loopState.CurrentCargo + loop.HarvestRatePerSecond * deltaTime);
                        if (loopState.CurrentCargo >= loop.MaxCargo - 0.01f)
                        {
                            loopState.Phase = MiningLoopPhase.TravellingToDropoff;
                            loopState.PhaseTimer = math.max(0.1f, config.ReturnDistanceMeters / math.max(0.1f, loop.TravelSpeedMetersPerSecond));
                        }
                        break;

                    case MiningLoopPhase.TravellingToDropoff:
                        loopState.PhaseTimer -= deltaTime;
                        if (loopState.PhaseTimer <= 0f)
                        {
                            loopState.Phase = MiningLoopPhase.DroppingOff;
                            var dropDuration = loopState.CurrentCargo / math.max(0.1f, loop.DropoffRatePerSecond);
                            loopState.PhaseTimer = math.max(config.DefaultDropoffIntervalSeconds, dropDuration);
                        }
                        break;

                    case MiningLoopPhase.DroppingOff:
                        loopState.PhaseTimer -= deltaTime;
                        loopState.CurrentCargo = math.max(0f, loopState.CurrentCargo - loop.DropoffRatePerSecond * deltaTime);
                        if (loopState.CurrentCargo <= 0.01f || loopState.PhaseTimer <= 0f)
                        {
                            loopState.CurrentCargo = 0f;
                            loopState.Phase = MiningLoopPhase.Idle;
                            loopState.PhaseTimer = 0f;
                        }
                        break;
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
