using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Shared.Demo;

namespace Space4X.Presentation
{
    /// <summary>
    /// Visual profile ID enum for debug visual types.
    /// </summary>
    public enum VisualProfileId : byte
    {
        DebugVillager = 0,
        DebugHome = 1,
        DebugWork = 2,
        DebugAsteroid = 3,
        DebugCarrier = 4,
        DebugMiner = 5
    }

    /// <summary>
    /// Blob struct for a single visual profile entry.
    /// </summary>
    public struct VisualProfileEntry
    {
        public int MeshIndex;
        public int MaterialIndex;
        public float BaseScale;
    }

    /// <summary>
    /// Blob catalog root containing all visual profile entries.
    /// </summary>
    public struct VisualProfileCatalogBlob
    {
        public BlobArray<VisualProfileEntry> Entries;
    }

    /// <summary>
    /// Singleton component storing the visual profile catalog blob.
    /// </summary>
    public struct VisualProfileCatalog : IComponentData
    {
        public BlobAssetReference<VisualProfileCatalogBlob> CatalogBlob;
    }

    /// <summary>
    /// Component that references a visual profile ID for an entity.
    /// </summary>
    public struct VisualProfileIdComponent : IComponentData
    {
        public VisualProfileId ProfileId;
    }

    /// <summary>
    /// Bootstrap system that creates the visual profile catalog blob.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct VisualProfileBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // Build blob data
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<VisualProfileCatalogBlob>();

            // Allocate entries using the highest enum + 1
            var entries = builder.Allocate(ref root.Entries, (int)VisualProfileId.DebugMiner + 1);

            // Inline assignments - no lambda, no captured ref locals
            entries[(int)VisualProfileId.DebugVillager] = new VisualProfileEntry
            {
                MeshIndex = 0,
                MaterialIndex = 0,
                BaseScale = 1f
            };

            entries[(int)VisualProfileId.DebugHome] = new VisualProfileEntry
            {
                MeshIndex = 0,
                MaterialIndex = 1,
                BaseScale = 1.2f
            };

            entries[(int)VisualProfileId.DebugWork] = new VisualProfileEntry
            {
                MeshIndex = 0,
                MaterialIndex = 2,
                BaseScale = 1.2f
            };

            entries[(int)VisualProfileId.DebugAsteroid] = new VisualProfileEntry
            {
                MeshIndex = 0,
                MaterialIndex = 0,
                BaseScale = 1.5f
            };

            entries[(int)VisualProfileId.DebugCarrier] = new VisualProfileEntry
            {
                MeshIndex = 0,
                MaterialIndex = 0,
                BaseScale = 2f
            };

            entries[(int)VisualProfileId.DebugMiner] = new VisualProfileEntry
            {
                MeshIndex = 0,
                MaterialIndex = 0,
                BaseScale = 1f
            };

            var blobAsset = builder.CreateBlobAssetReference<VisualProfileCatalogBlob>(Allocator.Persistent);
            builder.Dispose();

            // Create singleton entity with catalog
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<VisualProfileCatalog>(entity);
            state.EntityManager.SetComponentData(entity, new VisualProfileCatalog
            {
                CatalogBlob = blobAsset
            });
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Bootstrap runs once in OnCreate, nothing to do here
        }
    }

    /// <summary>
    /// System that assigns visual components (MaterialMeshInfo) to entities based on their visual profile.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(VisualProfileBootstrapSystem))]
    public partial struct AssignVisualsSystem : ISystem
    {
        private ComponentLookup<VisualProfileCatalog> _catalogLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _catalogLookup = state.GetComponentLookup<VisualProfileCatalog>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check if catalog exists
            if (!SystemAPI.HasSingleton<VisualProfileCatalog>())
            {
                return;
            }

            // Check if DemoRenderReady exists (for render components)
            if (!SystemAPI.HasSingleton<DemoRenderReady>())
            {
                // Demoted to debug log to reduce noise
                // UnityEngine.Debug.LogWarning("[AssignVisualsSystem] DemoRenderReady singleton missing; skipping visual assignment.");
                return; // Can't add render components without RenderMeshArray
            }

            _catalogLookup.Update(ref state);

            var catalogEntity = SystemAPI.GetSingletonEntity<VisualProfileCatalog>();
            var catalog = _catalogLookup[catalogEntity];
            var catalogBlob = catalog.CatalogBlob;

            var renderReadyEntity = SystemAPI.GetSingletonEntity<DemoRenderReady>();
            var renderMeshArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(renderReadyEntity);

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Query entities that have VisualProfileIdComponent but no MaterialMeshInfo
            foreach (var (profileId, entity) in SystemAPI
                         .Query<RefRO<VisualProfileIdComponent>>()
                         .WithNone<MaterialMeshInfo>()
                         .WithEntityAccess())
            {
                var profileIndex = (int)profileId.ValueRO.ProfileId;
                if (profileIndex < 0 || profileIndex >= catalogBlob.Value.Entries.Length)
                {
                    continue;
                }

                var entry = catalogBlob.Value.Entries[profileIndex];

                // Assign MaterialMeshInfo using the same pattern as Space4XPresentationLifecycleSystem
                ecb.AddSharedComponentManaged(entity, renderMeshArray);
                ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(entry.MeshIndex, entry.MaterialIndex));

                // Apply base scale if LocalTransform exists
                if (em.HasComponent<Unity.Transforms.LocalTransform>(entity))
                {
                    var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(entity);
                    transform.Scale = entry.BaseScale;
                    ecb.SetComponent(entity, transform);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

