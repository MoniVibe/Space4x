using System.Collections.Generic;
using Space4X.Authoring;
using Space4X.Editor.PrefabMakerTool.Models;
using Space4X.Registry;
using PrefabGenerationResult = Space4X.Editor.PrefabMaker.GenerationResult;
using UnityEditor;
using UnityEngine;

namespace Space4X.Editor.Generators
{
    /// <summary>
    /// Generates thin presentation token prefabs for weapons (muzzle flashes, etc.).
    /// These are visual-only prefabs with no gameplay logic.
    /// </summary>
    public class WeaponPresentationGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabGenerationResult result)
        {
            if (options.PlaceholdersOnly)
            {
                result.Warnings.Add("Weapon presentation tokens are optional. Skipping generation in placeholders-only mode.");
                return false;
            }
            
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.weapons == null || catalog.weapons.Count == 0)
            {
                result.Warnings.Add("WeaponCatalog has no weapons defined or not found.");
                return false;
            }
            
            EnsureDirectory($"{PrefabBasePath}/Weapons");
            bool anyChanged = false;
            
            foreach (var weaponData in catalog.weapons)
            {
                if (string.IsNullOrWhiteSpace(weaponData.id)) continue;
                
                var prefabPath = $"{PrefabBasePath}/Weapons/{weaponData.id}_Muzzle.prefab";
                var go = LoadOrCreatePrefab(prefabPath, $"{weaponData.id}_Muzzle", out bool isNew);
                
                // Ensure components
                var weaponId = go.GetComponent<WeaponIdAuthoring>();
                if (weaponId == null) weaponId = go.AddComponent<WeaponIdAuthoring>();
                weaponId.Id = weaponData.id;
                
                if (go.GetComponent<StyleTokensAuthoring>() == null)
                    go.AddComponent<StyleTokensAuthoring>();
                
                // Ensure visual child
                Transform visualTransform = go.transform.Find("Visual");
                if (visualTransform == null)
                {
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    visual.name = "Visual";
                    visual.transform.SetParent(go.transform);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = Vector3.one * 0.1f;
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                }
                
                SavePrefab(go, prefabPath, isNew, result);
                anyChanged = true;
            }
            
            return anyChanged;
        }
        
        public override void Validate(PrefabMaker.ValidationReport report)
        {
            // Validation for weapon presentation tokens
            // (Cross-reference validation is handled in TemplateValidator)
        }
        
        private Space4X.Authoring.WeaponCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var catalogPrefabPath = $"{catalogPath}/WeaponCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            return prefab != null ? prefab.GetComponent<Space4X.Authoring.WeaponCatalogAuthoring>() : null;
        }
    }
    
    /// <summary>
    /// Authoring component for weapon ID (presentation token only).
    /// </summary>
    public class WeaponIdAuthoring : MonoBehaviour
    {
        public string Id;
    }
}

