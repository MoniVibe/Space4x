using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.VFX;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("add_node_to_graph")]
    public static class AddNodeToGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var nodeTypeIdentifier = @params["node_type"]?.ToString();
                Debug.Log($"[MCP Tools] AddNodeToGraphTool invoked for '{graphPath}' variant '{nodeTypeIdentifier}'");
                var positionX = @params["position_x"]?.ToObject<float?>();
                var positionY = @params["position_y"]?.ToObject<float?>();
                var properties = @params["properties"]?.ToObject<Dictionary<string, JToken>>();

                if (!positionX.HasValue || !positionY.HasValue)
                {
                    return Response.Error("position_x and position_y are required");
                }

                if (string.IsNullOrWhiteSpace(nodeTypeIdentifier))
                {
                    return Response.Error("node_type is required");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    return Response.Error(error);
                }

                // Sync controller before operations
                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                controller.GetType().GetMethod("SyncControllerFromModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                object variant;
                string resolvedIdentifier;
                string variantCategory;

                if (!TryFindVariant(nodeTypeIdentifier, out variant, out resolvedIdentifier, out variantCategory, out error))
                {
                    return Response.Error(error);
                }

                // Find AddNode method
                var allAddNodeMethods = controller.GetType()
                    .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                    .Where(m => m.Name == "AddNode")
                    .ToArray();
                
                var addNodeMethod = allAddNodeMethods
                    .FirstOrDefault(m => m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(Vector2));

                if (addNodeMethod == null)
                {
                    return Response.Error("Unable to locate VFXViewController.AddNode method");
                }

                var variantType = variant?.GetType();
                var uniqueIdentifier = VfxGraphReflectionHelpers.GetProperty(variant, "uniqueIdentifier") as string
                    ?? VfxGraphReflectionHelpers.InvokeInstanceMethod(variant, "GetUniqueIdentifier") as string
                    ?? resolvedIdentifier;
                var modelType = variantType?.GetProperty("modelType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(variant) as Type;

                var position = new Vector2(positionX.Value, positionY.Value);
                object nodeController = null;
                UnityEngine.Object fallbackModel = null;
                try
                {
                    // Try the variant directly first
                    nodeController = addNodeMethod.Invoke(controller, new[] { (object)position, variant, null });
                }
                catch (Exception ex)
                {
                    var innerEx = ex.InnerException ?? ex;
                    var innerDetails = innerEx.ToString();
                    
                    // Try finding a method that takes just position and variant type
                    var altMethod = allAddNodeMethods.FirstOrDefault(m => 
                        m.GetParameters().Length == 2 && 
                        m.GetParameters()[0].ParameterType == typeof(Vector2));
                    
                    if (altMethod != null)
                    {
                        try
                        {
                            nodeController = altMethod.Invoke(controller, new[] { (object)position, variant });
                        }
                        catch (Exception altEx)
                        {
                            var altInner = altEx.InnerException ?? altEx;
                            var altDetails = altInner.ToString();
                            nodeController = TryInstantiateModelDirectly(controller, variant, modelType, position, out fallbackModel);
                            if (nodeController == null)
                            {
                                return Response.Error($"Failed to add node (both methods failed). First: {innerDetails}. Second: {altDetails}");
                            }
                        }
                    }
                    else
                    {
                        nodeController = TryInstantiateModelDirectly(controller, variant, modelType, position, out fallbackModel);
                        if (nodeController == null)
                        {
                            return Response.Error($"Failed to add node to graph: {innerDetails}. Variant type: {variant?.GetType().FullName}");
                        }
                    }
                }

                if (nodeController == null)
                {
                    return Response.Error($"Failed to instantiate node controller. Variant type: {variant?.GetType().FullName}");
                }

                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model == null && fallbackModel != null)
                {
                    model = fallbackModel;
                }

                if (model == null)
                {
                    return Response.Error("Node was created but model could not be retrieved");
                }

                // Check for duplicate nodes at the same position (within a small tolerance)
                var existingNodes = BuildNodeMap(controller);
                var nodeId = model.GetInstanceID();
                const float duplicateTolerance = 5f;
                bool foundNearbyDuplicate = false;
                string duplicateWarning = null;
                
                foreach (var existingNode in existingNodes.Values)
                {
                    var existingModel = VfxGraphReflectionHelpers.GetProperty(existingNode, "model") as UnityEngine.Object;
                    if (existingModel == null || existingModel.GetInstanceID() == nodeId)
                    {
                        continue;
                    }

                    var existingNodeType = existingModel.GetType();
                    var existingPositionProperty = existingNodeType.GetProperty("position");
                    if (existingPositionProperty != null)
                    {
                        var existingPosition = existingPositionProperty.GetValue(existingModel);
                        if (existingPosition is Vector2 existingPos)
                        {
                            var distance = Vector2.Distance(position, existingPos);
                            if (distance < duplicateTolerance)
                            {
                                foundNearbyDuplicate = true;
                                var existingName = existingModel.name ?? existingNodeType.Name;
                                duplicateWarning = $"Node at position ({position.x}, {position.y}) is very close to existing node '{existingName}' at ({existingPos.x}, {existingPos.y}). Distance: {distance:F2}";
                                Debug.LogWarning($"[MCP Tools] {duplicateWarning}");
                                
                                // Auto-adjust position slightly to avoid overlap
                                position = new Vector2(position.x + duplicateTolerance + 1f, position.y);
                                VfxGraphReflectionHelpers.SetModelPosition(model, position);
                                Debug.Log($"[MCP Tools] Auto-adjusted node position to ({position.x}, {position.y}) to avoid overlap");
                            }
                        }
                    }
                }

                if (properties != null && model != null)
                {
                    ApplySettings(model, properties);
                }

                // Use SyncAndSave helper for safe, guarded asset saving
                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                var nodeSummary = BuildNodeSummary(nodeController);
                
                var responseData = new Dictionary<string, object>
                {
                    ["graphPath"] = graphPath,
                    ["node"] = nodeSummary,
                    ["variant"] = new Dictionary<string, object>
                    {
                        ["identifier"] = resolvedIdentifier,
                        ["category"] = variantCategory,
                        ["type"] = variant?.GetType().FullName
                    }
                };
                
                if (foundNearbyDuplicate && !string.IsNullOrEmpty(duplicateWarning))
                {
                    responseData["warning"] = duplicateWarning;
                    responseData["positionAdjusted"] = true;
                }

                return Response.Success($"Node {resolvedIdentifier} added to graph", responseData);
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to add node to graph: {ex.Message}");
            }
        }

        private static Dictionary<string, object> BuildNodeSummary(object nodeController)
        {
            var summary = new Dictionary<string, object>();
            var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model");
            var unityObj = model as UnityEngine.Object;
            var nodeType = model?.GetType();

            summary["id"] = unityObj?.GetInstanceID() ?? nodeController.GetHashCode();
            summary["name"] = unityObj?.name ?? nodeType?.Name ?? "Unknown";
            summary["type"] = nodeType?.FullName ?? nodeController.GetType().FullName;

            var positionProperty = nodeType?.GetProperty("position");
            if (positionProperty != null)
            {
                var rawPosition = positionProperty.GetValue(model);
                if (rawPosition is Vector2 position)
                {
                    summary["position"] = new { x = position.x, y = position.y };
                }
            }

            return summary;
        }

        private static void ApplySettings(UnityEngine.Object model, IDictionary<string, JToken> settings)
        {
            if (settings == null || settings.Count == 0)
            {
                return;
            }

            var setSettingValue = model.GetType().GetMethod("SetSettingValue", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (setSettingValue == null)
            {
                return;
            }

            foreach (var kvp in settings)
            {
                try
                {
                    setSettingValue.Invoke(model, new object[] { kvp.Key, kvp.Value.ToObject<object>() });
                }
                catch (Exception settingEx)
                {
                    Debug.LogWarning($"[MCP Tools] Failed to apply setting '{kvp.Key}': {settingEx.Message}");
                }
            }
        }

        private static object TryInstantiateModelDirectly(object controller, object variant, Type modelType, Vector2 position, out UnityEngine.Object createdModel)
        {
            createdModel = null;
            if (controller == null || variant == null || modelType == null)
            {
                return null;
            }

            var controllerType = controller.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var vfxOperatorType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXOperator");
            var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");
            var vfxParameterType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXParameter");

            MethodInfo addMethod = null;
            object[] args = null;

            if (vfxOperatorType != null && vfxOperatorType.IsAssignableFrom(modelType))
            {
                addMethod = controllerType.GetMethod("AddVFXOperator", flags, null, new[] { typeof(Vector2), variant.GetType() }, null)
                            ?? controllerType.GetMethod("AddVFXOperator", flags);
            }
            else if (vfxContextType != null && vfxContextType.IsAssignableFrom(modelType))
            {
                addMethod = controllerType.GetMethod("AddVFXContext", flags, null, new[] { typeof(Vector2), variant.GetType() }, null)
                            ?? controllerType.GetMethod("AddVFXContext", flags);
            }
            else if (vfxParameterType != null && vfxParameterType.IsAssignableFrom(modelType))
            {
                addMethod = controllerType.GetMethods(flags)
                    .FirstOrDefault(m => m.Name == "AddVFXParameter" && m.GetParameters().Length >= 2);
            }

            if (addMethod == null)
            {
                return null;
            }

            var parameters = addMethod.GetParameters();
            if (parameters.Length == 2)
            {
                args = new object[] { position, variant };
            }
            else if (parameters.Length == 3)
            {
                args = new object[] { position, variant, true };
            }
            else
            {
                return null;
            }

            object modelObject;
            try
            {
                modelObject = addMethod.Invoke(controller, args);
            }
            catch (Exception invokeEx)
            {
                Debug.LogException(invokeEx);
                return null;
            }

            if (modelObject is UnityEngine.Object unityObj)
            {
                createdModel = unityObj;
            }
            else if (modelObject != null)
            {
                var modelProperty = modelObject.GetType().GetProperty("model", flags);
                if (modelProperty != null)
                {
                    createdModel = modelProperty.GetValue(modelObject) as UnityEngine.Object;
                }
            }

            var syncMethod = controllerType.GetMethod("SyncControllerFromModel", flags, null, new[] { typeof(bool).MakeByRefType() }, null);
            if (syncMethod != null)
            {
                var syncArgs = new object[] { false };
                try
                {
                    syncMethod.Invoke(controller, syncArgs);
                }
                catch (Exception syncEx)
                {
                    Debug.LogException(syncEx);
                }
            }

            var vfxModelType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXModel");
            MethodInfo getControllerMethod = null;
            if (vfxModelType != null)
            {
                getControllerMethod = controllerType.GetMethod("GetNewNodeController", flags, null, new[] { vfxModelType }, null)
                                      ?? controllerType.GetMethod("GetRootNodeController", flags, null, new[] { vfxModelType, typeof(int) }, null);
            }

            object nodeController = null;
            if (getControllerMethod != null && createdModel != null)
            {
                try
                {
                    if (getControllerMethod.GetParameters().Length == 1)
                    {
                        nodeController = getControllerMethod.Invoke(controller, new[] { createdModel });
                    }
                    else if (getControllerMethod.GetParameters().Length == 2)
                    {
                        nodeController = getControllerMethod.Invoke(controller, new object[] { createdModel, -1 });
                    }
                }
                catch (Exception controllerEx)
                {
                    Debug.LogException(controllerEx);
                }
            }

                return nodeController;
        }

        private static Dictionary<int, object> BuildNodeMap(object controller)
        {
            var map = new Dictionary<int, object>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    map[model.GetInstanceID()] = nodeController;
                }
            }
            return map;
        }

        private static bool TryFindVariant(string nodeTypeIdentifier, out object variant, out string resolvedIdentifier, out string variantCategory, out string error)
        {
            variant = null;
            resolvedIdentifier = null;
            variantCategory = null;
            error = null;

            var normalized = nodeTypeIdentifier?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                error = "node_type is required";
                return false;
            }

            // Allow fully-qualified type names for explicit control
            var explicitType = Type.GetType(normalized, throwOnError: false);
            if (explicitType == null)
            {
                explicitType = VfxGraphReflectionHelpers.GetEditorType(normalized);
            }
            if (explicitType != null)
            {
                var vfxModelType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXModel");
                if (vfxModelType != null && vfxModelType.IsAssignableFrom(explicitType))
                {
                    try
                    {
                        // Try to find this type in the VFX library instead of creating directly
                        // This is a fallback - the main search should handle most cases
                        Debug.LogWarning($"[MCP Tools] Explicit type '{normalized}' found but should be matched via VFXLibrary. Type: {explicitType.FullName}");
                    }
                    catch (Exception typeEx)
                    {
                        Debug.LogWarning($"[MCP Tools] Failed to process explicit type '{normalized}': {typeEx.Message}");
                    }
                }
            }

            var descriptors = new List<(object descriptor, object variant)>();
            void Collect(string methodName)
            {
                foreach (var descriptor in VfxGraphReflectionHelpers.GetLibraryDescriptors(methodName))
                {
                    var enumerableDescriptor = descriptor as IEnumerable;
                    var toEnumerate = enumerableDescriptor ?? new[] { descriptor };
                    descriptors.AddRange(VfxGraphReflectionHelpers.EnumerateVariants(toEnumerate));
                }
            }

            Collect("GetContexts");
            Collect("GetOperators");
            Collect("GetParameters");

            var matches = new List<(object descriptor, object variant, string identifier, string category)>();

            foreach (var (descriptor, candidate) in descriptors)
            {
                if (candidate == null)
                {
                    continue;
                }

                var variantType = candidate.GetType();
                var name = variantType.GetProperty("name")?.GetValue(candidate) as string;
                var category = variantType.GetProperty("category")?.GetValue(candidate) as string;
                var uniqueIdentifier = variantType.GetMethod("GetUniqueIdentifier", Type.EmptyTypes)?.Invoke(candidate, null) as string;

                var descriptorSynonyms = descriptor?.GetType().GetProperty("synonyms")?.GetValue(descriptor) as IEnumerable<string> ?? Array.Empty<string>();

                var candidates = new List<(string value, int priority)>
                {
                    (uniqueIdentifier, 1), // Highest priority - exact unique identifier
                    (string.IsNullOrEmpty(category) ? null : $"{category}/{name}", 2), // Category/name format
                    (name, 3), // Just name
                };
                foreach (var synonym in descriptorSynonyms)
                {
                    candidates.Add((synonym, 4)); // Synonyms lowest priority
                }

                var matchingCandidates = candidates
                    .Where(c => !string.IsNullOrEmpty(c.value) && string.Equals(c.value, normalized, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.priority)
                    .ToList();

                if (matchingCandidates.Any())
                {
                    matches.Add((descriptor, candidate, uniqueIdentifier ?? name ?? normalized, category));
                }
            }

            if (matches.Count == 0)
            {
                Debug.LogWarning($"[MCP Tools] No variant match for '{nodeTypeIdentifier}'. Descriptor count={descriptors.Count}");
                var sampleVariants = descriptors
                    .Select(tuple => new
                    {
                        category = tuple.variant?.GetType().GetProperty("category")?.GetValue(tuple.variant) as string,
                        name = tuple.variant?.GetType().GetProperty("name")?.GetValue(tuple.variant) as string,
                        type = tuple.variant?.GetType().FullName,
                        synonyms = tuple.descriptor?.GetType().GetProperty("synonyms")?.GetValue(tuple.descriptor) as IEnumerable<string>
                    })
                    .Select(v => new
                    {
                        identifier = string.IsNullOrEmpty(v.category) ? v.name : $"{v.category}/{v.name}",
                        type = v.type,
                        synonyms = v.synonyms != null ? string.Join(";", v.synonyms) : string.Empty
                    })
                    .Where(v => !string.IsNullOrEmpty(v.identifier))
                    .Distinct()
                    .Take(200)
                    .ToArray();

                foreach (var variantInfo in sampleVariants)
                {
                    Debug.LogWarning($"[MCP Tools] Variant sample: identifier='{variantInfo.identifier}', type='{variantInfo.type}', synonyms='{variantInfo.synonyms}'");
                }
                error = $"No VFX variant found matching '{nodeTypeIdentifier}'.";
                return false;
            }

            (object descriptorMatch, object variantMatch, string identifier, string categoryMatch) selectedMatch;
            
            if (matches.Count > 1)
            {
                // Try to disambiguate by checking which match has the most specific identifier
                // Prefer matches where the identifier exactly matches the input
                var exactMatches = matches.Where(m => 
                    string.Equals(m.identifier, normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals($"{m.category}/{m.identifier}", normalized, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (exactMatches.Count == 1)
                {
                    Debug.Log($"[MCP Tools] Disambiguated {matches.Count} matches to 1 exact match: {exactMatches[0].identifier}");
                    selectedMatch = exactMatches[0];
                }
                else
                {
                    var options = string.Join(", ", matches.Select(m => $"'{m.identifier}' (category: {m.category})"));
                    Debug.LogWarning($"[MCP Tools] Ambiguous match for '{nodeTypeIdentifier}'. Found {matches.Count} matches: {options}");
                    error = $"Ambiguous node_type. Matches: {options}";
                    return false;
                }
            }
            else
            {
                selectedMatch = matches[0];
            }

            variant = selectedMatch.variantMatch;
            resolvedIdentifier = selectedMatch.identifier;
            variantCategory = selectedMatch.categoryMatch;
            return true;
        }
    }
}


