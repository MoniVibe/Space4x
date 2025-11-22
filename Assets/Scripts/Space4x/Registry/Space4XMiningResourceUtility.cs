using PureDOTS.Runtime.Components;
using Unity.Collections;

namespace Space4X.Registry
{
    /// <summary>
    /// Maps spine resource identifiers to the enum used by the mining demo systems.
    /// </summary>
    internal static class Space4XMiningResourceUtility
    {
        public static ResourceType MapToResourceType(in FixedString64Bytes resourceId, ResourceType fallback = ResourceType.Minerals)
        {
            if (resourceId == CreateMineralsId())
            {
                return ResourceType.Minerals;
            }

            if (resourceId == CreateRareMetalsId())
            {
                return ResourceType.RareMetals;
            }

            if (resourceId == CreateEnergyCrystalsId())
            {
                return ResourceType.EnergyCrystals;
            }

            if (resourceId == CreateOrganicMatterId())
            {
                return ResourceType.OrganicMatter;
            }

            return fallback;
        }

        public static ResourceType MapToResourceType(in ResourceTypeId resourceTypeId, ResourceType fallback = ResourceType.Minerals)
        {
            return MapToResourceType(resourceTypeId.Value, fallback);
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
    }
}
