using PureDOTS.Runtime.WorldGen;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.WorldGen
{
    [DisallowMultipleComponent]
    public sealed class WorldGenDefinitionsAuthoring : MonoBehaviour
    {
        [Tooltip("Base definitions (optional).")]
        public WorldGenDefinitionsCatalogAsset baseCatalog;

        [Tooltip("Additional definitions (mods / overrides). Later entries override earlier entries by Id.")]
        public WorldGenDefinitionsCatalogAsset[] additionalCatalogs;
    }

    public sealed class WorldGenDefinitionsBaker : Baker<WorldGenDefinitionsAuthoring>
    {
        public override void Bake(WorldGenDefinitionsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            if (!WorldGenDefinitionsCatalogAsset.TryBuildMergedBlobAsset(
                    authoring.baseCatalog,
                    authoring.additionalCatalogs,
                    out var blob,
                    out var definitionsHash,
                    out _,
                    out var error))
            {
                Debug.LogError($"[WorldGenDefinitionsBaker] Failed to build definitions: {error}", authoring);
                return;
            }

            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new WorldGenDefinitionsComponent
            {
                DefinitionsHash = definitionsHash,
                Definitions = blob
            });
        }
    }
}

