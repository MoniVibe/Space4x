using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("get_project_info")]
    public static class GetProjectInfoTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var projectName = Path.GetFileName(projectRoot);
                
                // Check for graph systems
                bool hasVisualScripting = HasPackage("com.unity.visualscripting");
                bool hasShaderGraph = HasPackage("com.unity.shadergraph");
                bool hasVFXGraph = HasPackage("com.unity.visualeffectgraph");
                bool hasTimeline = HasPackage("com.unity.timeline");
                
                // Get package list from manifest
                var packages = GetPackageList();
                
                return Response.Success("Project information retrieved", new
                {
                    projectName = projectName,
                    projectRoot = projectRoot,
                    unityVersion = Application.unityVersion,
                    graphSystems = new
                    {
                        visualScripting = hasVisualScripting,
                        shaderGraph = hasShaderGraph,
                        vfxGraph = hasVFXGraph,
                        timeline = hasTimeline
                    },
                    packageCount = packages.Count,
                    packages = packages
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to get project info: {ex.Message}");
            }
        }
        
        private static bool HasPackage(string packageName)
        {
            try
            {
                var manifestPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json");
                if (File.Exists(manifestPath))
                {
                    string manifestContent = File.ReadAllText(manifestPath);
                    return manifestContent.Contains(packageName);
                }
            }
            catch { }
            return false;
        }
        
        private static List<string> GetPackageList()
        {
            var packages = new List<string>();
            try
            {
                var manifestPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json");
                if (File.Exists(manifestPath))
                {
                    var manifest = JObject.Parse(File.ReadAllText(manifestPath));
                    var dependencies = manifest["dependencies"] as JObject;
                    if (dependencies != null)
                    {
                        foreach (var prop in dependencies.Properties())
                        {
                            packages.Add(prop.Name);
                        }
                    }
                }
            }
            catch { }
            return packages;
        }
    }
}

