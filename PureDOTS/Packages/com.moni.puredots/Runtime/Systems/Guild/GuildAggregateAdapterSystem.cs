using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Guild;
using PureDOTS.Runtime.Motivation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Guild
{
    /// <summary>
    /// Bridges existing Guild entities to the generic aggregate system.
    /// Creates aggregate entities for guilds and links them via GuildAggregateAdapter.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct GuildAggregateAdapterSystem : ISystem
    {
        // Type ID constant for Guild aggregate type
        // Using a high value to avoid conflicts with other aggregate types
        private const ushort GuildTypeId = 10;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            // Find guilds without adapters
            foreach (var (guild, entity) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Aggregates.Guild>>()
                .WithAbsent<GuildAggregateAdapter>()
                .WithEntityAccess())
            {
                // Create aggregate entity
                var aggregateEntity = ecb.CreateEntity();

                // Generate seed from guild name hash
                var guildName = guild.ValueRO.GuildName;
                uint seed = 0;
                for (int i = 0; i < guildName.Length && i < 8; i++)
                {
                    seed = (seed << 8) | (byte)guildName[i];
                }

                // Add aggregate components
                ecb.AddComponent(aggregateEntity, new AggregateIdentity
                {
                    TypeId = GuildTypeId,
                    Seed = seed
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

                // Add adapter to guild entity
                ecb.AddComponent(entity, new GuildAggregateAdapter
                {
                    AggregateEntity = aggregateEntity
                });

                // Mark aggregate as dirty for initial stats calculation
                ecb.AddComponent<AggregateStatsDirtyTag>(aggregateEntity);
            }
        }
    }
}

