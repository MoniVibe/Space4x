using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Bridges Space4X personal relations into PureDOTS entity relations.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4X.Systems.AI.Space4XStrikeCraftRecognitionSystem))]
    public partial struct Space4XPersonalRelationBridgeSystem : ISystem
    {
        private BufferLookup<EntityRelation> _entityRelationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<PersonalRelationEntry>();
            _entityRelationLookup = state.GetBufferLookup<EntityRelation>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _entityRelationLookup.Update(ref state);

            var currentTick = timeState.Tick;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (personalRelations, entity) in SystemAPI.Query<DynamicBuffer<PersonalRelationEntry>>().WithEntityAccess())
            {
                if (!_entityRelationLookup.HasBuffer(entity))
                {
                    ecb.AddBuffer<EntityRelation>(entity);
                    continue;
                }

                var relations = _entityRelationLookup[entity];
                for (int i = 0; i < personalRelations.Length; i++)
                {
                    var entry = personalRelations[i];
                    if (entry.Other == Entity.Null)
                    {
                        continue;
                    }

                    var mappedType = MapRelationKind(entry.Kind, entry.Score);
                    var lastTick = entry.LastInteractionTick > 0 ? entry.LastInteractionTick : currentTick;

                    int relationIndex = RelationCalculator.FindRelationIndex(relations, entry.Other);
                    if (relationIndex >= 0)
                    {
                        var relation = relations[relationIndex];
                        if (relation.LastInteractionTick > entry.LastInteractionTick)
                        {
                            continue;
                        }

                        relation.Type = mappedType;
                        relation.Intensity = entry.Score;
                        relation.LastInteractionTick = lastTick;
                        relation.FirstMetTick = relation.FirstMetTick == 0 ? lastTick : relation.FirstMetTick;
                        relation.Trust = ToByte01(entry.Trust);
                        relation.Fear = ToByte01(entry.Fear);
                        relation.Familiarity = (byte)math.max((int)relation.Familiarity, (int)ComputeFamiliarity(entry.Score));
                        relation.Respect = ComputeRespect(entry.Score);

                        relations[relationIndex] = relation;
                    }
                    else
                    {
                        relations.Add(new EntityRelation
                        {
                            OtherEntity = entry.Other,
                            Type = mappedType,
                            Intensity = entry.Score,
                            InteractionCount = 1,
                            FirstMetTick = lastTick,
                            LastInteractionTick = lastTick,
                            Trust = ToByte01(entry.Trust),
                            Familiarity = ComputeFamiliarity(entry.Score),
                            Respect = ComputeRespect(entry.Score),
                            Fear = ToByte01(entry.Fear)
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }

        private static byte ToByte01(half value)
        {
            return (byte)math.clamp((int)math.round((float)value * 100f), 0, 100);
        }

        private static byte ComputeFamiliarity(sbyte score)
        {
            return (byte)math.clamp(math.abs((int)score), 0, 100);
        }

        private static byte ComputeRespect(sbyte score)
        {
            int value = 50 + score / 2;
            return (byte)math.clamp(value, 0, 100);
        }

        private static RelationType MapRelationKind(PersonalRelationKind kind, sbyte score)
        {
            switch (kind)
            {
                case PersonalRelationKind.Friend:
                    return score >= 70 ? RelationType.CloseFriend : RelationType.Friend;
                case PersonalRelationKind.Rival:
                    return RelationType.Rival;
                case PersonalRelationKind.Family:
                    return RelationType.Sibling;
                case PersonalRelationKind.Mentor:
                    return RelationType.Mentor;
                case PersonalRelationKind.Protege:
                    return RelationType.Student;
                case PersonalRelationKind.Comrade:
                    return RelationType.Colleague;
                case PersonalRelationKind.Debtor:
                case PersonalRelationKind.Creditor:
                    return RelationType.BusinessPartner;
                case PersonalRelationKind.BloodFeud:
                    return RelationType.Nemesis;
                default:
                    if (score >= 30)
                    {
                        return RelationType.Acquaintance;
                    }
                    if (score <= -30)
                    {
                        return RelationType.Rival;
                    }
                    return RelationType.Stranger;
            }
        }
    }
}
