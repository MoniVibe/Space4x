using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
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
    /// Exports carrier-held resources into colony industry stock so production can consume mining output.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.CarrierPickupSystem))]
    public partial struct Space4XCarrierResourceExportSystem : ISystem
    {
        private EntityQuery _colonyQuery;
        private ComponentLookup<ColonyIndustryStock> _stockLookup;
        private ComponentLookup<Space4XColony> _colonyLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _colonyQuery = SystemAPI.QueryBuilder()
                .WithAll<Space4XColony, ColonyIndustryStock>()
                .Build();

            _stockLookup = state.GetComponentLookup<ColonyIndustryStock>(false);
            _colonyLookup = state.GetComponentLookup<Space4XColony>(false);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
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

            if (_colonyQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var config = Space4XCarrierExportConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XCarrierExportConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            var deltaTime = math.max(0f, timeState.FixedDeltaTime);
            var transferBudget = math.max(0f, config.TransferRatePerSecond) * deltaTime;
            if (transferBudget <= 0f)
            {
                return;
            }

            var maxDistanceSq = config.MaxTransferDistance > 0f
                ? config.MaxTransferDistance * config.MaxTransferDistance
                : -1f;

            _stockLookup.Update(ref state);
            _colonyLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            using var colonyEntities = _colonyQuery.ToEntityArray(Allocator.Temp);

            foreach (var (storage, transform, carrier) in SystemAPI.Query<DynamicBuffer<ResourceStorage>, RefRO<LocalTransform>>()
                         .WithAll<Carrier>()
                         .WithEntityAccess())
            {
                if (storage.Length == 0)
                {
                    continue;
                }

                var targetColony = ResolveTargetColony(carrier, transform.ValueRO.Position, maxDistanceSq, colonyEntities);
                if (targetColony == Entity.Null || !_stockLookup.HasComponent(targetColony))
                {
                    continue;
                }

                var stock = _stockLookup[targetColony];
                var remaining = transferBudget;
                var updated = false;

                for (int i = 0; i < storage.Length && remaining > 1e-4f; i++)
                {
                    var slot = storage[i];
                    if (slot.Amount <= 1e-4f)
                    {
                        continue;
                    }

                    var transfer = math.min(slot.Amount, remaining);
                    if (transfer <= 0f)
                    {
                        continue;
                    }

                    if (!TryApplyToStock(ref stock, slot.Type, transfer))
                    {
                        continue;
                    }

                    slot.Amount -= transfer;
                    storage[i] = slot;
                    remaining -= transfer;
                    updated = true;
                }

                if (!updated)
                {
                    continue;
                }

                stock.LastUpdateTick = timeState.Tick;
                _stockLookup[targetColony] = stock;

                if (_colonyLookup.HasComponent(targetColony))
                {
                    var colony = _colonyLookup[targetColony];
                    colony.StoredResources = math.max(0f, stock.OreReserve + stock.SuppliesReserve + stock.ResearchReserve);
                    _colonyLookup[targetColony] = colony;
                }
            }
        }

        private Entity ResolveTargetColony(Entity carrier, float3 carrierPosition, float maxDistanceSq, NativeArray<Entity> colonies)
        {
            var affiliationTarget = Entity.Null;
            var hasAffiliations = _affiliationLookup.HasBuffer(carrier);
            if (hasAffiliations)
            {
                var affiliations = _affiliationLookup[carrier];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var affiliation = affiliations[i];
                    if (affiliation.Type != AffiliationType.Colony || affiliation.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (!IsWithinRange(affiliation.Target, carrierPosition, maxDistanceSq, out _))
                    {
                        continue;
                    }

                    return affiliation.Target;
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
                if (colony == Entity.Null)
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

                if (!IsWithinRange(colony, carrierPosition, maxDistanceSq, out var distSq))
                {
                    continue;
                }

                if (distSq < bestDistSq)
                {
                    best = colony;
                    bestDistSq = distSq;
                }
            }

            return best;
        }

        private bool IsWithinRange(Entity colony, float3 carrierPosition, float maxDistanceSq, out float distanceSq)
        {
            distanceSq = 0f;
            if (_transformLookup.HasComponent(colony))
            {
                distanceSq = math.distancesq(_transformLookup[colony].Position, carrierPosition);
                if (maxDistanceSq > 0f && distanceSq > maxDistanceSq)
                {
                    return false;
                }

                return true;
            }

            return maxDistanceSq <= 0f;
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
    }
}
