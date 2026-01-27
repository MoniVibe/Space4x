using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Builds filtered graph view per faction from global graph + KnownFacts.
    /// When KnownFacts system exists, entities route using their world model (which can be stale or wrong).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ColdPathSystemGroup))]
    [UpdateAfter(typeof(NavGraphStateUpdateSystem))]
    public partial struct NavKnowledgeGraphBuilderSystem : ISystem
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
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            // TODO: When KnownFacts system exists:
            // 1. Query entities with NavKnowledgeFilter
            // 2. For each filter, build a filtered view of the navigation graph:
            //    - Use KnownFacts to determine which nodes/edges exist (may be stale)
            //    - Use KnownFacts to determine which edges are blocked (sieges, blockades)
            //    - Use KnownFacts to determine which warp relays/roads are online/captured/destroyed
            // 3. Store filtered graph view (or mark edges as filtered in original graph)
            // 4. Pathfinding queries use filtered graph instead of ground truth

            // For now, this is a placeholder that can be extended when KnownFacts system exists
            // Currently, pathfinding uses ground truth (no filtering)
        }
    }
}






















