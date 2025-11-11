using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_texture")]
    public static class CreateTextureTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string texturePath = @params["texture_path"]?.ToString();
                int width = @params["width"]?.ToObject<int>() ?? 256;
                int height = @params["height"]?.ToObject<int>() ?? 256;
                string textureFormat = @params["texture_format"]?.ToString() ?? "RGBA32";
                bool replaceExisting = @params["replace_existing"]?.ToObject<bool>() ?? false;
                
                if (string.IsNullOrEmpty(texturePath))
                {
                    return Response.Error("texture_path is required");
                }
                
                // Ensure path has .png extension (default)
                if (!texturePath.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) &&
                    !texturePath.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase) &&
                    !texturePath.EndsWith(".tga", System.StringComparison.OrdinalIgnoreCase))
                {
                    texturePath += ".png";
                }
                
                // Check if texture already exists
                if (File.Exists(texturePath) && !replaceExisting)
                {
                    return Response.Error($"Texture already exists at {texturePath}. Set replace_existing=true to overwrite.");
                }
                
                // Validate dimensions
                if (width <= 0 || height <= 0 || width > 8192 || height > 8192)
                {
                    return Response.Error($"Invalid dimensions: {width}x{height}. Must be between 1x1 and 8192x8192.");
                }
                
                // Parse texture format
                TextureFormat format = TextureFormat.RGBA32;
                if (!Enum.TryParse<TextureFormat>(textureFormat, out format))
                {
                    return Response.Error($"Invalid texture format: {textureFormat}. Use a valid TextureFormat enum value.");
                }
                
                // Create placeholder texture (solid color)
                Color fillColor = Color.white;
                if (@params["fill_color"] != null)
                {
                    var colorObj = @params["fill_color"];
                    float r = colorObj["r"]?.ToObject<float>() ?? 1f;
                    float g = colorObj["g"]?.ToObject<float>() ?? 1f;
                    float b = colorObj["b"]?.ToObject<float>() ?? 1f;
                    float a = colorObj["a"]?.ToObject<float>() ?? 1f;
                    fillColor = new Color(r, g, b, a);
                }
                
                // Create texture
                Texture2D texture = new Texture2D(width, height, format, false);
                Color[] pixels = new Color[width * height];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = fillColor;
                }
                texture.SetPixels(pixels);
                texture.Apply();
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(texturePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Encode and save texture
                byte[] bytes = texture.EncodeToPNG();
                File.WriteAllBytes(texturePath, bytes);
                
                // Import the texture
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh();
                
                // Clean up
                UnityEngine.Object.DestroyImmediate(texture);
                
                return Response.Success($"Texture created successfully", new
                {
                    texturePath = texturePath,
                    width = width,
                    height = height,
                    format = textureFormat
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create texture: {ex.Message}");
            }
        }
    }
}

