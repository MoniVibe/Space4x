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
    /// Generates thin presentation token prefabs for turrets (turret shells with sockets).
    /// These are visual-only prefabs with no gameplay logic.
    /// </summary>
    public class TurretPresentationGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabGenerationResult result)
        {
            if (options.PlaceholdersOnly)
            {
                result.Warnings.Add("Turret presentation tokens are optional. Skipping generation in placeholders-only mode.");
                return false;
            }
            
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.turrets == null || catalog.turrets.Count == 0)
            {
                result.Warnings.Add("TurretCatalog has no turrets defined or not found.");
                return false;
            }
            
            EnsureDirectory($"{PrefabBasePath}/Turrets");
            bool anyChanged = false;
            
            foreach (var turretData in catalog.turrets)
            {
                if (string.IsNullOrWhiteSpace(turretData.id)) continue;
                
                var prefabPath = $"{PrefabBasePath}/Turrets/{turretData.id}.prefab";
                var go = LoadOrCreatePrefab(prefabPath, turretData.id, out bool isNew);
                
                var turretId = go.GetComponent<TurretIdAuthoring>();
                if (turretId == null) turretId = go.AddComponent<TurretIdAuthoring>();
                turretId.Id = turretData.id;
                
                if (go.GetComponent<StyleTokensAuthoring>() == null)
                    go.AddComponent<StyleTokensAuthoring>();
                
                // Ensure visual child
                if (go.transform.Find("Visual") == null)
                {
                    var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    visual.name = "Visual";
                    visual.transform.SetParent(go.transform);
                    visual.transform.localPosition = Vector3.zero;
                    visual.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
                    Object.DestroyImmediate(visual.GetComponent<Collider>());
                }
                
                // Ensure socket for muzzle binding
                var socketName = turretData.socketName ?? "Socket_Muzzle";
                var socketTransform = go.transform.Find(socketName);
                if (socketTransform == null)
                {
                    var socket = new GameObject(socketName);
                    socket.transform.SetParent(go.transform);
                    socket.transform.localPosition = new Vector3(0, 0.2f, 0);
                }
                
                SavePrefab(go, prefabPath, isNew, result);
                anyChanged = true;
            }
            
            return anyChanged;
        }
        
        public override void Validate(PrefabMaker.ValidationReport report)
        {
            // Validation for turret presentation tokens
            // (Cross-reference validation is handled in TemplateValidator)
        }
        
        private Space4X.Authoring.TurretCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var catalogPrefabPath = $"{catalogPath}/TurretCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(catalogPrefabPath);
            return prefab != null ? prefab.GetComponent<Space4X.Authoring.TurretCatalogAuthoring>() : null;
        }
    }
    
    /// <summary>
    /// Authoring component for turret ID (presentation token only).
    /// </summary>
    public class TurretIdAuthoring : MonoBehaviour
    {
        public string Id;
    }
}

