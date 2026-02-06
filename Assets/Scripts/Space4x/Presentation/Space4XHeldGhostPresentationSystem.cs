using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Interaction;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Presentation
{
    public struct HandHeldGhostBaseTint : IComponentData
    {
        public float4 Value;
    }

    /// <summary>
    /// Ghosts held entities by lowering their alpha while they are grabbed by the hand.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(Space4XBehaviorPresentationSystem))]
    [UpdateBefore(typeof(Space4XRenderTintSyncSystem))]
    public partial struct Space4XHeldGhostPresentationSystem : ISystem
    {
        private const float GhostAlpha = 0.35f;

        private ComponentLookup<HeldByPlayer> _heldByPlayerLookup;
        private ComponentLookup<HandHeldTag> _handHeldLookup;
        private ComponentLookup<RenderOwner> _renderOwnerLookup;
        private ComponentLookup<HandHeldGhostBaseTint> _baseTintLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RenderTint>();
            _heldByPlayerLookup = state.GetComponentLookup<HeldByPlayer>(true);
            _handHeldLookup = state.GetComponentLookup<HandHeldTag>(true);
            _renderOwnerLookup = state.GetComponentLookup<RenderOwner>(true);
            _baseTintLookup = state.GetComponentLookup<HandHeldGhostBaseTint>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            _heldByPlayerLookup.Update(ref state);
            _handHeldLookup.Update(ref state);
            _renderOwnerLookup.Update(ref state);
            _baseTintLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            try
            {
                foreach (var (tint, entity) in SystemAPI.Query<RefRW<RenderTint>>()
                             .WithAll<HandHeldTag>()
                             .WithEntityAccess())
                {
                    ApplyGhost(ref ecb, entity, ref tint.ValueRW);
                }

                foreach (var (tint, _, entity) in SystemAPI.Query<RefRW<RenderTint>, RefRO<HeldByPlayer>>()
                             .WithEntityAccess())
                {
                    if (!_heldByPlayerLookup.IsComponentEnabled(entity))
                    {
                        continue;
                    }

                    ApplyGhost(ref ecb, entity, ref tint.ValueRW);
                }

                foreach (var (owner, tint, entity) in SystemAPI.Query<RefRO<RenderOwner>, RefRW<RenderTint>>()
                             .WithEntityAccess())
                {
                    if (IsEntityHeld(owner.ValueRO.Owner))
                    {
                        ApplyGhost(ref ecb, entity, ref tint.ValueRW);
                    }
                }

                foreach (var (baseTint, tint, entity) in SystemAPI.Query<RefRO<HandHeldGhostBaseTint>, RefRW<RenderTint>>()
                             .WithEntityAccess())
                {
                    bool stillHeld = IsEntityHeld(entity);
                    if (!stillHeld && _renderOwnerLookup.HasComponent(entity))
                    {
                        stillHeld = IsEntityHeld(_renderOwnerLookup[entity].Owner);
                    }

                    if (stillHeld)
                    {
                        var ghostTint = baseTint.ValueRO.Value;
                        ghostTint.w = math.min(ghostTint.w, GhostAlpha);
                        tint.ValueRW.Value = ghostTint;
                    }
                    else
                    {
                        tint.ValueRW.Value = baseTint.ValueRO.Value;
                        ecb.RemoveComponent<HandHeldGhostBaseTint>(entity);
                    }
                }
            }
            finally
            {
                ecb.Playback(state.EntityManager);
                ecb.Dispose();
            }
        }

        private bool IsEntityHeld(Entity entity)
        {
            if (entity == Entity.Null)
            {
                return false;
            }

            if (_handHeldLookup.HasComponent(entity))
            {
                return true;
            }

            return _heldByPlayerLookup.HasComponent(entity) && _heldByPlayerLookup.IsComponentEnabled(entity);
        }

        private void ApplyGhost(ref EntityCommandBuffer ecb, Entity entity, ref RenderTint tint)
        {
            if (!_baseTintLookup.HasComponent(entity))
            {
                ecb.AddComponent(entity, new HandHeldGhostBaseTint { Value = tint.Value });
                var ghostTint = tint.Value;
                ghostTint.w = math.min(ghostTint.w, GhostAlpha);
                tint.Value = ghostTint;
                return;
            }

            var baseTint = _baseTintLookup[entity].Value;
            baseTint.w = math.min(baseTint.w, GhostAlpha);
            tint.Value = baseTint;
        }
    }
}
