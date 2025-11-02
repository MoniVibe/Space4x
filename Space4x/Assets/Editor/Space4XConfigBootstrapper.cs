using System.IO;
using PureDOTS.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;
using Unity.Scenes.Editor;

public static class Space4XConfigBootstrapper
{
    private const string ConfigFolder = "Assets/Space4X/Config";
    private const string RuntimeConfigPath = ConfigFolder + "/PureDotsRuntimeConfig.asset";
    private const string ResourceCatalogPath = ConfigFolder + "/PureDotsResourceTypes.asset";
    private const string SpatialProfilePath = ConfigFolder + "/DefaultSpatialPartitionProfile.asset";

    [MenuItem("Coplay/Space4X/Ensure PureDOTS Config Assets")]
    public static void EnsureAssets()
    {
        Directory.CreateDirectory(ConfigFolder);

        var resourceCatalog = LoadOrCreate<ResourceTypeCatalog>(ResourceCatalogPath, "PureDotsResourceTypes");
        EnsureResourceCatalogContents(resourceCatalog);

        var runtimeConfig = LoadOrCreate<PureDotsRuntimeConfig>(RuntimeConfigPath, "PureDotsRuntimeConfig");
        EnsureRuntimeConfigContents(runtimeConfig, resourceCatalog);

        var spatialProfile = LoadOrCreate<SpatialPartitionProfile>(SpatialProfilePath, "DefaultSpatialPartitionProfile");
        EnsureSpatialProfileContents(spatialProfile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("Space4X PureDOTS config assets ensured.");
    }

    [MenuItem("Coplay/Space4X/Configure SubScene Anchor")]
    public static void ConfigureSubSceneAnchor()
    {
        const string anchorName = "Space4X Registry SubScene";
        const string subScenePath = "Assets/Scenes/Demo/Space4XRegistryDemo_SubScene.unity";

        var anchor = GameObject.Find(anchorName);
        if (anchor == null)
        {
            anchor = new GameObject(anchorName);
        }

        var subScene = anchor.GetComponent<SubScene>();
        if (subScene == null)
        {
            subScene = anchor.AddComponent<SubScene>();
        }

        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(subScenePath);
        if (sceneAsset == null)
        {
            Debug.LogError($"Unable to locate subscene asset at '{subScenePath}'.");
            return;
        }

        subScene.SceneAsset = sceneAsset;
        subScene.AutoLoadScene = true;
        EditorUtility.SetDirty(subScene);

#if UNITY_EDITOR
        if (!subScene.IsLoaded)
        {
            SubSceneUtility.EditScene(subScene);
        }
#endif
    }

    private static T LoadOrCreate<T>(string path, string assetName) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        asset.name = assetName;
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureResourceCatalogContents(ResourceTypeCatalog catalog)
    {
        if (catalog == null)
        {
            return;
        }

        var serialized = new SerializedObject(catalog);
        var entriesProp = serialized.FindProperty("entries");
        if (entriesProp == null)
        {
            return;
        }

        entriesProp.ClearArray();

        AddResourceEntry(entriesProp, 0, "wood", new Color(0.7411765f, 0.5137255f, 0.25490198f, 1f));
        AddResourceEntry(entriesProp, 1, "stone", new Color(0.52156866f, 0.5372549f, 0.5568628f, 1f));

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void AddResourceEntry(SerializedProperty entriesProp, int index, string id, Color color)
    {
        entriesProp.InsertArrayElementAtIndex(index);
        var element = entriesProp.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("id").stringValue = id;
        element.FindPropertyRelative("displayColor").colorValue = color;
    }

    private static void EnsureRuntimeConfigContents(PureDotsRuntimeConfig runtimeConfig, ResourceTypeCatalog resourceTypes)
    {
        if (runtimeConfig == null)
        {
            return;
        }

        var serialized = new SerializedObject(runtimeConfig);

        var timeProp = serialized.FindProperty("_time");
        if (timeProp != null)
        {
            timeProp.FindPropertyRelative("fixedDeltaTime").floatValue = 1f / 60f;
            timeProp.FindPropertyRelative("defaultSpeedMultiplier").floatValue = 1f;
            timeProp.FindPropertyRelative("pauseOnStart").boolValue = false;
        }

        var historyProp = serialized.FindProperty("_history");
        if (historyProp != null)
        {
            historyProp.FindPropertyRelative("defaultStrideSeconds").floatValue = 5f;
            historyProp.FindPropertyRelative("criticalStrideSeconds").floatValue = 1f;
            historyProp.FindPropertyRelative("lowVisibilityStrideSeconds").floatValue = 30f;
            historyProp.FindPropertyRelative("defaultHorizonSeconds").floatValue = 60f;
            historyProp.FindPropertyRelative("midHorizonSeconds").floatValue = 300f;
            historyProp.FindPropertyRelative("extendedHorizonSeconds").floatValue = 600f;
            historyProp.FindPropertyRelative("checkpointIntervalSeconds").floatValue = 20f;
            historyProp.FindPropertyRelative("eventLogRetentionSeconds").floatValue = 30f;
            historyProp.FindPropertyRelative("memoryBudgetMegabytes").floatValue = 1024f;
            historyProp.FindPropertyRelative("defaultTicksPerSecond").floatValue = 90f;
            historyProp.FindPropertyRelative("minTicksPerSecond").floatValue = 60f;
            historyProp.FindPropertyRelative("maxTicksPerSecond").floatValue = 120f;
            historyProp.FindPropertyRelative("strideScale").floatValue = 1f;
        }

        var resourceProp = serialized.FindProperty("_resourceTypes");
        if (resourceProp != null)
        {
            resourceProp.objectReferenceValue = resourceTypes;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runtimeConfig);
    }

    private static void EnsureSpatialProfileContents(SpatialPartitionProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        var serialized = new SerializedObject(profile);
        serialized.FindProperty("_center").vector3Value = Vector3.zero;
        serialized.FindProperty("_extent").vector3Value = new Vector3(512f, 64f, 512f);
        serialized.FindProperty("_cellSize").floatValue = 4f;
        serialized.FindProperty("_minCellSize").floatValue = 1f;
        serialized.FindProperty("_overrideCellCounts").boolValue = false;
        serialized.FindProperty("_manualCellCounts").vector3IntValue = new Vector3Int(128, 1, 128);
        serialized.FindProperty("_lockYAxisToOne").boolValue = true;
        serialized.FindProperty("_providerType").enumValueIndex = (int)SpatialProviderType.HashedGrid;
        serialized.FindProperty("_hashSeed").uintValue = 0;
        serialized.FindProperty("_drawGizmo").boolValue = true;
        serialized.FindProperty("_gizmoColor").colorValue = new Color(0.1f, 0.8f, 1f, 0.35f);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(profile);
    }
}
