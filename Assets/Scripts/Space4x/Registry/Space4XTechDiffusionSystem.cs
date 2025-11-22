using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Applies deterministic tech diffusion progress and upgrades tech tiers when complete.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(GameplayFixedStepSyncSystem))]
    public partial struct Space4XTechDiffusionSystem : ISystem
    {
        private ComponentLookup<TechLevel> _techLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GameplayFixedStep>();

            _techLookup = state.GetComponentLookup<TechLevel>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = SystemAPI.GetSingleton<GameplayFixedStep>().FixedDeltaTime;

            _techLookup.Update(ref state);

            var hasTelemetry = SystemAPI.TryGetSingletonEntity<TechDiffusionTelemetry>(out var telemetryEntity);
            var telemetry = hasTelemetry ? state.EntityManager.GetComponentData<TechDiffusionTelemetry>(telemetryEntity) : default;
            var hasLog = hasTelemetry && state.EntityManager.HasBuffer<TechDiffusionCommandLogEntry>(telemetryEntity);
            var log = hasLog ? state.EntityManager.GetBuffer<TechDiffusionCommandLogEntry>(telemetryEntity) : default;
            var activeCount = 0;
            uint completedCount = 0;

            foreach (var (diffusion, entity) in SystemAPI.Query<RefRW<TechDiffusionState>>().WithEntityAccess())
            {
                if (diffusion.ValueRO.Active == 0 || diffusion.ValueRO.DiffusionDurationSeconds <= 0f)
                {
                    continue;
                }

                if (!_techLookup.HasComponent(entity))
                {
                    continue;
                }

                activeCount++;
                var progress = diffusion.ValueRO.DiffusionProgressSeconds + math.max(0f, deltaTime);
                var duration = math.max(1e-4f, diffusion.ValueRO.DiffusionDurationSeconds);
                var completed = progress + 1e-4f >= duration;

                if (completed)
                {
                    progress = duration;

                    var tech = _techLookup[entity];
                    tech.MiningTech = (byte)math.max((int)tech.MiningTech, (int)diffusion.ValueRO.TargetMiningTech);
                    tech.CombatTech = (byte)math.max((int)tech.CombatTech, (int)diffusion.ValueRO.TargetCombatTech);
                    tech.HaulingTech = (byte)math.max((int)tech.HaulingTech, (int)diffusion.ValueRO.TargetHaulingTech);
                    tech.ProcessingTech = (byte)math.max((int)tech.ProcessingTech, (int)diffusion.ValueRO.TargetProcessingTech);
                    tech.LastUpgradeTick = time.Tick;
                    _techLookup[entity] = tech;

                    diffusion.ValueRW.Active = 0;

                    if (hasLog)
                    {
                        log.Add(new TechDiffusionCommandLogEntry
                        {
                            Tick = time.Tick,
                            TargetEntity = entity,
                            SourceEntity = diffusion.ValueRO.SourceEntity,
                            MiningTech = tech.MiningTech,
                            CombatTech = tech.CombatTech,
                            HaulingTech = tech.HaulingTech,
                            ProcessingTech = tech.ProcessingTech
                        });
                    }

                    completedCount += 1;
                    activeCount--;
                }

                diffusion.ValueRW.DiffusionProgressSeconds = progress;
                if (diffusion.ValueRO.DiffusionStartTick == 0)
                {
                    diffusion.ValueRW.DiffusionStartTick = time.Tick;
                }
            }

            if (hasTelemetry)
            {
                telemetry.ActiveDiffusions = activeCount;
                telemetry.CompletedUpgrades += completedCount;
                telemetry.LastUpdateTick = time.Tick;
                if (completedCount > 0)
                {
                    telemetry.LastUpgradeTick = time.Tick;
                }

                state.EntityManager.SetComponentData(telemetryEntity, telemetry);
            }
        }
    }
}
