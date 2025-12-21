using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Presentation
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    public partial struct Space4XGhostScaleSystem : ISystem
    {
        private ComponentLookup<PresentationScale> _presentationScaleLookup;
        private ComponentLookup<PresentationScaleMultiplier> _scaleMultiplierLookup;
        private ComponentLookup<LocalTransform> _sourceTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _presentationScaleLookup = state.GetComponentLookup<PresentationScale>(true);
            _scaleMultiplierLookup = state.GetComponentLookup<PresentationScaleMultiplier>(true);
            _sourceTransformLookup = state.GetComponentLookup<LocalTransform>(true);
            state.RequireForUpdate<GhostTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            _presentationScaleLookup.Update(ref state);
            _scaleMultiplierLookup.Update(ref state);
            _sourceTransformLookup.Update(ref state);

            foreach (var (ghostSource, ghostTransform, ghostEntity) in SystemAPI
                         .Query<RefRO<GhostSourceEntity>, RefRW<LocalTransform>>()
                         .WithAll<GhostTag>()
                         .WithEntityAccess())
            {
                var sourceEntity = ghostSource.ValueRO.SourceEntity;
                if (sourceEntity == Entity.Null || !_sourceTransformLookup.HasComponent(sourceEntity))
                {
                    continue;
                }

                float baseScale = 1f;
                if (_presentationScaleLookup.HasComponent(sourceEntity))
                {
                    baseScale = math.max(0.001f, _presentationScaleLookup[sourceEntity].Value);
                }

                float multiplier = 1f;
                if (_scaleMultiplierLookup.HasComponent(ghostEntity))
                {
                    multiplier = math.max(0.001f, _scaleMultiplierLookup[ghostEntity].Value);
                }

                var sourceTransform = _sourceTransformLookup[sourceEntity];
                var updated = ghostTransform.ValueRO;
                updated.Scale = math.max(0.001f, sourceTransform.Scale * baseScale * multiplier);
                ghostTransform.ValueRW = updated;
            }
        }
    }
}
