using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using UnityEngine;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Safety net for Entities Graphics:
    /// - Any entity with RenderKey + MaterialMeshInfo where Material or Mesh is negative
    ///   will have MaterialMeshInfo removed before the presentation phase.
    /// - This prevents EmitDrawCommandsJob from ever seeing -1 keys.
    ///
    /// NOTE:
    /// - This is a guard, not the primary render pipeline.
    ///   ApplyRenderCatalogSystem is still responsible for assigning valid MaterialMeshInfo.
    /// </summary>
    [UpdateInGroup(typeof(Space4XRenderSystemGroup))]
    [UpdateAfter(typeof(ApplyRenderCatalogSystem))]
    public partial struct StripInvalidMaterialMeshInfoSystem : ISystem
    {
        private EntityQuery _invalidQuery;

        public void OnCreate(ref SystemState state)
        {
            _invalidQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<RenderKey>(),
                ComponentType.ReadOnly<MaterialMeshInfo>());

            state.RequireForUpdate<RenderKey>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_invalidQuery.IsEmptyIgnoreFilter)
                return;

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            int invalidCount = 0;

            foreach (var (mmi, entity) in SystemAPI
                         .Query<RefRO<MaterialMeshInfo>>()
                         .WithAll<RenderKey>()
                         .WithEntityAccess())
            {
                var materialIndex = mmi.ValueRO.Material;
                var meshIndex     = mmi.ValueRO.Mesh;

                // Default struct value is all zeros. Valid entries use negative static indices
                // (RenderMeshArray) or positive Batch IDs. Only strip the uninitialized case.
                if (materialIndex != 0 || meshIndex != 0)
                    continue;

                invalidCount++;
                ecb.RemoveComponent<MaterialMeshInfo>(entity);
            }


#if UNITY_EDITOR
            if (invalidCount > 0)
            {
                LogRemoved(invalidCount);
            }
#endif

            ecb.Playback(em);
            ecb.Dispose();
        }

#if UNITY_EDITOR
        [BurstDiscard]
        private static void LogRemoved(int invalidCount)
        {
            Debug.LogWarning(
                $"[StripInvalidMaterialMeshInfoSystem] Removed MaterialMeshInfo from {invalidCount} entity(ies) " +
                "because MaterialMeshInfo was left at its default (0,0). Check catalog assignment.");
        }
#endif
    }
}
