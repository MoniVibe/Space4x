using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using Space4X.Rendering;

namespace Space4X.Rendering.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct DebugVerifyVisualsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderKey>();
            state.RequireForUpdate<MaterialMeshInfo>();
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity firstEntity = Entity.Null;
            RenderKey firstKey = default;
            MaterialMeshInfo firstMmi = default;
            LocalTransform firstXform = default;
            RenderBounds firstBounds = default;
            int count = 0;

            foreach (var (rk, mmi, xform, bounds, entity)
                     in SystemAPI.Query<
                            RefRO<RenderKey>,
                            RefRO<MaterialMeshInfo>,
                            RefRO<LocalTransform>,
                            RefRO<RenderBounds>
                        >()
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
            }

            if (count == 0)
            {
                Debug.LogWarning("[DebugVerifyVisualsSystem] No entities with RenderKey + MaterialMeshInfo found.");
                return;
            }

            AABB aabb = firstBounds.Value;
            float3 center  = aabb.Center;
            float3 extents = aabb.Extents;

            Debug.Log(
                $"[DebugVerifyVisualsSystem] Found {count} entities with RenderKey + MaterialMeshInfo. " +
                $"First={firstEntity} ArchetypeId={firstKey.ArchetypeId} " +
                $"MMI(Material={firstMmi.Material}, Mesh={firstMmi.Mesh}, SubMesh={firstMmi.SubMesh}) " +
                $"Pos={firstXform.Position} AABB(center={center}, extents={extents})");
        }
    }
}
