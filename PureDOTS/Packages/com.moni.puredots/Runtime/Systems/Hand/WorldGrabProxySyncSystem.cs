using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Hand;
using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;

namespace PureDOTS.Systems.Hand
{
    /// <summary>
    /// Copies proxy transforms onto the target entity while the proxy is held.
    /// Intended for non-physics world-grab targets (nebula volumes, landmarks).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(HandHoldFollowSystem))]
    [UpdateBefore(typeof(Unity.Physics.Systems.PhysicsSimulationGroup))]
    public partial struct WorldGrabProxySyncSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<PhysicsVelocity> _velocityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _velocityLookup = state.GetComponentLookup<PhysicsVelocity>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _velocityLookup.Update(ref state);

            foreach (var (proxy, proxyTransform, heldTag) in SystemAPI.Query<RefRO<WorldGrabProxy>, RefRO<LocalTransform>, RefRO<HandHeldTag>>())
            {
                var target = proxy.ValueRO.Target;
                if (target == Entity.Null || !_transformLookup.HasComponent(target))
                {
                    continue;
                }

                if (_velocityLookup.HasComponent(target))
                {
                    continue;
                }

                var targetTransform = _transformLookup[target];
                targetTransform.Position = proxyTransform.ValueRO.Position;
                _transformLookup[target] = targetTransform;
            }
        }
    }
}
