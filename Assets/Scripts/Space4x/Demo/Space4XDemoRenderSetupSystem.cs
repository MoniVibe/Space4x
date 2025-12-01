using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using PureDOTS.Demo.Rendering;

namespace Space4X.Demo
{
    /// <summary>
    /// Populates the PureDOTS RenderMeshArraySingleton with actual meshes and materials
    /// so that OrbitCubeSystem and other demo systems can render entities.
    /// 
    /// This system runs after SharedRenderBootstrap creates the empty singleton,
    /// and replaces it with a populated RenderMeshArray containing cube meshes.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SharedRenderBootstrap))]
    public partial struct Space4XDemoRenderSetupSystem : ISystem
    {
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderMeshArraySingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var worldName = state.WorldUnmanaged.Name.ToString();
            var em = state.EntityManager;

            // Find the RenderMeshArraySingleton entity via an EntityQuery
            var renderSingletonQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<RenderMeshArraySingleton>());

            if (renderSingletonQuery.IsEmptyIgnoreFilter)
            {
                Debug.Log(
                    $"[Space4XDemoRenderSetupSystem] ({worldName}) No RenderMeshArraySingleton found; skipping setup.");
                return;
            }

            var singletonEntity = renderSingletonQuery.GetSingletonEntity();

            // Create cube mesh from primitive
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cubeMesh = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(tmp);

            // Create materials with URP Simple Lit shader (matching PureDOTS expectation)
            // We need at least 2 materials: one for debug cube (magenta) and at least 1 for orbit cubes
            // Creating 5 materials total: 1 debug + 4 orbit colors
            var debugMaterial = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"))
            {
                enableInstancing = true
            };
            debugMaterial.SetColor("_BaseColor", Color.magenta); // Bright magenta for debug cube

            var orbitMaterials = new Material[]
            {
                new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { enableInstancing = true },
                new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { enableInstancing = true },
                new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { enableInstancing = true },
                new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { enableInstancing = true }
            };

            // Set orbit cube colors: red, green, blue, yellow
            orbitMaterials[0].SetColor("_BaseColor", Color.red);
            orbitMaterials[1].SetColor("_BaseColor", Color.green);
            orbitMaterials[2].SetColor("_BaseColor", Color.blue);
            orbitMaterials[3].SetColor("_BaseColor", Color.yellow);

            // Combine all materials: debug at index 0, orbit colors at 1-4
            var allMaterials = new Material[5];
            allMaterials[0] = debugMaterial;
            for (int i = 0; i < orbitMaterials.Length; i++)
            {
                allMaterials[1 + i] = orbitMaterials[i];
            }

            // Build RenderMeshArray with meshes at indices 0-3 (as expected by DemoMeshIndices)
            // Mesh index 3 (VillageVillagerMeshIndex) is used for orbit cubes
            // We need at least 4 meshes: indices 0, 1, 2, 3 (even if some are duplicates)
            var meshes = new[] { cubeMesh, cubeMesh, cubeMesh, cubeMesh }; // 4 meshes at indices 0-3
            var renderMeshArray = new RenderMeshArray(
                allMaterials, // 5 materials: index 0 (debug), indices 1-4 (orbit colors)
                meshes
            );

            // Replace the empty RenderMeshArray with our populated one
            em.SetSharedComponentManaged(singletonEntity, new RenderMeshArraySingleton
            {
                Value = renderMeshArray
            });

            Debug.Log(
                $"[Space4XDemoRenderSetupSystem] ({worldName}) Populated RenderMeshArray with " +
                $"Meshes={meshes.Length}, Materials={allMaterials.Length}, Shader=Universal Render Pipeline/Simple Lit.");
        }
    }
}

