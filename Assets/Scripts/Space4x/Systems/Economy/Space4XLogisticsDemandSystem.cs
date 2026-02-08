using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Populates logistics board demand entries based on colony resource shortfalls.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4X.Systems.AI.Space4XAIMissionBoardSystem))]
    public partial struct Space4XLogisticsDemandSystem : ISystem
    {
        private EntityStorageInfoLookup _entityInfoLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XColony>();
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var boardEntity = EnsureBoard(ref state, out var boardConfig);
            var board = state.EntityManager.GetComponentData<LogisticsBoard>(boardEntity);
            if (boardConfig.BroadcastIntervalTicks > 0 && time.Tick - board.LastUpdateTick < boardConfig.BroadcastIntervalTicks)
            {
                return;
            }

            _entityInfoLookup.Update(ref state);

            var demandBuffer = state.EntityManager.GetBuffer<LogisticsDemandEntry>(boardEntity);
            demandBuffer.Clear();

            var hasWeights = TryGetResourceWeights(ref state, out var resourceWeights);
            var tick = time.Tick;
            var stockLookup = state.GetComponentLookup<ColonyIndustryStock>(true);
            stockLookup.Update(ref state);

            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>().WithEntityAccess())
            {
                if (!IsValidEntity(entity))
                {
                    continue;
                }

                var desired = math.max(200f, colony.ValueRO.Population * 0.002f);
                var shortage = desired - colony.ValueRO.StoredResources;
                var resourceType = ResourceType.Minerals;
                var resourceIndex = (ushort)resourceType;
                var useEssentials = false;

                if (stockLookup.HasComponent(entity))
                {
                    var stock = stockLookup[entity];
                    var perEssential = desired * 0.25f;
                    var shortageFood = perEssential - stock.FoodReserve;
                    var shortageWater = perEssential - stock.WaterReserve;
                    var shortageSupplies = perEssential - stock.SuppliesReserve;
                    var shortageFuel = perEssential - stock.FuelReserve;

                    var maxShortage = 0f;
                    if (shortageFood > maxShortage)
                    {
                        maxShortage = shortageFood;
                        resourceType = ResourceType.Food;
                    }
                    if (shortageWater > maxShortage)
                    {
                        maxShortage = shortageWater;
                        resourceType = ResourceType.Water;
                    }
                    if (shortageSupplies > maxShortage)
                    {
                        maxShortage = shortageSupplies;
                        resourceType = ResourceType.Supplies;
                    }
                    if (shortageFuel > maxShortage)
                    {
                        maxShortage = shortageFuel;
                        resourceType = ResourceType.Fuel;
                    }

                    if (maxShortage > 10f)
                    {
                        shortage = maxShortage;
                        resourceIndex = (ushort)resourceType;
                        useEssentials = true;
                    }
                }

                if (!useEssentials)
                {
                    if (shortage <= 10f)
                    {
                        continue;
                    }

                    var seedHash = (uint)math.hash(new uint3((uint)entity.Index, (uint)colony.ValueRO.Population, tick));
                    resourceType = hasWeights
                        ? Space4XResourceDistributionUtility.RollResource(Space4XResourceBand.Logistics, resourceWeights, seedHash)
                        : Space4XResourceSelection.SelectLogisticsResource(seedHash);
                    resourceIndex = (ushort)resourceType;
                }
                var priority = colony.ValueRO.Status switch
                {
                    Space4XColonyStatus.InCrisis => (byte)5,
                    Space4XColonyStatus.Besieged => (byte)4,
                    Space4XColonyStatus.Growing => (byte)3,
                    _ => (byte)2
                };

                var contextHash = math.hash(new uint3((uint)entity.Index, resourceIndex, tick));

                demandBuffer.Add(new LogisticsDemandEntry
                {
                    SiteEntity = entity,
                    ResourceTypeIndex = resourceIndex,
                    RequiredUnits = shortage,
                    DeliveredUnits = 0f,
                    ReservedUnits = 0f,
                    OutstandingUnits = shortage,
                    Priority = priority,
                    LastUpdateTick = tick,
                    ContextHash = contextHash
                });
            }

            board.LastUpdateTick = tick;
            state.EntityManager.SetComponentData(boardEntity, board);
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityInfoLookup.Exists(entity);
        }

        private bool TryGetResourceWeights(ref SystemState state, out DynamicBuffer<Space4XResourceWeightEntry> weights)
        {
            weights = default;
            if (!SystemAPI.TryGetSingletonEntity<Space4XResourceDistributionConfig>(out var entity))
            {
                return false;
            }

            if (!state.EntityManager.HasBuffer<Space4XResourceWeightEntry>(entity))
            {
                return false;
            }

            weights = state.EntityManager.GetBuffer<Space4XResourceWeightEntry>(entity);
            if (weights.Length != (int)ResourceType.Count)
            {
                Space4XResourceDistributionDefaults.PopulateDefaults(ref weights);
            }

            return weights.Length > 0;
        }

        private Entity EnsureBoard(ref SystemState state, out LogisticsBoardConfig config)
        {
            if (SystemAPI.TryGetSingletonEntity<LogisticsBoard>(out var entity))
            {
                config = SystemAPI.GetSingleton<LogisticsBoardConfig>();
                return entity;
            }

            entity = state.EntityManager.CreateEntity(typeof(LogisticsBoard), typeof(LogisticsBoardConfig));
            config = LogisticsBoardConfig.Default;

            state.EntityManager.SetComponentData(entity, new LogisticsBoard
            {
                BoardId = new FixedString64Bytes("logistics.sim"),
                AuthorityEntity = Entity.Null,
                DomainEntity = Entity.Null,
                LastUpdateTick = 0
            });
            state.EntityManager.SetComponentData(entity, config);
            state.EntityManager.AddBuffer<LogisticsDemandEntry>(entity);
            state.EntityManager.AddBuffer<LogisticsReservationEntry>(entity);
            state.EntityManager.AddBuffer<LogisticsClaimRequest>(entity);

            return entity;
        }
    }
}
