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
    // System struct cannot be Burst-compiled when OnCreate uses CreateArchetype (creates managed arrays)
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DropOnlyHarvestDepositSystem))]
    public partial struct ResourceDropSpawnerSystem : ISystem
    {

        // OnCreate cannot be Burst-compiled when using CreateArchetype (creates managed arrays)
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ResourceDropConfig>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out EndSimulationEntityCommandBufferSystem.Singleton ecbSingleton))
            {
                return;
            }

            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (loopState, dropConfig, transform) in SystemAPI
                         .Query<RefRW<MiningLoopState>, RefRW<ResourceDropConfig>, RefRO<LocalTransform>>()
                         .WithAll<DropOnlyHarvesterTag>())
            {
                if (loopState.ValueRO.Phase != MiningLoopPhase.Harvesting)
                {
                    continue;
                }

                dropConfig.ValueRW.TimeSinceLastDrop += SystemAPI.Time.DeltaTime;
                if (dropConfig.ValueRW.TimeSinceLastDrop < dropConfig.ValueRO.DropIntervalSeconds)
                {
                    continue;
                }

                dropConfig.ValueRW.TimeSinceLastDrop = 0f;
                var pileEntity = ecb.CreateEntity();
                var position = transform.ValueRO.Position;
                var jitterDir = math.normalize(new float3(Noise(position.xy * 1.1f), Noise(position.yz * 1.3f), Noise(position.xz * 1.7f)));
                var jitter = jitterDir * dropConfig.ValueRO.DropRadiusMeters;
                var amount = math.min(dropConfig.ValueRO.MaxStack, loopState.ValueRO.CurrentCargo + dropConfig.ValueRO.DropIntervalSeconds * 0.1f);
                ecb.AddComponent<ResourcePile>(pileEntity);
                ecb.SetComponent(pileEntity, new ResourcePile
                {
                    Amount = amount,
                    Position = position + jitter
                });
                ecb.AddComponent<ResourcePileMeta>(pileEntity);
                ecb.SetComponent(pileEntity, new ResourcePileMeta
                {
                    ResourceTypeId = dropConfig.ValueRO.ResourceTypeId,
                    DecaySeconds = dropConfig.ValueRO.DecaySeconds,
                    MaxCapacity = dropConfig.ValueRO.MaxStack
                });
                ecb.AddComponent<ResourcePileVelocity>(pileEntity);
                ecb.SetComponent(pileEntity, new ResourcePileVelocity
                {
                    Velocity = jitterDir * 0.1f
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
                    Mass = math.max(0.1f, amount),
                    MaxHoldDistance = 50f,
                    ThrowImpulseMultiplier = 1f,
                    FollowLerp = 0.25f
                });
                loopState.ValueRW.CurrentCargo = math.max(0f, loopState.ValueRW.CurrentCargo - amount);
            }

            // ECB playback is handled by EndSimulationEntityCommandBufferSystem
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
