using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Cooperation;
using PureDOTS.Runtime.Relations;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using PureDOTS.Systems.Relations;
using Unity.Mathematics;
using Unity.Collections;

namespace PureDOTS.Systems.Cooperation
{
    /// <summary>
    /// System that updates cooperation cohesion to include relation bonuses.
    /// Formula: cohesion = skillSynergy(40%) + relationBonus(30%) + communicationClarity(20%) + experience(10%)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(RelationUpdateSystem))]
    public partial struct RelationCohesionSystem : ISystem
    {
        private EntityStorageInfoLookup _entityInfoLookup;
        private BufferLookup<EntityRelation> _relationLookup;
        private BufferLookup<ProductionTeamMember> _teamMemberLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _entityInfoLookup = state.GetEntityStorageInfoLookup();
            _relationLookup = state.GetBufferLookup<EntityRelation>(true);
            _teamMemberLookup = state.GetBufferLookup<ProductionTeamMember>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
                return;

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            _entityInfoLookup.Update(ref state);
            _relationLookup.Update(ref state);
            _teamMemberLookup.Update(ref state);

            foreach (var (team, entity) in SystemAPI.Query<RefRW<ProductionTeam>>().WithEntityAccess())
            {
                // Inline relation bonus calculation to avoid Burst external call ABI issues
                float relationBonus = 0.5f;
                if (_teamMemberLookup.HasBuffer(entity))
                {
                    var members = _teamMemberLookup[entity];
                    if (members.Length >= 2)
                    {
                        float relationSum = 0f;
                        int pairCount = 0;

                        for (int i = 0; i < members.Length - 1; i++)
                        {
                            var memberA = members[i].MemberEntity;
                            if (memberA == Entity.Null || !_entityInfoLookup.Exists(memberA))
                            {
                                continue;
                            }

                            for (int j = i + 1; j < members.Length; j++)
                            {
                                var memberB = members[j].MemberEntity;
                                if (memberB == Entity.Null || !_entityInfoLookup.Exists(memberB))
                                {
                                    continue;
                                }

                                if (!_relationLookup.HasBuffer(memberA))
                                {
                                    continue;
                                }

                                var relations = _relationLookup[memberA];
                                for (int k = 0; k < relations.Length; k++)
                                {
                                    if (relations[k].OtherEntity != memberB)
                                    {
                                        continue;
                                    }

                                    float normalized = math.clamp((relations[k].Intensity + 100f) / 200f, 0f, 1f);
                                    relationSum += normalized;
                                    pairCount++;
                                    break;
                                }
                            }
                        }

                        if (pairCount > 0)
                        {
                            relationBonus = relationSum / pairCount;
                        }
                    }
                }

                float skillSynergy = math.saturate(team.ValueRO.MemberCount / 8f);
                float communicationClarity = math.select(0.4f, 0.6f, team.ValueRO.Status == ProductionTeamStatus.Active);
                float experienceFactor = math.saturate((team.ValueRO.MemberCount - 1f) / 5f);

                float newCohesion =
                    skillSynergy * 0.4f +
                    relationBonus * 0.3f +
                    communicationClarity * 0.2f +
                    experienceFactor * 0.1f;

                team.ValueRW.Cohesion = math.clamp(newCohesion, 0f, 1f);
                team.ValueRW.Status = ProductionTeamStatus.Active;
            }
        }
    }
}

