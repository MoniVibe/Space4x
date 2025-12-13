using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Space4X.Authoring;
using Space4X.EditorUtilities;
using Space4X.Registry;
using UnityEditor;
using UnityEngine;
using HullSlotData = Space4X.Authoring.HullCatalogAuthoring.HullSlotData;

namespace Space4X.Editor
{
    /// <summary>
    /// Reverse-adopt tool - reads legacy prefabs/scenes and proposes catalog specs.
    /// </summary>
    public static class ReverseAdopt
    {
        public class ProposedSpec
        {
            public string Id;
            public string Type; // Hull, Module, Station, etc.
            public Dictionary<string, object> Properties = new Dictionary<string, object>();
            public List<HullSlotData> Sockets = new List<HullSlotData>();
            public StyleTokens StyleTokens;
        }

        public static List<ProposedSpec> ProposeSpecsFromPrefabs(string prefabDir, string outputPath)
        {
            var proposals = new List<ProposedSpec>();

            if (!Directory.Exists(prefabDir))
            {
                return proposals;
            }

            var prefabs = Directory.GetFiles(prefabDir, "*.prefab", SearchOption.AllDirectories);
            foreach (var prefabPath in prefabs)
            {
                var assetPath = AssetPathUtil.ToAssetRelativePath(prefabPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null) continue;

                var proposal = AnalyzePrefab(prefab, assetPath);
                if (proposal != null)
                {
                    proposals.Add(proposal);
                }
            }

            // Export proposals as JSON
            if (!string.IsNullOrEmpty(outputPath))
            {
                ExportProposals(proposals, outputPath);
            }

            return proposals;
        }

        private static ProposedSpec AnalyzePrefab(GameObject prefab, string prefabPath)
        {
            var proposal = new ProposedSpec
            {
                Id = prefab.name.Replace(".prefab", "").ToLower()
            };

            // Determine type
            if (prefab.GetComponent<HullIdAuthoring>() != null || 
                prefab.GetComponent<CapitalShipAuthoring>() != null ||
                prefab.GetComponent<CarrierAuthoring>() != null)
            {
                proposal.Type = "Hull";
                AnalyzeHullPrefab(prefab, proposal);
            }
            else if (prefab.GetComponent<ModuleIdAuthoring>() != null)
            {
                proposal.Type = "Module";
                AnalyzeModulePrefab(prefab, proposal);
            }
            else if (prefab.GetComponent<StationIdAuthoring>() != null)
            {
                proposal.Type = "Station";
                AnalyzeStationPrefab(prefab, proposal);
            }
            else
            {
                // Try to infer type from name/path
                var pathLower = prefabPath.ToLower();
                if (pathLower.Contains("hull") || pathLower.Contains("ship") || pathLower.Contains("carrier"))
                {
                    proposal.Type = "Hull";
                    AnalyzeHullPrefab(prefab, proposal);
                }
                else if (pathLower.Contains("module"))
                {
                    proposal.Type = "Module";
                    AnalyzeModulePrefab(prefab, proposal);
                }
                else
                {
                    return null; // Unknown type
                }
            }

            // Extract style tokens
            var styleTokens = prefab.GetComponent<StyleTokensAuthoring>();
            if (styleTokens != null)
            {
                proposal.StyleTokens = new StyleTokens
                {
                    Palette = styleTokens.palette,
                    Roughness = styleTokens.roughness,
                    Pattern = styleTokens.pattern
                };
            }

            return proposal;
        }

        private static void AnalyzeHullPrefab(GameObject prefab, ProposedSpec proposal)
        {
            // Extract sockets
            for (int i = 0; i < prefab.transform.childCount; i++)
            {
                var child = prefab.transform.GetChild(i);
                if (child.name.StartsWith("Socket_"))
                {
                    var slot = ParseSocketName(child.name);
                    if (slot != null)
                    {
                        proposal.Sockets.Add(slot);
                    }
                }
            }

            // Extract hangar capacity
            var hangarCap = prefab.GetComponent<HangarCapacityAuthoring>();
            if (hangarCap != null)
            {
                proposal.Properties["hangarCapacity"] = hangarCap.capacity;
            }

            // Extract category
            if (prefab.GetComponent<CapitalShipAuthoring>() != null)
            {
                proposal.Properties["category"] = "CapitalShip";
            }
            else if (prefab.GetComponent<CarrierAuthoring>() != null)
            {
                proposal.Properties["category"] = "Carrier";
            }
        }

        private static void AnalyzeModulePrefab(GameObject prefab, ProposedSpec proposal)
        {
            var mountReq = prefab.GetComponent<MountRequirementAuthoring>();
            if (mountReq != null)
            {
                proposal.Properties["requiredMount"] = mountReq.mountType.ToString();
                proposal.Properties["requiredSize"] = mountReq.mountSize.ToString();
            }

            var moduleFunction = prefab.GetComponent<ModuleFunctionAuthoring>();
            if (moduleFunction != null)
            {
                proposal.Properties["function"] = moduleFunction.function.ToString();
                proposal.Properties["functionCapacity"] = moduleFunction.capacity;
            }
        }

        private static void AnalyzeStationPrefab(GameObject prefab, ProposedSpec proposal)
        {
            var stationId = prefab.GetComponent<StationIdAuthoring>();
            if (stationId != null)
            {
                proposal.Properties["isRefitFacility"] = stationId.isRefitFacility;
                proposal.Properties["facilityZoneRadius"] = stationId.facilityZoneRadius;
            }
        }

        private static HullCatalogAuthoring.HullSlotData ParseSocketName(string socketName)
        {
            // Format: Socket_MountType_Size_Index
            var parts = socketName.Split('_');
            if (parts.Length < 3) return null;

            if (Enum.TryParse<MountType>(parts[1], true, out var mountType) &&
                Enum.TryParse<MountSize>(parts[2], true, out var mountSize))
            {
                return new HullCatalogAuthoring.HullSlotData
                {
                    type = mountType,
                    size = mountSize
                };
            }

            return null;
        }

        private static void ExportProposals(List<ProposedSpec> proposals, string outputPath)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(proposals, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(outputPath, json);
            AssetDatabase.ImportAsset(outputPath);
        }
    }
}

