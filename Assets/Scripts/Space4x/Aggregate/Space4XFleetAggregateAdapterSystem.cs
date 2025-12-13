using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Motivation;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Aggregate
{
    /// <summary>
    /// Adapter for fleet entities, bridging them to the generic aggregate system.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XFleetAggregateAdapterSystem : ISystem
    {
        // Type ID constant for Fleet aggregate type
        private const ushort FleetTypeId = 200; // Game-specific type ID

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Find fleets without adapters (using Space4XFleet component)
            // Note: This assumes Space4XFleet exists - if not, this will be a stub
            foreach (var (fleet, entity) in SystemAPI.Query<RefRO<Space4XFleet>>()
                .WithAbsent<BandAggregateAdapter>() // Reuse BandAggregateAdapter or create FleetAggregateAdapter
                .WithEntityAccess())
            {
                // Create aggregate entity
                var aggregateEntity = ecb.CreateEntity();

                // Add aggregate components
                ecb.AddComponent(aggregateEntity, new AggregateIdentity
                {
                    TypeId = FleetTypeId,
                    Seed = (uint)fleet.ValueRO.FleetId.GetHashCode() // Use fleet ID as seed
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

                // Add adapter to fleet entity (reusing BandAggregateAdapter for now)
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


















