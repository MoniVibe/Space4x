using PureDOTS.Config;
using Unity.Entities;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Singleton component referencing the baked job definition catalog blob.
    /// </summary>
    public struct JobDefinitionCatalogComponent : IComponentData
    {
        public BlobAssetReference<JobDefinitionCatalogBlob> Catalog;
    }
}

