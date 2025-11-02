using PureDOTS.Presentation.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace PureDOTS.Presentation.Systems
{
    /// <summary>
    /// Applies placeholder render data to entities requesting presentation.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.HandSystemGroup))]
    public partial struct PresentationAssignmentSystem : ISystem
    {
        private EntityQuery _configQuery;

        public void OnCreate(ref SystemState state)
        {
            _configQuery = SystemAPI.QueryBuilder()
                .WithAll<PresentationRenderCatalog, PresentationConfigTag>()
                .Build();
            state.RequireForUpdate(_configQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var catalogEntity = _configQuery.GetSingletonEntity();
            var catalog = SystemAPI.GetComponent<PresentationRenderCatalog>(catalogEntity).Blob;
            if (!catalog.IsCreated)
            {
                return;
            }

            var renderArray = state.EntityManager.GetSharedComponentManaged<RenderMeshArray>(catalogEntity);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (request, entity) in SystemAPI
                         .Query<RefRO<PresentationRequest>>()
                         .WithEntityAccess()
                         .WithNone<PresentationAssignedTag, MaterialMeshInfo>())
            {
                ref var entry = ref catalog.Value.Prototypes[(int)request.ValueRO.Prototype];

                ecb.AddSharedComponentManaged(entity, renderArray);
                ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(entry.MeshIndex, entry.MaterialIndex));

                float3 scale = entry.DefaultScale;
                if ((request.ValueRO.Flags & PresentationRequestFlags.HasScaleOverride) != 0 && request.ValueRO.UniformScale > 0f)
                {
                    scale = new float3(request.ValueRO.UniformScale);
                }

                float3 boundsExtents = entry.BoundsExtents;
                if ((request.ValueRO.Flags & PresentationRequestFlags.HasScaleOverride) != 0 && request.ValueRO.UniformScale > 0f)
                {
                    boundsExtents *= request.ValueRO.UniformScale;
                }

                var postTransform = float4x4.Scale(scale);
                if (state.EntityManager.HasComponent<PostTransformMatrix>(entity))
                {
                    ecb.SetComponent(entity, new PostTransformMatrix { Value = postTransform });
                }
                else
                {
                    ecb.AddComponent(entity, new PostTransformMatrix { Value = postTransform });
                }

                var renderBounds = new RenderBounds
                {
                    Value = new MinMaxAABB
                    {
                        Min = float3.zero - boundsExtents,
                        Max = float3.zero + boundsExtents
                    }
                };

                if (state.EntityManager.HasComponent<RenderBounds>(entity))
                {
                    ecb.SetComponent(entity, renderBounds);
                }
                else
                {
                    ecb.AddComponent(entity, renderBounds);
                }

                float4 color = entry.DefaultColor;
                if ((request.ValueRO.Flags & PresentationRequestFlags.HasColorOverride) != 0)
                {
                    color = request.ValueRO.Color;
                }

                var baseColor = new URPMaterialPropertyBaseColor { Value = color };
                if (state.EntityManager.HasComponent<URPMaterialPropertyBaseColor>(entity))
                {
                    ecb.SetComponent(entity, baseColor);
                }
                else
                {
                    ecb.AddComponent(entity, baseColor);
                }

                ecb.AddComponent<PresentationAssignedTag>(entity);
                ecb.RemoveComponent<PresentationRequest>(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
