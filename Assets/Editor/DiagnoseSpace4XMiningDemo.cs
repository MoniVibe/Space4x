using UnityEngine;
using UnityEditor;
using Space4X.Registry;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Spatial;
using System;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Diagnoses Space4X_MiningDemo GameObject without freezing the editor.
    /// Uses direct component access instead of Unity MCP get_components.
    /// </summary>
    public static class DiagnoseSpace4XMiningDemo
    {
        [MenuItem("Tools/Space4X/Diagnose: Space4X_MiningDemo (Safe)")]
        public static void DiagnoseSafe()
        {
            try
            {
                Debug.Log("=== Safe Diagnosis of Space4X_MiningDemo ===\n");

                var space4xRoot = GameObject.Find("Space4X_MiningDemo");
                if (space4xRoot == null)
                {
                    Debug.LogError("✗ Space4X_MiningDemo GameObject not found!");
                    return;
                }

                Debug.Log($"✓ Space4X_MiningDemo found at position: {space4xRoot.transform.position}");
                Debug.Log($"  Active: {space4xRoot.activeSelf}, ActiveInHierarchy: {space4xRoot.activeInHierarchy}");
                Debug.Log($"  Layer: {space4xRoot.layer}, Tag: {space4xRoot.tag}");

                // Check components without using get_components MCP tool
                var components = space4xRoot.GetComponents<Component>();
                Debug.Log($"\nComponents found: {components.Length}");
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        Debug.LogWarning("  ⚠ Null component detected!");
                        continue;
                    }
                    Debug.Log($"  - {comp.GetType().Name} (InstanceID: {comp.GetInstanceID()})");
                }

                // Check specific components
                var authoring = space4xRoot.GetComponent<Space4XMiningDemoAuthoring>();
                if (authoring == null)
                {
                    Debug.LogError("✗ Space4XMiningDemoAuthoring component missing!");
                }
                else
                {
                    Debug.Log("\n✓ Space4XMiningDemoAuthoring found");
                    Debug.Log($"  Carriers: {authoring.Carriers?.Length ?? 0}");
                    Debug.Log($"  MiningVessels: {authoring.MiningVessels?.Length ?? 0}");
                    Debug.Log($"  Asteroids: {authoring.Asteroids?.Length ?? 0}");
                    
                    // Check for potential issues
                    if (authoring.Carriers != null && authoring.Carriers.Length > 100)
                    {
                        Debug.LogWarning($"  ⚠ Large carriers array: {authoring.Carriers.Length} entries");
                    }
                    if (authoring.MiningVessels != null && authoring.MiningVessels.Length > 100)
                    {
                        Debug.LogWarning($"  ⚠ Large miningVessels array: {authoring.MiningVessels.Length} entries");
                    }
                    if (authoring.Asteroids != null && authoring.Asteroids.Length > 100)
                    {
                        Debug.LogWarning($"  ⚠ Large asteroids array: {authoring.Asteroids.Length} entries");
                    }
                }

                var configAuthoring = space4xRoot.GetComponent<PureDotsConfigAuthoring>();
                if (configAuthoring == null)
                {
                    Debug.LogWarning("⚠ PureDotsConfigAuthoring component missing!");
                }
                else
                {
                    Debug.Log("\n✓ PureDotsConfigAuthoring found");
                    if (configAuthoring.config == null)
                    {
                        Debug.LogWarning("  ⚠ Runtime config not assigned!");
                    }
                    else
                    {
                        Debug.Log($"  ✓ Runtime config assigned: {configAuthoring.config.name}");
                    }
                }

                var spatialAuthoring = space4xRoot.GetComponent<SpatialPartitionAuthoring>();
                if (spatialAuthoring == null)
                {
                    Debug.LogWarning("⚠ SpatialPartitionAuthoring component missing!");
                }
                else
                {
                    Debug.Log("\n✓ SpatialPartitionAuthoring found");
                }

                // Check parent/children
                Debug.Log($"\nHierarchy:");
                Debug.Log($"  Parent: {(space4xRoot.transform.parent != null ? space4xRoot.transform.parent.name : "None")}");
                Debug.Log($"  Children: {space4xRoot.transform.childCount}");
                if (space4xRoot.transform.childCount > 0)
                {
                    for (int i = 0; i < space4xRoot.transform.childCount; i++)
                    {
                        var child = space4xRoot.transform.GetChild(i);
                        Debug.Log($"    - {child.name}");
                    }
                }

                // Check if it's in a SubScene
                var scene = space4xRoot.scene;
                Debug.Log($"\nScene: {scene.name}, Path: {scene.path}");
                if (string.IsNullOrEmpty(scene.path))
                {
                    Debug.LogWarning("  ⚠ GameObject is not in a saved scene (might be in SubScene)");
                }

                Debug.Log("\n=== Diagnosis Complete ===");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Diagnostic error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}




























