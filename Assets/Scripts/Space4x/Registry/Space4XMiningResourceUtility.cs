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

            if (resourceId == CreateOreId() || resourceId == CreateShortOreId())
            {
                resourceType = ResourceType.Ore;
                return true;
            }

            if (resourceId == CreateVolatilesId() || resourceId == CreateShortVolatilesId())
            {
                resourceType = ResourceType.Volatiles;
                return true;
            }

            if (resourceId == CreateTransplutonicOreId() || resourceId == CreateShortTransplutonicOreId())
            {
                resourceType = ResourceType.TransplutonicOre;
                return true;
            }

            if (resourceId == CreateExoticGasesId() || resourceId == CreateShortExoticGasesId())
            {
                resourceType = ResourceType.ExoticGases;
                return true;
            }

            if (resourceId == CreateVolatileMotesId() || resourceId == CreateShortVolatileMotesId())
            {
                resourceType = ResourceType.VolatileMotes;
                return true;
            }

            if (resourceId == CreateIndustrialCrystalsId() || resourceId == CreateShortIndustrialCrystalsId())
            {
                resourceType = ResourceType.IndustrialCrystals;
                return true;
            }

            if (resourceId == CreateIsotopesId() || resourceId == CreateShortIsotopesId())
            {
                resourceType = ResourceType.Isotopes;
                return true;
            }

            if (resourceId == CreateHeavyWaterId() || resourceId == CreateShortHeavyWaterId())
            {
                resourceType = ResourceType.HeavyWater;
                return true;
            }

            if (resourceId == CreateLiquidOzoneId() || resourceId == CreateShortLiquidOzoneId())
            {
                resourceType = ResourceType.LiquidOzone;
                return true;
            }

            if (resourceId == CreateStrontiumClathratesId() || resourceId == CreateShortStrontiumClathratesId())
            {
                resourceType = ResourceType.StrontiumClathrates;
                return true;
            }

            if (resourceId == CreateSalvageComponentsId() || resourceId == CreateShortSalvageComponentsId())
            {
                resourceType = ResourceType.SalvageComponents;
                return true;
            }

            if (resourceId == CreateBoosterGasId() || resourceId == CreateShortBoosterGasId())
            {
                resourceType = ResourceType.BoosterGas;
                return true;
            }

            if (resourceId == CreateRelicDataId() || resourceId == CreateShortRelicDataId())
            {
                resourceType = ResourceType.RelicData;
                return true;
            }

            if (resourceId == CreateFoodId() || resourceId == CreateShortFoodId())
            {
                resourceType = ResourceType.Food;
                return true;
            }

            if (resourceId == CreateWaterId() || resourceId == CreateShortWaterId())
            {
                resourceType = ResourceType.Water;
                return true;
            }

            if (resourceId == CreateSuppliesId() || resourceId == CreateShortSuppliesId())
            {
                resourceType = ResourceType.Supplies;
                return true;
            }

            if (resourceId == CreateFuelId() || resourceId == CreateShortFuelId())
            {
                resourceType = ResourceType.Fuel;
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

        private static FixedString64Bytes CreateOreId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('o'); id.Append('r'); id.Append('e');
            return id;
        }

        private static FixedString64Bytes CreateVolatilesId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('v'); id.Append('o'); id.Append('l'); id.Append('a'); id.Append('t'); id.Append('i'); id.Append('l'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateTransplutonicOreId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('t'); id.Append('r'); id.Append('a'); id.Append('n'); id.Append('s'); id.Append('p'); id.Append('l'); id.Append('u'); id.Append('t'); id.Append('o'); id.Append('n'); id.Append('i'); id.Append('c'); id.Append('_'); id.Append('o'); id.Append('r'); id.Append('e');
            return id;
        }

        private static FixedString64Bytes CreateExoticGasesId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('e'); id.Append('x'); id.Append('o'); id.Append('t'); id.Append('i'); id.Append('c'); id.Append('_'); id.Append('g'); id.Append('a'); id.Append('s'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateVolatileMotesId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('v'); id.Append('o'); id.Append('l'); id.Append('a'); id.Append('t'); id.Append('i'); id.Append('l'); id.Append('e'); id.Append('_'); id.Append('m'); id.Append('o'); id.Append('t'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateIndustrialCrystalsId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('i'); id.Append('n'); id.Append('d'); id.Append('u'); id.Append('s'); id.Append('t'); id.Append('r'); id.Append('i'); id.Append('a'); id.Append('l'); id.Append('_'); id.Append('c'); id.Append('r'); id.Append('y'); id.Append('s'); id.Append('t'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateIsotopesId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('i'); id.Append('s'); id.Append('o'); id.Append('t'); id.Append('o'); id.Append('p'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateHeavyWaterId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('h'); id.Append('e'); id.Append('a'); id.Append('v'); id.Append('y'); id.Append('_'); id.Append('w'); id.Append('a'); id.Append('t'); id.Append('e'); id.Append('r');
            return id;
        }

        private static FixedString64Bytes CreateLiquidOzoneId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('l'); id.Append('i'); id.Append('q'); id.Append('u'); id.Append('i'); id.Append('d'); id.Append('_'); id.Append('o'); id.Append('z'); id.Append('o'); id.Append('n'); id.Append('e');
            return id;
        }

        private static FixedString64Bytes CreateStrontiumClathratesId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('s'); id.Append('t'); id.Append('r'); id.Append('o'); id.Append('n'); id.Append('t'); id.Append('i'); id.Append('u'); id.Append('m'); id.Append('_'); id.Append('c'); id.Append('l'); id.Append('a'); id.Append('t'); id.Append('h'); id.Append('r'); id.Append('a'); id.Append('t'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateSalvageComponentsId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('s'); id.Append('a'); id.Append('l'); id.Append('v'); id.Append('a'); id.Append('g'); id.Append('e'); id.Append('_'); id.Append('c'); id.Append('o'); id.Append('m'); id.Append('p'); id.Append('o'); id.Append('n'); id.Append('e'); id.Append('n'); id.Append('t'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateBoosterGasId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('b'); id.Append('o'); id.Append('o'); id.Append('s'); id.Append('t'); id.Append('e'); id.Append('r'); id.Append('_'); id.Append('g'); id.Append('a'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateRelicDataId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('l'); id.Append('i'); id.Append('c'); id.Append('_'); id.Append('d'); id.Append('a'); id.Append('t'); id.Append('a');
            return id;
        }

        private static FixedString64Bytes CreateFoodId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('f'); id.Append('o'); id.Append('o'); id.Append('d');
            return id;
        }

        private static FixedString64Bytes CreateWaterId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('w'); id.Append('a'); id.Append('t'); id.Append('e'); id.Append('r');
            return id;
        }

        private static FixedString64Bytes CreateSuppliesId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('s'); id.Append('u'); id.Append('p'); id.Append('p'); id.Append('l'); id.Append('i'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateFuelId()
        {
            FixedString64Bytes id = default;
            id.Append('s'); id.Append('p'); id.Append('a'); id.Append('c'); id.Append('e'); id.Append('4'); id.Append('x'); id.Append('.');
            id.Append('r'); id.Append('e'); id.Append('s'); id.Append('o'); id.Append('u'); id.Append('r'); id.Append('c'); id.Append('e'); id.Append('.');
            id.Append('f'); id.Append('u'); id.Append('e'); id.Append('l');
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

        private static FixedString64Bytes CreateShortVolatilesId()
        {
            FixedString64Bytes id = default;
            id.Append('V'); id.Append('o'); id.Append('l'); id.Append('a'); id.Append('t'); id.Append('i'); id.Append('l'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortTransplutonicOreId()
        {
            FixedString64Bytes id = default;
            id.Append('T'); id.Append('r'); id.Append('a'); id.Append('n'); id.Append('s'); id.Append('p'); id.Append('l'); id.Append('u'); id.Append('t'); id.Append('o'); id.Append('n'); id.Append('i'); id.Append('c'); id.Append('O'); id.Append('r'); id.Append('e');
            return id;
        }

        private static FixedString64Bytes CreateShortExoticGasesId()
        {
            FixedString64Bytes id = default;
            id.Append('E'); id.Append('x'); id.Append('o'); id.Append('t'); id.Append('i'); id.Append('c'); id.Append('G'); id.Append('a'); id.Append('s'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortVolatileMotesId()
        {
            FixedString64Bytes id = default;
            id.Append('V'); id.Append('o'); id.Append('l'); id.Append('a'); id.Append('t'); id.Append('i'); id.Append('l'); id.Append('e'); id.Append('M'); id.Append('o'); id.Append('t'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortIndustrialCrystalsId()
        {
            FixedString64Bytes id = default;
            id.Append('I'); id.Append('n'); id.Append('d'); id.Append('u'); id.Append('s'); id.Append('t'); id.Append('r'); id.Append('i'); id.Append('a'); id.Append('l'); id.Append('C'); id.Append('r'); id.Append('y'); id.Append('s'); id.Append('t'); id.Append('a'); id.Append('l'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortIsotopesId()
        {
            FixedString64Bytes id = default;
            id.Append('I'); id.Append('s'); id.Append('o'); id.Append('t'); id.Append('o'); id.Append('p'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortHeavyWaterId()
        {
            FixedString64Bytes id = default;
            id.Append('H'); id.Append('e'); id.Append('a'); id.Append('v'); id.Append('y'); id.Append('W'); id.Append('a'); id.Append('t'); id.Append('e'); id.Append('r');
            return id;
        }

        private static FixedString64Bytes CreateShortLiquidOzoneId()
        {
            FixedString64Bytes id = default;
            id.Append('L'); id.Append('i'); id.Append('q'); id.Append('u'); id.Append('i'); id.Append('d'); id.Append('O'); id.Append('z'); id.Append('o'); id.Append('n'); id.Append('e');
            return id;
        }

        private static FixedString64Bytes CreateShortStrontiumClathratesId()
        {
            FixedString64Bytes id = default;
            id.Append('S'); id.Append('t'); id.Append('r'); id.Append('o'); id.Append('n'); id.Append('t'); id.Append('i'); id.Append('u'); id.Append('m'); id.Append('C'); id.Append('l'); id.Append('a'); id.Append('t'); id.Append('h'); id.Append('r'); id.Append('a'); id.Append('t'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortSalvageComponentsId()
        {
            FixedString64Bytes id = default;
            id.Append('S'); id.Append('a'); id.Append('l'); id.Append('v'); id.Append('a'); id.Append('g'); id.Append('e'); id.Append('C'); id.Append('o'); id.Append('m'); id.Append('p'); id.Append('o'); id.Append('n'); id.Append('e'); id.Append('n'); id.Append('t'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortBoosterGasId()
        {
            FixedString64Bytes id = default;
            id.Append('B'); id.Append('o'); id.Append('o'); id.Append('s'); id.Append('t'); id.Append('e'); id.Append('r'); id.Append('G'); id.Append('a'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortRelicDataId()
        {
            FixedString64Bytes id = default;
            id.Append('R'); id.Append('e'); id.Append('l'); id.Append('i'); id.Append('c'); id.Append('D'); id.Append('a'); id.Append('t'); id.Append('a');
            return id;
        }

        private static FixedString64Bytes CreateShortFoodId()
        {
            FixedString64Bytes id = default;
            id.Append('F'); id.Append('o'); id.Append('o'); id.Append('d');
            return id;
        }

        private static FixedString64Bytes CreateShortWaterId()
        {
            FixedString64Bytes id = default;
            id.Append('W'); id.Append('a'); id.Append('t'); id.Append('e'); id.Append('r');
            return id;
        }

        private static FixedString64Bytes CreateShortSuppliesId()
        {
            FixedString64Bytes id = default;
            id.Append('S'); id.Append('u'); id.Append('p'); id.Append('p'); id.Append('l'); id.Append('i'); id.Append('e'); id.Append('s');
            return id;
        }

        private static FixedString64Bytes CreateShortFuelId()
        {
            FixedString64Bytes id = default;
            id.Append('F'); id.Append('u'); id.Append('e'); id.Append('l');
            return id;
        }
    }
}
