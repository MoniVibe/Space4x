using System.IO;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Resource;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;

public static class Space4XConfigBootstrapper
{
    private const string ConfigFolder = "Assets/Space4X/Config";
    private const string RuntimeConfigPath = ConfigFolder + "/PureDotsRuntimeConfig.asset";
    private const string ResourceCatalogPath = ConfigFolder + "/PureDotsResourceTypes.asset";
    private const string RecipeCatalogPath = ConfigFolder + "/ResourceRecipeCatalog.asset";
    private const string SpatialProfilePath = ConfigFolder + "/DefaultSpatialPartitionProfile.asset";

    [MenuItem("Coplay/Space4X/Ensure PureDOTS Config Assets")]
    public static void EnsureAssets()
    {
        // Ensure folder exists using AssetDatabase
        if (!AssetDatabase.IsValidFolder(ConfigFolder))
        {
            string parentPath = "Assets/Space4X";
            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                AssetDatabase.CreateFolder("Assets", "Space4X");
            }
            AssetDatabase.CreateFolder(parentPath, "Config");
            AssetDatabase.Refresh();
        }

        var resourceCatalog = LoadOrCreate<ResourceTypeCatalog>(ResourceCatalogPath, "PureDotsResourceTypes");
        EnsureResourceCatalogContents(resourceCatalog);

        var recipeCatalog = LoadOrCreate<ResourceRecipeCatalog>(RecipeCatalogPath, "ResourceRecipeCatalog");
        EnsureRecipeCatalogContents(recipeCatalog);

        var runtimeConfig = LoadOrCreate<PureDotsRuntimeConfig>(RuntimeConfigPath, "PureDotsRuntimeConfig");
        EnsureRuntimeConfigContents(runtimeConfig, resourceCatalog, recipeCatalog);

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

        // Space4X resource types for dual mining demo
        AddResourceEntry(entriesProp, 0, "iron_ore", new Color(0.522f, 0.337f, 0.278f, 1f));
        AddResourceEntry(entriesProp, 1, "iron_ingot", new Color(0.705f, 0.705f, 0.72f, 1f));
        AddResourceEntry(entriesProp, 2, "biomass", new Color(0.37f, 0.6f, 0.29f, 1f));
        AddResourceEntry(entriesProp, 3, "nutrients", new Color(0.7f, 0.87f, 0.45f, 1f));
        AddResourceEntry(entriesProp, 4, "hydrocarbon_ice", new Color(0.3f, 0.48f, 0.68f, 1f));
        AddResourceEntry(entriesProp, 5, "refined_fuels", new Color(0.9f, 0.58f, 0.2f, 1f));
        AddResourceEntry(entriesProp, 6, "polymers", new Color(0.96f, 0.37f, 0.55f, 1f));
        AddResourceEntry(entriesProp, 7, "rare_earths", new Color(0.56f, 0.46f, 0.74f, 1f));
        AddResourceEntry(entriesProp, 8, "conductors", new Color(0.96f, 0.85f, 0.45f, 1f));
        AddResourceEntry(entriesProp, 9, "biopolymers", new Color(0.62f, 0.86f, 0.64f, 1f));

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

    private static void EnsureRuntimeConfigContents(PureDotsRuntimeConfig runtimeConfig, ResourceTypeCatalog resourceTypes, ResourceRecipeCatalog recipeCatalog)
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

        var recipeProp = serialized.FindProperty("_recipeCatalog");
        if (recipeProp != null)
        {
            recipeProp.objectReferenceValue = recipeCatalog;
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(runtimeConfig);
    }

    private static void EnsureRecipeCatalogContents(ResourceRecipeCatalog catalog)
    {
        if (catalog == null)
        {
            return;
        }

        var serialized = new SerializedObject(catalog);
        
        // Setup families
        var familiesProp = serialized.FindProperty("_families");
        if (familiesProp != null)
        {
            familiesProp.ClearArray();
            
            AddFamily(familiesProp, 0, "metals", "Metals", "iron_ore", "iron_ingot", "", "Baseline structural materials forged from iron.");
            AddFamily(familiesProp, 1, "organics", "Organics", "biomass", "nutrients", "biopolymers", "Biological inputs refined into life-support consumables.");
            AddFamily(familiesProp, 2, "petrochemicals", "Petrochemicals", "hydrocarbon_ice", "refined_fuels", "polymers", "Hydrocarbon streams cracked into fuels and versatile polymer chains.");
            AddFamily(familiesProp, 3, "electronics", "Electronics", "rare_earths", "conductors", "", "Rare elements processed into high-density conductors.");
        }

        // Setup recipes
        var recipesProp = serialized.FindProperty("_recipes");
        if (recipesProp != null)
        {
            recipesProp.ClearArray();
            
            AddRecipe(recipesProp, 0, "refine_iron_ingot", ResourceRecipeKind.Refinement, "refinery", "iron_ingot", 1, 6f, 
                new[] { ("iron_ore", 2) }, "Smelt ore from terrestrial deposits into workable ingots.");
            AddRecipe(recipesProp, 1, "refine_nutrients", ResourceRecipeKind.Refinement, "bio_lab", "nutrients", 1, 5f,
                new[] { ("biomass", 2) }, "Convert biomass into balanced nutrient slurry for colonies.");
            AddRecipe(recipesProp, 2, "refine_refined_fuels", ResourceRecipeKind.Refinement, "refinery", "refined_fuels", 1, 6f,
                new[] { ("hydrocarbon_ice", 2) }, "Crack hydrocarbon ice into stable propulsion-grade fuels.");
            AddRecipe(recipesProp, 3, "refine_conductors", ResourceRecipeKind.Refinement, "electronics_fab", "conductors", 1, 6f,
                new[] { ("rare_earths", 2) }, "Pull conductive filaments from concentrated rare earth deposits.");
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void AddFamily(SerializedProperty familiesProp, int index, string id, string displayName, string rawId, string refinedId, string compositeId, string description)
    {
        familiesProp.InsertArrayElementAtIndex(index);
        var element = familiesProp.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("id").stringValue = id;
        element.FindPropertyRelative("displayName").stringValue = displayName;
        element.FindPropertyRelative("rawResourceId").stringValue = rawId;
        element.FindPropertyRelative("refinedResourceId").stringValue = refinedId;
        element.FindPropertyRelative("compositeResourceId").stringValue = compositeId;
        element.FindPropertyRelative("description").stringValue = description;
    }

    private static void AddRecipe(SerializedProperty recipesProp, int index, string id, ResourceRecipeKind kind, string facilityTag, 
        string outputId, int outputAmount, float processSeconds, (string resourceId, int amount)[] inputs, string notes)
    {
        recipesProp.InsertArrayElementAtIndex(index);
        var element = recipesProp.GetArrayElementAtIndex(index);
        element.FindPropertyRelative("id").stringValue = id;
        element.FindPropertyRelative("kind").enumValueIndex = (int)kind;
        element.FindPropertyRelative("facilityTag").stringValue = facilityTag;
        element.FindPropertyRelative("outputResourceId").stringValue = outputId;
        element.FindPropertyRelative("outputAmount").intValue = outputAmount;
        element.FindPropertyRelative("processSeconds").floatValue = processSeconds;
        element.FindPropertyRelative("notes").stringValue = notes;

        var inputsProp = element.FindPropertyRelative("inputs");
        inputsProp.ClearArray();
        for (int i = 0; i < inputs.Length; i++)
        {
            inputsProp.InsertArrayElementAtIndex(i);
            var inputElement = inputsProp.GetArrayElementAtIndex(i);
            inputElement.FindPropertyRelative("resourceId").stringValue = inputs[i].resourceId;
            inputElement.FindPropertyRelative("amount").intValue = inputs[i].amount;
        }
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
