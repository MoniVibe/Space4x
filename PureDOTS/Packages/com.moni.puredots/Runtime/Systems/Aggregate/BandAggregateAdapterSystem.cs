using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Aggregate
{
    /// <summary>
    /// Bridges existing Band entities to the generic aggregate system.
    /// Creates aggregate entities for bands and links them via BandAggregateAdapter.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct BandAggregateAdapterSystem : ISystem
    {
        // Type ID constant for Band aggregate type
        private const ushort BandTypeId = 4; // Matches AggregateType.Band from AggregateEntityComponents

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Find bands without adapters
            foreach (var (bandId, entity) in SystemAPI.Query<RefRO<BandId>>()
                .WithAbsent<BandAggregateAdapter>()
                .WithEntityAccess())
            {
                // Create aggregate entity
                var aggregateEntity = ecb.CreateEntity();

                // Add aggregate components
                ecb.AddComponent(aggregateEntity, new AggregateIdentity
                {
                    TypeId = BandTypeId,
                    Seed = (uint)bandId.ValueRO.Value // Use band ID as seed
                });

                ecb.AddComponent(aggregateEntity, new AggregateStats
                {
                    AvgInitiative = 0f,
                    AvgVengefulForgiving = 0f,
                    AvgBoldCraven = 0f,
                    AvgCorruptPure = 0f,
                    AvgChaoticLawful = 0f,
                    AvgEvilGood = 0f,
                    AvgMightMagic = 0f,
                    AvgAmbition = 0f,
                    StatusCoverage = 0f,
                    WealthCoverage = 0f,
                    PowerCoverage = 0f,
                    KnowledgeCoverage = 0f,
                    MemberCount = 0,
                    LastRecalcTick = 0
                });

                ecb.AddComponent(aggregateEntity, new AmbientGroupConditions
                {
                    AmbientCourage = 0f,
                    AmbientCaution = 0f,
                    AmbientAnger = 0f,
                    AmbientCompassion = 0f,
                    AmbientDrive = 0f,
                    ExpectationLoyalty = 0f,
                    ExpectationConformity = 0f,
                    ToleranceForOutliers = 0f,
                    LastUpdateTick = 0
                });

                // Add motivation components for group ambitions
                ecb.AddComponent(aggregateEntity, new MotivationDrive
                {
                    InitiativeCurrent = 100,
                    InitiativeMax = 200,
                    LoyaltyCurrent = 100,
                    LoyaltyMax = 200,
                    PrimaryLoyaltyTarget = Entity.Null,
                    LastInitiativeTick = 0
                });

                // Add adapter to band entity
                ecb.AddComponent(entity, new BandAggregateAdapter
                {
                    AggregateEntity = aggregateEntity
                });

                // Mark aggregate as dirty for initial stats calculation
                ecb.AddComponent<AggregateStatsDirtyTag>(aggregateEntity);
            }
        }
    }
}

