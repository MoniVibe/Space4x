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

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("list_graph_variants")]
    public static class ListGraphVariantsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var categoryFilter = @params["category"]?.ToString();
                var searchTerm = @params["search"]?.ToString();
                
                int limit = 500;
                var limitToken = @params["limit"];
                if (limitToken != null && limitToken.Type != JTokenType.Null)
                {
                    switch (limitToken.Type)
                    {
                        case JTokenType.Integer:
                            limit = Math.Max(0, limitToken.Value<int>());
                            break;
                        case JTokenType.Float:
                            limit = Math.Max(0, (int)Math.Floor(limitToken.Value<double>()));
                            break;
                        default:
                            if (int.TryParse(limitToken.ToString(), out var parsedLimit))
                            {
                                limit = Math.Max(0, parsedLimit);
                            }
                            else
                            {
                                Debug.LogWarning($"[MCP Tools] list_graph_variants received non-numeric limit value '{limitToken}'. Falling back to default {limit}.");
                            }
                            break;
                    }
                }

                var allVariants = new List<Dictionary<string, object>>();

                void CollectVariants(string methodName, string category)
                {
                    foreach (var descriptor in VfxGraphReflectionHelpers.GetLibraryDescriptors(methodName))
                    {
                        var enumerableDescriptor = descriptor as IEnumerable;
                        var toEnumerate = enumerableDescriptor ?? new[] { descriptor };
                        
                        foreach (var (descriptorObj, variant) in VfxGraphReflectionHelpers.EnumerateVariants(toEnumerate))
                        {
                            if (variant == null)
                            {
                                continue;
                            }

                            var variantType = variant.GetType();
                            var name = variantType.GetProperty("name")?.GetValue(variant) as string;
                            var variantCategory = variantType.GetProperty("category")?.GetValue(variant) as string ?? category;
                            var uniqueIdentifier = variantType.GetMethod("GetUniqueIdentifier", Type.EmptyTypes)?.Invoke(variant, null) as string;
                            var modelType = variantType.GetProperty("modelType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(variant) as Type;

                            var descriptorSynonyms = descriptorObj?.GetType().GetProperty("synonyms")?.GetValue(descriptorObj) as IEnumerable<string> ?? Array.Empty<string>();

                            // Apply filters
                            if (!string.IsNullOrEmpty(categoryFilter) && !string.Equals(variantCategory, categoryFilter, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }

                            if (!string.IsNullOrEmpty(searchTerm))
                            {
                                var searchLower = searchTerm.ToLowerInvariant();
                                var matchesName = name != null && name.ToLowerInvariant().Contains(searchLower);
                                var matchesIdentifier = uniqueIdentifier != null && uniqueIdentifier.ToLowerInvariant().Contains(searchLower);
                                var matchesSynonym = descriptorSynonyms.Any(s => s != null && s.ToLowerInvariant().Contains(searchLower));
                                
                                if (!matchesName && !matchesIdentifier && !matchesSynonym)
                                {
                                    continue;
                                }
                            }

                            var variantInfo = new Dictionary<string, object>
                            {
                                ["identifier"] = uniqueIdentifier ?? name ?? "Unknown",
                                ["name"] = name ?? "Unknown",
                                ["category"] = variantCategory,
                                ["modelType"] = modelType?.FullName,
                                ["synonyms"] = descriptorSynonyms.ToArray()
                            };

                            allVariants.Add(variantInfo);

                            if (allVariants.Count >= limit)
                            {
                                return;
                            }
                        }
                    }
                }

                CollectVariants("GetContexts", "Context");
                if (allVariants.Count < limit)
                {
                    CollectVariants("GetOperators", "Operator");
                }
                if (allVariants.Count < limit)
                {
                    CollectVariants("GetParameters", "Parameter");
                }

                return Response.Success($"Found {allVariants.Count} VFX variants", new
                {
                    count = allVariants.Count,
                    variants = allVariants,
                    filters = new
                    {
                        category = categoryFilter,
                        search = searchTerm,
                        limit
                    }
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list graph variants: {ex.Message}");
            }
        }
    }
}

