using PureDOTS.Runtime.Components;
using Unity.Entities;
using Unity.Physics.Systems;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Custom system group for time management systems.
    /// Runs first in InitializationSystemGroup.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class TimeSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Environment simulation group; runs after physics and before spatial indexing.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class EnvironmentSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Spatial systems run after environment state updates and before gameplay logic.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnvironmentSystemGroup))]
    [UpdateBefore(typeof(GameplaySystemGroup))]
    public partial class SpatialSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Hot path system group - runs every tick, no throttling.
    /// Contains systems that process many entities with simple math (movement, steering).
    /// Must be tiny, branch-light, data tight. No allocations, no pathfinding calls.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup), OrderFirst = true)]
    public partial class HotPathSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Warm path system group - throttled, staggered updates.
    /// Contains systems that do local pathfinding, group decisions, replanning.
    /// Throttled (K queries/tick), staggered updates, local A* only.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(HotPathSystemGroup))]
    public partial class WarmPathSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Cold path system group - long intervals, event-driven.
    /// Contains systems that do strategic planning, graph building, multi-modal routing.
    /// Event-driven or long intervals (50-200 ticks), strategic planning.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(WarmPathSystemGroup))]
    public partial class ColdPathSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Shared AI systems that feed data into gameplay domains.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateBefore(typeof(VillagerSystemGroup))]
    public partial class AISystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// High level gameplay simulation group containing domain-specific subgroups.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(TransportPhaseGroup))]
    public partial class GameplaySystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for rewind/history recording.
    /// Runs after simulation to capture state.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(HistoryPhaseGroup))]
    public partial class HistorySystemGroup : InstrumentedComponentSystemGroup
    {
        protected override void OnUpdate()
        {
            if (IsHistoryDisabled())
            {
                return;
            }

            base.OnUpdate();
        }

        private bool IsHistoryDisabled()
        {
            if (!World.IsCreated)
            {
                return false;
            }

            var entityManager = World.EntityManager;
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<HistorySettings>());
            if (!query.TryGetSingleton(out HistorySettings settings))
            {
                return false;
            }

            return settings.StrideScale <= 0f;
        }
    }

    /// <summary>
    /// Fixed-step job systems for villagers. Runs inside FixedStepSimulation before high-level AI.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class VillagerJobFixedStepGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for villager AI and behavior.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(AISystemGroup))]
    public partial class VillagerSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for resource management.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(VillagerSystemGroup))]
    public partial class ResourceSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for power network management.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ResourceSystemGroup))]
    public partial class PowerSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for miracle effect processing.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ResourceSystemGroup))]
    public partial class MiracleEffectSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for perception systems.
    /// Runs after spatial grid, before AI systems.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    public partial class PerceptionSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for interrupt handling.
    /// Runs after perception/combat/group logic, before AI/GOAP systems.
    /// </summary>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    // Removed invalid UpdateAfter: CombatSystemGroup runs under PhysicsSystemGroup; handle ordering at group composition.
    [UpdateBefore(typeof(AISystemGroup))]
    public partial class InterruptSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for group decision systems.
    /// Runs after group membership, before interrupt handling.
    /// </summary>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateBefore(typeof(InterruptSystemGroup))]
    public partial class GroupDecisionSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for combat systems.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial class CombatSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for thrown object physics setup (velocity, gravity, etc.).
    /// Runs before BuildPhysicsWorld to ensure thrown objects have proper initial state.
    /// Game-specific systems (e.g., MiracleTokenVelocitySystem) should use this group for ordering.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(PhysicsSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(Unity.Physics.Systems.BuildPhysicsWorld))]
    public partial class ThrownObjectPrePhysicsSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for divine hand interaction.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial class HandSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for vegetation systems.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial class VegetationSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// System group for construction systems.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ResourceSystemGroup))]
    public partial class ConstructionSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// High-priority system group for camera/input handling. Executes at the end of InitializationSystemGroup
    /// so input is processed before the SimulationSystemGroup begins.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial class CameraInputSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Late simulation group for cleanup and state recording.
    /// </summary>
    /// <remarks>See Docs/TruthSources/RuntimeLifecycle_TruthSource.md for canonical ordering expectations.</remarks>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial class LateSimulationSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// PureDOTS presentation system group for rendering/UI bridge systems.
    /// Runs under Unity's PresentationSystemGroup for proper frame-time execution.
    /// Consumes simulation data for visualization. Guarded by PresentationRewindGuardSystem.
    /// </summary>
    /// <remarks>
    /// This group provides logical organization for PureDOTS presentation systems.
    /// All systems in this group ultimately run in Unity's PresentationSystemGroup.
    /// See Docs/FoundationGuidelines.md for presentation system group policy.
    /// </remarks>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial class PureDotsPresentationSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Presentation group for structural changes that must precede presentation updates.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.BeginPresentationECBSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.EndPresentationECBSystem))]
    public partial class StructuralChangePresentationSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// Presentation group for component-data updates after structural changes.
    /// </summary>
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    [UpdateAfter(typeof(StructuralChangePresentationSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.EndPresentationECBSystem))]
    public partial class UpdatePresentationSystemGroup : InstrumentedComponentSystemGroup { }

    /// <summary>
    /// [DEPRECATED] Old PresentationSystemGroup - use Unity.Entities.PresentationSystemGroup or PureDotsPresentationSystemGroup instead.
    /// This type is kept for compatibility but should not be used in new code.
    /// </summary>
    [System.Obsolete("Use Unity.Entities.PresentationSystemGroup or PureDOTS.Systems.PureDotsPresentationSystemGroup instead. See Docs/FoundationGuidelines.md for policy.")]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial class PresentationSystemGroup : ComponentSystemGroup { }
}
