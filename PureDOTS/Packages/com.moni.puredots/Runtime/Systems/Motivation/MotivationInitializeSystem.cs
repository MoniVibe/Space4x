using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Motivation
{
    /// <summary>
    /// Ensures entities marked as Motivation owners have properly sized slot buffers.
    /// Can be filtered by a tag component if desired.
    /// Runs in InitializationSystemGroup to set up motivation infrastructure.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct MotivationInitializeSystem : ISystem
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

            // Give every entity with MotivationDrive a MotivationSlot buffer and MotivationIntent if missing.
            foreach (var (drive, entity) in SystemAPI.Query<RefRO<MotivationDrive>>().WithEntityAccess())
            {
                if (!em.HasBuffer<MotivationSlot>(entity))
                {
                    var buffer = em.AddBuffer<MotivationSlot>(entity);
                    // Pre-populate slots according to config – only Dreams/Aspirations/Wishes.
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
























