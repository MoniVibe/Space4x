using PureDOTS.Runtime.ComplexEntities;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Runtime
{
    /// <summary>
    /// Bridge system that upgrades existing Space4X carriers to use the complex entity system.
    /// Converts legacy Carrier components to ComplexEntityIdentity + ComplexEntityCoreAxes.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ComplexEntityCarrierBridgeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var currentTick = SystemAPI.TryGetSingleton<TickTimeState>(out var tickState)
                ? tickState.Tick
                : (SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u);

            // Find carriers that need conversion
            foreach (var (carrier, transform, entity) in SystemAPI.Query<
                RefRO<Carrier>,
                RefRO<LocalTransform>>()
                .WithNone<ComplexEntityIdentity>()
                .WithEntityAccess())
            {
                // Create complex entity identity
                var identity = new ComplexEntityIdentity
                {
                    StableId = unchecked((ulong)carrier.ValueRO.CarrierId.GetHashCode()),
                    EntityType = ComplexEntityType.Carrier,
                    CreationTick = currentTick
                };

                var cell = int3.zero;
                ushort localX = 0;
                ushort localY = 0;
                if (SystemAPI.TryGetSingleton<SpatialGridConfig>(out var gridConfig))
                {
                    SpatialHash.Quantize(transform.ValueRO.Position, gridConfig, out cell);
                    var local = (transform.ValueRO.Position - gridConfig.WorldMin) / math.max(gridConfig.CellSize, 1e-3f);
                    var frac = local - math.floor(local);
                    localX = (ushort)math.clamp(frac.x * ushort.MaxValue, 0, ushort.MaxValue);
                    localY = (ushort)math.clamp(frac.y * ushort.MaxValue, 0, ushort.MaxValue);
                }

                // Create core axes from carrier data
                var coreAxes = new ComplexEntityCoreAxes
                {
                    Cell = cell,
                    LocalX = localX,
                    LocalY = localY,
                    VelX = 0,
                    VelY = 0,
                    HeadingQ = 0,
                    HealthQ = ushort.MaxValue,
                    MassQ = 0,
                    CapacityQ = 0,
                    LoadQ = 0,
                    Flags = 0,
                    CrewCount = 0,
                    Reserved0 = 0
                };

                // Add complex entity components
                ecb.AddComponent(entity, identity);
                ecb.AddComponent(entity, coreAxes);

                // Add operational state component (disabled by default)
                ecb.AddComponent(entity, new ComplexEntityOperationalState
                {
                    OperationalMode = 0,
                    TargetEntity = Entity.Null,
                    StateFlags = 0,
                    LastUpdateTick = currentTick
                });
                ecb.SetComponentEnabled<ComplexEntityOperationalState>(entity, false);

                // Add narrative detail component (disabled by default)
                ecb.AddComponent(entity, new ComplexEntityNarrativeDetail
                {
                    NarrativeBlob = default,
                    NarrativeFlags = 0,
                    LastNarrativeTick = currentTick
                });
                ecb.SetComponentEnabled<ComplexEntityNarrativeDetail>(entity, false);
            }
        }
    }

    /// <summary>
    /// Helper methods for registering activation triggers for complex entities.
    /// </summary>
    public static class ComplexEntityTriggerHelpers
    {
        /// <summary>
        /// Registers an entity as having player focus (selected/inspected).
        /// </summary>
        public static void RegisterFocusTarget(EntityCommandBuffer ecb, Entity entity)
        {
            ecb.AddComponent<FocusTargetTag>(entity);
        }

        /// <summary>
        /// Registers an entity as combat-ready or engaged in combat.
        /// </summary>
        public static void RegisterCombatReady(EntityCommandBuffer ecb, Entity entity)
        {
            ecb.AddComponent<CombatReadyTag>(entity);
        }

        /// <summary>
        /// Registers an entity as performing docking operations.
        /// </summary>
        public static void RegisterDockingActive(EntityCommandBuffer ecb, Entity entity)
        {
            ecb.AddComponent<DockingActiveTag>(entity);
        }

        /// <summary>
        /// Registers an entity as within active bubble (viewport/frustum).
        /// </summary>
        public static void RegisterActiveBubble(EntityCommandBuffer ecb, Entity entity)
        {
            ecb.AddComponent<ActiveBubbleTag>(entity);
        }

        /// <summary>
        /// Registers an inspection request for an entity (UI detail panel).
        /// </summary>
        public static void RegisterInspectionRequest(EntityCommandBuffer ecb, Entity entity)
        {
            ecb.AddComponent<InspectionRequest>(entity);
        }

        /// <summary>
        /// Removes an inspection request (closes detail panel).
        /// </summary>
        public static void RemoveInspectionRequest(EntityCommandBuffer ecb, Entity entity)
        {
            ecb.RemoveComponent<InspectionRequest>(entity);
        }
    }
}
