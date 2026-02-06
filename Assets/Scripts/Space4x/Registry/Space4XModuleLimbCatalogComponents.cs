using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public enum ModuleLimbFamily : byte
    {
        Cooling = 0,
        Sensors = 1,
        Lensing = 2,
        Projector = 3,
        Guidance = 4,
        Actuator = 5,
        Structural = 6,
        Power = 7
    }

    /// <summary>
    /// Normalized limb coverage for a module instance. 0 = missing, 1 = fully functional.
    /// </summary>
    public struct ModuleLimbProfile : IComponentData
    {
        public float Cooling;
        public float Sensors;
        public float Lensing;
        public float Projector;
        public float Guidance;
        public float Actuator;
        public float Structural;
        public float Power;
    }

    public struct ModuleLimbSpec
    {
        public FixedString64Bytes ModuleId;
        public ModuleLimbProfile Profile;
    }

    public struct ModuleLimbCatalogBlob
    {
        public BlobArray<ModuleLimbSpec> Modules;
    }

    public struct ModuleLimbCatalogSingleton : IComponentData
    {
        public BlobAssetReference<ModuleLimbCatalogBlob> Catalog;
    }
}
