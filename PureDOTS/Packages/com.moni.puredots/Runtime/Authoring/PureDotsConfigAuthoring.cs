using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Config;
using PureDOTS.Runtime.Resource;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    public sealed class PureDotsConfigAuthoring : MonoBehaviour
    {
        public PureDotsRuntimeConfig config;
    }

    public sealed class PureDotsConfigBaker : Baker<PureDotsConfigAuthoring>
    {
        public override void Bake(PureDotsConfigAuthoring authoring)
        {
            Debug.Log($"[PureDotsConfigBaker] Baking started for {authoring.name}");
            if (authoring.config == null)
            {
                Debug.LogWarning("PureDotsConfigAuthoring has no config asset assigned.", authoring);
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);

#if UNITY_EDITOR
            Debug.Log("[PureDotsConfigBaker] Bake start", authoring);
#endif

            var timeConfig = authoring.config.Time.ToComponent();
            var history = authoring.config.History.ToComponent();
            var pooling = authoring.config.Pooling.ToComponent();
            var threading = authoring.config.Threading.ToComponent();

            // Bake time config (used by TimeSettingsConfigSystem to initialize singletons)
            AddComponent(entity, timeConfig);

            AddComponent(entity, history);
            AddComponent(entity, pooling);
            AddComponent(entity, new PoolingSettings { Value = pooling });
            AddComponent(entity, threading);

            // Bake ResourceTypeIndex blob asset
            if (authoring.config.ResourceTypes != null && authoring.config.ResourceTypes.entries.Count > 0)
            {
                var catalog = authoring.config.ResourceTypes;
#if UNITY_EDITOR
                Debug.Log($"[PureDotsConfigBaker] Resource types entries = {catalog.entries.Count}", catalog);
#endif
                var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ResourceTypeIndexBlob>();

                var idsBuilder = builder.Allocate(ref root.Ids, catalog.entries.Count);
                var displayNamesBuilder = builder.Allocate(ref root.DisplayNames, catalog.entries.Count);
                var colorsBuilder = builder.Allocate(ref root.Colors, catalog.entries.Count);

                for (int i = 0; i < catalog.entries.Count; i++)
                {
                    var entry = catalog.entries[i];
                    idsBuilder[i] = new FixedString64Bytes(entry.id);
                    builder.AllocateString(ref displayNamesBuilder[i], entry.id); // Use ID as display name for now
                    colorsBuilder[i] = entry.displayColor;
                }

                var blobAsset = builder.CreateBlobAssetReference<ResourceTypeIndexBlob>(Allocator.Persistent);
                builder.Dispose();

                AddComponent(entity, new ResourceTypeIndex { Catalog = blobAsset });
#if UNITY_EDITOR
                Debug.Log("[PureDotsConfigBaker] ResourceTypeIndex component added", authoring);
#endif
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PureDotsConfigBaker] ResourceTypes catalog missing or empty", authoring.config);
#endif
            }

            var recipeCatalog = authoring.config.RecipeCatalog;
            if (recipeCatalog != null)
            {
#if UNITY_EDITOR
                Debug.Log($"[PureDotsConfigBaker] Recipe catalog families={recipeCatalog.Families?.Count ?? 0} recipes={recipeCatalog.Recipes?.Count ?? 0}", recipeCatalog);
#endif
                BuildRecipeSet(entity, recipeCatalog);
            }
            else
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PureDotsConfigBaker] RecipeCatalog asset missing", authoring.config);
#endif
            }
        }

        private void BuildRecipeSet(Entity entity, ResourceRecipeCatalog catalog)
        {
            var families = catalog.Families;
            var recipes = catalog.Recipes;

            int familyCount = families?.Count ?? 0;
            int recipeCount = recipes?.Count ?? 0;

            if (familyCount == 0 && recipeCount == 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[PureDotsConfigBaker] Recipe catalog contains no entries", catalog);
#endif
                return;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ResourceRecipeSetBlob>();

            var familiesBuilder = builder.Allocate(ref root.Families, familyCount);
            for (int i = 0; i < familyCount; i++)
            {
                ref var familyBlob = ref familiesBuilder[i];
                var definition = families![i];

                familyBlob.Id = ToFixedString64(definition.id);
                familyBlob.DisplayName = ToFixedString64(definition.displayName);
                familyBlob.RawResourceId = ToFixedString64(definition.rawResourceId);
                familyBlob.RefinedResourceId = ToFixedString64(definition.refinedResourceId);
                familyBlob.CompositeResourceId = ToFixedString64(definition.compositeResourceId);
                familyBlob.Description = ToFixedString128(definition.description);
            }

            var recipesBuilder = builder.Allocate(ref root.Recipes, recipeCount);
            for (int i = 0; i < recipeCount; i++)
            {
                ref var recipeBlob = ref recipesBuilder[i];
                var definition = recipes![i];

                recipeBlob.Id = ToFixedString64(definition.id);
                recipeBlob.Kind = definition.kind;
                recipeBlob.FacilityTag = ToFixedString32(definition.facilityTag);
                recipeBlob.OutputResourceId = ToFixedString64(definition.outputResourceId);
                recipeBlob.OutputAmount = math.max(1, definition.outputAmount);
                recipeBlob.ProcessSeconds = math.max(0f, definition.processSeconds);
                recipeBlob.Notes = ToFixedString128(definition.notes);

                var inputs = definition.inputs;
                var ingredientBuilder = builder.Allocate(ref recipeBlob.Ingredients, inputs?.Length ?? 0);
                if (inputs != null)
                {
                    for (int j = 0; j < inputs.Length; j++)
                    {
                        ref var ingredientBlob = ref ingredientBuilder[j];
                        ingredientBlob.ResourceId = ToFixedString64(inputs[j].resourceId);
                        ingredientBlob.Amount = math.max(1, inputs[j].amount);
                    }
                }
            }

            var blobAsset = builder.CreateBlobAssetReference<ResourceRecipeSetBlob>(Allocator.Persistent);
            builder.Dispose();

            AddComponent(entity, new ResourceRecipeSet
            {
                Value = blobAsset
            });
#if UNITY_EDITOR
            Debug.Log("[PureDotsConfigBaker] ResourceRecipeSet component added", catalog);
#endif
        }

        private static FixedString32Bytes ToFixedString32(string value)
        {
            FixedString32Bytes result = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Append(value.Trim());
            }

            return result;
        }

        private static FixedString64Bytes ToFixedString64(string value)
        {
            FixedString64Bytes result = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Append(value.Trim());
            }

            return result;
        }

        private static FixedString128Bytes ToFixedString128(string value)
        {
            FixedString128Bytes result = default;
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Append(value.Trim());
            }

            return result;
        }
    }
}
