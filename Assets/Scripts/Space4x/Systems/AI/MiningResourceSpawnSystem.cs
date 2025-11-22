using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Converts miner cargo crossing a chunk threshold into SpawnResource pickups for carriers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselGatheringSystem))]
    public partial struct MiningResourceSpawnSystem : ISystem
    {
        private const float SpawnThresholdFraction = 0.5f;
        private const float SpawnChunkFraction = 0.5f;
        private const float YieldSpawnFallbackFraction = 0.25f;

        private EntityQuery _vesselQuery;
        private ComponentLookup<MiningYield> _yieldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _vesselQuery = SystemAPI.QueryBuilder()
                .WithAll<MiningVessel, SpawnResourceRequest, LocalTransform>()
                .Build();

            _yieldLookup = state.GetComponentLookup<MiningYield>(false);
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_vesselQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();

            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _yieldLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var hasCommandLog = SystemAPI.TryGetSingletonBuffer<MiningCommandLogEntry>(out var commandLog);

            foreach (var (vessel, transform, requests, entity) in SystemAPI
                         .Query<RefRW<MiningVessel>, RefRO<LocalTransform>, DynamicBuffer<SpawnResourceRequest>>()
                         .WithEntityAccess())
            {
                var vesselValue = vessel.ValueRO;
                var hasYield = _yieldLookup.HasComponent(entity);
                var spawnType = vesselValue.CargoResourceType;
                var pendingCargo = vesselValue.CurrentCargo;
                var yieldThreshold = 0f;

                if (hasYield)
                {
                    var yield = _yieldLookup[entity];
                    yieldThreshold = yield.SpawnThreshold;

                    spawnType = Space4XMiningResourceUtility.MapToResourceType(yield.ResourceId, spawnType);
                    if (vesselValue.CargoResourceType != spawnType)
                    {
                        vesselValue.CargoResourceType = spawnType;
                    }
                }

                var threshold = hasYield
                    ? DetermineYieldThreshold(yieldThreshold, vesselValue.CargoCapacity)
                    : math.max(1f, vesselValue.CargoCapacity * SpawnThresholdFraction);
                var chunkSize = hasYield
                    ? math.max(1f, threshold)
                    : math.max(1f, vesselValue.CargoCapacity * SpawnChunkFraction);

                while (pendingCargo >= threshold)
                {
                    var chunkAmount = math.min(pendingCargo, chunkSize);
                    requests.Add(new SpawnResourceRequest
                    {
                        Type = spawnType,
                        Amount = chunkAmount,
                        Position = transform.ValueRO.Position,
                        SourceEntity = entity,
                        RequestedTick = timeState.Tick
                    });
                    pendingCargo -= chunkAmount;
                }

                vesselValue.CurrentCargo = pendingCargo;

                if (hasYield)
                {
                    var yield = _yieldLookup[entity];
                    yield.PendingAmount = pendingCargo;
                    yield.SpawnReady = pendingCargo >= threshold ? (byte)1 : (byte)0;
                    _yieldLookup[entity] = yield;
                }

                if (math.abs(vesselValue.CurrentCargo - vessel.ValueRO.CurrentCargo) > 0.001f ||
                    vesselValue.CargoResourceType != vessel.ValueRO.CargoResourceType)
                {
                    vessel.ValueRW = vesselValue;
                }

                if (requests.Length == 0)
                {
                    continue;
                }

                for (var i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    var spawnEntity = ecb.CreateEntity();
                    ecb.AddComponent(spawnEntity, LocalTransform.FromPositionRotationScale(request.Position, quaternion.identity, 1f));
                    ecb.AddComponent(spawnEntity, new SpawnResource
                    {
                        Type = request.Type,
                        Amount = request.Amount,
                        SourceEntity = request.SourceEntity,
                        SpawnTick = timeState.Tick
                    });

                    if (hasCommandLog)
                    {
                        commandLog.Add(new MiningCommandLogEntry
                        {
                            Tick = timeState.Tick,
                            CommandType = MiningCommandType.Spawn,
                            SourceEntity = request.SourceEntity,
                            TargetEntity = spawnEntity,
                            ResourceType = request.Type,
                            Amount = request.Amount,
                            Position = request.Position
                        });
                    }
                }

                requests.Clear();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static float DetermineYieldThreshold(float yieldThreshold, float cargoCapacity)
        {
            if (yieldThreshold > 0f)
            {
                return yieldThreshold;
            }

            return math.max(1f, cargoCapacity * YieldSpawnFallbackFraction);
        }
    }
}
