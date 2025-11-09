using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;
using System.Collections.Generic;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_mesh")]
    public static class CreateMeshTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string meshPath = @params["mesh_path"]?.ToString();
                string meshType = @params["mesh_type"]?.ToString() ?? "plane";
                bool replaceExisting = @params["replace_existing"]?.ToObject<bool>() ?? false;
                
                if (string.IsNullOrEmpty(meshPath))
                {
                    return Response.Error("mesh_path is required");
                }
                
                // Ensure path has .asset extension
                if (!meshPath.EndsWith(".asset", System.StringComparison.OrdinalIgnoreCase))
                {
                    meshPath += ".asset";
                }
                
                // Check if mesh already exists
                if (File.Exists(meshPath) && !replaceExisting)
                {
                    return Response.Error($"Mesh already exists at {meshPath}. Set replace_existing=true to overwrite.");
                }
                
                // Create mesh based on type
                Mesh mesh = null;
                meshType = meshType.ToLower();
                
                switch (meshType)
                {
                    case "plane":
                        mesh = CreatePlane();
                        break;
                    case "cube":
                        mesh = CreateCube();
                        break;
                    case "sphere":
                        mesh = CreateSphere();
                        break;
                    case "cylinder":
                        mesh = CreateCylinder();
                        break;
                    default:
                        return Response.Error($"Unknown mesh type: {meshType}. Supported types: plane, cube, sphere, cylinder");
                }
                
                if (mesh == null)
                {
                    return Response.Error("Failed to create mesh");
                }
                
                mesh.name = Path.GetFileNameWithoutExtension(meshPath);
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(meshPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save mesh asset
                AssetDatabase.CreateAsset(mesh, meshPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return Response.Success($"Mesh created successfully", new
                {
                    meshPath = meshPath,
                    meshType = meshType,
                    meshName = mesh.name,
                    vertexCount = mesh.vertexCount,
                    triangleCount = mesh.triangles.Length / 3
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create mesh: {ex.Message}");
            }
        }
        
        private static Mesh CreatePlane()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, 0.5f),
                new Vector3(-0.5f, 0, 0.5f)
            };
            mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(1, 1),
                new Vector2(0, 1)
            };
            mesh.normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };
            mesh.RecalculateBounds();
            return mesh;
        }
        
        private static Mesh CreateCube()
        {
            Mesh mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                // Front face
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f),
                // Back face
                new Vector3(0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                // Top face
                new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, 0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                // Bottom face
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                // Right face
                new Vector3(0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0.5f),
                // Left face
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, -0.5f)
            };
            mesh.triangles = new int[]
            {
                0, 2, 1, 0, 3, 2,      // Front
                4, 6, 5, 4, 7, 6,      // Back
                8, 10, 9, 8, 11, 10,   // Top
                12, 14, 13, 12, 15, 14, // Bottom
                16, 18, 17, 16, 19, 18, // Right
                20, 22, 21, 20, 23, 22  // Left
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        
        private static Mesh CreateSphere()
        {
            // Simple sphere approximation
            int segments = 16;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            
            // Generate vertices
            for (int y = 0; y <= segments; y++)
            {
                float theta = (float)y / segments * Mathf.PI;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);
                
                for (int x = 0; x <= segments; x++)
                {
                    float phi = (float)x / segments * 2f * Mathf.PI;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);
                    
                    Vector3 vertex = new Vector3(
                        cosPhi * sinTheta,
                        cosTheta,
                        sinPhi * sinTheta
                    ) * 0.5f;
                    vertices.Add(vertex);
                }
            }
            
            // Generate triangles
            for (int y = 0; y < segments; y++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int current = y * (segments + 1) + x;
                    int next = current + segments + 1;
                    
                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);
                    
                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }
            
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        
        private static Mesh CreateCylinder()
        {
            int segments = 16;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            
            // Top and bottom centers
            vertices.Add(new Vector3(0, 0.5f, 0)); // Top center
            vertices.Add(new Vector3(0, -0.5f, 0)); // Bottom center
            
            // Generate side vertices
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * 2f * Mathf.PI;
                float x = Mathf.Cos(angle) * 0.5f;
                float z = Mathf.Sin(angle) * 0.5f;
                
                vertices.Add(new Vector3(x, 0.5f, z)); // Top ring
                vertices.Add(new Vector3(x, -0.5f, z)); // Bottom ring
            }
            
            // Top cap
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(0);
                triangles.Add(2 + i * 2);
                triangles.Add(2 + ((i + 1) % (segments + 1)) * 2);
            }
            
            // Bottom cap
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(1);
                triangles.Add(2 + ((i + 1) % (segments + 1)) * 2 + 1);
                triangles.Add(2 + i * 2 + 1);
            }
            
            // Side faces
            for (int i = 0; i < segments; i++)
            {
                int top1 = 2 + i * 2;
                int top2 = 2 + ((i + 1) % (segments + 1)) * 2;
                int bottom1 = 2 + i * 2 + 1;
                int bottom2 = 2 + ((i + 1) % (segments + 1)) * 2 + 1;
                
                triangles.Add(top1);
                triangles.Add(bottom1);
                triangles.Add(top2);
                
                triangles.Add(top2);
                triangles.Add(bottom1);
                triangles.Add(bottom2);
            }
            
            Mesh mesh = new Mesh();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

