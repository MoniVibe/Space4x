using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Space4X.Editor
{
    public static class CreateRefitCatalogs
    {
        [MenuItem("Tools/Space4X/Create Refit Catalog Assets")]
        public static void CreateCatalogs()
        {
            var catalogsPath = "Assets/Data/Catalogs";
            if (!AssetDatabase.IsValidFolder(catalogsPath))
            {
                var parentPath = "Assets/Data";
                if (!AssetDatabase.IsValidFolder(parentPath))
                {
                    AssetDatabase.CreateFolder("Assets", "Data");
                }
                AssetDatabase.CreateFolder(parentPath, "Catalogs");
            }

            CreateModuleCatalog(catalogsPath);
            CreateHullCatalog(catalogsPath);
            CreateRefitRepairTuning(catalogsPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Space4X Refit catalog assets created successfully!");
        }

        private static void CreateModuleCatalog(string path)
        {
            var assetPath = $"{path}/ModuleCatalog.prefab";
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existingPrefab != null)
            {
                Debug.Log($"ModuleCatalog already exists at {assetPath}");
                return;
            }

            var temp = new GameObject("ModuleCatalogAuthoring");
            var catalog = temp.AddComponent<ModuleCatalogAuthoring>();
            catalog.modules = new System.Collections.Generic.List<ModuleCatalogAuthoring.ModuleSpecData>
            {
                new ModuleCatalogAuthoring.ModuleSpecData { id = "reactor-mk1", moduleClass = ModuleClass.Reactor, requiredMount = MountType.Core, requiredSize = MountSize.S, massTons = 40f, powerDrawMW = -120f, offenseRating = 0, defenseRating = 0, utilityRating = 2, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "engine-mk1", moduleClass = ModuleClass.Engine, requiredMount = MountType.Engine, requiredSize = MountSize.S, massTons = 20f, powerDrawMW = 30f, offenseRating = 0, defenseRating = 0, utilityRating = 2, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "laser-s-1", moduleClass = ModuleClass.Laser, requiredMount = MountType.Weapon, requiredSize = MountSize.S, massTons = 8f, powerDrawMW = 15f, offenseRating = 3, defenseRating = 0, utilityRating = 0, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "pd-s-1", moduleClass = ModuleClass.PointDefense, requiredMount = MountType.Weapon, requiredSize = MountSize.S, massTons = 6f, powerDrawMW = 8f, offenseRating = 1, defenseRating = 2, utilityRating = 0, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "missile-s-1", moduleClass = ModuleClass.Missile, requiredMount = MountType.Weapon, requiredSize = MountSize.S, massTons = 9f, powerDrawMW = 12f, offenseRating = 4, defenseRating = 0, utilityRating = 0, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "shield-s-1", moduleClass = ModuleClass.Shield, requiredMount = MountType.Defense, requiredSize = MountSize.S, massTons = 18f, powerDrawMW = 35f, offenseRating = 0, defenseRating = 4, utilityRating = 0, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "armor-s-1", moduleClass = ModuleClass.Armor, requiredMount = MountType.Defense, requiredSize = MountSize.S, massTons = 14f, powerDrawMW = 0f, offenseRating = 0, defenseRating = 3, utilityRating = 0, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "hangar-s-1", moduleClass = ModuleClass.Hangar, requiredMount = MountType.Hangar, requiredSize = MountSize.S, massTons = 60f, powerDrawMW = 80f, offenseRating = 0, defenseRating = 0, utilityRating = 3, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "repair-s-1", moduleClass = ModuleClass.RepairDrones, requiredMount = MountType.Utility, requiredSize = MountSize.S, massTons = 10f, powerDrawMW = 20f, offenseRating = 0, defenseRating = 0, utilityRating = 4, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "scanner-s-1", moduleClass = ModuleClass.Scanner, requiredMount = MountType.Utility, requiredSize = MountSize.S, massTons = 5f, powerDrawMW = 5f, offenseRating = 0, defenseRating = 0, utilityRating = 2, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "reactor-mk2", moduleClass = ModuleClass.Reactor, requiredMount = MountType.Core, requiredSize = MountSize.M, massTons = 65f, powerDrawMW = -200f, offenseRating = 0, defenseRating = 0, utilityRating = 3, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "engine-mk2", moduleClass = ModuleClass.Engine, requiredMount = MountType.Engine, requiredSize = MountSize.M, massTons = 32f, powerDrawMW = 50f, offenseRating = 0, defenseRating = 0, utilityRating = 3, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "missile-m-1", moduleClass = ModuleClass.Missile, requiredMount = MountType.Weapon, requiredSize = MountSize.M, massTons = 16f, powerDrawMW = 20f, offenseRating = 7, defenseRating = 0, utilityRating = 0, defaultEfficiency = 1f },
                new ModuleCatalogAuthoring.ModuleSpecData { id = "shield-m-1", moduleClass = ModuleClass.Shield, requiredMount = MountType.Defense, requiredSize = MountSize.M, massTons = 22f, powerDrawMW = 50f, offenseRating = 0, defenseRating = 6, utilityRating = 0, defaultEfficiency = 1f }
            };

            PrefabUtility.SaveAsPrefabAsset(temp, assetPath);
            Object.DestroyImmediate(temp);
        }

        private static void CreateHullCatalog(string path)
        {
            var assetPath = $"{path}/HullCatalog.prefab";
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existingPrefab != null)
            {
                Debug.Log($"HullCatalog already exists at {assetPath}");
                return;
            }

            var temp = new GameObject("HullCatalogAuthoring");
            var catalog = temp.AddComponent<HullCatalogAuthoring>();
            catalog.hulls = new System.Collections.Generic.List<HullCatalogAuthoring.HullSpecData>
            {
                new HullCatalogAuthoring.HullSpecData
                {
                    id = "lcv-sparrow",
                    baseMassTons = 300f,
                    fieldRefitAllowed = true,
                    slots = new System.Collections.Generic.List<HullCatalogAuthoring.HullSlotData>
                    {
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Core, size = MountSize.S },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Engine, size = MountSize.S },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Hangar, size = MountSize.S },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Weapon, size = MountSize.S },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Weapon, size = MountSize.S },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Defense, size = MountSize.S },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Utility, size = MountSize.S }
                    }
                },
                new HullCatalogAuthoring.HullSpecData
                {
                    id = "cv-mule",
                    baseMassTons = 700f,
                    fieldRefitAllowed = false,
                    slots = new System.Collections.Generic.List<HullCatalogAuthoring.HullSlotData>
                    {
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Core, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Engine, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Hangar, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Hangar, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Weapon, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Weapon, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Defense, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Defense, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Utility, size = MountSize.M },
                        new HullCatalogAuthoring.HullSlotData { type = MountType.Utility, size = MountSize.M }
                    }
                }
            };

            PrefabUtility.SaveAsPrefabAsset(temp, assetPath);
            Object.DestroyImmediate(temp);
        }

        private static void CreateRefitRepairTuning(string path)
        {
            var assetPath = $"{path}/RefitRepairTuning.prefab";
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (existingPrefab != null)
            {
                Debug.Log($"RefitRepairTuning already exists at {assetPath}");
                return;
            }

            var temp = new GameObject("RefitRepairTuningAuthoring");
            var tuning = temp.AddComponent<RefitRepairTuningAuthoring>();
            tuning.baseRefitSeconds = 60f;
            tuning.massSecPerTon = 1.5f;
            tuning.sizeMultS = 1f;
            tuning.sizeMultM = 1.6f;
            tuning.sizeMultL = 2.4f;
            tuning.stationTimeMult = 1f;
            tuning.fieldTimeMult = 1.5f;
            tuning.globalFieldRefitEnabled = true;
            tuning.repairRateEffPerSecStation = 0.01f;
            tuning.repairRateEffPerSecField = 0.005f;
            tuning.rewirePenaltySeconds = 20f;

            PrefabUtility.SaveAsPrefabAsset(temp, assetPath);
            Object.DestroyImmediate(temp);
        }
    }
}

