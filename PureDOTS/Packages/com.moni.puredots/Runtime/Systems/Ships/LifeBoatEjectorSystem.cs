using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Ejects lifeboats when hull is destroyed or bridge is destroyed with AutoEject enabled.
    /// Spawns pod entities via ECB.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(DerelictClassifierSystem))]
    public partial struct LifeBoatEjectorSystem : ISystem
    {
        // Instance field for Burst-compatible FixedString pattern (initialized in OnCreate)
        private FixedString32Bytes _bridgeIdPattern;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            
            // Initialize FixedString pattern here (OnCreate is not Burst-compiled)
            _bridgeIdPattern = new FixedString32Bytes("bridge");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            transformLookup.Update(ref state);

            var job = new LifeBoatEjectorJob
            {
                Ecb = ecb,
                TransformLookup = transformLookup,
                BridgeIdPattern = _bridgeIdPattern
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct LifeBoatEjectorJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public FixedString32Bytes BridgeIdPattern;

            void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                ref CrewState crew,
                ref LifeBoatConfig lifeBoatConfig,
                in HullState hull,
                in DerelictState derelict,
                in LocalTransform transform,
                in DynamicBuffer<ModuleRuntimeStateElement> modules,
                in ShipLayoutRef layoutRef)
            {
                // Check if ejection should occur
                bool shouldEject = false;

                // Check hull kill
                if (hull.HP <= 0f && derelict.Stage >= 2)
                {
                    shouldEject = true;
                }

                // Check bridge destroyed with auto-eject
                if (!shouldEject && lifeBoatConfig.AutoEject != 0 && layoutRef.Blob.IsCreated)
                {
                    ref var layout = ref layoutRef.Blob.Value;
                    for (int i = 0; i < layout.Modules.Length && i < modules.Length; i++)
                    {
                        ref var moduleSlot = ref layout.Modules[i];
                        if (moduleSlot.Id.IndexOf(BridgeIdPattern) >= 0)
                        {
                            ref var module = ref modules.ElementAt(i);
                            if (module.Destroyed != 0)
                            {
                                shouldEject = true;
                                break;
                            }
                        }
                    }
                }

                if (!shouldEject || crew.Alive == 0)
                {
                    return;
                }

                // Eject pods
                int podsToEject = math.min(lifeBoatConfig.Count, (crew.Alive + lifeBoatConfig.Seats - 1) / lifeBoatConfig.Seats);
                int crewRemaining = crew.Alive;

                for (int i = 0; i < podsToEject && crewRemaining > 0; i++)
                {
                    int podCrew = math.min(crewRemaining, lifeBoatConfig.Seats);
                    crewRemaining -= podCrew;

                    // Spawn pod entity
                    var podEntity = Ecb.CreateEntity(chunkIndex);
                    Ecb.AddComponent(chunkIndex, podEntity, new CrewPod
                    {
                        SourceShip = entity,
                        Occupants = (byte)podCrew,
                        MaxSeats = lifeBoatConfig.Seats,
                        Rescued = 0,
                        Captured = 0
                    });

                    // Position pod near ship
                    float3 podPos = transform.Position + new float3(
                        (i - podsToEject * 0.5f) * 5f,
                        0f,
                        (i % 2) * 3f
                    );

                    Ecb.AddComponent(chunkIndex, podEntity, LocalTransform.FromPositionRotation(
                        podPos,
                        quaternion.identity
                    ));
                }

                // Update crew state
                crew.InPods += crew.Alive - crewRemaining;
                crew.Alive = crewRemaining;
            }
        }
    }
}

