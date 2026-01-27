using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Systems;
using PureDOTS.Systems.Ships;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Emits HazardSlice entries from ongoing fires/radiation leaks.
    /// Integrates with BuildHazardSlicesSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ModuleCriticalEffectsSystem))]
    [UpdateBefore(typeof(BuildHazardSlicesSystem))]
    public partial struct HazardEmitFromDamageSystem : ISystem
    {
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString32Bytes _reactorIdPattern;
        private FixedString32Bytes _engineIdPattern;
        private BufferLookup<HazardSlice> _sliceBufferLookup;
        private ComponentLookup<LocalTransform> _transformLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            
            // Initialize FixedString patterns here (OnCreate is not Burst-compiled)
            _reactorIdPattern = new FixedString32Bytes("reactor");
            _engineIdPattern = new FixedString32Bytes("engine");
            _sliceBufferLookup = state.GetBufferLookup<HazardSlice>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var deltaTime = timeState.DeltaTime;

            // Find or create hazard slice buffer singleton
            Entity sliceBufferEntity;
            if (!SystemAPI.TryGetSingletonEntity<HazardSliceBuffer>(out sliceBufferEntity))
            {
                sliceBufferEntity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<HazardSliceBuffer>(sliceBufferEntity);
                state.EntityManager.AddBuffer<HazardSlice>(sliceBufferEntity);
            }

            var sliceBuffer = SystemAPI.GetBuffer<HazardSlice>(sliceBufferEntity);

            _transformLookup.Update(ref state);
            _sliceBufferLookup.Update(ref state);

            var job = new HazardEmitFromDamageJob
            {
                CurrentTick = currentTick,
                DeltaTime = deltaTime,
                SliceBufferEntity = sliceBufferEntity,
                SliceBufferLookup = _sliceBufferLookup,
                TransformLookup = _transformLookup,
                ReactorIdPattern = _reactorIdPattern,
                EngineIdPattern = _engineIdPattern
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
            state.Dependency.Complete(); // Need to complete before BuildHazardSlicesSystem reads buffer
        }

        [BurstCompile]
        public partial struct HazardEmitFromDamageJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            public Entity SliceBufferEntity;
            [NativeDisableParallelForRestriction] public BufferLookup<HazardSlice> SliceBufferLookup;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
            [ReadOnly] public FixedString32Bytes ReactorIdPattern;
            [ReadOnly] public FixedString32Bytes EngineIdPattern;

            void Execute(
                Entity entity,
                ref DynamicBuffer<ModuleRuntimeStateElement> modules,
                in ShipLayoutRef layoutRef,
                in LocalTransform transform)
            {
                if (!layoutRef.Blob.IsCreated || !TransformLookup.HasComponent(entity))
                {
                    return;
                }

                ref var layout = ref layoutRef.Blob.Value;
                float3 shipPos = transform.Position;
                var sliceBuffer = SliceBufferLookup[SliceBufferEntity];

                // Check for reactor destruction (radiation hazard)
                for (int i = 0; i < layout.Modules.Length && i < modules.Length; i++)
                {
                    ref var moduleSlot = ref layout.Modules[i];
                    var module = modules[i];

                    if (moduleSlot.Id.IndexOf(ReactorIdPattern) >= 0 && module.Destroyed != 0)
                    {
                        // Emit radiation hazard
                        var radiationSlice = new HazardSlice
                        {
                            Center = shipPos,
                            Vel = float3.zero, // Stationary radiation leak
                            Radius0 = 50f, // Initial radius
                            RadiusGrow = 10f, // Growing radiation cloud
                            StartTick = CurrentTick,
                            EndTick = CurrentTick + 1000, // Long duration
                            Kind = HazardKind.Plague, // Use Plague for radiation contamination
                            ChainRadius = 0f,
                            ContagionProb = 0.1f, // Radiation spread chance
                            HomingConeCos = 0f,
                            SprayVariance = 0f,
                            TeamMask = 0xFFFFFFFF, // Affects all teams
                            Seed = (uint)entity.Index
                        };

                        sliceBuffer.Add(radiationSlice);
                    }

                    // Check for fire (simplified - would track fire state)
                    if (moduleSlot.Id.IndexOf(EngineIdPattern) >= 0 && module.Destroyed != 0 && module.HP < module.MaxHP * 0.5f)
                    {
                        // Emit fire hazard
                        var fireSlice = new HazardSlice
                        {
                            Center = shipPos,
                            Vel = float3.zero,
                            Radius0 = 20f,
                            RadiusGrow = 5f,
                            StartTick = CurrentTick,
                            EndTick = CurrentTick + 100, // Shorter duration
                            Kind = HazardKind.AoE,
                            ChainRadius = 0f,
                            ContagionProb = 0f,
                            HomingConeCos = 0f,
                            SprayVariance = 0f,
                            TeamMask = 0xFFFFFFFF,
                            Seed = (uint)entity.Index + 1000
                        };

                        sliceBuffer.Add(fireSlice);
                    }
                }
            }
        }
    }
}

