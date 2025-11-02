using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PureDOTS.Showcase.Editor
{
    public static class DualMiningSceneSetup
    {
        private const string RootObjectName = "ShowcaseRoot";
        private const string GodgameLoopName = "GodgameLoop";
        private const string Space4XLoopName = "Space4XLoop";

        private static readonly Vector3 GodgameOffset = new(-200f, 0f, 0f);
        private static readonly Vector3 Space4XOffset = new(200f, 0f, 0f);

        private const string ConfigAssetPath = "Assets/PureDOTS/Config/PureDotsRuntimeConfig.asset";

        private const string PureDotsConfigAuthoringTypeName = "PureDotsConfigAuthoring";
        private const string GodgameAuthoringTypeName = "Godgame.Authoring.GodgameSampleRegistryAuthoring";
        private const string Space4XAuthoringTypeName = "Space4X.Registry.Space4XSampleRegistryAuthoring";
        private const string SpatialPartitionAuthoringTypeName = "SpatialPartitionAuthoring";

        [MenuItem("PureDOTS/Showcase/Reset Dual Mining Loops", priority = 0)]
        public static void ResetDualMiningLoops()
        {
            if (!EnsureSceneIsLoaded())
            {
                return;
            }

            var configAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ConfigAssetPath);
            if (configAsset == null)
            {
                Debug.LogError($"DualMiningSceneSetup: Could not locate config asset at '{ConfigAssetPath}'.");
                return;
            }

            var root = EnsureRoot();
            if (root == null)
            {
                return;
            }

            var godgame = EnsureChild(root.transform, GodgameLoopName);
            var space4X = EnsureChild(root.transform, Space4XLoopName);

            if (godgame == null || space4X == null)
            {
                return;
            }

            ConfigureLoop(godgame, GodgameOffset, configAsset, GodgameAuthoringTypeName, Space4XAuthoringTypeName);
            ConfigureLoop(space4X, Space4XOffset, configAsset, Space4XAuthoringTypeName, GodgameAuthoringTypeName);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static bool EnsureSceneIsLoaded()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogError("DualMiningSceneSetup: No active scene is loaded.");
                return false;
            }

            if (!scene.isLoaded)
            {
                Debug.LogError($"DualMiningSceneSetup: Active scene '{scene.path}' is not loaded.");
                return false;
            }

            return true;
        }

        private static GameObject EnsureRoot()
        {
            var scene = SceneManager.GetActiveScene();
            var root = scene.GetRootGameObjects().FirstOrDefault(go => go.name == RootObjectName);
            if (root == null)
            {
                root = new GameObject(RootObjectName);
                Undo.RegisterCreatedObjectUndo(root, "Create Showcase Root");
            }

            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            return root;
        }

        private static GameObject EnsureChild(Transform parent, string name)
        {
            var child = parent.Cast<Transform>().FirstOrDefault(t => t.name == name)?.gameObject;
            if (child == null)
            {
                child = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
                child.transform.SetParent(parent, false);
            }
            else if (child.transform.parent != parent)
            {
                Undo.SetTransformParent(child.transform, parent, $"Reparent {name}");
            }

            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            return child;
        }

        private static void ConfigureLoop(GameObject loopObject, Vector3 offset, UnityEngine.Object configAsset, string primaryAuthoringTypeName, string oppositeAuthoringTypeName)
        {
            Undo.RecordObject(loopObject.transform, "Position Loop");
            loopObject.transform.localPosition = offset;

            var configComponent = EnsureComponent(loopObject, PureDotsConfigAuthoringTypeName);
            if (configComponent != null)
            {
                SetSerializedReference(configComponent, "config", configAsset);
            }

            var primaryComponent = EnsureComponent(loopObject, primaryAuthoringTypeName);
            if (primaryComponent == null)
            {
                Debug.LogError($"DualMiningSceneSetup: Could not add required component '{primaryAuthoringTypeName}' to '{loopObject.name}'.");
            }

            RemoveComponent(loopObject, oppositeAuthoringTypeName);

            if (primaryAuthoringTypeName == Space4XAuthoringTypeName)
            {
                EnsureComponent(loopObject, SpatialPartitionAuthoringTypeName);
            }
        }

        private static Component EnsureComponent(GameObject target, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var type = FindType(typeName);
            if (type == null)
            {
                Debug.LogError($"DualMiningSceneSetup: Unable to locate type '{typeName}'.");
                return null;
            }

            var component = target.GetComponent(type);
            if (component != null)
            {
                return component;
            }

            Undo.RecordObject(target, $"Add {type.Name}");
            return Undo.AddComponent(target, type);
        }

        private static void RemoveComponent(GameObject target, string typeName)
        {
            var type = FindType(typeName);
            if (type == null)
            {
                return;
            }

            var component = target.GetComponent(type);
            if (component != null)
            {
                Undo.DestroyObjectImmediate(component);
            }
        }

        private static void SetSerializedReference(Component component, string propertyName, UnityEngine.Object value)
        {
            if (component == null || string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            var so = new SerializedObject(component);
            var property = so.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Type FindType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.Ordinal) ||
                                     string.Equals(t.Name, typeName, StringComparison.Ordinal));
        }
    }
}
