using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Motivation;
using PureDOTS.Systems.Motivation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Aggregate
{
    /// <summary>
    /// Ensures aggregate entities have motivation components initialized.
    /// Runs after MotivationInitializeSystem to add motivation infrastructure to aggregates.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(MotivationInitializeSystem))]
    public partial struct AggregateMotivationBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MotivationConfigState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<MotivationConfigState>();
            var em = state.EntityManager;

            // Give every aggregate with AggregateIdentity a MotivationSlot buffer and MotivationIntent if missing
            foreach (var (identity, entity) in SystemAPI.Query<RefRO<AggregateIdentity>>().WithEntityAccess())
            {
                // Check if already has MotivationDrive (added by adapter systems)
                if (!em.HasComponent<MotivationDrive>(entity))
                {
                    em.AddComponentData(entity, new MotivationDrive
                    {
                        InitiativeCurrent = 100,
                        InitiativeMax = 200,
                        LoyaltyCurrent = 100,
                        LoyaltyMax = 200,
                        PrimaryLoyaltyTarget = Entity.Null,
                        LastInitiativeTick = 0
                    });
                }

                if (!em.HasBuffer<MotivationSlot>(entity))
                {
                    var buffer = em.AddBuffer<MotivationSlot>(entity);
                    // Pre-populate slots according to config
                    AddSlots(ref buffer, MotivationLayer.Dream, config.DefaultDreamSlots);
                    AddSlots(ref buffer, MotivationLayer.Aspiration, config.DefaultAspirationSlots);
                    AddSlots(ref buffer, MotivationLayer.Wish, config.DefaultWishSlots);
                }

                if (!em.HasComponent<MotivationIntent>(entity))
                {
                    em.AddComponentData(entity, new MotivationIntent
                    {
                        ActiveSlotIndex = 255,
                        ActiveLayer = MotivationLayer.Dream,
                        ActiveSpecId = -1
                    });
                }

                if (!em.HasComponent<LegacyPoints>(entity))
                {
                    em.AddComponentData(entity, new LegacyPoints
                    {
                        TotalEarned = 0,
                        Unspent = 0
                    });
                }
            }
        }

        [BurstCompile]
        private static void AddSlots(ref DynamicBuffer<MotivationSlot> buffer, MotivationLayer layer, byte count)
        {
            for (int i = 0; i < count; i++)
            {
                buffer.Add(new MotivationSlot
                {
                    Layer = layer,
                    Status = MotivationStatus.Inactive,
                    LockFlags = MotivationLockFlags.None,
                    SpecId = -1,
                    Importance = 0,
                    Progress = 0,
                    StartedTick = 0,
                    TargetEntity = Entity.Null,
                    Param0 = 0,
                    Param1 = 0
                });
            }
        }
    }
}

