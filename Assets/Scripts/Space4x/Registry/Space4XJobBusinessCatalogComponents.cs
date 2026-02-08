using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public enum Space4XJobKind : byte
    {
        Mining = 0,
        Refining = 1,
        Hauling = 2,
        Repair = 3,
        Survey = 4,
        Patrol = 5,
        TradeBroker = 6,
        Construction = 7,
        Salvage = 8,
        Security = 9,
        Production = 10
    }

    public enum Space4XBusinessKind : byte
    {
        MiningCompany = 0,
        HaulageCompany = 1,
        Shipwright = 2,
        StationServices = 3,
        MarketHub = 4,
        SalvageCrew = 5,
        Agriplex = 6,
        FuelWorks = 7,
        IndustrialFoundry = 8,
        DeepCoreSyndicate = 9
    }

    public enum Space4XBusinessOwnerKind : byte
    {
        Individual = 0,
        Group = 1,
        Faction = 2,
        Empire = 3
    }

    public struct Space4XJobResourceSpec
    {
        public ResourceType Type;
        public float Units;

        public static Space4XJobResourceSpec Create(ResourceType type, float units)
        {
            return new Space4XJobResourceSpec
            {
                Type = type,
                Units = units
            };
        }
    }

    public struct Space4XJobDefinition
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes Name;
        public Space4XJobKind Kind;
        public FacilityBusinessClass RequiredFacility;
        public byte MinTechTier;
        public byte DurationTicks;
        public float StandingGate;
        public BlobArray<Space4XJobResourceSpec> Inputs;
        public BlobArray<Space4XJobResourceSpec> Outputs;
    }

    public struct Space4XBusinessDefinition
    {
        public FixedString64Bytes Id;
        public FixedString64Bytes Name;
        public Space4XBusinessKind Kind;
        public Space4XBusinessOwnerKind OwnerKind;
        public FacilityBusinessClass PrimaryFacility;
        public float StartingCredits;
        public BlobArray<FixedString64Bytes> JobIds;
    }

    public struct Space4XJobCatalogBlob
    {
        public BlobArray<Space4XJobDefinition> Jobs;
    }

    public struct Space4XBusinessCatalogBlob
    {
        public BlobArray<Space4XBusinessDefinition> Businesses;
    }

    public struct Space4XJobCatalogSingleton : IComponentData
    {
        public BlobAssetReference<Space4XJobCatalogBlob> Catalog;
    }

    public struct Space4XBusinessCatalogSingleton : IComponentData
    {
        public BlobAssetReference<Space4XBusinessCatalogBlob> Catalog;
    }
}
