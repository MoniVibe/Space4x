using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Resource;
using UnityEngine;

namespace PureDOTS.Authoring
{
    /// <summary>
    /// ScriptableObject catalog describing resource families and the processing
    /// recipes that convert raw materials into refined and composite outputs.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResourceRecipeCatalog",
        menuName = "PureDOTS/Resource Recipe Catalog",
        order = 0)]
    public sealed class ResourceRecipeCatalog : ScriptableObject
    {
        [SerializeField]
        private ResourceFamilyDefinition[] _families = Array.Empty<ResourceFamilyDefinition>();

        [SerializeField]
        private ResourceRecipeDefinition[] _recipes = Array.Empty<ResourceRecipeDefinition>();

        public IReadOnlyList<ResourceFamilyDefinition> Families => _families;

        public IReadOnlyList<ResourceRecipeDefinition> Recipes => _recipes;
    }

    [Serializable]
    public struct ResourceFamilyDefinition
    {
        [Tooltip("Identifier used to reference the family (e.g., metals, organics).")]
        public string id;

        [Tooltip("Display name for UI tooling.")]
        public string displayName;

        [Tooltip("Resource id for the primary raw input mined in this family.")]
        public string rawResourceId;

        [Tooltip("Resource id for the refined output produced from the raw input.")]
        public string refinedResourceId;

        [Tooltip("Resource id for the advanced composite representative of this family.")]
        public string compositeResourceId;

        [TextArea]
        [Tooltip("Optional notes describing the role of this family in the economy.")]
        public string description;
    }

    [Serializable]
    public struct ResourceRecipeDefinition
    {
        [Tooltip("Unique recipe identifier (e.g., refine_iron_ingot).")]
        public string id;

        [Tooltip("High level classification of the recipe (refinement, composite, byproduct).")]
        public ResourceRecipeKind kind;

        [Tooltip("Logical facility tag or processing lane required for this recipe (e.g., refinery, fab).")]
        public string facilityTag;

        [Tooltip("Resource id produced by this recipe.")]
        public string outputResourceId;

        [Tooltip("Number of units produced per completion.")]
        public int outputAmount;

        [Tooltip("Seconds required to complete the recipe once.")]
        public float processSeconds;

        [Tooltip("List of ingredients consumed each time the recipe executes.")]
        public ResourceIngredientDefinition[] inputs;

        [TextArea]
        [Tooltip("Optional notes for designers to capture intent or balancing guidance.")]
        public string notes;
    }

    [Serializable]
    public struct ResourceIngredientDefinition
    {
        [Tooltip("Resource identifier for the ingredient consumed.")]
        public string resourceId;

        [Tooltip("Amount consumed per recipe execution.")]
        public int amount;
    }
}

