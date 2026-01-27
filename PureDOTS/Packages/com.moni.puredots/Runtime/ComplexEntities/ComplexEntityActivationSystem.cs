using PureDOTS.Runtime.ComplexEntities;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.ComplexEntities
{
    /// <summary>
    /// Determines which complex entities should have operational expansion enabled
    /// based on active bubble, focus, combat, and docking triggers.
    /// Runs at reduced cadence to avoid overhead.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ComplexEntityOperationalStateSystem))]
    [BurstCompile]
    public partial struct ComplexEntityActivationSystem : ISystem
    {
        private uint _lastUpdateTick;
        private const uint UpdateCadence = 5; // Check every 5 ticks

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _lastUpdateTick = 0;
            state.RequireForUpdate<SimulationFeatureFlags>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check feature flag
            var featureFlags = SystemAPI.GetSingleton<SimulationFeatureFlags>();
            if ((featureFlags.Flags & SimulationFeatureFlags.ComplexEntitiesEnabled) == 0)
                return;

            if ((featureFlags.Flags & SimulationFeatureFlags.ComplexEntityOperationalExpansionEnabled) == 0)
                return;

            if (!SystemAPI.TryGetSingleton<TickTimeState>(out var tickState))
                return;
            var currentTick = tickState.Tick;
            if (currentTick - _lastUpdateTick < UpdateCadence)
                return;

            _lastUpdateTick = currentTick;

            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            // Enable operational state for entities with activation triggers
            foreach (var (coreAxes, entity) in SystemAPI.Query<RefRW<ComplexEntityCoreAxes>>()
                .WithAny<ActiveBubbleTag>()
                .WithAny<FocusTargetTag>()
                .WithAny<CombatReadyTag>()
                .WithAny<DockingActiveTag>()
                .WithAll<ComplexEntityIdentity>()
                .WithEntityAccess())
            {
                var axes = coreAxes.ValueRW;
                var hasOp = SystemAPI.HasComponent<ComplexEntityOperationalState>(entity);
                var opEnabled = hasOp && SystemAPI.IsComponentEnabled<ComplexEntityOperationalState>(entity);
                if ((axes.Flags & ComplexEntityFlags.OperationalActive) == 0 || !opEnabled)
                {
                    // Ensure operational state component exists
                    if (!hasOp)
                    {
                        ecb.AddComponent(entity, new ComplexEntityOperationalState
                        {
                            OperationalMode = 0,
                            TargetEntity = Entity.Null,
                            StateFlags = 0,
                            LastUpdateTick = currentTick
                        });
                    }
                    ecb.SetComponentEnabled<ComplexEntityOperationalState>(entity, true);

                    // Ensure sparse axes buffer exists for operational entities (rare, small internal capacity)
                    if (!SystemAPI.HasBuffer<ComplexEntitySparseAxesBuffer>(entity))
                    {
                        ecb.AddBuffer<ComplexEntitySparseAxesBuffer>(entity);
                    }
                    axes.Flags |= ComplexEntityFlags.OperationalActive;
                    ecb.SetComponent(entity, axes);
                }
            }

            // Disable operational state for entities without activation triggers
            foreach (var (coreAxes, entity) in SystemAPI.Query<RefRW<ComplexEntityCoreAxes>>()
                .WithNone<ActiveBubbleTag>()
                .WithNone<FocusTargetTag>()
                .WithNone<CombatReadyTag>()
                .WithNone<DockingActiveTag>()
                .WithAll<ComplexEntityIdentity>()
                .WithEntityAccess())
            {
                var axes = coreAxes.ValueRW;
                var hasOp = SystemAPI.HasComponent<ComplexEntityOperationalState>(entity);
                var opEnabled = hasOp && SystemAPI.IsComponentEnabled<ComplexEntityOperationalState>(entity);
                if ((axes.Flags & ComplexEntityFlags.OperationalActive) != 0 || opEnabled)
                {
                    if (hasOp)
                    {
                        ecb.SetComponentEnabled<ComplexEntityOperationalState>(entity, false);
                    }

                    // Remove sparse axes buffer when leaving operational (saves memory; small hot population only)
                    if (SystemAPI.HasBuffer<ComplexEntitySparseAxesBuffer>(entity))
                    {
                        ecb.RemoveComponent<ComplexEntitySparseAxesBuffer>(entity);
                    }
                    axes.Flags &= ~ComplexEntityFlags.OperationalActive;
                    ecb.SetComponent(entity, axes);
                }
            }
        }
    }
}
