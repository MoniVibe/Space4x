#if UNITY_EDITOR || DEVELOPMENT_BUILD
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using PureDOTS.Runtime.Core;
using Space4X.Rendering;
using Space4XRenderKey = Space4X.Rendering.RenderKey;

namespace Space4X.Rendering.Systems
{
    using Debug = UnityEngine.Debug;

    [UpdateInGroup(typeof(Space4XRenderSystemGroup))]
    [UpdateAfter(typeof(StripInvalidMaterialMeshInfoSystem))]
    [UpdateAfter(typeof(ApplyRenderCatalogSystem))]
    public partial struct DebugVerifyVisualsSystem : ISystem
    {
        private bool _loggedOnce;
        private int _lastInvalid;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XRenderKey>();
        }

        public void OnUpdate(ref SystemState state)
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return; // no O(N) scan in player builds
#endif
            if (RuntimeMode.IsHeadless)
            {
                state.Enabled = false;
                return;
            }

            int count = 0;
            int invalidCount = 0;

            Entity firstEntity = Entity.Null;
            Space4XRenderKey firstKey = default;
            MaterialMeshInfo firstMmi = default;
            LocalTransform firstXform = default;
            RenderBounds firstBounds = default;

            foreach (var (rk, mmi, xform, bounds, entity)
                     in SystemAPI.Query<
                            RefRO<Space4XRenderKey>,
                            RefRO<MaterialMeshInfo>,
                            RefRO<LocalTransform>,
                            RefRO<RenderBounds>>()
                         .WithEntityAccess())
            {
                count++;

                if (count == 1)
                {
                    firstEntity = entity;
                    firstKey    = rk.ValueRO;
                    firstMmi    = mmi.ValueRO;
                    firstXform  = xform.ValueRO;
                    firstBounds = bounds.ValueRO;
                }

                if (IsDefaultMaterialMeshInfo(mmi.ValueRO))
                {
                    invalidCount++;
                }
            }

            if (count == 0)
            {
                return;
            }

            if (!_loggedOnce || invalidCount != _lastInvalid)
            {
                LogCounts(
                    count,
                    invalidCount,
                    firstEntity,
                    firstKey,
                    firstMmi,
                    firstXform,
                    firstBounds);
                _loggedOnce = true;
                _lastInvalid = invalidCount;
            }
        }

        static bool IsDefaultMaterialMeshInfo(MaterialMeshInfo mmi)
        {
            // Array-backed entries encode indices as negative numbers.
            // The only invalid state we care about is the default struct (0,0).
            return mmi.Material == 0 && mmi.Mesh == 0;
        }

        [BurstDiscard]
        static void LogCounts(
            int count,
            int invalidCount,
            Entity firstEntity,
            Space4XRenderKey firstKey,
            MaterialMeshInfo firstMmi,
            LocalTransform firstXform,
            RenderBounds firstBounds)
        {
            if (count == 0)
            {
                return;
            }

            var aabb = firstBounds.Value;
            float3 center  = aabb.Center;
            float3 extents = aabb.Extents;

            if (invalidCount > 0)
            {
                Debug.LogError(
                    $"[DebugVerifyVisualsSystem] Found {invalidCount} entities with INVALID MaterialMeshInfo! " +
                    $"Total entities: {count}. This should be 0.");
            }

            Debug.Log(
                $"[DebugVerifyVisualsSystem] Found {count} entities with RenderKey + MaterialMeshInfo. " +
                $"InvalidMMI={invalidCount} " +
                $"First={firstEntity} ArchetypeId={firstKey.ArchetypeId} " +
                $"MMI(Material={firstMmi.Material}, Mesh={firstMmi.Mesh}, SubMesh={firstMmi.SubMesh}) " +
                $"Pos={firstXform.Position} AABB(center={center}, extents={extents})");
        }
    }
}
#endif
#endif
