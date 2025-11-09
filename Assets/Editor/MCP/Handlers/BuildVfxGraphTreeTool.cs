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
    [McpForUnityTool("build_vfx_graph_tree")]
    public static class BuildVfxGraphTreeTool
    {
        private class BlockTemplate
        {
            public string[] Keywords { get; }
            public Dictionary<string, object> Properties { get; }

            public BlockTemplate(string[] keywords, Dictionary<string, object> properties = null)
            {
                Keywords = keywords ?? Array.Empty<string>();
                Properties = properties ?? new Dictionary<string, object>();
            }
        }

        private class ContextTemplate
        {
            public string Role { get; }
            public string[] Keywords { get; }
            public Vector2 Position { get; }
            public List<BlockTemplate> Blocks { get; }

            public ContextTemplate(string role, string[] keywords, Vector2 position, IEnumerable<BlockTemplate> blocks = null)
            {
                Role = role;
                Keywords = keywords ?? Array.Empty<string>();
                Position = position;
                Blocks = blocks?.ToList() ?? new List<BlockTemplate>();
            }
        }

        private class VariantSelection
        {
            public object Variant { get; set; }
            public string Identifier { get; set; }
            public string Name { get; set; }
            public string Category { get; set; }
        }

        private class ContextResult
        {
            public string Role { get; set; }
            public string Identifier { get; set; }
            public int ContextId { get; set; }
            public List<BlockResult> Blocks { get; } = new List<BlockResult>();
        }

        private class BlockResult
        {
            public string Identifier { get; set; }
            public bool Added { get; set; }
            public string Message { get; set; }
        }

        private class ConnectionResult
        {
            public int SourceContextId { get; set; }
            public int TargetContextId { get; set; }
            public bool Connected { get; set; }
            public string Message { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var overwrite = @params["overwrite"]?.ToObject<bool?>() ?? false;
                var spawnRate = @params["spawn_rate"]?.ToObject<float?>() ?? 32f;

                if (string.IsNullOrWhiteSpace(graphPath))
                {
                    return Response.Error("graph_path is required");
                }

                if (!graphPath.EndsWith(".vfx", StringComparison.OrdinalIgnoreCase))
                {
                    return Response.Error("graph_path must end with .vfx");
                }

                var contextTemplates = BuildContextTemplates(spawnRate);
                var warnings = new List<string>();

                if (!EnsureGraph(graphPath, overwrite, out var resource, out var error))
                {
                    return Response.Error(error);
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    return Response.Error(error);
                }

                // Sync before modifications
                VfxGraphReflectionHelpers.InvokeInstanceMethod(controller, "LightApplyChanges");
                controller.GetType().GetMethod("SyncControllerFromModel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?.Invoke(controller, new object[] { false });

                var contextResults = new List<ContextResult>();

                foreach (var template in contextTemplates)
                {
                    if (!TryFindContextVariant(template.Keywords, out var variantInfo, out error))
                    {
                        return Response.Error($"Unable to find context for role '{template.Role}': {error}");
                    }

                    var createParams = JObject.FromObject(new
                    {
                        graph_path = graphPath,
                        context_type = variantInfo.Identifier,
                        position_x = template.Position.x,
                        position_y = template.Position.y
                    });

                    var createResponse = CreateContextTool.HandleCommand(createParams);
                    if (!TryExtractSuccess(createResponse, out var createJson, out error))
                    {
                        return Response.Error($"Failed to create context '{template.Role}': {error}");
                    }

                    var dataToken = createJson["data"] as JObject;
                    if (dataToken == null)
                    {
                        return Response.Error($"Context '{template.Role}' returned unexpected response");
                    }

                    var contextId = dataToken["contextId"]?.ToObject<int>() ?? 0;
                    if (contextId == 0)
                    {
                        return Response.Error($"Context '{template.Role}' did not return a valid contextId");
                    }

                    var contextResult = new ContextResult
                    {
                        Role = template.Role,
                        Identifier = variantInfo.Identifier,
                        ContextId = contextId
                    };

                    // Add blocks for this context
                    foreach (var block in template.Blocks)
                    {
                        if (!TryFindBlockVariant(block.Keywords, out var blockVariant, out var blockError))
                        {
                            warnings.Add($"Block not found for context '{template.Role}': {blockError}");
                            contextResult.Blocks.Add(new BlockResult
                            {
                                Identifier = string.Join(" ", block.Keywords),
                                Added = false,
                                Message = blockError
                            });
                            continue;
                        }

                        var blockParams = JObject.FromObject(new
                        {
                            graph_path = graphPath,
                            context_id = contextId,
                            block_type = blockVariant.Identifier
                        });

                        var blockResponse = AddBlockToContextTool.HandleCommand(blockParams);
                        if (!TryExtractSuccess(blockResponse, out var blockJson, out var blockErrorMessage))
                        {
                            warnings.Add($"Failed to add block '{blockVariant.Identifier}' to context '{template.Role}': {blockErrorMessage}");
                            contextResult.Blocks.Add(new BlockResult
                            {
                                Identifier = blockVariant.Identifier,
                                Added = false,
                                Message = blockErrorMessage
                            });
                            continue;
                        }

                        var blockData = blockJson["data"] as JObject;
                        var blockId = blockData?["blockId"]?.ToObject<int>() ?? 0;

                        // Apply block properties, if any
                        if (block.Properties.Count > 0 && blockId != 0)
                        {
                            TryApplyBlockProperties(graphPath, contextId, blockId, block.Properties, warnings);
                        }

                        contextResult.Blocks.Add(new BlockResult
                        {
                            Identifier = blockVariant.Identifier,
                            Added = true,
                            Message = "Added"
                        });
                    }

                    contextResults.Add(contextResult);
                }

                var connections = new List<ConnectionResult>();
                for (int i = 0; i < contextResults.Count - 1; i++)
                {
                    var source = contextResults[i];
                    var target = contextResults[i + 1];

                    var connectParams = JObject.FromObject(new
                    {
                        graph_path = graphPath,
                        source_context_id = source.ContextId,
                        target_context_id = target.ContextId,
                        source_slot_index = 0,
                        target_slot_index = 0
                    });

                    var connectResponse = ConnectFlowContextsTool.HandleCommand(connectParams);
                    if (!TryExtractSuccess(connectResponse, out _, out var connectError))
                    {
                        warnings.Add($"Failed to connect '{source.Role}' to '{target.Role}': {connectError}");
                        connections.Add(new ConnectionResult
                        {
                            SourceContextId = source.ContextId,
                            TargetContextId = target.ContextId,
                            Connected = false,
                            Message = connectError
                        });
                    }
                    else
                    {
                        connections.Add(new ConnectionResult
                        {
                            SourceContextId = source.ContextId,
                            TargetContextId = target.ContextId,
                            Connected = true,
                            Message = "Connected"
                        });
                    }
                }

                // Final sync and save
                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);

                var responseData = new Dictionary<string, object>
                {
                    ["graphPath"] = graphPath,
                    ["contexts"] = contextResults.Select(c => new Dictionary<string, object>
                    {
                        ["role"] = c.Role,
                        ["identifier"] = c.Identifier,
                        ["contextId"] = c.ContextId,
                        ["blocks"] = c.Blocks.Select(b => new Dictionary<string, object>
                        {
                            ["identifier"] = b.Identifier,
                            ["added"] = b.Added,
                            ["message"] = b.Message
                        }).ToArray()
                    }).ToArray(),
                    ["connections"] = connections.Select(conn => new Dictionary<string, object>
                    {
                        ["sourceContextId"] = conn.SourceContextId,
                        ["targetContextId"] = conn.TargetContextId,
                        ["connected"] = conn.Connected,
                        ["message"] = conn.Message
                    }).ToArray()
                };

                if (warnings.Count > 0)
                {
                    responseData["warnings"] = warnings.ToArray();
                }

                return Response.Success($"Built VFX graph tree with {contextResults.Count} contexts", responseData);
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to build VFX graph tree: {ex.Message}");
            }
        }

        private static List<ContextTemplate> BuildContextTemplates(float spawnRate)
        {
            return new List<ContextTemplate>
            {
                new ContextTemplate(
                    "Spawn",
                    new[] { "spawn", "constant" },
                    new Vector2(-300f, 0f),
                    new []
                    {
                        new BlockTemplate(
                            new[] { "constant", "spawn", "rate" },
                            new Dictionary<string, object>
                            {
                                ["rate"] = spawnRate
                            })
                    }),
                new ContextTemplate(
                    "Initialize",
                    new[] { "initialize" },
                    new Vector2(-50f, 0f),
                    new []
                    {
                        new BlockTemplate(new[] { "set", "position", "sphere" }),
                        new BlockTemplate(new[] { "set", "velocity" })
                    }),
                new ContextTemplate(
                    "Update",
                    new[] { "update" },
                    new Vector2(200f, 0f)),
                new ContextTemplate(
                    "Output",
                    new[] { "output", "particle", "quad" },
                    new Vector2(450f, 0f))
            };
        }

        private static bool EnsureGraph(string graphPath, bool overwrite, out object resource, out string error)
        {
            resource = null;
            error = null;

            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(graphPath);
            if (existing != null && overwrite)
            {
                if (!AssetDatabase.DeleteAsset(graphPath))
                {
                    error = $"Unable to overwrite existing asset at {graphPath}";
                    return false;
                }
                AssetDatabase.Refresh();
                existing = null;
            }

            if (existing == null)
            {
                if (!VfxGraphReflectionHelpers.TryCreateGraph(graphPath, out resource, out error))
                {
                    return false;
                }

                return true;
            }

            return VfxGraphReflectionHelpers.TryGetResource(graphPath, out resource, out error);
        }

        private static bool TryExtractSuccess(object response, out JObject responseJson, out string error)
        {
            responseJson = response as JObject;
            if (responseJson == null)
            {
                error = "Unexpected response type";
                return false;
            }

            var successToken = responseJson["success"];
            if (successToken != null && successToken.Type == JTokenType.Boolean && successToken.Value<bool>())
            {
                error = null;
                return true;
            }

            error = responseJson["error"]?.ToString() ?? "Unknown error";
            return false;
        }

        private static bool TryFindContextVariant(IEnumerable<string> keywords, out VariantSelection variantInfo, out string error)
        {
            return TryFindVariant("GetContexts", keywords, out variantInfo, out error);
        }

        private static bool TryFindBlockVariant(IEnumerable<string> keywords, out VariantSelection variantInfo, out string error)
        {
            return TryFindVariant("GetBlocks", keywords, out variantInfo, out error);
        }

        private static bool TryFindVariant(string libraryMethod, IEnumerable<string> keywords, out VariantSelection variantInfo, out string error)
        {
            variantInfo = null;
            error = null;

            var keywordList = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).ToList() ?? new List<string>();
            if (keywordList.Count == 0)
            {
                error = "No keywords provided for variant search";
                return false;
            }

            var descriptors = VfxGraphReflectionHelpers.GetLibraryDescriptors(libraryMethod);
            if (descriptors == null)
            {
                error = $"Unable to query VFX library via {libraryMethod}";
                return false;
            }

            VariantSelection bestCandidate = null;
            int bestScore = -1;

            foreach (var descriptor in descriptors)
            {
                foreach (var (desc, variant) in VfxGraphReflectionHelpers.EnumerateVariants(new[] { descriptor }))
                {
                    if (variant == null)
                    {
                        continue;
                    }

                    var metadata = GatherVariantMetadata(desc, variant);
                    var score = ScoreVariant(metadata, keywordList);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestCandidate = new VariantSelection
                        {
                            Variant = variant,
                            Identifier = metadata.Identifier,
                            Name = metadata.Name,
                            Category = metadata.Category
                        };
                    }
                }
            }

            if (bestCandidate == null || bestScore <= 0 || string.IsNullOrEmpty(bestCandidate.Identifier))
            {
                error = $"No variant found matching keywords: {string.Join(", ", keywordList)}";
                return false;
            }

            variantInfo = bestCandidate;
            return true;
        }

        private static (string Identifier, string Name, string Category, IEnumerable<string> Synonyms) GatherVariantMetadata(object descriptor, object variant)
        {
            string identifier = null;
            string name = null;
            string category = null;
            IEnumerable<string> synonyms = Array.Empty<string>();

            if (variant != null)
            {
                var variantType = variant.GetType();
                identifier = variantType.GetMethod("GetUniqueIdentifier", Type.EmptyTypes)?.Invoke(variant, null) as string;
                name = variantType.GetProperty("name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(variant) as string;
                category = variantType.GetProperty("category", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(variant) as string;
            }

            if (descriptor != null)
            {
                synonyms = VfxGraphReflectionHelpers.GetProperty(descriptor, "synonyms") as IEnumerable<string> ?? Array.Empty<string>();
            }

            return (identifier, name, category, synonyms);
        }

        private static int ScoreVariant((string Identifier, string Name, string Category, IEnumerable<string> Synonyms) metadata, List<string> keywords)
        {
            int score = 0;

            foreach (var keyword in keywords)
            {
                if (string.IsNullOrEmpty(keyword))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(metadata.Identifier) &&
                    metadata.Identifier.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 4;
                }
                else if (!string.IsNullOrEmpty(metadata.Name) &&
                         metadata.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 3;
                }
                else if (!string.IsNullOrEmpty(metadata.Category) &&
                         metadata.Category.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    score += 2;
                }
                else if (metadata.Synonyms != null && metadata.Synonyms.Any(s => !string.IsNullOrEmpty(s) &&
                         s.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    score += 1;
                }
            }

            return score;
        }

        private static void TryApplyBlockProperties(string graphPath, int contextId, int blockId, Dictionary<string, object> properties, List<string> warnings)
        {
            try
            {
                if (!VfxGraphReflectionHelpers.TryGetResource(graphPath, out var resource, out var error))
                {
                    warnings.Add($"Unable to refresh block properties: {error}");
                    return;
                }

                if (!VfxGraphReflectionHelpers.TryGetViewController(resource, true, out var controller, out error))
                {
                    warnings.Add($"Unable to refresh block controller: {error}");
                    return;
                }

                var contextMap = BuildContextMap(controller);
                if (!contextMap.TryGetValue(contextId, out var contextController))
                {
                    warnings.Add($"Context {contextId} not found when applying block properties");
                    return;
                }

                var blockModel = FindBlockModel(contextController, blockId);
                if (blockModel == null)
                {
                    warnings.Add($"Block {blockId} not found when applying properties");
                    return;
                }

                var blockType = blockModel.GetType();
                var setSettingValue = blockType.GetMethod("SetSettingValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                foreach (var kvp in properties)
                {
                    try
                    {
                        if (setSettingValue != null)
                        {
                            setSettingValue.Invoke(blockModel, new[] { kvp.Key, kvp.Value });
                        }
                        else
                        {
                            VfxGraphReflectionHelpers.SafePropertySet(blockModel, kvp.Key, kvp.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Failed to set block property '{kvp.Key}': {ex.Message}");
                    }
                }

                VfxGraphReflectionHelpers.SyncAndSave(controller, resource);
            }
            catch (Exception ex)
            {
                warnings.Add($"Failed to apply block properties: {ex.Message}");
            }
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
                    var vfxContextType = VfxGraphReflectionHelpers.GetEditorType("UnityEditor.VFX.VFXContext");
                    if (vfxContextType != null && vfxContextType.IsAssignableFrom(model.GetType()))
                    {
                        map[model.GetInstanceID()] = nodeController;
                    }
                }
            }

            return map;
        }

        private static object FindBlockModel(object contextController, int blockId)
        {
            var contextModel = VfxGraphReflectionHelpers.GetProperty(contextController, "model");
            if (contextModel == null)
            {
                return null;
            }

            var children = VfxGraphReflectionHelpers.GetProperty(contextModel, "children", includeNonPublic: true) as System.Collections.IEnumerable;
            if (children == null)
            {
                return null;
            }

            foreach (var child in children)
            {
                if (child is UnityEngine.Object unityObject && unityObject.GetInstanceID() == blockId)
                {
                    return child;
                }
            }

            return null;
        }
    }
}


