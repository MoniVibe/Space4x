using PureDOTS.Presentation.Runtime;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace PureDOTS.Presentation.Systems
{
    /// <summary>
    /// Ensures miracle effects spawned by the hand receive placeholder presentation.
    /// Runs before the shared presentation assignment system so visuals appear the same frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.HandSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.HandMiracleSystem))]
    public partial struct HandEffectPresentationRequestSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PresentationRenderCatalog>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (effect, entity) in SystemAPI
                         .Query<RefRO<MiracleEffect>>()
                         .WithEntityAccess()
                         .WithNone<PresentationAssignedTag, PresentationRequest>())
            {
                float radius = math.max(0.5f, effect.ValueRO.Radius);
                var request = PresentationRequest.WithColor(
                    PresentationPrototype.MiracleEffect,
                    new float4(1f, 0.7f, 0.3f, 0.9f),
                    radius);

                ecb.AddComponent(entity, request);
            }

            ecb.Playback(state.EntityManager);
        }
    }

    /// <summary>
    /// Creates and updates the DOTS-driven hand cursor presentation entity.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct HandCursorPresentationSystem : ISystem
    {
        private EntityQuery _cursorQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandSingletonTag>();
            state.RequireForUpdate<PresentationRenderCatalog>();
            _cursorQuery = state.GetEntityQuery(ComponentType.ReadOnly<HandCursorPresentationTag>());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            Entity cursorEntity;

            if (_cursorQuery.IsEmpty)
            {
                cursorEntity = em.CreateEntity();
                em.AddComponent<HandCursorPresentationTag>(cursorEntity);
                em.AddComponentData(cursorEntity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
                em.AddComponentData(cursorEntity, PresentationRequest.Create(PresentationPrototype.HandCursor, 1f));
            }
            else
            {
                cursorEntity = _cursorQuery.GetSingletonEntity();
            }

            var handState = SystemAPI.GetSingleton<HandState>();
            var transform = em.GetComponentData<LocalTransform>(cursorEntity);
            transform.Position = handState.WorldPosition + new float3(0f, 0.15f, 0f);
            em.SetComponentData(cursorEntity, transform);

            if (em.HasComponent<PostTransformMatrix>(cursorEntity))
            {
                float scale = handState.PrimaryPressed == 1 ? 1.2f : 1f;
                em.SetComponentData(cursorEntity, new PostTransformMatrix { Value = float4x4.Scale(scale) });
            }

            if (em.HasComponent<URPMaterialPropertyBaseColor>(cursorEntity))
            {
                var color = em.GetComponentData<URPMaterialPropertyBaseColor>(cursorEntity).Value;
                color.w = handState.SecondaryPressed == 1 ? 0.85f : 1f;
                em.SetComponentData(cursorEntity, new URPMaterialPropertyBaseColor { Value = color });
            }
        }
    }
}
