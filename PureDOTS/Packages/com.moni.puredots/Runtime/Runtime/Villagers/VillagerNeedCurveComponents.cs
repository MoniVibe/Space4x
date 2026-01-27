using PureDOTS.Config;
using Unity.Entities;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Singleton component referencing the baked need curve catalog blob.
    /// </summary>
    public struct VillagerNeedCurveCatalogComponent : IComponentData
    {
        public BlobAssetReference<VillagerNeedCurveCatalogBlob> Catalog;
    }
}

