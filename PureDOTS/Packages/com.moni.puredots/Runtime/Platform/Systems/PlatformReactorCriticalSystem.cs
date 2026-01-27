using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Handles reactor critical failures: safe shutdown vs meltdown.
    /// On reactor destruction or segment destruction with reactor, rolls for safe shutdown.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PlatformSegmentDestructionSystem))]
    public partial struct PlatformReactorCriticalSystem : ISystem
    {
        private BufferLookup<PlatformSegmentState> _segmentStatesLookup;
        private BufferLookup<PlatformModuleSlot> _moduleSlotsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlatformSegmentState>();
            _segmentStatesLookup = state.GetBufferLookup<PlatformSegmentState>(false);
            _moduleSlotsLookup = state.GetBufferLookup<PlatformModuleSlot>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
            var random = new Unity.Mathematics.Random((uint)SystemAPI.Time.ElapsedTime + 1);

            _segmentStatesLookup.Update(ref state);
            _moduleSlotsLookup.Update(ref state);

            foreach (var (transform, entity) in SystemAPI.Query<
                RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!_segmentStatesLookup.HasBuffer(entity) || !_moduleSlotsLookup.HasBuffer(entity))
                {
                    continue;
                }

                var segmentStates = _segmentStatesLookup[entity];
                var moduleSlots = _moduleSlotsLookup[entity];
                var platformEntity = entity;

                CheckReactorFailures(
                    ref state,
                    ref ecb,
                    ref platformEntity,
                    in transform.ValueRO,
                    ref segmentStates,
                    in moduleSlots,
                    ref random);
            }
        }

        [BurstCompile]
        private static void CheckReactorFailures(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            ref Entity platformEntity,
            in LocalTransform transform,
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            in DynamicBuffer<PlatformModuleSlot> moduleSlots,
            ref Unity.Mathematics.Random random)
        {
            for (int i = 0; i < segmentStates.Length; i++)
            {
                var segmentState = segmentStates[i];

                if ((segmentState.Status & SegmentStatusFlags.ReactorPresent) == 0)
                {
                    continue;
                }

                bool reactorDestroyed = false;

                if ((segmentState.Status & SegmentStatusFlags.Destroyed) != 0)
                {
                    reactorDestroyed = true;
                }
                else
                {
                    for (int j = 0; j < moduleSlots.Length; j++)
                    {
                        if (moduleSlots[j].SegmentIndex == segmentState.SegmentIndex &&
                            moduleSlots[j].State == ModuleSlotState.Destroyed)
                        {
                            reactorDestroyed = true;
                            break;
                        }
                    }
                }

                if (reactorDestroyed && (segmentState.Status & SegmentStatusFlags.ReactorCritical) == 0)
                {
                    segmentState.Status |= SegmentStatusFlags.ReactorCritical;
                    segmentStates[i] = segmentState;

                    GetReactorDef(segmentState.SegmentIndex, in moduleSlots, out var reactorDef);
                    var shutdownChance = reactorDef.SafeShutdownChance;
                    var roll = random.NextFloat();

                    if (roll <= shutdownChance)
                    {
                        segmentState.Status &= ~SegmentStatusFlags.ReactorPresent;
                        segmentState.Status &= ~SegmentStatusFlags.ReactorCritical;
                        segmentStates[i] = segmentState;
                    }
                    else
                    {
                        var pos = transform.Position;
                        TriggerMeltdown(
                            ref ecb,
                            ref platformEntity,
                            segmentState.SegmentIndex,
                            in pos,
                            in reactorDef);
                    }
                }
            }
        }

        [BurstCompile]
        private static void GetReactorDef(int segmentIndex, in DynamicBuffer<PlatformModuleSlot> moduleSlots, out ReactorDef def)
        {
            def = new ReactorDef
            {
                ReactorDefId = 0,
                MaxOutput = 1000f,
                FailureMode = ReactorFailureMode.Explosive,
                MeltdownDamage = 5000f,
                MeltdownRadius = 100f,
                SafeShutdownChance = 0.3f
            };

            for (int i = 0; i < moduleSlots.Length; i++)
            {
                if (moduleSlots[i].SegmentIndex == segmentIndex)
                {
                    return;
                }
            }
        }

        [BurstCompile]
        private static void TriggerMeltdown(
            ref EntityCommandBuffer ecb,
            ref Entity platformEntity,
            int segmentIndex,
            in float3 worldPosition,
            in ReactorDef reactorDef)
        {
            ecb.AddBuffer<PlatformExplosionEvent>(platformEntity);
            var explosionBuffer = ecb.SetBuffer<PlatformExplosionEvent>(platformEntity);
            explosionBuffer.Add(new PlatformExplosionEvent
            {
                SourcePlatform = platformEntity,
                WorldPosition = worldPosition,
                DamageAmount = reactorDef.MeltdownDamage,
                Radius = reactorDef.MeltdownRadius,
                TypeFlags = DamageTypeFlags.Thermal | DamageTypeFlags.Energy
            });

            ecb.AddBuffer<PlatformReactorMeltdownEvent>(platformEntity);
            var meltdownBuffer = ecb.SetBuffer<PlatformReactorMeltdownEvent>(platformEntity);
            meltdownBuffer.Add(new PlatformReactorMeltdownEvent
            {
                PlatformEntity = platformEntity,
                SegmentIndex = segmentIndex,
                WorldPosition = worldPosition,
                DamageAmount = reactorDef.MeltdownDamage,
                Radius = reactorDef.MeltdownRadius
            });
        }
    }
}

