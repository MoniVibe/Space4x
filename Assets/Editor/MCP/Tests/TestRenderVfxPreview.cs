using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace PureDOTS.Editor.MCP.Tests
{
    /// <summary>
    /// Quick menu test for render_vfx_preview MCP tool.
    /// </summary>
    public static class TestRenderVfxPreview
    {
        private const string GraphPath = "Assets/VFX/BlueSnowOrb.vfx";

        [MenuItem("Tools/MCP/Test Render VFX Preview")]
        public static void Test()
        {
            Debug.Log("=== Testing render_vfx_preview ===");

            var parameters = new JObject
            {
                ["Smoke Rate"] = 60f,
                ["Flames Rate"] = 0f,
                ["Sparks Rate"] = 1200f,
                ["Smoke Lifetime (Min/Max)"] = new JArray { 0.6f, 1.4f }
            };

            var request = new JObject
            {
                ["graph_path"] = GraphPath,
                ["size"] = 320,
                ["seconds"] = 0.5f,
                ["fps"] = 16,
                ["frames"] = 1,
                ["params"] = parameters
            };

            var result = RenderVfxPreviewTool.HandleCommand(request);

            if (result is JObject jObj)
            {
                var success = jObj["success"]?.ToObject<bool>() == true;
                Debug.Log($"Success: {success}");
                Debug.Log(jObj.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            else
            {
                Debug.LogError($"Unexpected result from render_vfx_preview: {result}");
            }
        }
    }
}

