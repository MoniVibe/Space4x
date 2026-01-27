using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PureDOTS.Authoring
{
    /// <summary>
    /// Authoring component for terrain/ground plane in DOTS.
    /// Creates a large flat ground mesh for visibility of movement.
    /// Part of PureDOTS foundation - can be used by any project.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerrainAuthoring : MonoBehaviour
    {
        [Header("Terrain Size")]
        [Tooltip("Size of the terrain plane (X and Z dimensions)")]
        public float2 size = new float2(100f, 100f);

        [Header("Subdivisions")]
        [Tooltip("Number of subdivisions for the terrain mesh (more = smoother but more vertices)")]
        [Range(1, 50)]
        public int subdivisions = 10;

        [Header("Material")]
        [Tooltip("Material to use for rendering the terrain. If null, a simple URP/Lit material is created.")]
        public Material terrainMaterial;

        static Material s_FallbackMaterial;

        void Reset()
        {
            EnsureMesh();
            EnsureMaterial();
        }

        void Awake()
        {
            EnsureMesh();
            EnsureMaterial();
        }

        void OnValidate()
        {
            EnsureMesh();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EnsureMaterial();
            }
#endif
        }

        void EnsureMesh()
        {
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError($"[TerrainAuthoring] MeshFilter component is missing on '{gameObject.name}'. Adding it now.");
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
            
            int clampedSubdivisions = math.max(1, subdivisions);
            var mesh = meshFilter.sharedMesh;

            bool needsNewMesh = mesh == null || mesh.vertexCount != (clampedSubdivisions + 1) * (clampedSubdivisions + 1);
            if (needsNewMesh)
            {
                if (mesh != null)
#if UNITY_EDITOR
                    DestroyImmediate(mesh);
#else
                    Destroy(mesh);
#endif

                mesh = new Mesh
                {
                    name = $"{name}_TerrainMesh"
                };
            }
            else
            {
                mesh.Clear();
            }

            PopulateMesh(mesh, size, clampedSubdivisions);
            meshFilter.sharedMesh = mesh;
        }

        void EnsureMaterial()
        {
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogError($"[TerrainAuthoring] MeshRenderer component is missing on '{gameObject.name}'. Adding it now.");
                renderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (terrainMaterial == null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    var editorMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Ground_Default.mat");
                    if (editorMaterial != null)
                    {
                        terrainMaterial = editorMaterial;
                    }
                }
#endif

                if (terrainMaterial == null)
                {
                    if (s_FallbackMaterial == null)
                    {
                        var shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (shader == null)
                        {
                            shader = Shader.Find("Standard");
                        }

                        s_FallbackMaterial = new Material(shader)
                        {
                            name = "GeneratedTerrainMaterial",
                            color = new Color(0.4f, 0.5f, 0.3f)
                        };
                        s_FallbackMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    }

                    terrainMaterial = s_FallbackMaterial;
                }
            }

            if (renderer.sharedMaterial != terrainMaterial)
            {
                renderer.sharedMaterial = terrainMaterial;
            }
        }

        static void PopulateMesh(Mesh mesh, float2 planeSize, int planeSubdivisions)
        {
            float width = math.max(0.1f, planeSize.x);
            float depth = math.max(0.1f, planeSize.y);
            int vertsX = planeSubdivisions + 1;
            int vertsZ = planeSubdivisions + 1;
            int totalVertices = vertsX * vertsZ;

            var vertices = new Vector3[totalVertices];
            var normals = new Vector3[totalVertices];
            var uvs = new Vector2[totalVertices];

            float stepX = width / planeSubdivisions;
            float stepZ = depth / planeSubdivisions;
            float halfWidth = width * 0.5f;
            float halfDepth = depth * 0.5f;

            for (int z = 0; z < vertsZ; z++)
            {
                for (int x = 0; x < vertsX; x++)
                {
                    int index = z * vertsX + x;
                    float offsetX = x * stepX - halfWidth;
                    float offsetZ = z * stepZ - halfDepth;
                    vertices[index] = new Vector3(offsetX, 0f, offsetZ);
                    normals[index] = Vector3.up;
                    uvs[index] = new Vector2((float)x / planeSubdivisions, (float)z / planeSubdivisions);
                }
            }

            int trianglesPerQuad = 6;
            int quadCount = planeSubdivisions * planeSubdivisions;
            var triangles = new int[quadCount * trianglesPerQuad];
            int triIndex = 0;

            for (int z = 0; z < planeSubdivisions; z++)
            {
                for (int x = 0; x < planeSubdivisions; x++)
                {
                    int bottomLeft = z * vertsX + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + vertsX;
                    int topRight = topLeft + 1;

                    triangles[triIndex++] = bottomLeft;
                    triangles[triIndex++] = topLeft;
                    triangles[triIndex++] = bottomRight;

                    triangles[triIndex++] = bottomRight;
                    triangles[triIndex++] = topLeft;
                    triangles[triIndex++] = topRight;
                }
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
        }
    }
}

