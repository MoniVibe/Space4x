using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Synchronises presentation companion visuals with the authoritative ECS transforms.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PresentationRecycleSystem))]
    public partial struct PresentationHandleSyncSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _visualTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _visualTransformLookup = state.GetComponentLookup<LocalTransform>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _visualTransformLookup.Update(ref state);

            var config = PresentationHandleSyncConfig.Default;
            SystemAPI.TryGetSingleton<PresentationHandleSyncConfig>(out config);

            foreach (var (sourceTransform, handle) in SystemAPI
                         .Query<RefRO<LocalTransform>, RefRO<PresentationHandle>>())
            {
                var visual = handle.ValueRO.Visual;
                if (visual == Entity.Null || !_visualTransformLookup.HasComponent(visual))
                {
                    continue;
                }

                var desired = sourceTransform.ValueRO;
                desired.Position += math.mul(desired.Rotation, config.VisualOffset);

                var current = _visualTransformLookup[visual];
                var blended = BlendTransform(current, desired, config);
                _visualTransformLookup[visual] = blended;
            }
        }

        private static LocalTransform BlendTransform(in LocalTransform current, in LocalTransform target, in PresentationHandleSyncConfig config)
        {
            var positionLerp = math.saturate(config.PositionLerp);
            var rotationLerp = math.saturate(config.RotationLerp);
            var scaleLerp = math.saturate(config.ScaleLerp);

            var position = positionLerp >= 0.999f
                ? target.Position
                : math.lerp(current.Position, target.Position, positionLerp);

            var rotation = rotationLerp >= 0.999f
                ? target.Rotation
                : math.normalizesafe(math.slerp(current.Rotation, target.Rotation, rotationLerp));

            var scale = scaleLerp >= 0.999f
                ? target.Scale
                : math.lerp(current.Scale, target.Scale, scaleLerp);

            return LocalTransform.FromPositionRotationScale(position, rotation, scale);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
