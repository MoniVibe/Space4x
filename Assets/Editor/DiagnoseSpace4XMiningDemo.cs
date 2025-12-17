using UnityEngine;
using UnityEditor;
using Space4X.Registry;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Spatial;
using System;
using UnityDebug = UnityEngine.Debug;

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
                UnityDebug.Log("=== Safe Diagnosis of Space4X_MiningDemo ===\n");

                var space4xRoot = GameObject.Find("Space4X_MiningDemo");
                if (space4xRoot == null)
                {
                    UnityDebug.LogError("✗ Space4X_MiningDemo GameObject not found!");
                    return;
                }

                UnityDebug.Log($"✓ Space4X_MiningDemo found at position: {space4xRoot.transform.position}");
                UnityDebug.Log($"  Active: {space4xRoot.activeSelf}, ActiveInHierarchy: {space4xRoot.activeInHierarchy}");
                UnityDebug.Log($"  Layer: {space4xRoot.layer}, Tag: {space4xRoot.tag}");

                // Check components without using get_components MCP tool
                var components = space4xRoot.GetComponents<Component>();
                UnityDebug.Log($"\nComponents found: {components.Length}");
                foreach (var comp in components)
                {
                    if (comp == null)
                    {
                        UnityDebug.LogWarning("  ⚠ Null component detected!");
                        continue;
                    }
                    UnityDebug.Log($"  - {comp.GetType().Name} (InstanceID: {comp.GetInstanceID()})");
                }

                // Check specific components
                var authoring = space4xRoot.GetComponent<Space4XMiningDemoAuthoring>();
                if (authoring == null)
                {
                    UnityDebug.LogError("✗ Space4XMiningDemoAuthoring component missing!");
                }
                else
                {
                    UnityDebug.Log("\n✓ Space4XMiningDemoAuthoring found");
                    UnityDebug.Log($"  Carriers: {authoring.Carriers?.Length ?? 0}");
                    UnityDebug.Log($"  MiningVessels: {authoring.MiningVessels?.Length ?? 0}");
                    UnityDebug.Log($"  Asteroids: {authoring.Asteroids?.Length ?? 0}");
                    
                    // Check for potential issues
                    if (authoring.Carriers != null && authoring.Carriers.Length > 100)
                    {
                        UnityDebug.LogWarning($"  ⚠ Large carriers array: {authoring.Carriers.Length} entries");
                    }
                    if (authoring.MiningVessels != null && authoring.MiningVessels.Length > 100)
                    {
                        UnityDebug.LogWarning($"  ⚠ Large miningVessels array: {authoring.MiningVessels.Length} entries");
                    }
                    if (authoring.Asteroids != null && authoring.Asteroids.Length > 100)
                    {
                        UnityDebug.LogWarning($"  ⚠ Large asteroids array: {authoring.Asteroids.Length} entries");
                    }
                }

                var configAuthoring = space4xRoot.GetComponent<PureDotsConfigAuthoring>();
                if (configAuthoring == null)
                {
                    UnityDebug.LogWarning("⚠ PureDotsConfigAuthoring component missing!");
                }
                else
                {
                    UnityDebug.Log("\n✓ PureDotsConfigAuthoring found");
                    if (configAuthoring.config == null)
                    {
                        UnityDebug.LogWarning("  ⚠ Runtime config not assigned!");
                    }
                    else
                    {
                        UnityDebug.Log($"  ✓ Runtime config assigned: {configAuthoring.config.name}");
                    }
                }

                var spatialAuthoring = space4xRoot.GetComponent<SpatialPartitionAuthoring>();
                if (spatialAuthoring == null)
                {
                    UnityDebug.LogWarning("⚠ SpatialPartitionAuthoring component missing!");
                }
                else
                {
                    UnityDebug.Log("\n✓ SpatialPartitionAuthoring found");
                }

                // Check parent/children
                UnityDebug.Log($"\nHierarchy:");
                UnityDebug.Log($"  Parent: {(space4xRoot.transform.parent != null ? space4xRoot.transform.parent.name : "None")}");
                UnityDebug.Log($"  Children: {space4xRoot.transform.childCount}");
                if (space4xRoot.transform.childCount > 0)
                {
                    for (int i = 0; i < space4xRoot.transform.childCount; i++)
                    {
                        var child = space4xRoot.transform.GetChild(i);
                        UnityDebug.Log($"    - {child.name}");
                    }
                }

                // Check if it's in a SubScene
                var scene = space4xRoot.scene;
                UnityDebug.Log($"\nScene: {scene.name}, Path: {scene.path}");
                if (string.IsNullOrEmpty(scene.path))
                {
                    UnityDebug.LogWarning("  ⚠ GameObject is not in a saved scene (might be in SubScene)");
                }

                UnityDebug.Log("\n=== Diagnosis Complete ===");
            }
            catch (Exception ex)
            {
                UnityDebug.LogError($"Diagnostic error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}




























