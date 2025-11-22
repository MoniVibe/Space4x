using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Systems.AI;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Converts MiningYield readiness into SpawnResourceRequests so carriers can pick up mined output.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup))]
    [UpdateBefore(typeof(MiningResourceSpawnSystem))]
    public partial struct Space4XMiningYieldSpawnBridgeSystem : ISystem
    {
        private EntityQuery _query;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAll<MiningYield, MiningVessel, SpawnResourceRequest, LocalTransform>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_query);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();

            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (yield, vessel, transform, requests, entity) in SystemAPI
                         .Query<RefRW<MiningYield>, RefRW<MiningVessel>, RefRO<LocalTransform>, DynamicBuffer<SpawnResourceRequest>>()
                         .WithEntityAccess())
            {
                var threshold = DetermineThreshold(yield.ValueRO.SpawnThreshold, vessel.ValueRO.CargoCapacity);
                if (threshold <= 0f || yield.ValueRO.PendingAmount < threshold)
                {
                    yield.ValueRW.SpawnReady = yield.ValueRO.PendingAmount >= threshold ? (byte)1 : (byte)0;
                    continue;
                }

                var vesselValue = vessel.ValueRO;
                var resolvedType = Space4XMiningResourceUtility.MapToResourceType(yield.ValueRO.ResourceId, vesselValue.CargoResourceType);
                vesselValue.CargoResourceType = resolvedType;

                var pending = yield.ValueRO.PendingAmount;
                var chunkSize = math.max(threshold, 0.01f);
                var spawned = false;

                while (pending >= threshold - 1e-4f)
                {
                    var spawnAmount = math.min(pending, chunkSize);
                    requests.Add(new SpawnResourceRequest
                    {
                        Type = resolvedType,
                        Amount = spawnAmount,
                        Position = transform.ValueRO.Position,
                        SourceEntity = entity,
                        RequestedTick = time.Tick
                    });

                    pending -= spawnAmount;
                    vesselValue.CurrentCargo = math.max(0f, vesselValue.CurrentCargo - spawnAmount);
                    spawned = true;

                    if (spawnAmount <= 0f)
                    {
                        break;
                    }
                }

                yield.ValueRW.PendingAmount = pending;
                yield.ValueRW.SpawnReady = pending >= threshold ? (byte)1 : (byte)0;

                if (spawned || vesselValue.CargoResourceType != vessel.ValueRO.CargoResourceType)
                {
                    vessel.ValueRW = vesselValue;
                }
            }
        }

        private static float DetermineThreshold(float yieldThreshold, float cargoCapacity)
        {
            if (yieldThreshold > 0f)
            {
                return yieldThreshold;
            }

            return math.max(1f, cargoCapacity * 0.25f);
        }
    }
}
