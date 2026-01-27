using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Construction;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Construction
{
    /// <summary>
    /// Applies player/god influence to construction intents.
    /// Handles manual placement, vetoes, and category locks.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ConstructionPatternSelectionSystem))]
    public partial struct ConstructionIntentInfluenceSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // Process groups with ConstructionIntent buffers
            foreach (var (intents, entity) in SystemAPI.Query<DynamicBuffer<ConstructionIntent>>().WithEntityAccess())
            {
                // TODO: Read player/god influence commands from a command buffer or singleton
                // For now, this is a stub that can be extended by game-specific systems
                // Example: Player vetoes category, boosts urgency, manually places building

                // Stub implementation:
                // - Player commands would come from UI/input systems
                // - This system would apply them to intents:
                //   - Boost/dampen Urgency by category
                //   - Mark intents as Status=Blocked if vetoed
                //   - Create new intents with Source=Player for manual placements
            }
        }
    }
}
























