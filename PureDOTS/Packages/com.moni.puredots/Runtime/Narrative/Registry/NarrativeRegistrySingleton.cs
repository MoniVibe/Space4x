using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Narrative
{
    /// <summary>
    /// Singleton component holding blob asset references to narrative registries.
    /// </summary>
    public struct NarrativeRegistrySingleton : IComponentData
    {
        public BlobAssetReference<SituationArchetypeRegistry> SituationRegistry;
        public BlobAssetReference<NarrativeEventRegistry> EventRegistry;
    }
}

