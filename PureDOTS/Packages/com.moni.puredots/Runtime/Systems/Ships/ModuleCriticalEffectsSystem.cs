using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Systems;
using PureDOTS.Systems.Combat;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Applies critical effects when modules are destroyed.
    /// Runs after ResolveDirectionalDamageSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ResolveDirectionalDamageSystem))]
    public partial struct ModuleCriticalEffectsSystem : ISystem
    {
        // Instance fields for Burst-compatible FixedString patterns (initialized in OnCreate)
        private FixedString32Bytes _engineId;
        private FixedString32Bytes _reactorId;
        private FixedString32Bytes _bridgeId;
        private FixedString32Bytes _lifeSupportId;
        private FixedString32Bytes _radShieldId;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            
            // Initialize FixedString patterns (OnCreate is not Burst-compiled)
            _engineId = new FixedString32Bytes("engine");
            _reactorId = new FixedString32Bytes("reactor");
            _bridgeId = new FixedString32Bytes("bridge");
            _lifeSupportId = new FixedString32Bytes("life_support");
            _radShieldId = new FixedString32Bytes("rad_shield");
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

            var job = new ModuleCriticalEffectsJob
            {
                Ecb = ecb,
                EngineId = _engineId,
                ReactorId = _reactorId,
                BridgeId = _bridgeId,
                LifeSupportId = _lifeSupportId,
                RadShieldId = _radShieldId
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ModuleCriticalEffectsJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public FixedString32Bytes EngineId;
            [ReadOnly] public FixedString32Bytes ReactorId;
            [ReadOnly] public FixedString32Bytes BridgeId;
            [ReadOnly] public FixedString32Bytes LifeSupportId;
            [ReadOnly] public FixedString32Bytes RadShieldId;

            void Execute(
                [ChunkIndexInQuery] int chunkIndex,
                Entity entity,
                ref DynamicBuffer<ModuleDamageEvent> moduleDamageEvents,
                ref DynamicBuffer<ModuleRuntimeStateElement> modules,
                ref CrewState crew,
                in ShipLayoutRef layoutRef)
            {
                if (!layoutRef.Blob.IsCreated)
                {
                    moduleDamageEvents.Clear();
                    return;
                }

                ref var layout = ref layoutRef.Blob.Value;

                // Process module damage events
                for (int i = 0; i < moduleDamageEvents.Length; i++)
                {
                    var damageEvent = moduleDamageEvents[i];

                    if (damageEvent.WasDestroyed == 0)
                    {
                        continue; // Module not destroyed
                    }

                    if (damageEvent.ModuleIndex >= layout.Modules.Length)
                    {
                        continue; // Invalid index
                    }

                    ref var moduleSlot = ref layout.Modules[damageEvent.ModuleIndex];
                    byte criticality = moduleSlot.Criticality;

                    // Apply consequences based on module type and criticality
                    // Module ID patterns: "engine", "reactor", "bridge", "life_support", "rad_shield"

                    if (moduleSlot.Id.Equals(EngineId))
                    {
                        // Engine destroyed: reduce max speed
                        // TODO: Apply speed reduction via MovementState or ship stats
                        // For now, mark module as disabled
                        if (damageEvent.ModuleIndex < modules.Length)
                        {
                            ref var module = ref modules.ElementAt(damageEvent.ModuleIndex);
                            module.Disabled = 1;
                        }
                    }
                    else if (moduleSlot.Id.Equals(ReactorId))
                    {
                        // Reactor destroyed: power cut, radiation hazard
                        // TODO: Apply power reduction
                        // Emit radiation hazard (will be handled by HazardEmitFromDamageSystem)
                        if (damageEvent.ModuleIndex < modules.Length)
                        {
                            ref var module = ref modules.ElementAt(damageEvent.ModuleIndex);
                            module.Disabled = 1;
                        }
                    }
                    else if (moduleSlot.Id.Equals(BridgeId))
                    {
                        // Bridge destroyed: crew loss, command disabled
                        int crewLoss = (int)(crew.Alive * (criticality / 10f) * 0.3f); // 30% of criticality ratio
                        crew.Alive = math.max(0, crew.Alive - crewLoss);
                        // TODO: Disable command/control systems
                    }
                    else if (moduleSlot.Id.Equals(LifeSupportId))
                    {
                        // Life support destroyed: crew attrition
                        // TODO: Apply ongoing crew loss over time
                    }
                    else if (moduleSlot.Id.Equals(RadShieldId))
                    {
                        // Radiation shield destroyed: crew exposed to radiation
                        // TODO: Apply radiation damage to crew
                    }

                    // Apply crew loss based on criticality
                    if (criticality > 0)
                    {
                        int crewLoss = (int)(criticality * 0.1f * crew.Alive); // 10% per criticality point
                        crew.Alive = math.max(0, crew.Alive - crewLoss);
                    }
                }

                // Clear processed events
                moduleDamageEvents.Clear();
            }
        }
    }
}

