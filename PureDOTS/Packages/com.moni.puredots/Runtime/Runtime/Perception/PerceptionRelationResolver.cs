using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Armies;
using PureDOTS.Runtime.Bands;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Deception;
using PureDOTS.Runtime.Social;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Perception
{
    public struct RelationResolution
    {
        public sbyte Score;
        public PerceivedRelationKind Kind;
        public PerceivedRelationFlags Flags;
    }

    public static class PerceptionRelationResolver
    {
        public static RelationResolution Resolve(
            Entity observer,
            Entity target,
            DetectableCategory category,
            in BufferLookup<EntityRelation> relationLookup,
            in ComponentLookup<FactionId> factionLookup,
            in ComponentLookup<DisguiseIdentity> disguiseLookup,
            in BufferLookup<DisguiseDiscovery> discoveryLookup,
            in ComponentLookup<VillagerId> villagerLookup,
            in ComponentLookup<VillageId> villageLookup,
            in ComponentLookup<BandId> bandLookup,
            in ComponentLookup<ArmyId> armyLookup,
            in NativeArray<FactionRelationships> factionRelationships)
        {
            var hasObserverFaction = TryGetFactionId(observer, observer, factionLookup, disguiseLookup, discoveryLookup, villagerLookup, villageLookup, bandLookup, armyLookup, out var observerFactionId);
            var hasTargetFaction = TryGetFactionId(observer, target, factionLookup, disguiseLookup, discoveryLookup, villagerLookup, villageLookup, bandLookup, armyLookup, out var targetFactionId);
            var hasFactionRelation = TryGetFactionRelationship(observerFactionId, targetFactionId, hasObserverFaction, hasTargetFaction, factionRelationships, out var factionScore);
            var factionKind = hasFactionRelation ? ResolveKindFromScore(factionScore) : PerceivedRelationKind.Unknown;

            if (relationLookup.HasBuffer(observer))
            {
                var relations = relationLookup[observer];
                var relationIndex = RelationCalculator.FindRelationIndex(relations, target);
                if (relationIndex >= 0)
                {
                    var relation = relations[relationIndex];
                    if (TryResolvePersonalRelation(relation, out var personalKind))
                    {
                        var flags = PerceivedRelationFlags.FromPersonal;
                        if (hasFactionRelation)
                        {
                            if (personalKind == PerceivedRelationKind.Ally && factionKind != PerceivedRelationKind.Ally)
                            {
                                flags |= PerceivedRelationFlags.ForcedAlly;
                            }
                            else if (personalKind == PerceivedRelationKind.Hostile && factionKind != PerceivedRelationKind.Hostile)
                            {
                                flags |= PerceivedRelationFlags.ForcedHostile;
                            }
                        }

                        return new RelationResolution
                        {
                            Score = ClampScore(relation.Intensity),
                            Kind = personalKind,
                            Flags = flags
                        };
                    }
                }
            }

            if (hasFactionRelation)
            {
                return new RelationResolution
                {
                    Score = factionScore,
                    Kind = factionKind,
                    Flags = PerceivedRelationFlags.FromFaction
                };
            }

            if (category == DetectableCategory.Ally)
            {
                return new RelationResolution
                {
                    Score = 127,
                    Kind = PerceivedRelationKind.Ally,
                    Flags = PerceivedRelationFlags.FromCategory
                };
            }

            if (category == DetectableCategory.Enemy)
            {
                return new RelationResolution
                {
                    Score = -128,
                    Kind = PerceivedRelationKind.Hostile,
                    Flags = PerceivedRelationFlags.FromCategory
                };
            }

            if (category == DetectableCategory.Neutral)
            {
                return new RelationResolution
                {
                    Score = 0,
                    Kind = PerceivedRelationKind.Neutral,
                    Flags = PerceivedRelationFlags.FromCategory
                };
            }

            return new RelationResolution
            {
                Score = 0,
                Kind = PerceivedRelationKind.Unknown,
                Flags = PerceivedRelationFlags.None
            };
        }

        private static bool TryGetFactionId(
            Entity observer,
            Entity entity,
            in ComponentLookup<FactionId> factionLookup,
            in ComponentLookup<DisguiseIdentity> disguiseLookup,
            in BufferLookup<DisguiseDiscovery> discoveryLookup,
            in ComponentLookup<VillagerId> villagerLookup,
            in ComponentLookup<VillageId> villageLookup,
            in ComponentLookup<BandId> bandLookup,
            in ComponentLookup<ArmyId> armyLookup,
            out int factionId)
        {
            if (disguiseLookup.HasComponent(entity))
            {
                var disguise = disguiseLookup[entity];
                if (disguise.IsActive != 0 && disguise.CoverFactionId != 0)
                {
                    if (discoveryLookup.HasBuffer(observer))
                    {
                        var discovery = discoveryLookup[observer];
                        for (int i = 0; i < discovery.Length; i++)
                        {
                            var d = discovery[i];
                            if (d.TargetEntity == entity && d.IsExposed != 0)
                            {
                                factionId = disguise.TrueFactionId;
                                return true;
                            }
                        }
                    }

                    factionId = disguise.CoverFactionId;
                    return true;
                }
            }

            if (factionLookup.HasComponent(entity))
            {
                factionId = factionLookup[entity].Value;
                return true;
            }

            if (villagerLookup.HasComponent(entity))
            {
                factionId = villagerLookup[entity].FactionId;
                return true;
            }

            if (villageLookup.HasComponent(entity))
            {
                factionId = villageLookup[entity].FactionId;
                return true;
            }

            if (bandLookup.HasComponent(entity))
            {
                factionId = bandLookup[entity].FactionId;
                return true;
            }

            if (armyLookup.HasComponent(entity))
            {
                factionId = armyLookup[entity].FactionId;
                return true;
            }

            factionId = 0;
            return false;
        }

        private static bool TryGetFactionRelationship(
            int observerFactionId,
            int targetFactionId,
            bool hasObserverFaction,
            bool hasTargetFaction,
            in NativeArray<FactionRelationships> factionRelationships,
            out sbyte relationship)
        {
            if (!hasObserverFaction || !hasTargetFaction || !factionRelationships.IsCreated)
            {
                relationship = 0;
                return false;
            }

            for (int i = 0; i < factionRelationships.Length; i++)
            {
                if (factionRelationships[i].FactionId == observerFactionId)
                {
                    relationship = factionRelationships[i].GetRelationship(targetFactionId);
                    return true;
                }
            }

            relationship = 0;
            return false;
        }

        private static bool TryResolvePersonalRelation(in EntityRelation relation, out PerceivedRelationKind kind)
        {
            if (relation.Type == RelationType.None || relation.Type == RelationType.Stranger)
            {
                kind = PerceivedRelationKind.Unknown;
                return false;
            }

            if (RelationCalculator.IsNegativeRelation(relation.Type) || relation.Intensity < 0)
            {
                kind = PerceivedRelationKind.Hostile;
                return true;
            }

            if (RelationCalculator.IsPositiveRelation(relation.Type) ||
                RelationCalculator.IsFamilyRelation(relation.Type) ||
                RelationCalculator.IsRomanticRelation(relation.Type) ||
                RelationCalculator.IsProfessionalRelation(relation.Type) ||
                relation.Intensity > 0)
            {
                kind = PerceivedRelationKind.Ally;
                return true;
            }

            kind = PerceivedRelationKind.Neutral;
            return true;
        }

        private static PerceivedRelationKind ResolveKindFromScore(sbyte score)
        {
            if (score > 0)
            {
                return PerceivedRelationKind.Ally;
            }

            if (score < 0)
            {
                return PerceivedRelationKind.Hostile;
            }

            return PerceivedRelationKind.Neutral;
        }

        private static sbyte ClampScore(sbyte score)
        {
            return (sbyte)math.clamp((int)score, -128, 127);
        }
    }
}
