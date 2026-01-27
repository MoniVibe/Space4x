using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Presentation;
using PureDOTS.Runtime.Stats;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Stats
{
    /// <summary>
    /// Presentation adapter system for stat display bindings.
    /// Converts stat data into presentation spawn/recycle requests for HUDs.
    /// Uses request buffers only; no structural changes except ECB playback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct StatPresentationAdapterSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // This system adapts stat data for presentation layer
            // It reads StatDisplayBinding components and generates presentation requests
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<PresentationCommandQueue>(out _))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Process StatDisplayBinding components and generate presentation requests
            // This is a placeholder - actual implementation will:
            // 1. Query entities with StatDisplayBinding
            // 2. Look up referenced entity's stats
            // 3. Generate presentation spawn requests for stat displays
            // 4. Support Minimal/Fancy binding sets (loaded from StatBindingLoader)

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// System for hot-swapping between Minimal and Fancy stat binding sets.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(Unity.Entities.PresentationSystemGroup))]
    public partial struct StatBindingSwapSystem : ISystem
    {
        private byte _currentBindingSet; // 0 = Minimal, 1 = Fancy

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _currentBindingSet = 0; // Default to Minimal
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // This system provides functionality to swap binding sets
            // Implementation will be added when binding loader is integrated
            if (_currentBindingSet > 1)
            {
                _currentBindingSet = 1; // Clamp to known binding sets
            }
        }

        /// <summary>
        /// Swap to Minimal binding set.
        /// </summary>
        public void SwapToMinimal(ref SystemState state)
        {
            _currentBindingSet = 0;
            // Trigger presentation refresh
        }

        /// <summary>
        /// Swap to Fancy binding set.
        /// </summary>
        public void SwapToFancy(ref SystemState state)
        {
            _currentBindingSet = 1;
            // Trigger presentation refresh
        }
    }
}

