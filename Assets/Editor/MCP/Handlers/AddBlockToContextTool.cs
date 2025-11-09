using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("add_block_to_context")]
    public static class AddBlockToContextTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var contextIdToken = @params["context_id"];
                var blockTypeIdentifier = @params["block_type"]?.ToString();
                var insertIndex = @params["insert_index"]?.ToObject<int?>();

                if (string.IsNullOrWhiteSpace(blockTypeIdentifier))
                {
                    return Response.Error("block_type is required");
                }

                if (!TryParseNodeId(contextIdToken, out var contextId, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    return Response.Error(error);
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                var syncArgs = new object[] { false };
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                var contextMap = BuildContextMap(controller);

                if (!contextMap.TryGetValue(contextId, out var contextController))
                {
                    return Response.Error($"Context with id {contextId} not found");
                }

                var model = VfxGraphReflectionHelpers.GetProperty(contextController, "model") as UnityEngine.Object;
                if (model == null)
                {
                    return Response.Error($"Context controller found but model is null");
                }

                if (!TryFindBlockVariant(blockTypeIdentifier, out var variant, out var resolvedIdentifier, out error))
                {
                    return Response.Error(error);
                }

                var blockModel = AddBlockToContext(model, variant, insertIndex);
                if (blockModel == null)
                {
                    return Response.Error($"Failed to add block '{resolvedIdentifier}' to context");
                }

                var unityBlock = blockModel as UnityEngine.Object;

                var settingsToken = @params["settings"] as JObject;
                if (settingsToken != null && unityBlock != null)
                {
                    ApplyPropertySettings(unityBlock, settingsToken);
                }

                var slotValuesToken = @params["slot_values"] as JObject;
                if (slotValuesToken != null && unityBlock != null)
                {
                    ApplySlotValues(unityBlock, slotValuesToken);
                }

                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, syncArgs);

                EditorUtility.SetDirty(resource as UnityEngine.Object);
                VfxGraphReflectionHelpers.WriteAssetWithSubAssets(resource);
                AssetDatabase.SaveAssets();

                return Response.Success($"Block {resolvedIdentifier} added to context", new
                {
                    graphPath,
                    contextId,
                    blockType = resolvedIdentifier,
                    blockId = unityBlock?.GetInstanceID()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to add block to context: {ex.Message}");
            }
        }

        private static bool TryParseNodeId(JToken token, out int id, out string error)
        {
            error = null;
            id = 0;

            if (token == null)
            {
                error = "context_id is required";
                return false;
            }

            if (token.Type == JTokenType.Integer && token.ToObject<int?>() is int directId)
            {
                id = directId;
                return true;
            }

            var asString = token.ToString();
            if (int.TryParse(asString, out id))
            {
                return true;
            }

            error = $"Unable to parse context id '{asString}'";
            return false;
        }

        private static Dictionary<int, object> BuildContextMap(object controller)
        {
            var map = new Dictionary<int, object>();
            var nodesEnumerable = VfxGraphReflectionHelpers.GetProperty(controller, "nodes");
            foreach (var nodeController in VfxGraphReflectionHelpers.Enumerate(nodesEnumerable))
            {
                var model = VfxGraphReflectionHelpers.GetProperty(nodeController, "model") as UnityEngine.Object;
                if (model != null)
                {
                    var modelType = model.GetType();
                    var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");
                    if (vfxContextType != null && vfxContextType.IsAssignableFrom(modelType))
                    {
                        map[model.GetInstanceID()] = nodeController;
                    }
                }
            }
            return map;
        }

        private static bool TryFindBlockVariant(string blockTypeIdentifier, out object variant, out string resolvedIdentifier, out string error)
        {
            variant = null;
            resolvedIdentifier = null;
            error = null;

            var normalized = blockTypeIdentifier?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                error = "block_type is required";
                return false;
            }

            var descriptors = new List<(object descriptor, object variant)>();
            foreach (var descriptor in VfxGraphReflectionHelpers.GetLibraryDescriptors("GetBlocks"))
            {
                if (descriptor is System.Collections.IEnumerable enumerable)
                {
                    descriptors.AddRange(VfxGraphReflectionHelpers.EnumerateVariants(enumerable));
                }
                else
                {
                    descriptors.AddRange(VfxGraphReflectionHelpers.EnumerateVariants(new[] { descriptor }));
                }
            }

            var matches = new List<(object variant, string identifier)>();

            foreach (var (_, candidate) in descriptors)
            {
                if (candidate == null)
                {
                    continue;
                }

                var variantType = candidate.GetType();
                var name = variantType.GetProperty("name")?.GetValue(candidate) as string;
                var uniqueIdentifier = variantType.GetMethod("GetUniqueIdentifier", Type.EmptyTypes)?.Invoke(candidate, null) as string;

                if (string.Equals(uniqueIdentifier, normalized, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(name, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add((candidate, uniqueIdentifier ?? name ?? normalized));
                }
            }

            if (matches.Count == 0)
            {
                error = $"No block variant found matching '{blockTypeIdentifier}'";
                return false;
            }

            if (matches.Count > 1)
            {
                error = $"Ambiguous block_type. Matches: {string.Join(", ", matches.Select(m => m.identifier))}";
                return false;
            }

            variant = matches[0].variant;
            resolvedIdentifier = matches[0].identifier;
            return true;
        }

        private static object AddBlockToContext(object contextModel, object variant, int? insertIndex)
        {
            var contextType = contextModel.GetType();

            var addChildMethod = contextType.GetMethod("AddChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (addChildMethod != null)
            {
                try
                {
                    if (insertIndex.HasValue)
                    {
                        var parameters = addChildMethod.GetParameters();
                        if (parameters.Length >= 2)
                        {
                            return addChildMethod.Invoke(contextModel, new object[] { variant, insertIndex.Value });
                        }
                    }
                    return addChildMethod.Invoke(contextModel, new[] { variant });
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] AddChild failed: {ex.Message}");
                }
            }

            var createBlockMethod = contextType.GetMethod("CreateBlock", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (createBlockMethod != null)
            {
                try
                {
                        var blockModel = createBlockMethod.Invoke(contextModel, new[] { variant });
                        if (blockModel != null && insertIndex.HasValue)
                        {
                            MoveBlockToIndex(contextModel, blockModel, insertIndex.Value);
                        }
                        return blockModel;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] CreateBlock failed: {ex.Message}");
                }
            }

            return null;
        }

        private static void MoveBlockToIndex(object contextModel, object blockModel, int targetIndex)
        {
            var contextType = contextModel.GetType();
            var moveMethod = contextType.GetMethod("MoveChild", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (moveMethod != null)
            {
                try
                {
                    moveMethod.Invoke(contextModel, new object[] { blockModel, targetIndex });
                }
                catch
                {
                }
            }
        }

        private static void ApplyPropertySettings(UnityEngine.Object block, JObject settings)
        {
            foreach (var property in settings.Properties())
            {
                try
                {
                    if (!TrySetProperty(block, property.Name, property.Value))
                    {
                        Debug.LogWarning($"[MCP Tools] add_block_to_context: Failed to set property '{property.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] add_block_to_context: Exception setting property '{property.Name}': {ex.Message}");
                }
            }
        }

        private static bool TrySetProperty(object target, string propertyName, JToken token)
        {
            if (target == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            var targetType = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var property = targetType.GetProperty(propertyName, flags);
            if (property != null && property.CanWrite)
            {
                try
                {
                    var converted = ConvertTokenToType(token, property.PropertyType);
                    property.SetValue(target, converted);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] add_block_to_context: Failed to set property '{propertyName}': {ex.Message}");
                }
            }

            var field = targetType.GetField(propertyName, flags);
            if (field != null)
            {
                try
                {
                    var converted = ConvertTokenToType(token, field.FieldType);
                    field.SetValue(target, converted);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] add_block_to_context: Failed to set field '{propertyName}': {ex.Message}");
                }
            }

            try
            {
                var converted = ConvertTokenToType(token, null);
                if (VfxGraphReflectionHelpers.SafePropertySet(target, propertyName, converted))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MCP Tools] add_block_to_context: SafePropertySet failed for '{propertyName}': {ex.Message}");
            }

            return false;
        }

        private static void ApplySlotValues(UnityEngine.Object block, JObject slotValues)
        {
            foreach (var property in slotValues.Properties())
            {
                try
                {
                    var slot = FindSlot(block, property.Name);
                    if (slot == null)
                    {
                        Debug.LogWarning($"[MCP Tools] add_block_to_context: Slot '{property.Name}' not found");
                        continue;
                    }

                    if (!TrySetSlotValue(slot, property.Value))
                    {
                        Debug.LogWarning($"[MCP Tools] add_block_to_context: Failed to set slot '{property.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] add_block_to_context: Exception setting slot '{property.Name}': {ex.Message}");
                }
            }
        }

        private static object FindSlot(object blockModel, string slotName)
        {
            if (blockModel == null || string.IsNullOrEmpty(slotName))
            {
                return null;
            }

            var modelType = blockModel.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var propertyName in new[] { "inputSlots", "outputSlots" })
            {
                var slotsProperty = modelType.GetProperty(propertyName, flags);
                if (slotsProperty == null)
                {
                    continue;
                }

                if (slotsProperty.GetValue(blockModel) is System.Collections.IEnumerable enumerable)
                {
                    foreach (var slot in VfxGraphReflectionHelpers.Enumerate(enumerable))
                    {
                        if (SlotMatches(slot, slotName))
                        {
                            return slot;
                        }
                    }
                }
            }

            return null;
        }

        private static bool SlotMatches(object slot, string slotName)
        {
            if (slot == null)
            {
                return false;
            }

            var slotType = slot.GetType();
            var name = slotType.GetProperty("name")?.GetValue(slot) as string;
            var path = slotType.GetProperty("path")?.GetValue(slot) as string;

            return string.Equals(name, slotName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(path, slotName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySetSlotValue(object slot, JToken value)
        {
            if (slot == null)
            {
                return false;
            }

            var slotType = slot.GetType();
            var setValueMethod = slotType.GetMethod("SetValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (setValueMethod != null)
            {
                try
                {
                    var parameterType = setValueMethod.GetParameters().FirstOrDefault()?.ParameterType ?? typeof(object);
                    var converted = ConvertTokenToType(value, parameterType);
                    setValueMethod.Invoke(slot, new[] { converted });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] add_block_to_context: SetValue failed: {ex.Message}");
                }
            }

            var valueProperty = slotType.GetProperty("value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (valueProperty != null && valueProperty.CanWrite)
            {
                try
                {
                    var converted = ConvertTokenToType(value, valueProperty.PropertyType);
                    valueProperty.SetValue(slot, converted);
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MCP Tools] add_block_to_context: Direct slot value set failed: {ex.Message}");
                }
            }

            return false;
        }

        private static object ConvertTokenToType(JToken token, Type targetType)
        {
            if (token == null)
            {
                return null;
            }

            if (targetType == null)
            {
                return token.ToObject<object>();
            }

            try
            {
                if (targetType == typeof(float) || targetType == typeof(float?))
                {
                    return token.ToObject<float>();
                }

                if (targetType == typeof(int) || targetType == typeof(int?))
                {
                    return token.ToObject<int>();
                }

                if (targetType == typeof(bool) || targetType == typeof(bool?))
                {
                    return token.ToObject<bool>();
                }

                if (targetType == typeof(string))
                {
                    return token.ToString();
                }

                if (targetType == typeof(Vector2))
                {
                    var obj = token.ToObject<JObject>();
                    return new Vector2(
                        obj?["x"]?.ToObject<float>() ?? 0f,
                        obj?["y"]?.ToObject<float>() ?? 0f);
                }

                if (targetType == typeof(Vector3))
                {
                    var obj = token.ToObject<JObject>();
                    return new Vector3(
                        obj?["x"]?.ToObject<float>() ?? 0f,
                        obj?["y"]?.ToObject<float>() ?? 0f,
                        obj?["z"]?.ToObject<float>() ?? 0f);
                }

                if (targetType == typeof(Vector4))
                {
                    var obj = token.ToObject<JObject>();
                    return new Vector4(
                        obj?["x"]?.ToObject<float>() ?? 0f,
                        obj?["y"]?.ToObject<float>() ?? 0f,
                        obj?["z"]?.ToObject<float>() ?? 0f,
                        obj?["w"]?.ToObject<float>() ?? 0f);
                }

                if (targetType == typeof(Color))
                {
                    var obj = token.ToObject<JObject>();
                    return new Color(
                        obj?["r"]?.ToObject<float>() ?? obj?["x"]?.ToObject<float>() ?? 0f,
                        obj?["g"]?.ToObject<float>() ?? obj?["y"]?.ToObject<float>() ?? 0f,
                        obj?["b"]?.ToObject<float>() ?? obj?["z"]?.ToObject<float>() ?? 0f,
                        obj?["a"]?.ToObject<float>() ?? obj?["w"]?.ToObject<float>() ?? 1f);
                }

                return token.ToObject(targetType);
            }
            catch
            {
                return token.ToObject(targetType);
            }
        }
    }
}
