using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Logistics.Components;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Moves hauler shuttles between carriers and colonies to deliver mined resources.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.CarrierPickupSystem))]
    [UpdateBefore(typeof(Space4XCarrierResourceExportSystem))]
    public partial struct Space4XHaulerShuttleSystem : ISystem
    {
        private EntityQuery _carrierQuery;
        private EntityQuery _colonyQuery;
        private EntityQuery _missingStateQuery;

        private BufferLookup<ResourceStorage> _storageLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<ColonyIndustryStock> _stockLookup;
        private ComponentLookup<Space4XColony> _colonyLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<HaulerCapacity> _capacityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, ResourceStorage, LocalTransform>()
                .Build();

            _colonyQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XColony, ColonyIndustryStock>()
                .Build();

            _missingStateQuery = SystemAPI.QueryBuilder()
                .WithAll<HaulerTag>()
                .WithNone<Space4XHaulerShuttleState>()
                .Build();

            _storageLookup = state.GetBufferLookup<ResourceStorage>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _stockLookup = state.GetComponentLookup<ColonyIndustryStock>(false);
            _colonyLookup = state.GetComponentLookup<Space4XColony>(false);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _capacityLookup = state.GetComponentLookup<HaulerCapacity>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            if (_carrierQuery.IsEmptyIgnoreFilter || _colonyQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var config = Space4XHaulerShuttleConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XHaulerShuttleConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var deltaTime = math.max(0f, timeState.FixedDeltaTime);
            var pickupRadiusSq = config.PickupRadius * config.PickupRadius;
            var dropoffRadiusSq = config.DropoffRadius * config.DropoffRadius;

            _storageLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _stockLookup.Update(ref state);
            _colonyLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _capacityLookup.Update(ref state);

            if (!_missingStateQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<Space4XHaulerShuttleState>(_missingStateQuery);
            }

            using var carriers = _carrierQuery.ToEntityArray(Allocator.Temp);
            using var colonies = _colonyQuery.ToEntityArray(Allocator.Temp);

            float deliveredTotal = 0f;

            foreach (var (stateRW, transformRW, haulerEntity) in SystemAPI.Query<RefRW<Space4XHaulerShuttleState>, RefRW<LocalTransform>>()
                         .WithAll<HaulerTag>()
                         .WithEntityAccess())
            {
                var shuttle = stateRW.ValueRW;
                var position = transformRW.ValueRO.Position;

                if (shuttle.Phase == Space4XHaulerShuttlePhase.Idle)
                {
                    if (TryAssignCarrier(position, config.MinCarrierLoad, carriers, colonies, out var carrier, out var colony, out var cargoType))
                    {
                        shuttle.TargetCarrier = carrier;
                        shuttle.TargetColony = colony;
                        shuttle.CargoType = cargoType;
                        shuttle.CargoAmount = 0f;
                        shuttle.Phase = Space4XHaulerShuttlePhase.ToCarrier;
                    }

                    stateRW.ValueRW = shuttle;
                    continue;
                }

                if (shuttle.Phase == Space4XHaulerShuttlePhase.ToCarrier)
                {
                    if (shuttle.TargetCarrier == Entity.Null ||
                        !_transformLookup.HasComponent(shuttle.TargetCarrier) ||
                        !_storageLookup.HasBuffer(shuttle.TargetCarrier))
                    {
                        Reset(ref shuttle);
                        stateRW.ValueRW = shuttle;
                        continue;
                    }

                    var carrierPos = _transformLookup[shuttle.TargetCarrier].Position;
                    var distSq = math.distancesq(position, carrierPos);
                    if (distSq > pickupRadiusSq)
                    {
                        position = StepTowards(position, carrierPos, config.Speed * deltaTime);
                        transformRW.ValueRW = LocalTransform.FromPositionRotationScale(position, transformRW.ValueRO.Rotation, transformRW.ValueRO.Scale);
                        stateRW.ValueRW = shuttle;
                        continue;
                    }

                    var capacity = ResolveCapacity(haulerEntity);
                    if (capacity <= 0f)
                    {
                        Reset(ref shuttle);
                        stateRW.ValueRW = shuttle;
                        continue;
                    }

                    var storage = _storageLookup[shuttle.TargetCarrier];
                    if (!TryTransferFromCarrier(ref storage, shuttle.CargoType, capacity, config.TransferRatePerSecond * deltaTime, ref shuttle))
                    {
                        Reset(ref shuttle);
                        stateRW.ValueRW = shuttle;
                        continue;
                    }

                    _storageLookup[shuttle.TargetCarrier] = storage;

                    if (shuttle.CargoAmount >= capacity - 1e-3f)
                    {
                        shuttle.Phase = Space4XHaulerShuttlePhase.ToColony;
                    }

                    stateRW.ValueRW = shuttle;
                    continue;
                }

                if (shuttle.Phase == Space4XHaulerShuttlePhase.ToColony)
                {
                    if (shuttle.TargetColony == Entity.Null || !_transformLookup.HasComponent(shuttle.TargetColony))
                    {
                        Reset(ref shuttle);
                        stateRW.ValueRW = shuttle;
                        continue;
                    }

                    var colonyPos = _transformLookup[shuttle.TargetColony].Position;
                    var distSq = math.distancesq(position, colonyPos);
                    if (distSq > dropoffRadiusSq)
                    {
                        position = StepTowards(position, colonyPos, config.Speed * deltaTime);
                        transformRW.ValueRW = LocalTransform.FromPositionRotationScale(position, transformRW.ValueRO.Rotation, transformRW.ValueRO.Scale);
                        stateRW.ValueRW = shuttle;
                        continue;
                    }

                    if (_stockLookup.HasComponent(shuttle.TargetColony) && shuttle.CargoAmount > 1e-3f)
                    {
                        var stock = _stockLookup[shuttle.TargetColony];
                        if (TryApplyToStock(ref stock, shuttle.CargoType, shuttle.CargoAmount))
                        {
                            deliveredTotal += shuttle.CargoAmount;
                            shuttle.CargoAmount = 0f;
                            stock.LastUpdateTick = timeState.Tick;
                            _stockLookup[shuttle.TargetColony] = stock;

                            if (_colonyLookup.HasComponent(shuttle.TargetColony))
                            {
                                var colony = _colonyLookup[shuttle.TargetColony];
                                colony.StoredResources = math.max(0f, stock.OreReserve + stock.SuppliesReserve + stock.ResearchReserve);
                                _colonyLookup[shuttle.TargetColony] = colony;
                            }
                        }
                    }

                    Reset(ref shuttle);
                    stateRW.ValueRW = shuttle;
                }
            }

            if (deliveredTotal > 0f &&
                SystemAPI.TryGetSingleton<TelemetryExportConfig>(out var exportConfig) &&
                exportConfig.Enabled != 0 &&
                (exportConfig.Flags & TelemetryExportFlags.IncludeTelemetryMetrics) != 0 &&
                SystemAPI.TryGetSingletonBuffer<TelemetryMetric>(out var telemetry))
            {
                var cadence = exportConfig.CadenceTicks > 0 ? exportConfig.CadenceTicks : 30u;
                if (timeState.Tick % cadence == 0)
                {
                    telemetry.AddMetric("space4x.mining.haulerDelivered", deliveredTotal, TelemetryMetricUnit.Custom);
                }
            }
        }

        private bool TryAssignCarrier(
            float3 haulerPosition,
            float minCarrierLoad,
            NativeArray<Entity> carriers,
            NativeArray<Entity> colonies,
            out Entity carrier,
            out Entity colony,
            out ResourceType cargoType)
        {
            carrier = Entity.Null;
            colony = Entity.Null;
            cargoType = ResourceType.Minerals;

            float bestDistanceSq = float.MaxValue;
            float bestAmount = 0f;

            for (int i = 0; i < carriers.Length; i++)
            {
                var candidate = carriers[i];
                if (candidate == Entity.Null || !_transformLookup.HasComponent(candidate) || !_storageLookup.HasBuffer(candidate))
                {
                    continue;
                }

                var storage = _storageLookup[candidate];
                var total = 0f;
                var dominantType = ResourceType.Minerals;
                var dominantAmount = 0f;

                for (int j = 0; j < storage.Length; j++)
                {
                    var slot = storage[j];
                    total += math.max(0f, slot.Amount);
                    if (slot.Amount > dominantAmount)
                    {
                        dominantAmount = slot.Amount;
                        dominantType = slot.Type;
                    }
                }

                if (total < minCarrierLoad || dominantAmount <= 1e-3f)
                {
                    continue;
                }

                var candidatePos = _transformLookup[candidate].Position;
                var distSq = math.distancesq(haulerPosition, candidatePos);

                if (distSq < bestDistanceSq || (math.abs(distSq - bestDistanceSq) < 0.01f && dominantAmount > bestAmount))
                {
                    var resolvedColony = ResolveTargetColony(candidate, candidatePos, colonies);
                    if (resolvedColony == Entity.Null)
                    {
                        continue;
                    }

                    carrier = candidate;
                    colony = resolvedColony;
                    cargoType = dominantType;
                    bestDistanceSq = distSq;
                    bestAmount = dominantAmount;
                }
            }

            return carrier != Entity.Null && colony != Entity.Null;
        }

        private Entity ResolveTargetColony(Entity carrier, float3 carrierPosition, NativeArray<Entity> colonies)
        {
            var affiliationTarget = Entity.Null;
            if (_affiliationLookup.HasBuffer(carrier))
            {
                var affiliations = _affiliationLookup[carrier];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var affiliation = affiliations[i];
                    if (affiliation.Type == AffiliationType.Colony && affiliation.Target != Entity.Null)
                    {
                        return affiliation.Target;
                    }
                }

                for (int i = 0; i < affiliations.Length; i++)
                {
                    var affiliation = affiliations[i];
                    if (affiliation.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (affiliation.Type == AffiliationType.Faction || affiliation.Type == AffiliationType.Empire)
                    {
                        affiliationTarget = affiliation.Target;
                        break;
                    }
                }
            }

            Entity best = Entity.Null;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < colonies.Length; i++)
            {
                var colony = colonies[i];
                if (colony == Entity.Null || !_transformLookup.HasComponent(colony))
                {
                    continue;
                }

                if (affiliationTarget != Entity.Null)
                {
                    if (!_affiliationLookup.HasBuffer(colony))
                    {
                        continue;
                    }

                    var affiliations = _affiliationLookup[colony];
                    var match = false;
                    for (int j = 0; j < affiliations.Length; j++)
                    {
                        var entry = affiliations[j];
                        if ((entry.Type == AffiliationType.Faction || entry.Type == AffiliationType.Empire) && entry.Target == affiliationTarget)
                        {
                            match = true;
                            break;
                        }
                    }

                    if (!match)
                    {
                        continue;
                    }
                }

                var distSq = math.distancesq(_transformLookup[colony].Position, carrierPosition);
                if (distSq < bestDistSq)
                {
                    best = colony;
                    bestDistSq = distSq;
                }
            }

            return best;
        }

        private float ResolveCapacity(Entity hauler)
        {
            if (_capacityLookup.HasComponent(hauler))
            {
                var cap = _capacityLookup[hauler];
                return math.max(0f, cap.MaxMass);
            }

            return 0f;
        }

        private static bool TryTransferFromCarrier(ref DynamicBuffer<ResourceStorage> storage, ResourceType type, float capacity, float stepLimit, ref Space4XHaulerShuttleState state)
        {
            if (capacity <= 0f)
            {
                return false;
            }

            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type != type)
                {
                    continue;
                }

                var slot = storage[i];
                var available = math.max(0f, slot.Amount);
                if (available <= 1e-4f)
                {
                    return false;
                }

                var remainingCapacity = math.max(0f, capacity - state.CargoAmount);
                if (remainingCapacity <= 1e-4f)
                {
                    return true;
                }

                var transfer = math.min(available, math.min(remainingCapacity, stepLimit));
                if (transfer <= 0f)
                {
                    return false;
                }

                slot.Amount -= transfer;
                storage[i] = slot;
                state.CargoAmount += transfer;
                return true;
            }

            return false;
        }

        private static bool TryApplyToStock(ref ColonyIndustryStock stock, ResourceType type, float amount)
        {
            switch (type)
            {
                case ResourceType.Minerals:
                case ResourceType.RareMetals:
                case ResourceType.Ore:
                    stock.OreReserve += amount;
                    return true;
                case ResourceType.EnergyCrystals:
                case ResourceType.OrganicMatter:
                    stock.SuppliesReserve += amount;
                    return true;
                default:
                    return false;
            }
        }

        private static float3 StepTowards(float3 current, float3 target, float maxDistance)
        {
            var delta = target - current;
            var distance = math.length(delta);
            if (distance <= maxDistance || distance <= 1e-5f)
            {
                return target;
            }

            return current + delta / distance * maxDistance;
        }

        private static void Reset(ref Space4XHaulerShuttleState shuttle)
        {
            shuttle.TargetCarrier = Entity.Null;
            shuttle.TargetColony = Entity.Null;
            shuttle.CargoAmount = 0f;
            shuttle.Phase = Space4XHaulerShuttlePhase.Idle;
        }
    }
}
