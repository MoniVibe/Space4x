using PureDOTS.Runtime.ComplexEntities;
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

            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            // Find carriers that need conversion
            foreach (var (carrier, entity, transform) in SystemAPI.Query<
                RefRO<Carrier>,
                Entity>()
                .WithAll<LocalTransform>()
                .WithNone<ComplexEntityIdentity>()
                .WithEntityAccess())
            {
                // Create complex entity identity
                var identity = new ComplexEntityIdentity
                {
                    StableId = new FixedString64Bytes($"carrier_{carrier.ValueRO.CarrierId}"),
                    EntityType = ComplexEntityType.Carrier,
                    CreationTick = currentTick
                };

                // Create core axes from carrier data
                var coreAxes = new ComplexEntityCoreAxes
                {
                    Position = transform.Position,
                    Velocity = float3.zero, // Will be updated by movement systems
                    Mass = 1000f, // Default mass, can be overridden
                    Capacity = carrier.ValueRO.TotalCapacity,
                    CurrentLoad = carrier.ValueRO.CurrentLoad,
                    Health = 1.0f, // Default health
                    Flags = 0
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
