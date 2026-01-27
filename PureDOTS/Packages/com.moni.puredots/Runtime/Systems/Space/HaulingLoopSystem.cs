using PureDOTS.Runtime.Space;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct HaulingLoopSystem : ISystem
    {
        private ComponentLookup<ResourcePile> _pileLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<StorehouseInventory> _storehouseInventoryLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HaulingLoopState>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _pileLookup = state.GetComponentLookup<ResourcePile>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _storehouseInventoryLookup = state.GetComponentLookup<StorehouseInventory>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            _pileLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _storehouseInventoryLookup.Update(ref state);

            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // Process haulers using IJobEntity for zero-GC, Burst-friendly processing
            var processJob = new ProcessHaulingJob
            {
                DeltaTime = deltaTime,
                PileLookup = _pileLookup,
                TransformLookup = _transformLookup,
                StorehouseInventoryLookup = _storehouseInventoryLookup
            };
            state.Dependency = processJob.ScheduleParallel(state.Dependency);

            // Process piles for cleanup/destruction using IJobEntity
            var cleanupJob = new CleanupPilesJob
            {
                ECB = ecb.AsParallelWriter()
            };
            state.Dependency = cleanupJob.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct ProcessHaulingJob : IJobEntity
        {
            public float DeltaTime;
            public ComponentLookup<ResourcePile> PileLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            public ComponentLookup<StorehouseInventory> StorehouseInventoryLookup;

            public void Execute(Entity entity, ref HaulingLoopState loopState, in HaulingLoopConfig config, ref HaulingJob job, in LocalTransform transform)
            {
                var haulerPosition = transform.Position;

                switch (loopState.Phase)
                {
                    case HaulingLoopPhase.Idle:
                        if (job.SourceEntity == Entity.Null || !PileLookup.HasComponent(job.SourceEntity))
                        {
                            ClearJob(ref job);
                            break;
                        }

                        loopState.Phase = HaulingLoopPhase.TravellingToPickup;
                        loopState.PhaseTimer = ComputeTravelTime(haulerPosition, PileLookup[job.SourceEntity].Position, config.TravelSpeedMetersPerSecond);
                        break;

                    case HaulingLoopPhase.TravellingToPickup:
                        if (job.SourceEntity == Entity.Null || !PileLookup.HasComponent(job.SourceEntity))
                        {
                            loopState.Phase = HaulingLoopPhase.Idle;
                            ClearJob(ref job);
                            break;
                        }
                        loopState.PhaseTimer -= DeltaTime;
                        if (loopState.PhaseTimer <= 0f)
                        {
                            loopState.Phase = HaulingLoopPhase.Loading;
                        }
                        break;

                    case HaulingLoopPhase.Loading:
                        if (job.SourceEntity == Entity.Null || !PileLookup.HasComponent(job.SourceEntity))
                        {
                            loopState.Phase = HaulingLoopPhase.Idle;
                            ClearJob(ref job);
                            break;
                        }
                        var needed = config.MaxCargo - loopState.CurrentCargo;
                        if (needed <= 0.01f)
                        {
                            loopState.Phase = HaulingLoopPhase.TravellingToDropoff;
                            loopState.PhaseTimer = ComputeTravelTime(haulerPosition, DestinationPosition(job), config.TravelSpeedMetersPerSecond);
                            break;
                        }

                        var pile = PileLookup[job.SourceEntity];
                        var take = math.min(needed, math.min(config.LoadRatePerSecond * DeltaTime, pile.Amount));
                        pile.Amount -= take;
                        loopState.CurrentCargo += take;
                        PileLookup[job.SourceEntity] = pile;

                        if (loopState.CurrentCargo >= config.MaxCargo - 0.01f)
                        {
                            loopState.Phase = HaulingLoopPhase.TravellingToDropoff;
                            loopState.PhaseTimer = ComputeTravelTime(haulerPosition, DestinationPosition(job), config.TravelSpeedMetersPerSecond);
                        }
                        break;

                    case HaulingLoopPhase.TravellingToDropoff:
                        if (job.DestinationEntity == Entity.Null || !StorehouseInventoryLookup.HasComponent(job.DestinationEntity))
                        {
                            loopState.Phase = HaulingLoopPhase.Idle;
                            ClearJob(ref job);
                            break;
                        }
                        loopState.PhaseTimer -= DeltaTime;
                        if (loopState.PhaseTimer <= 0f)
                        {
                            loopState.Phase = HaulingLoopPhase.Unloading;
                        }
                        break;

                    case HaulingLoopPhase.Unloading:
                        if (job.DestinationEntity == Entity.Null || !StorehouseInventoryLookup.HasComponent(job.DestinationEntity))
                        {
                            loopState.CurrentCargo = 0f;
                            loopState.Phase = HaulingLoopPhase.Idle;
                            ClearJob(ref job);
                            break;
                        }

                        var unload = math.min(loopState.CurrentCargo, config.UnloadRatePerSecond * DeltaTime);
                        loopState.CurrentCargo -= unload;
                        var inventory = StorehouseInventoryLookup[job.DestinationEntity];
                        inventory.TotalStored += unload;
                        StorehouseInventoryLookup[job.DestinationEntity] = inventory;
                        if (loopState.CurrentCargo <= 0.01f)
                        {
                            loopState.Phase = HaulingLoopPhase.Idle;
                            loopState.CurrentCargo = 0f;
                            loopState.PhaseTimer = 0f;
                            ClearJob(ref job);
                        }
                        break;
                }
            }

            private static void ClearJob(ref HaulingJob job)
            {
                job.SourceEntity = Entity.Null;
                job.DestinationEntity = Entity.Null;
                job.RequestedAmount = 0f;
                job.Urgency = 0f;
                job.ResourceValue = 0f;
            }

            private static float ComputeTravelTime(float3 from, float3 to, float speed)
            {
                if (speed <= 0f)
                {
                    return 1f;
                }
                var distance = math.length(to - from);
                return math.max(0.1f, distance / speed);
            }

            private float3 DestinationPosition(HaulingJob job)
            {
                if (job.DestinationEntity != Entity.Null && TransformLookup.HasComponent(job.DestinationEntity))
                {
                    return TransformLookup[job.DestinationEntity].Position;
                }
                return float3.zero;
            }
        }

        [BurstCompile]
        private partial struct CleanupPilesJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute([EntityIndexInQuery] int entityInQueryIndex, Entity entity, in ResourcePile pile)
            {
                if (pile.Amount <= 0.01f)
                {
                    ECB.DestroyEntity(entityInQueryIndex, entity);
                }
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
