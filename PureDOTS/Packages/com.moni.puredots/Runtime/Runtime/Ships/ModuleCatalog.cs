using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Ships
{
    public struct ModuleDefinition
    {
        public FixedString64Bytes ModuleId;
        public ModuleFamily Family;
        public ModuleClass Class;
        public MountType RequiredMount;
        public MountSize RequiredSize;
        public float Mass;
        public float PowerRequired;
        public float OffenseRating;
        public float DefenseRating;
        public float UtilityRating;
        public byte EfficiencyPercent;
    }

    public struct ModuleCatalogBlob
    {
        public BlobArray<ModuleDefinition> Definitions;
    }

    public struct ModuleCatalog : IComponentData
    {
        public BlobAssetReference<ModuleCatalogBlob> Catalog;
    }
}
