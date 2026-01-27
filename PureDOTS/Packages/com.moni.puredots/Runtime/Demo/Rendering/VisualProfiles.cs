#if PUREDOTS_SCENARIO && PUREDOTS_LEGACY_SCENARIO_ASM
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace PureDOTS.LegacyScenario.Rendering
{
    /// <summary>
    /// Visual intent marker used by gameplay/scenario systems to request a specific debug visual.
    /// A separate system translates this intent into renderable components.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "VisualProfile")]
    public struct VisualProfile : IComponentData
    {
        public VisualProfileId Id;
    }

    /// <summary>
    /// Enumerates debug visual profiles. Extend as needed for gameplay visuals.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "VisualProfileId")]
    public enum VisualProfileId : ushort
    {
        None = 0,

        // Debug/legacy scenario
        DebugVillager,
        DebugHome,
        DebugWork,
        DebugAsteroid,
        DebugCarrier,
        DebugMiner,
    }

    /// <summary>
    /// Blob catalog mapping VisualProfileId to mesh/material indices and base scale.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "VisualProfileCatalogBlob")]
    public struct VisualProfileCatalogBlob
    {
        public BlobArray<VisualProfileEntry> Entries;
    }

    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "VisualProfileEntry")]
    public struct VisualProfileEntry
    {
        public ushort MeshIndex;
        public ushort MaterialIndex;
        public float  BaseScale;
    }

    /// <summary>
    /// Singleton pointing at the visual profile catalog blob.
    /// </summary>
    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "VisualProfileCatalog")]
    public struct VisualProfileCatalog : IComponentData
    {
        public BlobAssetReference<VisualProfileCatalogBlob> Blob;
    }

    /// <summary>
    /// Legacy scenario bootstrap that builds a debug visual profile catalog for scenario entities.
    ///
    /// IMPORTANT: This is an example implementation for PureDOTS testing.
    /// Real games should use game-specific render catalogs and keys.
    ///
    /// Only runs when legacy scenario gates are enabled.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "VisualProfileBootstrapSystem")]
    public partial struct VisualProfileBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            if (SystemAPI.HasSingleton<VisualProfileCatalog>())
                return;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<VisualProfileCatalogBlob>();

            var entries = builder.Allocate(ref root.Entries, (int)VisualProfileId.DebugMiner + 1);

            // Inline assignments - no lambda, no captured ref locals
            entries[(int)VisualProfileId.DebugVillager] = new VisualProfileEntry
            {
                MeshIndex = ScenarioMeshIndices.VillageVillagerMeshIndex,
                MaterialIndex = ScenarioMeshIndices.ScenarioMaterialIndex,
                BaseScale = 1f
            };

            entries[(int)VisualProfileId.DebugHome] = new VisualProfileEntry
            {
                MeshIndex = ScenarioMeshIndices.VillageHomeMeshIndex,
                MaterialIndex = ScenarioMeshIndices.ScenarioMaterialIndex,
                BaseScale = 1f
            };

            entries[(int)VisualProfileId.DebugWork] = new VisualProfileEntry
            {
                MeshIndex = ScenarioMeshIndices.VillageWorkMeshIndex,
                MaterialIndex = ScenarioMeshIndices.ScenarioMaterialIndex,
                BaseScale = 1f
            };

            entries[(int)VisualProfileId.DebugAsteroid] = new VisualProfileEntry
            {
                MeshIndex = ScenarioMeshIndices.VillageVillagerMeshIndex,
                MaterialIndex = ScenarioMeshIndices.ScenarioMaterialIndex,
                BaseScale = 1f
            };

            entries[(int)VisualProfileId.DebugCarrier] = new VisualProfileEntry
            {
                MeshIndex = ScenarioMeshIndices.VillageVillagerMeshIndex,
                MaterialIndex = ScenarioMeshIndices.ScenarioMaterialIndex,
                BaseScale = 1f
            };

            entries[(int)VisualProfileId.DebugMiner] = new VisualProfileEntry
            {
                MeshIndex = ScenarioMeshIndices.VillageVillagerMeshIndex,
                MaterialIndex = ScenarioMeshIndices.ScenarioMaterialIndex,
                BaseScale = 1f
            };

            var blob = builder.CreateBlobAssetReference<VisualProfileCatalogBlob>(Allocator.Persistent);
            builder.Dispose();

            var e = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(e, new VisualProfileCatalog { Blob = blob });
        }

        public void OnUpdate(ref SystemState state)
        {
            state.Enabled = false;
        }
    }

    /// <summary>
    /// Legacy scenario system that assigns render components to entities with VisualProfile components.
    ///
    /// IMPORTANT: This is an example implementation for PureDOTS testing.
    /// Real games should implement proper render assignment systems using RenderKeys
    /// and game-specific render catalogs.
    ///
    /// Only runs when legacy scenario gates are enabled.
#if UNITY_EDITOR
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(SharedRenderBootstrap))]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [MovedFrom(true, "PureDOTS.Demo.Rendering", null, "AssignVisualsSystem")]
    public partial struct AssignVisualsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!LegacyScenarioGate.IsEnabled)
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<RenderMeshArraySingleton>();
            state.RequireForUpdate<VisualProfileCatalog>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;

            var renderQuery = em.CreateEntityQuery(ComponentType.ReadOnly<RenderMeshArraySingleton>());
            if (renderQuery.IsEmptyIgnoreFilter)
                return;

            var renderMeshArray = em.GetSharedComponentManaged<RenderMeshArraySingleton>(renderQuery.GetSingletonEntity()).Value;
            if (renderMeshArray == null)
                return;

            ref var catalog = ref SystemAPI.GetSingleton<VisualProfileCatalog>().Blob.Value;
            int meshCount = renderMeshArray.MeshReferences?.Length ?? 0;
            int materialCount = renderMeshArray.MaterialReferences?.Length ?? 0;

            // Guard: invalid array means MaterialMeshInfo would encode -1 indices -> Entities Graphics Key:-1 error.
            if (meshCount <= 0 || materialCount <= 0)
            {
                Debug.LogWarning("[AssignVisualsSystem] RenderMeshArraySingleton has no meshes/materials; skipping MaterialMeshInfo assignment.");
                return;
            }

            var assignQuery = SystemAPI.QueryBuilder()
                .WithAll<VisualProfile, LocalTransform>()
                .WithNone<MaterialMeshInfo>()
                .Build();

            using var entities = assignQuery.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
                return;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var profile = em.GetComponentData<VisualProfile>(entity);

                var id = profile.Id;
                if ((int)id >= catalog.Entries.Length)
                    continue;

                ref readonly var entry = ref catalog.Entries[(int)id];

                // Only add valid indices; skip bad catalog rows to avoid -1 encodings.
                if (entry.MeshIndex >= meshCount || entry.MaterialIndex >= materialCount)
                {
                    Debug.LogWarning($"[AssignVisualsSystem] Skipping VisualProfile {id} due to out-of-range mesh/material index ({entry.MeshIndex}/{entry.MaterialIndex}) for counts {meshCount}/{materialCount}.");
                    continue;
                }

                var mmi = MaterialMeshInfo.FromRenderMeshArrayIndices(entry.MeshIndex, entry.MaterialIndex);
                ecb.AddComponent(entity, mmi);

                // No WorldRenderBounds here - Entities Graphics can handle bounds later

                if (entry.BaseScale > 0f && em.HasComponent<LocalTransform>(entity))
                {
                    var xf = em.GetComponentData<LocalTransform>(entity);
                    if (math.abs(xf.Scale - entry.BaseScale) > 0.0001f)
                    {
                        xf.Scale = entry.BaseScale;
                        ecb.SetComponent(entity, xf);
                    }
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }
#endif
}

#endif
