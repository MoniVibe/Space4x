using PureDOTS.Runtime.Components;
using Unity.Collections;

namespace Space4X.Registry
{
    /// <summary>
    /// Maps spine resource identifiers to the enum used by the mining simulation systems.
    /// </summary>
    internal static class Space4XMiningResourceUtility
    {
        public static ResourceType MapToResourceType(in FixedString64Bytes resourceId, ResourceType fallback = ResourceType.Minerals)
        {
            return TryMapToResourceType(resourceId, out var resolved) ? resolved : fallback;
        }

        public static ResourceType MapToResourceType(in ResourceTypeId resourceTypeId, ResourceType fallback = ResourceType.Minerals)
        {
            return MapToResourceType(resourceTypeId.Value, fallback);
        }

        public static bool TryMapToResourceType(in FixedString64Bytes resourceId, out ResourceType resourceType)
        {
            if (resourceId == CreateMineralsId() || resourceId == CreateShortMineralsId())
            {
                resourceType = ResourceType.Minerals;
                return true;
            }

            if (resourceId == CreateRareMetalsId() || resourceId == CreateShortRareMetalsId())
            {
                resourceType = ResourceType.RareMetals;
                return true;
            }

            if (resourceId == CreateEnergyCrystalsId() || resourceId == CreateShortEnergyCrystalsId())
            {
                resourceType = ResourceType.EnergyCrystals;
                return true;
            }

            if (resourceId == CreateOrganicMatterId() || resourceId == CreateShortOrganicMatterId())
            {
                resourceType = ResourceType.OrganicMatter;
                return true;
            }

            if (resourceId == CreateShortOreId())
            {
                resourceType = ResourceType.Ore;
                return true;
            }

            resourceType = default;
            return false;
        }

        public static bool TryMapDepositIdToResourceId(byte depositId, out FixedString64Bytes resourceId)
        {
            switch (depositId)
            {
                case 1:
                    resourceId = CreateShortOreId();
                    return true;
                case 2:
                    resourceId = CreateRareMetalsId();
                    return true;
                case 3:
                    resourceId = CreateEnergyCrystalsId();
                    return true;
                case 4:
                    resourceId = CreateOrganicMatterId();
                    return true;
                default:
                    resourceId = default;
                    return false;
            }
        }

        private static FixedString64Bytes CreateMineralsId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('m'); id.Append('i'); id.Append('n'); id.Append('e'); id.Append('r'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateRareMetalsId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('r'); id.Append('a'); id.Append('r'); id.Append('e'); id.Append('_'); id.Append('m'); id.Append('e'); id.Append('t'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateEnergyCrystalsId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('e'); id.Append('n'); id.Append('e'); id.Append('r'); id.Append('g'); id.Append('y'); id.Append('_'); id.Append('c'); id.Append('r'); id.Append('y'); id.Append('s'); id.Append('t'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateOrganicMatterId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('o'); id.Append('r'); id.Append('g'); id.Append('a'); id.Append('n'); id.Append('i'); id.Append('c'); id.Append('_'); id.Append('m'); id.Append('a'); id.Append('t'); id.Append('t'); id.Append('e'); id.Append('r');
            return id;
        }

        private static FixedString64Bytes CreateShortMineralsId()
        {
            FixedString64Bytes id = default;
            id.Append('M'); id.Append('i'); id.Append('n'); id.Append('e'); id.Append('r'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortRareMetalsId()
        {
            FixedString64Bytes id = default;
            id.Append('R'); id.Append('a'); id.Append('r'); id.Append('e'); id.Append('M'); id.Append('e'); id.Append('t'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortEnergyCrystalsId()
        {
            FixedString64Bytes id = default;
            id.Append('E'); id.Append('n'); id.Append('e'); id.Append('r'); id.Append('g'); id.Append('y'); id.Append('C'); id.Append('r'); id.Append('y'); id.Append('s'); id.Append('t'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortOrganicMatterId()
        {
            FixedString64Bytes id = default;
            id.Append('O'); id.Append('r'); id.Append('g'); id.Append('a'); id.Append('n'); id.Append('i'); id.Append('c'); id.Append('M'); id.Append('a'); id.Append('t'); id.Append('t'); id.Append('e'); id.Append('r');
            return id;
        }

        private static FixedString64Bytes CreateShortOreId()
        {
            FixedString64Bytes id = default;
            id.Append('O'); id.Append('r'); id.Append('e');
            return id;
        }
    }
}
