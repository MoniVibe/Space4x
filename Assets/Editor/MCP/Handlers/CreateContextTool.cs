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
    [McpForUnityTool("create_context")]
    public static class CreateContextTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var contextTypeIdentifier = @params["context_type"]?.ToString();
                var positionX = @params["position_x"]?.ToObject<float?>();
                var positionY = @params["position_y"]?.ToObject<float?>();

                if (string.IsNullOrWhiteSpace(contextTypeIdentifier))
                {
                    return Response.Error("context_type is required");
                }

                if (!positionX.HasValue || !positionY.HasValue)
                {
                    return Response.Error("position_x and position_y are required");
                }

                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
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

                // Find context variant
                if (!TryFindContextVariant(contextTypeIdentifier, out var variant, out var resolvedIdentifier, out error))
                {
                    return Response.Error(error);
                }

                // Add context using AddNode (contexts are added as nodes)
                var position = new Vector2(positionX.Value, positionY.Value);
                var allAddNodeMethods = controller.GetType()
                    .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => m.Name == "AddNode")
                    .ToArray();

                var addNodeMethod = allAddNodeMethods
                    .FirstOrDefault(m => m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(Vector2));

                if (addNodeMethod == null)
                {
                    return Response.Error("Unable to locate VFXViewController.AddNode method");
                }

                object newContextController;
                try
                {
                    newContextController = addNodeMethod.Invoke(controller, new object[] { position, variant, null });
                }
                catch (Exception ex)
                {
                    return Response.Error($"Failed to add context: {ex.Message}");
                }

                if (newContextController == null)
                {
                    return Response.Error($"Failed to create context '{resolvedIdentifier}'");
                }

                var newContextModel = VfxGraphReflectionHelpers.GetProperty(newContextController, "model") as UnityEngine.Object;
                var newContextId = newContextModel?.GetInstanceID() ?? newContextController.GetHashCode();

                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                return Response.Success($"Context '{resolvedIdentifier}' created in graph {graphPath}", new
                {
                    graphPath,
                    contextId = newContextId,
                    contextType = resolvedIdentifier,
                    position = new { x = positionX.Value, y = positionY.Value }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create context: {ex.Message}");
            }
        }

        private static bool TryFindContextVariant(string contextTypeIdentifier, out object variant, out string resolvedIdentifier, out string error)
        {
            variant = null;
            resolvedIdentifier = null;
            error = null;

            var normalized = contextTypeIdentifier?.Trim();
            if (string.IsNullOrEmpty(normalized))
            {
                error = "context_type is required";
                return false;
            }

            var descriptors = new List<(object descriptor, object variant)>();
            foreach (var descriptor in VfxGraphReflectionHelpers.GetLibraryDescriptors("GetContexts"))
            {
                foreach (var (desc, var) in VfxGraphReflectionHelpers.EnumerateVariants(new[] { descriptor }))
                {
                    descriptors.Add((desc, var));
                }
            }

            var matches = new List<(object descriptor, object variant, string identifier)>();

            foreach (var (descriptor, candidate) in descriptors)
            {
                if (candidate == null) continue;

                var variantType = candidate.GetType();
                var name = VfxGraphReflectionHelpers.GetProperty(candidate, "name") as string;
                var uniqueIdentifier = VfxGraphReflectionHelpers.InvokeInstanceMethod(candidate, "GetUniqueIdentifier") as string;

                var candidates = new List<string> { uniqueIdentifier, name };
                var descriptorSynonyms = VfxGraphReflectionHelpers.GetProperty(descriptor, "synonyms") as IEnumerable<string>;
                if (descriptorSynonyms != null)
                {
                    candidates.AddRange(descriptorSynonyms);
                }

                if (candidates.Any(c => !string.IsNullOrEmpty(c) && string.Equals(c, normalized, StringComparison.OrdinalIgnoreCase)))
                {
                    matches.Add((descriptor, candidate, uniqueIdentifier ?? name ?? normalized));
                }
            }

            if (matches.Count == 0)
            {
                error = $"No VFX context variant found matching '{contextTypeIdentifier}'.";
                return false;
            }

            if (matches.Count > 1)
            {
                var exactMatches = matches.Where(m => string.Equals(m.identifier, normalized, StringComparison.OrdinalIgnoreCase)).ToList();
                if (exactMatches.Count == 1)
                {
                    matches = exactMatches;
                }
                else
                {
                    var options = string.Join(", ", matches.Select(m => $"'{m.identifier}'"));
                    error = $"Ambiguous context_type. Matches: {options}";
                    return false;
                }
            }

            var selectedMatch = matches[0];
            variant = selectedMatch.variant;
            resolvedIdentifier = selectedMatch.identifier;
            return true;
        }
    }
}

