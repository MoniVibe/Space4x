using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System.IO;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("identify_unity_instance")]
    public static class IdentifyUnityInstanceTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var activeScene = EditorSceneManager.GetActiveScene();
                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var projectName = Path.GetFileName(projectRoot);
                
                // Try to infer project type from scene path and project structure
                string projectType = "Unknown";
                if (activeScene.path.Contains("Space4x") || activeScene.path.Contains("Space4X") || 
                    projectRoot.Contains("Space4x") || projectRoot.Contains("Space4X"))
                {
                    projectType = "Space4x";
                }
                else if (activeScene.path.Contains("VFX") || activeScene.path.Contains("Visual Effect Graph") ||
                         projectRoot.Contains("VFXplayground") || projectRoot.Contains("VFXPlayground"))
                {
                    projectType = "VFXplayground";
                }
                else if (activeScene.path.Contains("Godgame") || projectRoot.Contains("Godgame"))
                {
                    projectType = "Godgame";
                }
                
                // Check scene hierarchy for project-specific GameObjects
                var hierarchy = EditorSceneManager.GetActiveScene().GetRootGameObjects();
                bool hasSpace4XContent = false;
                bool hasVFXContent = false;
                
                foreach (var go in hierarchy)
                {
                    if (go.name.Contains("MiningDemoSetup") || go.name.Contains("Carrier") || go.name.Contains("Asteroid"))
                    {
                        hasSpace4XContent = true;
                    }
                    if (go.name.Contains("VFX") || go.GetComponent<UnityEngine.VFX.VisualEffect>() != null)
                    {
                        hasVFXContent = true;
                    }
                }
                
                if (hasSpace4XContent && projectType == "Unknown")
                {
                    projectType = "Space4x";
                }
                if (hasVFXContent && projectType == "Unknown")
                {
                    projectType = "VFXplayground";
                }
                
                return Response.Success("Unity instance identified", new
                {
                    projectName = projectName,
                    projectType = projectType,
                    projectRoot = projectRoot,
                    activeScene = new
                    {
                        name = activeScene.name,
                        path = activeScene.path,
                        isLoaded = activeScene.isLoaded,
                        isDirty = activeScene.isDirty,
                        rootCount = hierarchy.Length
                    },
                    unityVersion = Application.unityVersion,
                    hasSpace4XContent = hasSpace4XContent,
                    hasVFXContent = hasVFXContent
                });
            }
            catch (System.Exception ex)
            {
                return Response.Error($"Failed to identify Unity instance: {ex.Message}");
            }
        }
    }
}

