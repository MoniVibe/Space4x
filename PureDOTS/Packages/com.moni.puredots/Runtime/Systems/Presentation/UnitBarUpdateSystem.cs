using PureDOTS.Input;
using PureDOTS.Systems.Input;
using PureDOTS.Systems.Presentation;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Presentation
{
    /// <summary>
    /// Updates UnitBarState component with normalized bar values (0-1) for HUD rendering.
    /// Reads entity stats (Health, ResourceStorage, FocusState, etc.) and writes normalized values.
    /// Games should extend this system to read their specific stat components.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SelectionSystem))]
    public partial struct UnitBarUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            // System runs on entities with UnitBarState
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Update bar state for all entities that have it
            // Games should add UnitBarState to entities that need HUD bars
            foreach (var (barStateRef, entity) in SystemAPI.Query<RefRW<UnitBarState>>()
                         .WithEntityAccess())
            {
                var barState = barStateRef.ValueRO;

                // Set IsSelected flag
                bool isSelected = state.EntityManager.HasComponent<SelectedTag>(entity);
                barState.IsSelected = (byte)(isSelected ? 1 : 0);

                // TODO: Games should extend this to read their specific stat components:
                // - Health component → Health01
                // - ResourceStorage → ResourceFill01
                // - FocusState → Focus01
                // - Mana/Energy/Stamina pools → respective 01 values
                // For now, leave values at defaults (0)

                barStateRef.ValueRW = barState;
            }
        }
    }
}

