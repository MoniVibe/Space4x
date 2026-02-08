using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Shared helpers for faction standing gates and kinship biasing.
    /// </summary>
    public static class Space4XStandingUtility
    {
        private const float DefaultStanding = 0.2f;
        private const float SameEmpireFloor = 0.25f;

        public static bool PassesMissionStandingGate(
            Entity agentEntity,
            in Space4XMissionOffer offer,
            in ComponentLookup<Space4XFaction> factionLookup,
            in ComponentLookup<EmpireMembership> empireLookup,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in BufferLookup<Space4XContactStanding> contactLookup,
            in BufferLookup<FactionRelationEntry> relationLookup,
            in BufferLookup<RacePresence> raceLookup,
            in BufferLookup<CulturePresence> cultureLookup)
        {
            if (offer.IssuerFactionId == 0 || offer.Issuer == Entity.Null || !factionLookup.HasComponent(offer.Issuer))
            {
                return true;
            }

            if (!TryResolveAgentFaction(agentEntity, in carrierLookup, in affiliationLookup, in factionLookup, out var agentFaction, out var agentFactionId))
            {
                return true;
            }

            if (agentFaction == offer.Issuer || agentFactionId == offer.IssuerFactionId)
            {
                return true;
            }

            var issuerFaction = offer.Issuer;
            var issuerProfile = factionLookup[issuerFaction];

            var standing = ResolveStanding(agentFaction, offer.IssuerFactionId, in contactLookup, in relationLookup);
            var sameEmpire = IsSameEmpire(agentFaction, issuerFaction, in factionLookup, in empireLookup, in affiliationLookup);
            if (sameEmpire && standing < SameEmpireFloor)
            {
                standing = SameEmpireFloor;
            }

            var sameRace = HasSharedDominantRace(agentFaction, issuerFaction, in raceLookup);
            var sameCulture = HasSharedDominantCulture(agentFaction, issuerFaction, in cultureLookup);

            var required = ResolveStandingRequirement(offer.Type);
            required += math.saturate(offer.Risk) * 0.1f;

            if (sameEmpire)
            {
                required -= 0.15f;
            }

            if (sameRace)
            {
                required -= 0.05f;
            }

            if (sameCulture)
            {
                required -= 0.05f;
            }

            if ((issuerProfile.Outlook & FactionOutlook.Xenophobe) != 0 && !sameEmpire)
            {
                required += 0.15f;
                if (sameRace && sameCulture)
                {
                    required -= 0.1f;
                }
            }
            else if ((issuerProfile.Outlook & FactionOutlook.Xenophile) != 0 && !sameEmpire)
            {
                required -= 0.05f;
            }

            required = math.clamp(required, 0f, 0.9f);
            return standing >= required;
        }

        public static bool PassesStandingGate(
            Entity agentEntity,
            Entity issuerFaction,
            ushort issuerFactionId,
            float requiredStanding,
            in ComponentLookup<Space4XFaction> factionLookup,
            in ComponentLookup<EmpireMembership> empireLookup,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in BufferLookup<Space4XContactStanding> contactLookup,
            in BufferLookup<FactionRelationEntry> relationLookup,
            in BufferLookup<RacePresence> raceLookup,
            in BufferLookup<CulturePresence> cultureLookup)
        {
            if (issuerFactionId == 0 || issuerFaction == Entity.Null || !factionLookup.HasComponent(issuerFaction))
            {
                return true;
            }

            if (!TryResolveFaction(agentEntity, in carrierLookup, in affiliationLookup, in factionLookup, out var agentFaction, out var agentFactionId))
            {
                return true;
            }

            if (agentFaction == issuerFaction || agentFactionId == issuerFactionId)
            {
                return true;
            }

            var issuerProfile = factionLookup[issuerFaction];
            var standing = ResolveStanding(agentFaction, issuerFactionId, in contactLookup, in relationLookup);
            var sameEmpire = IsSameEmpire(agentFaction, issuerFaction, in factionLookup, in empireLookup, in affiliationLookup);
            if (sameEmpire && standing < SameEmpireFloor)
            {
                standing = SameEmpireFloor;
            }

            var sameRace = HasSharedDominantRace(agentFaction, issuerFaction, in raceLookup);
            var sameCulture = HasSharedDominantCulture(agentFaction, issuerFaction, in cultureLookup);

            var required = math.clamp(requiredStanding, 0f, 0.9f);

            if (sameEmpire)
            {
                required -= 0.15f;
            }

            if (sameRace)
            {
                required -= 0.05f;
            }

            if (sameCulture)
            {
                required -= 0.05f;
            }

            if ((issuerProfile.Outlook & FactionOutlook.Xenophobe) != 0 && !sameEmpire)
            {
                required += 0.15f;
                if (sameRace && sameCulture)
                {
                    required -= 0.1f;
                }
            }
            else if ((issuerProfile.Outlook & FactionOutlook.Xenophile) != 0 && !sameEmpire)
            {
                required -= 0.05f;
            }

            required = math.clamp(required, 0f, 0.9f);
            return standing >= required;
        }

        public static bool TryResolveFaction(
            Entity entity,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<Space4XFaction> factionLookup,
            out Entity factionEntity,
            out ushort factionId)
        {
            factionEntity = Entity.Null;
            factionId = 0;

            if (entity != Entity.Null && factionLookup.HasComponent(entity))
            {
                factionEntity = entity;
                factionId = factionLookup[entity].FactionId;
                return true;
            }

            return TryResolveAgentFaction(entity, in carrierLookup, in affiliationLookup, in factionLookup, out factionEntity, out factionId);
        }

        private static bool TryResolveAgentFaction(
            Entity agentEntity,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<Space4XFaction> factionLookup,
            out Entity factionEntity,
            out ushort factionId)
        {
            factionEntity = Entity.Null;
            factionId = 0;

            if (carrierLookup.HasComponent(agentEntity))
            {
                var carrier = carrierLookup[agentEntity];
                if (carrier.AffiliationEntity != Entity.Null && factionLookup.HasComponent(carrier.AffiliationEntity))
                {
                    factionEntity = carrier.AffiliationEntity;
                }
            }

            if (factionEntity == Entity.Null && affiliationLookup.HasBuffer(agentEntity))
            {
                var affiliations = affiliationLookup[agentEntity];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Type != AffiliationType.Faction || tag.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (factionLookup.HasComponent(tag.Target))
                    {
                        factionEntity = tag.Target;
                        break;
                    }
                }
            }

            if (factionEntity == Entity.Null || !factionLookup.HasComponent(factionEntity))
            {
                return false;
            }

            factionId = factionLookup[factionEntity].FactionId;
            return true;
        }

        private static float ResolveStanding(
            Entity agentFaction,
            ushort issuerFactionId,
            in BufferLookup<Space4XContactStanding> contactLookup,
            in BufferLookup<FactionRelationEntry> relationLookup)
        {
            if (contactLookup.HasBuffer(agentFaction))
            {
                var buffer = contactLookup[agentFaction];
                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (entry.ContactFactionId == issuerFactionId)
                    {
                        return math.clamp((float)entry.Standing, 0f, 1f);
                    }
                }
            }

            if (relationLookup.HasBuffer(agentFaction))
            {
                var buffer = relationLookup[agentFaction];
                for (int i = 0; i < buffer.Length; i++)
                {
                    var relation = buffer[i].Relation;
                    if (relation.OtherFactionId == issuerFactionId)
                    {
                        var normalized = ((float)relation.Score + 100f) / 200f;
                        return math.clamp(normalized, 0f, 1f);
                    }
                }
            }

            return DefaultStanding;
        }

        private static bool IsSameEmpire(
            Entity leftFaction,
            Entity rightFaction,
            in ComponentLookup<Space4XFaction> factionLookup,
            in ComponentLookup<EmpireMembership> empireLookup,
            in BufferLookup<AffiliationTag> affiliationLookup)
        {
            var leftEmpire = ResolveEmpireEntity(leftFaction, in factionLookup, in empireLookup, in affiliationLookup);
            if (leftEmpire == Entity.Null)
            {
                return false;
            }

            var rightEmpire = ResolveEmpireEntity(rightFaction, in factionLookup, in empireLookup, in affiliationLookup);
            return rightEmpire != Entity.Null && leftEmpire == rightEmpire;
        }

        private static Entity ResolveEmpireEntity(
            Entity factionEntity,
            in ComponentLookup<Space4XFaction> factionLookup,
            in ComponentLookup<EmpireMembership> empireLookup,
            in BufferLookup<AffiliationTag> affiliationLookup)
        {
            if (factionEntity == Entity.Null || !factionLookup.HasComponent(factionEntity))
            {
                return Entity.Null;
            }

            var faction = factionLookup[factionEntity];
            if (faction.Type == FactionType.Empire)
            {
                return factionEntity;
            }

            if (empireLookup.HasComponent(factionEntity))
            {
                var membership = empireLookup[factionEntity];
                if (membership.Empire != Entity.Null)
                {
                    return membership.Empire;
                }
            }

            if (affiliationLookup.HasBuffer(factionEntity))
            {
                var affiliations = affiliationLookup[factionEntity];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Type == AffiliationType.Empire && tag.Target != Entity.Null)
                    {
                        return tag.Target;
                    }
                }
            }

            return Entity.Null;
        }

        private static bool HasSharedDominantRace(Entity leftFaction, Entity rightFaction, in BufferLookup<RacePresence> raceLookup)
        {
            return TryResolveDominantRace(leftFaction, in raceLookup, out var leftRace)
                   && TryResolveDominantRace(rightFaction, in raceLookup, out var rightRace)
                   && leftRace == rightRace;
        }

        private static bool HasSharedDominantCulture(Entity leftFaction, Entity rightFaction, in BufferLookup<CulturePresence> cultureLookup)
        {
            return TryResolveDominantCulture(leftFaction, in cultureLookup, out var leftCulture)
                   && TryResolveDominantCulture(rightFaction, in cultureLookup, out var rightCulture)
                   && leftCulture == rightCulture;
        }

        private static bool TryResolveDominantRace(Entity factionEntity, in BufferLookup<RacePresence> raceLookup, out ushort raceId)
        {
            raceId = 0;
            if (!raceLookup.HasBuffer(factionEntity))
            {
                return false;
            }

            var buffer = raceLookup[factionEntity];
            if (buffer.Length == 0)
            {
                return false;
            }

            var bestCount = int.MinValue;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Count > bestCount)
                {
                    bestCount = buffer[i].Count;
                    raceId = buffer[i].RaceId;
                }
            }

            return bestCount > 0;
        }

        private static bool TryResolveDominantCulture(Entity factionEntity, in BufferLookup<CulturePresence> cultureLookup, out ushort cultureId)
        {
            cultureId = 0;
            if (!cultureLookup.HasBuffer(factionEntity))
            {
                return false;
            }

            var buffer = cultureLookup[factionEntity];
            if (buffer.Length == 0)
            {
                return false;
            }

            var bestCount = int.MinValue;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Count > bestCount)
                {
                    bestCount = buffer[i].Count;
                    cultureId = buffer[i].CultureId;
                }
            }

            return bestCount > 0;
        }

        private static float ResolveStandingRequirement(Space4XMissionType type)
        {
            return type switch
            {
                Space4XMissionType.Scout => 0.1f,
                Space4XMissionType.Survey => 0.1f,
                Space4XMissionType.Mine => 0.15f,
                Space4XMissionType.Salvage => 0.15f,
                Space4XMissionType.Acquire => 0.15f,
                Space4XMissionType.HaulDelivery => 0.2f,
                Space4XMissionType.HaulProcure => 0.2f,
                Space4XMissionType.Resupply => 0.2f,
                Space4XMissionType.Trade => 0.2f,
                Space4XMissionType.Patrol => 0.25f,
                Space4XMissionType.Intercept => 0.25f,
                Space4XMissionType.Escort => 0.25f,
                Space4XMissionType.Repair => 0.25f,
                Space4XMissionType.Expedition => 0.3f,
                Space4XMissionType.BuildStation => 0.3f,
                Space4XMissionType.Raid => 0.35f,
                Space4XMissionType.Destroy => 0.35f,
                _ => 0.2f
            };
        }
    }
}
