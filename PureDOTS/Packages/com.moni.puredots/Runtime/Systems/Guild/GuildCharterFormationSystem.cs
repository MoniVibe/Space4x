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
    /// Handles bottom-up guild formation via charter signatures.
    /// Watches for GuildCharter entities and processes signatures.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GuildCharterFormationSystem : ISystem
    {
        private static readonly FixedString64Bytes DefaultGuildName = (FixedString64Bytes)"New Guild";
        private static readonly FixedString64Bytes EmptyProposal = default;

        private ComponentLookup<GuildAggregateAdapter> _guildAdapterLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _guildAdapterLookup = state.GetComponentLookup<GuildAggregateAdapter>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            _guildAdapterLookup.Update(ref state);

            // Process charters
            foreach (var (charter, signatures, entity) in SystemAPI.Query<
                RefRO<GuildCharter>,
                DynamicBuffer<CharterSignature>>().WithEntityAccess())
            {
                var charterValue = charter.ValueRO;

                // Check if signature window has expired
                if (currentTick > charterValue.SignatureWindowEndTick)
                {
                    // Window expired - destroy charter if not enough signatures
                    if (signatures.Length < charterValue.RequiredSignatures)
                    {
                        ecb.DestroyEntity(entity);
                    }
                    continue;
                }

                // Check if we have enough signatures to form guild
                if (signatures.Length >= charterValue.RequiredSignatures)
                {
                    // Create guild entity
                    CreateGuildFromCharter(ref state, ref ecb, in charterValue, in signatures, currentTick, out var guildEntity);

                    // Link founder and signatories via GroupMembership
                    // Note: This replaces old GuildMembership pattern
                    if (guildEntity != Entity.Null)
                    {
                        // Add GroupMembership to founder
                        if (SystemAPI.Exists(charterValue.FounderEntity))
                        {
                            ecb.AddComponent(charterValue.FounderEntity, new GroupMembership
                            {
                                Group = guildEntity,
                                Role = 2 // Master role
                            });
                        }

                        // Add GroupMembership to signatories
                        for (int i = 0; i < signatures.Length; i++)
                        {
                            var signer = signatures[i].SignerEntity;
                            if (SystemAPI.Exists(signer) && signer != charterValue.FounderEntity)
                            {
                                ecb.AddComponent(signer, new GroupMembership
                                {
                                    Group = guildEntity,
                                    Role = 0 // Member role
                                });
                            }
                        }

                        // Mark aggregate as dirty for stats calculation
                        if (_guildAdapterLookup.TryGetComponent(guildEntity, out var adapter))
                        {
                            if (SystemAPI.Exists(adapter.AggregateEntity))
                            {
                                ecb.AddComponent<AggregateStatsDirtyTag>(adapter.AggregateEntity);
                            }
                        }

                        // Destroy charter entity
                        ecb.DestroyEntity(entity);
                    }
                }
            }
        }

        private static void CreateGuildFromCharter(ref SystemState state, ref EntityCommandBuffer ecb, in GuildCharter charter, in DynamicBuffer<CharterSignature> signatures, uint currentTick, out Entity guildEntity)
        {
            // Create guild entity
            guildEntity = ecb.CreateEntity();

            // Add Guild component (using existing Aggregates namespace type)
            ecb.AddComponent(guildEntity, new PureDOTS.Runtime.Aggregates.Guild
            {
                Type = (PureDOTS.Runtime.Aggregates.Guild.GuildType)charter.ProposedGuildTypeId,
                GuildName = DefaultGuildName, // TODO: Generate from charter
                FoundedTick = currentTick,
                HomeVillage = Entity.Null,
                HeadquartersPosition = Unity.Mathematics.float3.zero,
                MemberCount = (ushort)(signatures.Length + 1), // +1 for founder
                AverageMemberLevel = 0f,
                TotalExperience = 0,
                ReputationScore = 50,
                CurrentMission = default(Unity.Collections.FixedString64Bytes)
            });

            // Add GuildId
            ecb.AddComponent(guildEntity, new GuildId
            {
                Id = (ushort)(currentTick % 65535) // Simple ID generation
            });

            // Add GuildWealth (initialized from charter fee)
            ecb.AddComponent(guildEntity, new GuildWealth
            {
                AverageMemberFortune = charter.CharterFee,
                PooledTreasury = charter.CharterFee,
                TotalAssets = charter.CharterFee
            });

            // Add GuildKnowledge (empty initially)
            ecb.AddComponent(guildEntity, new GuildKnowledge
            {
                DemonSlayingBonus = 0,
                UndeadSlayingBonus = 0,
                BossHuntingBonus = 0,
                CelestialCombatBonus = 0,
                EspionageEffectiveness = 0,
                CoordinationBonus = 0,
                SurvivalBonus = 0,
                DemonsKilled = 0,
                UndeadKilled = 0,
                BossesKilled = 0,
                CelestialsKilled = 0,
                ResearchFields = 0
            });

            // Add GuildLeadership (default democratic)
            ecb.AddComponent(guildEntity, new PureDOTS.Runtime.Aggregates.GuildLeadership
            {
                Governance = PureDOTS.Runtime.Aggregates.GuildLeadership.GovernanceType.Democratic,
                GuildMasterEntity = charter.FounderEntity,
                MasterElectedTick = currentTick,
                QuartermasterEntity = Entity.Null,
                RecruiterEntity = Entity.Null,
                DiplomatEntity = Entity.Null,
                WarMasterEntity = Entity.Null,
                SpyMasterEntity = Entity.Null,
                VoteInProgress = false,
                VoteProposal = EmptyProposal,
                VoteEndTick = 0
            });

            // GuildAggregateAdapterSystem will create the aggregate entity on next frame
        }
    }
}

