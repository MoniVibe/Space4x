using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace PureDOTS.Presentation.Runtime
{
    public static class PresentationRenderFactory
    {
        public readonly struct Assets
        {
            public readonly RenderMeshArray RenderArray;
            public readonly BlobAssetReference<PresentationRenderCatalogBlob> Catalog;

            public Assets(RenderMeshArray renderArray, BlobAssetReference<PresentationRenderCatalogBlob> catalog)
            {
                RenderArray = renderArray;
                Catalog = catalog;
            }
        }

        private const float DefaultDiscHeight = 0.05f;

        public static Assets CreateFallbackAssets()
        {
            var prototypeCount = (int)PresentationPrototype.Count;
            var meshes = new Mesh[prototypeCount];
            var materials = new Material[prototypeCount];

            using var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<PresentationRenderCatalogBlob>();
            var entries = builder.Allocate(ref root.Prototypes, prototypeCount);

            var cube = MeshFactory.CreateUnitCube("PureDOTS_Cube");
            var cross = MeshFactory.CreateCross("PureDOTS_Cross");
            var disc = MeshFactory.CreateDisc("PureDOTS_Disc", 24, DefaultDiscHeight);
            var octa = MeshFactory.CreateOctahedron("PureDOTS_Octahedron");

            SetupPrototype(PresentationPrototype.Villager, ref entries, meshes, materials,
                cross, MaterialFactory.CreateColorMaterial("Villager_Mat", new Color(0.78f, 0.66f, 0.51f)),
                new float3(0.6f, 1.8f, 0.6f),
                new float3(0.35f, 0.9f, 0.35f),
                new float4(0.78f, 0.66f, 0.51f, 1f));

            SetupPrototype(PresentationPrototype.ResourceNode, ref entries, meshes, materials,
                cube, MaterialFactory.CreateColorMaterial("ResourceNode_Mat", new Color(0.52f, 0.84f, 0.37f)),
                new float3(0.9f),
                new float3(0.5f),
                new float4(0.52f, 0.84f, 0.37f, 1f));

            SetupPrototype(PresentationPrototype.Building, ref entries, meshes, materials,
                cube, MaterialFactory.CreateColorMaterial("Building_Mat", new Color(0.55f, 0.55f, 0.6f)),
                new float3(4f, 2.5f, 4f),
                new float3(2f, 1.25f, 2f),
                new float4(0.55f, 0.55f, 0.6f, 1f));

            SetupPrototype(PresentationPrototype.Cloud, ref entries, meshes, materials,
                disc, MaterialFactory.CreateTransparentMaterial("Cloud_Mat", new Color(0.92f, 0.94f, 0.99f, 0.85f)),
                new float3(6f, 1f, 6f),
                new float3(3f, DefaultDiscHeight * 0.5f, 3f),
                new float4(0.92f, 0.94f, 0.99f, 0.85f));

            SetupPrototype(PresentationPrototype.Vegetation, ref entries, meshes, materials,
                cross, MaterialFactory.CreateDoubleSidedMaterial("Vegetation_Mat", new Color(0.26f, 0.62f, 0.29f)),
                new float3(0.8f, 1.6f, 0.8f),
                new float3(0.5f, 0.8f, 0.5f),
                new float4(0.26f, 0.62f, 0.29f, 1f));

            SetupPrototype(PresentationPrototype.MiracleToken, ref entries, meshes, materials,
                octa, MaterialFactory.CreateEmissiveMaterial("MiracleToken_Mat", new Color(1f, 0.85f, 0.35f)),
                new float3(1.1f),
                new float3(0.6f),
                new float4(1f, 0.85f, 0.35f, 1f));

            SetupPrototype(PresentationPrototype.MiracleEffect, ref entries, meshes, materials,
                disc, MaterialFactory.CreateEmissiveMaterial("MiracleEffect_Mat", new Color(1f, 0.65f, 0.2f), transparent: true),
                new float3(2.4f, 0.4f, 2.4f),
                new float3(1.2f, DefaultDiscHeight * 0.5f, 1.2f),
                new float4(1f, 0.65f, 0.2f, 0.85f));

            SetupPrototype(PresentationPrototype.HandCursor, ref entries, meshes, materials,
                octa, MaterialFactory.CreateEmissiveMaterial("HandCursor_Mat", new Color(0.3f, 0.75f, 1f)),
                new float3(0.6f),
                new float3(0.35f),
                new float4(0.3f, 0.75f, 1f, 1f));

            SetupPrototype(PresentationPrototype.ThrowableToken, ref entries, meshes, materials,
                cube, MaterialFactory.CreateColorMaterial("Throwable_Mat", new Color(0.93f, 0.58f, 0.28f)),
                new float3(0.45f),
                new float3(0.25f),
                new float4(0.93f, 0.58f, 0.28f, 1f));

            SetupPrototype(PresentationPrototype.Chunk, ref entries, meshes, materials,
                cube, MaterialFactory.CreateColorMaterial("Chunk_Mat", new Color(0.58f, 0.42f, 0.33f)),
                new float3(0.7f),
                new float3(0.4f),
                new float4(0.58f, 0.42f, 0.33f, 1f));

            var catalog = builder.CreateBlobAssetReference<PresentationRenderCatalogBlob>(Allocator.Persistent);
            var renderArray = new RenderMeshArray(materials, meshes);

            return new Assets(renderArray, catalog);
        }

        private static void SetupPrototype(
            PresentationPrototype prototype,
            ref BlobArray<PresentationPrototypeEntry> entries,
            Mesh[] meshes,
            Material[] materials,
            Mesh mesh,
            Material material,
            float3 defaultScale,
            float3 boundsExtents,
            float4 defaultColor)
        {
            int index = (int)prototype;
            meshes[index] = mesh;
            materials[index] = material;

            entries[index] = new PresentationPrototypeEntry
            {
                MeshIndex = index,
                MaterialIndex = index,
                DefaultScale = defaultScale,
                BoundsExtents = boundsExtents,
                DefaultColor = defaultColor
            };
        }

        private static class MeshFactory
        {
            public static Mesh CreateUnitCube(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };

                var vertices = new Vector3[]
                {
                    // Front
                    new (-0.5f, -0.5f, 0.5f),
                    new (0.5f, -0.5f, 0.5f),
                    new (0.5f, 0.5f, 0.5f),
                    new (-0.5f, 0.5f, 0.5f),
                    // Back
                    new (0.5f, -0.5f, -0.5f),
                    new (-0.5f, -0.5f, -0.5f),
                    new (-0.5f, 0.5f, -0.5f),
                    new (0.5f, 0.5f, -0.5f),
                    // Left
                    new (-0.5f, -0.5f, -0.5f),
                    new (-0.5f, -0.5f, 0.5f),
                    new (-0.5f, 0.5f, 0.5f),
                    new (-0.5f, 0.5f, -0.5f),
                    // Right
                    new (0.5f, -0.5f, 0.5f),
                    new (0.5f, -0.5f, -0.5f),
                    new (0.5f, 0.5f, -0.5f),
                    new (0.5f, 0.5f, 0.5f),
                    // Top
                    new (-0.5f, 0.5f, 0.5f),
                    new (0.5f, 0.5f, 0.5f),
                    new (0.5f, 0.5f, -0.5f),
                    new (-0.5f, 0.5f, -0.5f),
                    // Bottom
                    new (-0.5f, -0.5f, -0.5f),
                    new (0.5f, -0.5f, -0.5f),
                    new (0.5f, -0.5f, 0.5f),
                    new (-0.5f, -0.5f, 0.5f)
                };

                var normals = new Vector3[]
                {
                    Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                    Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                    Vector3.left, Vector3.left, Vector3.left, Vector3.left,
                    Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                    Vector3.up, Vector3.up, Vector3.up, Vector3.up,
                    Vector3.down, Vector3.down, Vector3.down, Vector3.down
                };

                var uvs = new Vector2[]
                {
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f),
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f),
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f),
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f),
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f),
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f)
                };

                var triangles = new int[]
                {
                    0, 2, 1, 0, 3, 2,
                    4, 6, 5, 4, 7, 6,
                    8, 10, 9, 8, 11, 10,
                    12, 14, 13, 12, 15, 14,
                    16, 18, 17, 16, 19, 18,
                    20, 22, 21, 20, 23, 22
                };

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                return mesh;
            }

            public static Mesh CreateDisc(string name, int segments, float height)
            {
                segments = math.clamp(segments, 3, 64);
                var vertexCount = segments + 1;
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };

                var vertices = new Vector3[vertexCount];
                var normals = new Vector3[vertexCount];
                var uvs = new Vector2[vertexCount];

                vertices[0] = new Vector3(0f, 0f, 0f);
                normals[0] = Vector3.up;
                uvs[0] = new Vector2(0.5f, 0.5f);

                for (int i = 0; i < segments; i++)
                {
                    float angle = (math.PI * 2f * i) / segments;
                    float x = math.cos(angle) * 0.5f;
                    float z = math.sin(angle) * 0.5f;
                    vertices[i + 1] = new Vector3(x, height, z);
                    normals[i + 1] = Vector3.up;
                    uvs[i + 1] = new Vector2(x + 0.5f, z + 0.5f);
                }

                var triangles = new int[segments * 3];
                for (int i = 0; i < segments; i++)
                {
                    int current = i + 1;
                    int next = i == segments - 1 ? 1 : current + 1;
                    int triIndex = i * 3;
                    triangles[triIndex] = 0;
                    triangles[triIndex + 1] = next;
                    triangles[triIndex + 2] = current;
                }

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);

                return mesh;
            }

            public static Mesh CreateCross(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };

                var vertices = new Vector3[]
                {
                    // Quad 1 front
                    new (-0.5f, 0f, 0f),
                    new (0.5f, 0f, 0f),
                    new (0.5f, 1f, 0f),
                    new (-0.5f, 1f, 0f),
                    // Quad 1 back
                    new (-0.5f, 0f, 0f),
                    new (-0.5f, 1f, 0f),
                    new (0.5f, 1f, 0f),
                    new (0.5f, 0f, 0f),
                    // Quad 2 front
                    new (0f, 0f, -0.5f),
                    new (0f, 0f, 0.5f),
                    new (0f, 1f, 0.5f),
                    new (0f, 1f, -0.5f),
                    // Quad 2 back
                    new (0f, 0f, -0.5f),
                    new (0f, 1f, -0.5f),
                    new (0f, 1f, 0.5f),
                    new (0f, 0f, 0.5f)
                };

                var normals = new Vector3[]
                {
                    Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
                    Vector3.back, Vector3.back, Vector3.back, Vector3.back,
                    Vector3.right, Vector3.right, Vector3.right, Vector3.right,
                    Vector3.left, Vector3.left, Vector3.left, Vector3.left
                };

                var uvs = new Vector2[]
                {
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f),
                    new (0f, 0f), new (0f, 1f), new (1f, 1f), new (1f, 0f),
                    new (0f, 0f), new (1f, 0f), new (1f, 1f), new (0f, 1f),
                    new (0f, 0f), new (0f, 1f), new (1f, 1f), new (1f, 0f)
                };

                var triangles = new int[]
                {
                    0, 2, 1, 0, 3, 2,
                    4, 6, 5, 4, 7, 6,
                    8, 10, 9, 8, 11, 10,
                    12, 14, 13, 12, 15, 14
                };

                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                return mesh;
            }

            public static Mesh CreateOctahedron(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };

                var vertices = new Vector3[]
                {
                    new (0f, 0.5f, 0f),
                    new (-0.5f, 0f, 0f),
                    new (0f, 0f, 0.5f),
                    new (0.5f, 0f, 0f),
                    new (0f, 0f, -0.5f),
                    new (0f, -0.5f, 0f)
                };

                var triangles = new int[]
                {
                    0, 1, 2,
                    0, 2, 3,
                    0, 3, 4,
                    0, 4, 1,
                    5, 2, 1,
                    5, 3, 2,
                    5, 4, 3,
                    5, 1, 4
                };

                mesh.SetVertices(vertices);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                mesh.UploadMeshData(false);
                return mesh;
            }
        }

        private static class MaterialFactory
        {
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
            private const string UrpLitShader = "Universal Render Pipeline/Lit";

            public static Material CreateColorMaterial(string name, Color color)
            {
                var material = CreateBaseMaterial(name);
                material.SetColor(BaseColorId, color);
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, color * 0.05f);
                return material;
            }

            public static Material CreateTransparentMaterial(string name, Color color)
            {
                var material = CreateBaseMaterial(name);
                material.SetColor(BaseColorId, color);
                material.EnableKeyword("_EMISSION");
                material.SetColor(EmissionColorId, color * 0.1f);
                ConfigureSurface(material, true);
                material.SetFloat("_ZWrite", 0f);
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetFloat("_AlphaClip", 0f);
                material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                return material;
            }

            public static Material CreateDoubleSidedMaterial(string name, Color color)
            {
                var material = CreateColorMaterial(name, color);
                material.SetInt("_CullMode", (int)CullMode.Off);
                material.EnableKeyword("_DOUBLESIDED_ON");
                return material;
            }

            public static Material CreateEmissiveMaterial(string name, Color color, bool transparent = false)
            {
                var material = CreateBaseMaterial(name);
                material.SetColor(BaseColorId, color);
                material.SetColor(EmissionColorId, color * 0.6f);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (transparent)
                {
                    ConfigureSurface(material, true);
                    material.SetFloat("_ZWrite", 0f);
                    material.renderQueue = (int)RenderQueue.Transparent;
                    material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                }
                return material;
            }

            private static Material CreateBaseMaterial(string name)
            {
                var shader = Shader.Find(UrpLitShader);
                if (shader == null)
                {
                    throw new InvalidOperationException($"Unable to find shader '{UrpLitShader}'. Ensure URP is included in build.");
                }

                var material = new Material(shader)
                {
                    name = name,
                    hideFlags = HideFlags.HideAndDontSave,
                    enableInstancing = true
                };

                ConfigureSurface(material, false);
                return material;
            }

            private static void ConfigureSurface(Material material, bool transparent)
            {
                material.SetFloat("_Surface", transparent ? 1f : 0f);
                if (transparent)
                {
                    material.SetOverrideTag("RenderType", "Transparent");
                }
                else
                {
                    material.SetOverrideTag("RenderType", "Opaque");
                }
            }
        }
    }
}
