using UnityEngine;
using UnityEditor;
using PureDOTS.Authoring;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Editor
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Quick fix to add MiningVisualManifest GameObject to the scene.
    /// </summary>
    public static class FixMiningVisualManifest
    {
        [MenuItem("Tools/Space4X/Fix: Add MiningVisualManifest")]
        public static void AddMiningVisualManifest()
        {
            var visualManifest = GameObject.Find("MiningVisualManifest");
            if (visualManifest == null)
            {
                visualManifest = new GameObject("MiningVisualManifest");
                visualManifest.AddComponent<MiningVisualManifestAuthoring>();
                Undo.RegisterCreatedObjectUndo(visualManifest, "Create MiningVisualManifest");
                UnityDebug.Log("✓ Created MiningVisualManifest GameObject with MiningVisualManifestAuthoring component");
            }
            else
            {
                var authoring = visualManifest.GetComponent<MiningVisualManifestAuthoring>();
                if (authoring == null)
                {
                    Undo.RecordObject(visualManifest, "Add MiningVisualManifestAuthoring");
                    visualManifest.AddComponent<MiningVisualManifestAuthoring>();
                    UnityDebug.Log("✓ Added MiningVisualManifestAuthoring component to existing MiningVisualManifest GameObject");
                }
                else
                {
                    UnityDebug.Log("✓ MiningVisualManifest already exists with MiningVisualManifestAuthoring component");
                }
            }
            
            EditorUtility.SetDirty(visualManifest);
        }
    }
}




























