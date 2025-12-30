using Digger.Modules.Core.Sources;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Digger
{
    public class TextureDetector : MonoBehaviour
    {
        [Header("Targeted texture (will be filled with the name of the texture you are targetting in Play mode)")]
        public string texture = "";

        private DiggerMaster diggerMaster;
        private static  readonly List<Vector4> uvs = new List<Vector4>();

        protected void Start()
        {
            diggerMaster = FindFirstObjectByType<DiggerMaster>();
        }

        // Simple Update showing how to use TextureDetector.GetTextureIndex method
        protected void Update()
        {
            if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 500, 1 << diggerMaster.Layer))
            {
                Debug.DrawLine(transform.position, hit.point, Color.green);
                int index = GetTextureIndex(hit, out Terrain terrain);
                this.texture = $"name: {terrain.terrainData.terrainLayers[index].name} | index: {index}";
            }
        }

        /// <summary>
        /// Convert a given RaycastHit to a texture index
        /// </summary>
        /// <param name="hit">Physics RaycastHit used to determine terrain location</param>
        /// <param name="hitDiggerObjects">Has hit digger objects</param>
        /// <returns></returns>
        public static int GetTextureIndex(RaycastHit hit, out Terrain terrain)
        {
            var chunk = hit.collider.GetComponent<ChunkObject>();
            if (chunk)
            {
                terrain = chunk.Terrain;
                Mesh diggerColliderMesh = chunk.Mesh;
                int baseVertexIndex = diggerColliderMesh.triangles[hit.triangleIndex * 3];

                if (chunk.Digger.MaterialType == TerrainMaterialType.URP) {
                    return GetMeshTextureIndex(new float4[] { 
                        GetTexcoord(diggerColliderMesh, baseVertexIndex, 2),
                        GetTexcoord(diggerColliderMesh, baseVertexIndex, 3),
                        GetTexcoord(diggerColliderMesh, baseVertexIndex, 4),
                        GetTexcoord(diggerColliderMesh, baseVertexIndex, 5) 
                    });
                }

                if (chunk.Digger.MaterialType == TerrainMaterialType.HDRP)
                {
                    return GetMeshTextureIndex(new float4[] {
                        GetTexcoord(diggerColliderMesh, baseVertexIndex, 2),
                        GetTexcoord(diggerColliderMesh, baseVertexIndex, 3) 
                    });
                }

                return GetMeshTextureIndex(new float4[] {
                    GetTexcoord(diggerColliderMesh, baseVertexIndex, 1),
                    GetTexcoord(diggerColliderMesh, baseVertexIndex, 2),
                    GetTexcoord(diggerColliderMesh, baseVertexIndex, 3),
                    diggerColliderMesh.tangents[baseVertexIndex] 
                });
            }

            terrain = hit.collider.GetComponent<Terrain>();
            if (terrain)
            {
                return GetTerrainTextureIndex(hit.point, terrain);
            }

            return 0;
        }

        private static float4 GetTexcoord(Mesh mesh, int baseVertexIndex, int channel)
        {
            uvs.Clear();
            mesh.GetUVs(channel, uvs);
            return uvs[baseVertexIndex];
        }

        /// <summary>Get number of textures added to the terrain</summary>
        /// <param name="worldPos"></param>
        /// <param name="terrain">Terrain to check textures</param>
        /// <returns>Array containing the relative mix of textures on the main terrain at this world position.</returns>
        public static float[] GetTextureMix(Vector3 worldPos, Terrain terrain)
        {
            var terrainData = terrain.terrainData;
            var terrainPos = terrain.transform.position;
            // calculate which splat map cell the worldPos falls within (ignoring y)
            int mapX = (int)(((worldPos.x - terrainPos.x) / terrainData.size.x) * terrainData.alphamapWidth);
            int mapZ = (int)(((worldPos.z - terrainPos.z) / terrainData.size.z) * terrainData.alphamapHeight);
            // get the splat data for this cell as a 1x1xN 3d array (where N = number of textures)
            float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
            // extract the 3D array data to a 1D array:
            float[] cellMix = new float[splatmapData.GetUpperBound(2) + 1];
            for (int n = 0; n < cellMix.Length; n++)
            {
                cellMix[n] = splatmapData[0, 0, n];
            }
            return cellMix;
        }

        /// <summary> Get most dominant texture given a world position on a terrain </summary>
        /// <param name="worldPos"></param>
        /// <param name="terrain">Terrain to check textures</param>
        /// <returns>Zero-based index of the most dominant texture on the main terrain at this world position.</returns>
        public static int GetTerrainTextureIndex(Vector3 worldPos, Terrain terrain)
        {
            float[] mix = GetTextureMix(worldPos, terrain);
            float maxMix = 0;
            int maxIndex = 0;
            // loop through each mix value and find the maximum
            for (int n = 0; n < mix.Length; n++)
            {
                if (mix[n] > maxMix)
                {
                    maxIndex = n;
                    maxMix = mix[n];
                }
            }
            return maxIndex;
        }

        public static int GetMeshTextureIndex(float4[] controls)
        {
            int index = -1;
            float max = -1f;
            for (int dc = 0; dc < controls.Length; dc++)
            {
                float4 test = controls[dc];
                for (int df = 0; df < 4; df++)
                {
                    if (test[df] > max)
                    {
                        max = test[df];
                        index = (dc * 4) + df;
                    }
                }
            }
            return index;
        }
    }
}