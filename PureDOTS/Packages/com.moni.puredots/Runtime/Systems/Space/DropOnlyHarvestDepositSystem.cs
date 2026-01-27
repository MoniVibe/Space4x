using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interaction;
using PureDOTS.Runtime.Space;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Space
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MiningLoopSystem))]
    public partial struct DropOnlyHarvestDepositSystem : ISystem
    {
        private EntityArchetype _pileArchetype;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DropOnlyHarvesterTag>();
            _pileArchetype = state.EntityManager.CreateArchetype(
                typeof(ResourcePile),
                typeof(ResourcePileMeta),
                typeof(ResourcePileVelocity));
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (loopStateRW, dropConfig, transform) in SystemAPI
                         .Query<RefRW<MiningLoopState>, RefRO<ResourceDropConfig>, RefRO<LocalTransform>>()
                         .WithAll<DropOnlyHarvesterTag>())
            {
                ref var loopState = ref loopStateRW.ValueRW;
                if (loopState.Phase != MiningLoopPhase.DroppingOff || loopState.CurrentCargo <= 0.01f)
                {
                    continue;
                }

                var pileEntity = ecb.CreateEntity(_pileArchetype);
                var position = transform.ValueRO.Position;
                var jitterDir = math.normalize(new float3(Noise(position.xy), Noise(position.yz), Noise(position.xz)));
                var jitter = jitterDir * dropConfig.ValueRO.DropRadiusMeters;
                ecb.SetComponent(pileEntity, new ResourcePile
                {
                    Amount = loopState.CurrentCargo,
                    Position = position + jitter
                });
                ecb.SetComponent(pileEntity, new ResourcePileMeta
                {
                    ResourceTypeId = dropConfig.ValueRO.ResourceTypeId,
                    DecaySeconds = dropConfig.ValueRO.DecaySeconds,
                    MaxCapacity = dropConfig.ValueRO.MaxStack
                });
                ecb.SetComponent(pileEntity, new ResourcePileVelocity
                {
                    Velocity = jitterDir * 0.25f
                });
                ecb.AddComponent<PickableTag>(pileEntity);
                ecb.AddComponent<HeldByPlayer>(pileEntity);
                ecb.SetComponentEnabled<HeldByPlayer>(pileEntity, false);
                ecb.AddComponent<MovementSuppressed>(pileEntity);
                ecb.SetComponentEnabled<MovementSuppressed>(pileEntity, false);
                ecb.AddComponent<BeingThrown>(pileEntity);
                ecb.SetComponentEnabled<BeingThrown>(pileEntity, false);
                ecb.AddComponent(pileEntity, new HandPickable
                {
                    Mass = math.max(0.1f, loopState.CurrentCargo),
                    MaxHoldDistance = 50f,
                    ThrowImpulseMultiplier = 1f,
                    FollowLerp = 0.25f
                });

                loopState.CurrentCargo = 0f;
                loopState.Phase = MiningLoopPhase.Idle;
                loopState.PhaseTimer = 0f;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        private static float Noise(float2 v)
        {
            return (math.hash(v) & 0xFFFF) / 65535f - 0.5f;
        }
    }
}
