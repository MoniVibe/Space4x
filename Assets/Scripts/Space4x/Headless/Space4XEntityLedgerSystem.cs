using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Time;
using PureDOTS.Runtime.Platform;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Headless
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XEntityLedgerSystem : ISystem
    {
        private const byte Lod0 = (byte)Space4XEntityLodTierKind.Lod0;
        private const byte Lod2 = (byte)Space4XEntityLodTierKind.Lod2;

        public void OnCreate(ref SystemState state)
        {
            if (!RuntimeMode.IsHeadless || !Application.isBatchMode)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<TimeState>().Tick;
            var entityManager = state.EntityManager;
            var ledgerEntity = EnsureLedgerEntity(entityManager);
            var ledger = entityManager.GetBuffer<Space4XEntityLedgerEntry>(ledgerEntity);

            for (int i = 0; i < ledger.Length; i++)
            {
                var entry = ledger[i];
                entry.LodTier = Lod2;
                ledger[i] = entry;
            }

            foreach (var (carrier, crewBuffer) in SystemAPI.Query<RefRO<Carrier>, DynamicBuffer<PlatformCrewMember>>())
            {
                var carrierId = carrier.ValueRO.CarrierId;
                for (int i = 0; i < crewBuffer.Length; i++)
                {
                    var crewEntity = crewBuffer[i].CrewEntity;
                    if (crewEntity == Entity.Null || !entityManager.Exists(crewEntity))
                    {
                        continue;
                    }

                    if (!entityManager.HasComponent<Space4XEntityId>(crewEntity))
                    {
                        continue;
                    }

                    var entityId = entityManager.GetComponentData<Space4XEntityId>(crewEntity).Id;
                    var entryIndex = FindEntry(ledger, entityId);
                    if (entryIndex < 0)
                    {
                        ledger.Add(new Space4XEntityLedgerEntry
                        {
                            EntityId = entityId,
                            CarrierId = carrierId,
                            LodTier = Lod0,
                            LastSeenTick = tick
                        });
                    }
                    else
                    {
                        var entry = ledger[entryIndex];
                        entry.CarrierId = carrierId;
                        entry.LodTier = Lod0;
                        entry.LastSeenTick = tick;
                        ledger[entryIndex] = entry;
                    }

                    if (entityManager.HasComponent<Space4XEntityLodTier>(crewEntity))
                    {
                        entityManager.SetComponentData(crewEntity, new Space4XEntityLodTier { Tier = Lod0 });
                    }
                    else
                    {
                        entityManager.AddComponentData(crewEntity, new Space4XEntityLodTier { Tier = Lod0 });
                    }
                }
            }
        }

        private static int FindEntry(DynamicBuffer<Space4XEntityLedgerEntry> ledger, in FixedString64Bytes entityId)
        {
            for (int i = 0; i < ledger.Length; i++)
            {
                if (ledger[i].EntityId.Equals(entityId))
                {
                    return i;
                }
            }
            return -1;
        }

        private static Entity EnsureLedgerEntity(EntityManager entityManager)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XEntityLedgerTag>());
            if (!query.IsEmptyIgnoreFilter)
            {
                var existing = query.GetSingletonEntity();
                if (!entityManager.HasBuffer<Space4XEntityLedgerEntry>(existing))
                {
                    entityManager.AddBuffer<Space4XEntityLedgerEntry>(existing);
                }
                return existing;
            }

            var ledgerEntity = entityManager.CreateEntity(typeof(Space4XEntityLedgerTag));
            entityManager.AddBuffer<Space4XEntityLedgerEntry>(ledgerEntity);
            return ledgerEntity;
        }
    }
}
