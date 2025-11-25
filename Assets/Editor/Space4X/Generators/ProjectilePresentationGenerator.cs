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
    /// Generates thin presentation token prefabs for projectiles (tracers, impacts, etc.).
    /// These are visual-only prefabs with no gameplay logic.
    /// </summary>
    public class ProjectilePresentationGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabGenerationResult result)
        {
            if (options.PlaceholdersOnly)
            {
                result.Warnings.Add("Projectile presentation tokens are optional. Skipping generation in placeholders-only mode.");
                return false;
            }
            
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.projectiles == null || catalog.projectiles.Count == 0)
            {
                result.Warnings.Add("ProjectileCatalog has no projectiles defined or not found.");
                return false;
            }
            
            EnsureDirectory($"{PrefabBasePath}/Projectiles");
            bool anyChanged = false;
            
            foreach (var projData in catalog.projectiles)
            {
                if (string.IsNullOrWhiteSpace(projData.id)) continue;
                
                // Generate tracer prefab
                var tracerPath = $"{PrefabBasePath}/Projectiles/{projData.id}_Tracer.prefab";
                var tracerGo = LoadOrCreatePrefab(tracerPath, $"{projData.id}_Tracer", out bool tracerNew);
                
                var tracerId = tracerGo.GetComponent<ProjectileIdAuthoring>();
                if (tracerId == null) tracerId = tracerGo.AddComponent<ProjectileIdAuthoring>();
                tracerId.Id = projData.id;
                
                if (tracerGo.GetComponent<StyleTokensAuthoring>() == null)
                    tracerGo.AddComponent<StyleTokensAuthoring>();
                
                if (tracerGo.transform.Find("Visual") == null)
                {
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    visual.name = "Visual";
                    visual.transform.SetParent(tracerGo.transform);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = new Vector3(0.05f, 0.5f, 0.05f);
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                }
                
                SavePrefab(tracerGo, tracerPath, tracerNew, result);
                anyChanged = true;
                
                // Generate impact prefab
                var impactPath = $"{PrefabBasePath}/Projectiles/{projData.id}_Impact.prefab";
                var impactGo = LoadOrCreatePrefab(impactPath, $"{projData.id}_Impact", out bool impactNew);
                
                var impactId = impactGo.GetComponent<ProjectileIdAuthoring>();
                if (impactId == null) impactId = impactGo.AddComponent<ProjectileIdAuthoring>();
                impactId.Id = projData.id;
                
                if (impactGo.GetComponent<StyleTokensAuthoring>() == null)
                    impactGo.AddComponent<StyleTokensAuthoring>();
                
                if (impactGo.transform.Find("Visual") == null)
                {
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    visual.name = "Visual";
                    visual.transform.SetParent(impactGo.transform);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = Vector3.one * 0.2f;
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                }
                
                SavePrefab(impactGo, impactPath, impactNew, result);
                anyChanged = true;
            }
            
            return anyChanged;
        }
        
        public override void Validate(PrefabMaker.ValidationReport report)
        {
            // Validation for projectile presentation tokens
            // (Cross-reference validation is handled in TemplateValidator)
        }
        
        private Space4X.Authoring.ProjectileCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var catalogPrefabPath = $"{catalogPath}/ProjectileCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            return prefab != null ? prefab.GetComponent<Space4X.Authoring.ProjectileCatalogAuthoring>() : null;
        }
    }
    
    /// <summary>
    /// Authoring component for projectile ID (presentation token only).
    /// </summary>
    public class ProjectileIdAuthoring : MonoBehaviour
    {
        public string Id;
    }
}

