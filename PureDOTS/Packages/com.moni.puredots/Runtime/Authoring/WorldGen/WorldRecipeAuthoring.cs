using PureDOTS.Runtime.WorldGen;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.WorldGen
{
    [DisallowMultipleComponent]
    public sealed class WorldRecipeAuthoring : MonoBehaviour
    {
        [Header("Recipe JSON")]
        [Tooltip("Primary recipe JSON source.")]
        public TextAsset recipeJsonAsset;

        [Tooltip("If set, this text overrides recipeJsonAsset.")]
        [TextArea(6, 30)]
        public string recipeJsonOverride;

        [Header("Optional definition binding")]
        [Tooltip("If recipe.definitionsHash is empty, this catalog is hashed and injected into the recipe before compilation.")]
        public WorldGenDefinitionsCatalogAsset definitionsCatalog;

        [Tooltip("Additional catalogs used for computing the injected definitionsHash.")]
        public WorldGenDefinitionsCatalogAsset[] additionalDefinitionCatalogs;
    }

    public sealed class WorldRecipeBaker : Baker<WorldRecipeAuthoring>
    {
        public override void Bake(WorldRecipeAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            var json = !string.IsNullOrWhiteSpace(authoring.recipeJsonOverride)
                ? authoring.recipeJsonOverride
                : authoring.recipeJsonAsset != null ? authoring.recipeJsonAsset.text : string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[WorldRecipeBaker] No recipe JSON provided; skipping WorldRecipe bake.", authoring);
                return;
            }

            if (!WorldRecipeIo.TryFromJson(json, out var recipe, out var error))
            {
                Debug.LogError($"[WorldRecipeBaker] Invalid recipe JSON: {error}", authoring);
                return;
            }

            if (string.IsNullOrWhiteSpace(recipe.definitionsHash) && (authoring.definitionsCatalog != null || (authoring.additionalDefinitionCatalogs != null && authoring.additionalDefinitionCatalogs.Length > 0)))
            {
                if (WorldGenDefinitionsCatalogAsset.TryComputeMergedHash(
                        authoring.definitionsCatalog,
                        authoring.additionalDefinitionCatalogs,
                        out var definitionsHash,
                        out var definitionsHashText,
                        out _))
                {
                    recipe.definitionsHash = string.IsNullOrWhiteSpace(definitionsHashText) ? definitionsHash.ToString() : definitionsHashText;
                }
            }

            if (!WorldRecipeCompiler.TryCompile(recipe, Allocator.Persistent, out var compiled, out error))
            {
                Debug.LogError($"[WorldRecipeBaker] Failed to compile recipe: {error}", authoring);
                return;
            }

            AddBlobAsset(ref compiled.Recipe, out _);
            AddComponent(entity, compiled);
        }
    }
}
