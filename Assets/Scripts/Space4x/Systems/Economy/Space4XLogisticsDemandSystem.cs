using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Transport;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Populates logistics board demand entries based on colony resource shortfalls.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateBefore(typeof(Space4X.Systems.AI.Space4XAIMissionBoardSystem))]
    public partial struct Space4XLogisticsDemandSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XColony>();
        }

        [BurstCompile]
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

            var demandBuffer = state.EntityManager.GetBuffer<LogisticsDemandEntry>(boardEntity);
            demandBuffer.Clear();

            var resourceKindCount = (ushort)(ResourceType.Ore + 1);
            var tick = time.Tick;

            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>().WithEntityAccess())
            {
                var desired = math.max(200f, colony.ValueRO.Population * 0.002f);
                var shortage = desired - colony.ValueRO.StoredResources;
                if (shortage <= 10f)
                {
                    continue;
                }

                var resourceIndex = (ushort)(((uint)colony.ValueRO.Population / 100000u) % resourceKindCount);
                var priority = colony.ValueRO.Status switch
                {
                    Space4XColonyStatus.InCrisis => (byte)5,
                    Space4XColonyStatus.Besieged => (byte)4,
                    Space4XColonyStatus.Growing => (byte)3,
                    _ => (byte)2
                };

                var contextHash = math.hash(new uint3((uint)entity.Index, resourceIndex, 0u));

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
