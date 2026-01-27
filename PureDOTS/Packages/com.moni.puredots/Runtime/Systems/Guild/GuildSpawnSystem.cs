using PureDOTS.Runtime.Aggregate;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Guild;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Guild
{
    /// <summary>
    /// Spawns archetypal guilds based on world conditions (top-down formation).
    /// Checks for world conditions and spawns guilds as needed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct GuildSpawnSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<GuildConfigState>();
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

            var configState = SystemAPI.GetSingleton<GuildConfigState>();

            // Only check periodically
            if (timeState.Tick % configState.FormationCheckFrequency != 0)
            {
                return;
            }

            if (!configState.Catalog.IsCreated)
            {
                return;
            }

            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            ref var catalog = ref configState.Catalog.Value;

            // Check each guild type spec for spawn conditions
            for (int i = 0; i < catalog.TypeSpecs.Length; i++)
            {
                ref var spec = ref catalog.TypeSpecs[i];
                
                // Check if this guild type should spawn
                // TODO: Implement world condition checks (tech tier, threats, etc.)
                // For now, this is a stub that can be extended by game-specific systems
                
                // Check if guild of this type already exists
                bool guildExists = false;
                foreach (var (guild, _) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Aggregates.Guild>>().WithEntityAccess())
                {
                    if ((byte)guild.ValueRO.Type == spec.TypeId)
                    {
                        guildExists = true;
                        break;
                    }
                }

                if (!guildExists)
                {
                    // Spawn guild (stub - game-specific systems should override this logic)
                    // CreateGuildEntity(ref state, ref ecb, in spec, timeState.Tick);
                }
            }
        }

        [BurstCompile]
        private static void CreateGuildEntity(ref SystemState state, ref EntityCommandBuffer ecb, ref GuildTypeSpec spec, uint currentTick, out Entity guildEntity)
        {
            // Create guild entity
            guildEntity = ecb.CreateEntity();

            // Add Guild component
            ecb.AddComponent(guildEntity, new PureDOTS.Runtime.Aggregates.Guild
            {
                Type = (PureDOTS.Runtime.Aggregates.Guild.GuildType)spec.TypeId,
                GuildName = spec.Label,
                FoundedTick = currentTick,
                HomeVillage = Entity.Null,
                HeadquartersPosition = float3.zero,
                MemberCount = 0,
                AverageMemberLevel = 0f,
                TotalExperience = 0,
                ReputationScore = 50,
                CurrentMission = default(FixedString64Bytes)
            });

            // Add GuildId
            ecb.AddComponent(guildEntity, new GuildId
            {
                Id = spec.TypeId
            });

            // Add GuildWealth
            ecb.AddComponent(guildEntity, new GuildWealth
            {
                AverageMemberFortune = 0f,
                PooledTreasury = 0f,
                TotalAssets = 0f
            });

            // Add GuildKnowledge
            ecb.AddComponent(guildEntity, new PureDOTS.Runtime.Guild.GuildKnowledge
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

            // Add GuildLeadership with default governance from spec
            ecb.AddComponent(guildEntity, new PureDOTS.Runtime.Aggregates.GuildLeadership
            {
                Governance = spec.DefaultGovernance,
                GuildMasterEntity = Entity.Null,
                MasterElectedTick = currentTick,
                QuartermasterEntity = Entity.Null,
                RecruiterEntity = Entity.Null,
                DiplomatEntity = Entity.Null,
                WarMasterEntity = Entity.Null,
                SpyMasterEntity = Entity.Null,
                VoteInProgress = false,
                VoteProposal = default(FixedString64Bytes),
                VoteEndTick = 0
            });

        }
    }
}

