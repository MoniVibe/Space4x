using System.Collections.Generic;
using Space4X.Registry;

namespace Space4X.Editor
{
    /// <summary>
    /// Options for prefab generation.
    /// </summary>
    public class PrefabMakerOptions
    {
        public string CatalogPath { get; set; } = "Assets/Data/Catalogs";
        public bool PlaceholdersOnly { get; set; } = true;
        public bool OverwriteMissingSockets { get; set; } = false;
        public bool DryRun { get; set; } = false;
        public PresentationSet PresentationSet { get; set; } = PresentationSet.Minimal;
        public HullCategory HullCategoryFilter { get; set; } = HullCategory.Other; // Other means no filter
        
        // Targeted generation
        public List<string> SelectedIds { get; set; } = null; // If set, only generate prefabs with these IDs
        public PrefabTemplateCategory? SelectedCategory { get; set; } = null; // If set, only generate this category
    }
    
    /// <summary>
    /// Prefab template category for targeted generation.
    /// </summary>
    public enum PrefabTemplateCategory
    {
        Hulls,
        Modules,
        Stations,
        Resources,
        Products,
        Aggregates,
        FX,
        Individuals,
        Weapons,
        Projectiles,
        Turrets
    }

    /// <summary>
    /// Presentation set variant (Minimal = primitives only, Fancy = slightly nicer placeholders).
    /// </summary>
    public enum PresentationSet
    {
        Minimal,
        Fancy
    }
}

