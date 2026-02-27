using PureDOTS.Input;
using Space4X.Registry;
using Space4X.Runtime.Interaction;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Interaction
{
    internal static class Space4XDivineHandInfluenceUtility
    {
        public static bool CanManipulateTarget(
            ref SystemState state,
            Entity target,
            in Space4XDivineHandPolicy policy,
            in ComponentLookup<LocalTransform> transformLookup,
            in ComponentLookup<SelectionOwner> ownerLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<FactionResources> factionResourcesLookup,
            in EntityQuery playerFlagshipQuery,
            in EntityQuery ownedInfluenceQuery,
            out bool ownedByPlayer,
            out float3 influenceCenter,
            out float influenceRadius)
        {
            ownedByPlayer = false;
            influenceCenter = float3.zero;
            influenceRadius = math.max(1f, policy.BaseInfluenceRadius);

            if (target == Entity.Null || !state.EntityManager.Exists(target))
            {
                return false;
            }

            if (ownerLookup.HasComponent(target) && ownerLookup[target].PlayerId == 0)
            {
                ownedByPlayer = true;
                if (policy.AllowOwnedAnywhere != 0)
                {
                    return true;
                }
            }

            if (!transformLookup.HasComponent(target))
            {
                return ownedByPlayer;
            }

            if (!TryResolvePlayerInfluence(
                    ref state,
                    in policy,
                    in transformLookup,
                    in ownerLookup,
                    in affiliationLookup,
                    in factionResourcesLookup,
                    in playerFlagshipQuery,
                    in ownedInfluenceQuery,
                    out influenceCenter,
                    out influenceRadius))
            {
                return ownedByPlayer;
            }

            var targetPosition = transformLookup[target].Position;
            var allowedRadius = influenceRadius + math.max(0f, policy.OutOfInfluenceGraceRadius);
            return math.distancesq(targetPosition, influenceCenter) <= allowedRadius * allowedRadius;
        }

        public static Space4XDivineHandPolicy NormalizePolicy(in Space4XDivineHandPolicy configured)
        {
            var defaults = Space4XDivineHandPolicy.CreateDefault();
            var minRadius = math.max(1f, configured.BaseInfluenceRadius > 0f ? configured.BaseInfluenceRadius : defaults.BaseInfluenceRadius);
            var maxRadius = math.max(minRadius, configured.MaxInfluenceRadius > 0f ? configured.MaxInfluenceRadius : defaults.MaxInfluenceRadius);
            var bonusPerInfluence = configured.BonusRadiusPerInfluencePoint >= 0f
                ? configured.BonusRadiusPerInfluencePoint
                : defaults.BonusRadiusPerInfluencePoint;
            var grace = configured.OutOfInfluenceGraceRadius >= 0f
                ? configured.OutOfInfluenceGraceRadius
                : defaults.OutOfInfluenceGraceRadius;

            return new Space4XDivineHandPolicy
            {
                BaseInfluenceRadius = minRadius,
                BonusRadiusPerInfluencePoint = bonusPerInfluence,
                MaxInfluenceRadius = maxRadius,
                OutOfInfluenceGraceRadius = grace,
                AllowOwnedAnywhere = configured.AllowOwnedAnywhere,
                AllowCelestialDirectPick = configured.AllowCelestialDirectPick
            };
        }

        private static bool TryResolvePlayerInfluence(
            ref SystemState state,
            in Space4XDivineHandPolicy policy,
            in ComponentLookup<LocalTransform> transformLookup,
            in ComponentLookup<SelectionOwner> ownerLookup,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<FactionResources> factionResourcesLookup,
            in EntityQuery playerFlagshipQuery,
            in EntityQuery ownedInfluenceQuery,
            out float3 center,
            out float radius)
        {
            center = float3.zero;
            radius = math.max(1f, policy.BaseInfluenceRadius);

            if (!playerFlagshipQuery.IsEmptyIgnoreFilter)
            {
                using var flagships = playerFlagshipQuery.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < flagships.Length; i++)
                {
                    var flagship = flagships[i];
                    if (flagship == Entity.Null || !state.EntityManager.Exists(flagship) || !transformLookup.HasComponent(flagship))
                    {
                        continue;
                    }

                    center = transformLookup[flagship].Position;
                    radius = ResolveInfluenceRadius(
                        flagship,
                        in policy,
                        in affiliationLookup,
                        in factionResourcesLookup);
                    return true;
                }
            }

            if (!ownedInfluenceQuery.IsEmptyIgnoreFilter)
            {
                using var ownedEntities = ownedInfluenceQuery.ToEntityArray(Allocator.Temp);
                using var ownedTransforms = ownedInfluenceQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

                for (int i = 0; i < ownedEntities.Length; i++)
                {
                    var entity = ownedEntities[i];
                    if (entity == Entity.Null || !state.EntityManager.Exists(entity) || !ownerLookup.HasComponent(entity))
                    {
                        continue;
                    }

                    if (ownerLookup[entity].PlayerId != 0)
                    {
                        continue;
                    }

                    center = ownedTransforms[i].Position;
                    radius = math.max(1f, policy.BaseInfluenceRadius);
                    return true;
                }
            }

            return false;
        }

        private static float ResolveInfluenceRadius(
            Entity flagship,
            in Space4XDivineHandPolicy policy,
            in BufferLookup<AffiliationTag> affiliationLookup,
            in ComponentLookup<FactionResources> factionResourcesLookup)
        {
            var radius = math.max(1f, policy.BaseInfluenceRadius);
            if (flagship != Entity.Null && affiliationLookup.HasBuffer(flagship))
            {
                var affiliations = affiliationLookup[flagship];
                for (int i = 0; i < affiliations.Length; i++)
                {
                    var tag = affiliations[i];
                    if (tag.Type != AffiliationType.Faction || tag.Target == Entity.Null || !factionResourcesLookup.HasComponent(tag.Target))
                    {
                        continue;
                    }

                    var influence = math.max(0f, factionResourcesLookup[tag.Target].Influence);
                    radius += influence * math.max(0f, policy.BonusRadiusPerInfluencePoint);
                    break;
                }
            }

            return math.clamp(radius, math.max(1f, policy.BaseInfluenceRadius), math.max(policy.BaseInfluenceRadius, policy.MaxInfluenceRadius));
        }
    }
}
