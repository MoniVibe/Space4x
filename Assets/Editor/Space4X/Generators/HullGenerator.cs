using System.Collections.Generic;
using System.Linq;
using Space4X.Authoring;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Space4X.Editor.Generators
{
    public class HullGenerator : BasePrefabGenerator
    {
        public override bool Generate(PrefabMakerOptions options, PrefabMaker.GenerationResult result)
        {
            var catalog = LoadCatalog(options.CatalogPath);
            if (catalog == null || catalog.hulls == null)
            {
                result.Errors.Add("Failed to load hull catalog");
                return false;
            }

            EnsureDirectory($"{PrefabBasePath}/Hulls");
            EnsureDirectory($"{PrefabBasePath}/CapitalShips");
            EnsureDirectory($"{PrefabBasePath}/Carriers");
            EnsureDirectory($"{PrefabBasePath}/Stations");

            bool anyChanged = false;
            foreach (var hullData in catalog.hulls)
            {
                // Apply category filter if specified
                if (options.HullCategoryFilter != HullCategory.Other && hullData.category != options.HullCategoryFilter)
                {
                    continue;
                }
                
                // Apply selected IDs filter if specified
                if (options.SelectedIds != null && options.SelectedIds.Count > 0 && !options.SelectedIds.Contains(hullData.id))
                {
                    continue;
                }
                
                // Apply selected category filter if specified
                if (options.SelectedCategory.HasValue && options.SelectedCategory.Value != PrefabTemplateCategory.Hulls)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(hullData.id))
                {
                    result.Warnings.Add("Skipping hull with empty ID");
                    continue;
                }

                var categoryFolder = GetCategoryFolder(hullData.category);
                var prefabPath = $"{PrefabBasePath}/{categoryFolder}/{hullData.id}.prefab";
                var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (existingPrefab != null && !options.OverwriteMissingSockets)
                {
                    result.SkippedCount++;
                    continue;
                }

                if (options.DryRun)
                {
                    result.CreatedCount++;
                    anyChanged = true;
                    continue;
                }

                var hullObj = LoadOrCreatePrefab(prefabPath, hullData.id, out bool isNew);

                // Add/update HullIdAuthoring
                var hullId = hullObj.GetComponent<HullIdAuthoring>();
                if (hullId == null) hullId = hullObj.AddComponent<HullIdAuthoring>();
                hullId.hullId = hullData.id;

                // Add/update HullSocketAuthoring
                var socketAuthoring = hullObj.GetComponent<HullSocketAuthoring>();
                if (socketAuthoring == null) socketAuthoring = hullObj.AddComponent<HullSocketAuthoring>();
                socketAuthoring.autoCreateFromCatalog = true;

                // Add category-specific components
                AddCategoryComponents(hullObj, hullData.category, hullData.hangarCapacity);

                // Add style tokens
                AddStyleTokens(hullObj, hullData.defaultPalette, hullData.defaultRoughness, hullData.defaultPattern);

                // Add hull variant
                var hullVariant = hullObj.GetComponent<HullVariantAuthoring>();
                if (hullVariant == null) hullVariant = hullObj.AddComponent<HullVariantAuthoring>();
                hullVariant.variant = hullData.variant;

                // Create socket child transforms with layout heuristics
                var layoutOverride = hullObj.GetComponent<SocketLayoutOverrideAuthoring>();
                var manualOverrides = layoutOverride != null ? layoutOverride.GetOverrideDictionary() : null;
                CreateHullSockets(hullObj, hullData.slots, hullData.category, options.OverwriteMissingSockets, manualOverrides);

                // Add placeholder visual
                if (options.PlaceholdersOnly)
                {
                    PlaceholderPrefabUtility.AddPlaceholderVisual(hullObj, GetPrefabType(hullData.category));
                }

                SavePrefab(hullObj, prefabPath, isNew, result);
                anyChanged = true;
            }

            return anyChanged;
        }

        public override void Validate(PrefabMaker.ValidationReport report)
        {
            var catalog = LoadCatalog("Assets/Data/Catalogs");
            if (catalog?.hulls == null) return;

            foreach (var hullData in catalog.hulls)
            {
                var categoryFolder = GetCategoryFolder(hullData.category);
                var prefabPath = $"{PrefabBasePath}/{categoryFolder}/{hullData.id}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (prefab == null)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Warning,
                        Message = $"Hull '{hullData.id}' has no prefab",
                        PrefabPath = prefabPath
                    });
                    continue;
                }

                // Validate sockets match catalog
                ValidateSockets(prefab, hullData, prefabPath, report);

                // Validate hangar capacity
                if (hullData.hangarCapacity > 0f)
                {
                    var hangarCap = prefab.GetComponent<HangarCapacityAuthoring>();
                    if (hangarCap == null || Mathf.Abs(hangarCap.capacity - hullData.hangarCapacity) > 0.01f)
                    {
                        report.Issues.Add(new PrefabMaker.ValidationIssue
                        {
                            Severity = PrefabMaker.ValidationSeverity.Warning,
                            Message = $"Hull '{hullData.id}' hangar capacity mismatch",
                            PrefabPath = prefabPath
                        });
                    }
                }
            }
        }

        private HullCatalogAuthoring LoadCatalog(string catalogPath)
        {
            var prefabPath = $"{catalogPath}/HullCatalog.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            return prefab?.GetComponent<HullCatalogAuthoring>();
        }

        private string GetCategoryFolder(HullCategory category)
        {
            switch (category)
            {
                case HullCategory.CapitalShip: return "CapitalShips";
                case HullCategory.Carrier: return "Carriers";
                case HullCategory.Station: return "Stations";
                default: return "Hulls";
            }
        }

        private PrefabType GetPrefabType(HullCategory category)
        {
            switch (category)
            {
                case HullCategory.CapitalShip: return PrefabType.CapitalShip;
                case HullCategory.Carrier: return PrefabType.Carrier;
                case HullCategory.Station: return PrefabType.Station;
                default: return PrefabType.Hull;
            }
        }

        private void AddCategoryComponents(GameObject hullObj, HullCategory category, float hangarCapacity)
        {
            var existingCapitalShip = hullObj.GetComponent<CapitalShipAuthoring>();
            var existingCarrier = hullObj.GetComponent<CarrierAuthoring>();
            if (existingCapitalShip != null) Object.DestroyImmediate(existingCapitalShip);
            if (existingCarrier != null) Object.DestroyImmediate(existingCarrier);

            switch (category)
            {
                case HullCategory.CapitalShip:
                    hullObj.AddComponent<CapitalShipAuthoring>();
                    break;
                case HullCategory.Carrier:
                    hullObj.AddComponent<CarrierAuthoring>();
                    break;
            }

            if (hangarCapacity > 0f)
            {
                var hangarCap = hullObj.GetComponent<HangarCapacityAuthoring>();
                if (hangarCap == null) hangarCap = hullObj.AddComponent<HangarCapacityAuthoring>();
                hangarCap.capacity = hangarCapacity;
            }
            else
            {
                var hangarCap = hullObj.GetComponent<HangarCapacityAuthoring>();
                if (hangarCap != null) Object.DestroyImmediate(hangarCap);
            }
        }

        private void AddStyleTokens(GameObject obj, byte palette, byte roughness, byte pattern)
        {
            if (palette == 0 && roughness == 128 && pattern == 0) return;

            var styleTokens = obj.GetComponent<StyleTokensAuthoring>();
            if (styleTokens == null) styleTokens = obj.AddComponent<StyleTokensAuthoring>();
            styleTokens.palette = palette;
            styleTokens.roughness = roughness;
            styleTokens.pattern = pattern;
        }

        private void CreateHullSockets(GameObject hullObj, List<HullCatalogAuthoring.HullSlotData> slots, HullCategory category, bool overwriteMissing, Dictionary<string, Vector3> manualOverrides = null)
        {
            if (slots == null || slots.Count == 0) return;

            // Calculate socket positions using layout heuristics (with manual overrides)
            var positions = SocketLayoutHeuristics.CalculateSocketPositions(slots, category, manualOverrides);

            var socketIndices = new Dictionary<string, int>();
            var expectedNames = new HashSet<string>();
            var positionIndex = 0;

            foreach (var slot in slots)
            {
                var key = $"{slot.type}_{slot.size}";
                if (!socketIndices.ContainsKey(key)) socketIndices[key] = 0;
                socketIndices[key]++;

                var socketName = $"Socket_{slot.type}_{slot.size}_{socketIndices[key]:D2}";
                expectedNames.Add(socketName);

                var existingSocket = hullObj.transform.Find(socketName);
                Vector3 socketPosition = positionIndex < positions.Count ? positions[positionIndex] : Vector3.zero;
                positionIndex++;

                if (existingSocket == null)
                {
                    var socketObj = new GameObject(socketName);
                    socketObj.transform.SetParent(hullObj.transform);
                    socketObj.transform.localPosition = socketPosition;
                    socketObj.transform.localRotation = Quaternion.identity;
                }
                else if (overwriteMissing)
                {
                    // Update position if overwriting
                    existingSocket.localPosition = socketPosition;
                    existingSocket.localRotation = Quaternion.identity;
                }
            }

            if (overwriteMissing)
            {
                var socketsToRemove = new List<Transform>();
                for (int i = 0; i < hullObj.transform.childCount; i++)
                {
                    var child = hullObj.transform.GetChild(i);
                    if (child.name.StartsWith("Socket_") && !expectedNames.Contains(child.name))
                    {
                        socketsToRemove.Add(child);
                    }
                }
                foreach (var socket in socketsToRemove)
                {
                    Object.DestroyImmediate(socket.gameObject);
                }
            }
        }

        private void ValidateSockets(GameObject prefab, HullCatalogAuthoring.HullSpecData hullData, string prefabPath, PrefabMaker.ValidationReport report)
        {
            var expectedSockets = new Dictionary<string, int>();
            if (hullData.slots != null)
            {
                foreach (var slot in hullData.slots)
                {
                    var key = $"{slot.type}_{slot.size}";
                    if (!expectedSockets.ContainsKey(key)) expectedSockets[key] = 0;
                    expectedSockets[key]++;
                }
            }

            var actualSockets = new Dictionary<string, int>();
            for (int i = 0; i < prefab.transform.childCount; i++)
            {
                var child = prefab.transform.GetChild(i);
                if (child.name.StartsWith("Socket_"))
                {
                    var parts = child.name.Split('_');
                    if (parts.Length >= 3)
                    {
                        var key = $"{parts[1]}_{parts[2]}";
                        if (!actualSockets.ContainsKey(key)) actualSockets[key] = 0;
                        actualSockets[key]++;
                    }
                }
            }

            foreach (var kvp in expectedSockets)
            {
                if (!actualSockets.ContainsKey(kvp.Key) || actualSockets[kvp.Key] != kvp.Value)
                {
                    report.Issues.Add(new PrefabMaker.ValidationIssue
                    {
                        Severity = PrefabMaker.ValidationSeverity.Error,
                        Message = $"Hull '{hullData.id}' socket mismatch: Expected {kvp.Value}×{kvp.Key}, found {(actualSockets.ContainsKey(kvp.Key) ? actualSockets[kvp.Key] : 0)}",
                        PrefabPath = prefabPath
                    });
                }
            }
        }
    }
}

