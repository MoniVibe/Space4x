using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    [Flags]
    public enum Space4XStationServiceFlags : uint
    {
        None = 0,
        Docking = 1u << 0,
        TradeMarket = 1u << 1,
        Shipyard = 1u << 2,
        Refit = 1u << 3,
        Overhaul = 1u << 4,
        Habitat = 1u << 5,
        MercenaryBoard = 1u << 6,
        MissionBoard = 1u << 7,
        Manufacturing = 1u << 8,
        SupplyDepot = 1u << 9
    }

    public enum Space4XStationSpecialization : byte
    {
        General = 0,
        TradeHub = 1,
        Shipyard = 2,
        Industrial = 3,
        Supply = 4,
        Military = 5,
        Habitat = 6,
        Mercenary = 7
    }

    /// <summary>
    /// Runtime profile for station services and specialization.
    /// </summary>
    public struct Space4XStationServiceProfile : IComponentData
    {
        public Space4XStationSpecialization Specialization;
        public Space4XStationServiceFlags Services;
        public byte Tier;
        public float ServiceScale;
    }

    /// <summary>
    /// Optional per-entity override for station service profile.
    /// </summary>
    public struct Space4XStationServiceProfileOverride : IComponentData
    {
        public byte Enabled;
        public Space4XStationSpecialization Specialization;
        public Space4XStationServiceFlags Services;
        public byte Tier;
        public float ServiceScale;
    }

    /// <summary>
    /// Relation and no-fly access policy for a station.
    /// </summary>
    public struct Space4XStationAccessPolicy : IComponentData
    {
        public float MinStandingForApproach;
        public float MinStandingForDock;
        public float WarningRadiusMeters;
        public float NoFlyRadiusMeters;
        public byte EnforceNoFlyZone;
        public byte DenyDockingWithoutStanding;
    }

    /// <summary>
    /// Optional per-entity override for station access policy.
    /// </summary>
    public struct Space4XStationAccessPolicyOverride : IComponentData
    {
        public byte Enabled;
        public float MinStandingForApproach;
        public float MinStandingForDock;
        public float WarningRadiusMeters;
        public float NoFlyRadiusMeters;
        public byte EnforceNoFlyZone;
        public byte DenyDockingWithoutStanding;
    }

    /// <summary>
    /// Marker that this vessel/entity intentionally ignores no-fly restrictions.
    /// </summary>
    public struct Space4XTrespassIntentTag : IComponentData
    {
    }

    /// <summary>
    /// Current no-fly violation state for a vessel/entity.
    /// </summary>
    public struct Space4XStationNoFlyViolation : IComponentData
    {
        public Entity Station;
        public float DistanceMeters;
        public float Severity;
        public uint LastTick;
        public byte InsideNoFly;
    }

    /// <summary>
    /// Standing utility used by station access and docking gates.
    /// </summary>
    public static class Space4XStationAccessUtility
    {
        private const float DefaultStanding = 0.2f;

        public static bool PassesStandingGate(
            Entity actorEntity,
            Entity stationEntity,
            float requiredStanding,
            in ComponentLookup<Carrier> carrierLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<Space4XFaction> factionLookup,
            in BufferLookup<Space4XContactStanding> contactLookup,
            in BufferLookup<FactionRelationEntry> relationLookup)
        {
            if (!TryResolveFaction(stationEntity, in carrierLookup, in affiliationLookup, in factionLookup, out var stationFaction, out var stationFactionId))
            {
                return true;
            }

            if (stationFaction == Entity.Null || stationFactionId == 0)
            {
                return true;
            }

            if (!TryResolveFaction(actorEntity, in carrierLookup, in affiliationLookup, in factionLookup, out var actorFaction, out var actorFactionId))
            {
                return true;
            }

            if (actorFaction == stationFaction || actorFactionId == stationFactionId)
            {
                return true;
            }

            var standing = ResolveStanding(actorFaction, stationFactionId, in contactLookup, in relationLookup);
            var required = math.clamp(requiredStanding, 0f, 1f);
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

            if (entity == Entity.Null)
            {
                return false;
            }

            if (factionLookup.HasComponent(entity))
            {
                factionEntity = entity;
                factionId = factionLookup[entity].FactionId;
                return true;
            }

            if (carrierLookup.HasComponent(entity))
            {
                var carrier = carrierLookup[entity];
                if (carrier.AffiliationEntity != Entity.Null && factionLookup.HasComponent(carrier.AffiliationEntity))
                {
                    factionEntity = carrier.AffiliationEntity;
                    factionId = factionLookup[factionEntity].FactionId;
                    return true;
                }
            }

            if (affiliationLookup.HasBuffer(entity))
            {
                var affiliations = affiliationLookup[entity];
                for (var i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Type != AffiliationType.Faction || tag.Target == Entity.Null)
                    {
                        continue;
                    }

                    if (!factionLookup.HasComponent(tag.Target))
                    {
                        continue;
                    }

                    factionEntity = tag.Target;
                    factionId = factionLookup[factionEntity].FactionId;
                    return true;
                }
            }

            return false;
        }

        private static float ResolveStanding(
            Entity actorFaction,
            ushort stationFactionId,
            in BufferLookup<Space4XContactStanding> contactLookup,
            in BufferLookup<FactionRelationEntry> relationLookup)
        {
            if (contactLookup.HasBuffer(actorFaction))
            {
                var standings = contactLookup[actorFaction];
                for (var i = 0; i < standings.Length; i++)
                {
                    if (standings[i].ContactFactionId == stationFactionId)
                    {
                        return math.clamp((float)standings[i].Standing, 0f, 1f);
                    }
                }
            }

            if (relationLookup.HasBuffer(actorFaction))
            {
                var relations = relationLookup[actorFaction];
                for (var i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i].Relation;
                    if (relation.OtherFactionId == stationFactionId)
                    {
                        var normalized = ((float)relation.Score + 100f) / 200f;
                        return math.clamp(normalized, 0f, 1f);
                    }
                }
            }

            return DefaultStanding;
        }
    }
}
