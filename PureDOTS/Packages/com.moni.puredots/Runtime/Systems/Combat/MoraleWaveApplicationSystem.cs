using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Applies morale wave effects to combat stats.
    /// Processes MoraleWaveTarget components created by MoraleWaveSystem and modifies CombatStats.Morale.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MoraleWaveSystem))]
    public partial struct MoraleWaveApplicationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            var combatStatsLookup = SystemAPI.GetComponentLookup<CombatStats>(false);

            combatStatsLookup.Update(ref state);

            // Process all morale wave targets
            foreach (var (waveTarget, entity) in SystemAPI.Query<
                RefRO<MoraleWaveTarget>>()
                .WithEntityAccess())
            {
                if (!combatStatsLookup.HasComponent(entity))
                    continue;

                var stats = combatStatsLookup[entity];
                
                // Apply intensity to morale (intensity is typically -0.3 to +0.3, scale to morale points)
                // Intensity of -0.3 = -3 morale points, +0.3 = +3 morale points
                float moraleChange = waveTarget.ValueRO.AppliedIntensity * 10f;
                float newMorale = math.clamp(stats.Morale + moraleChange, 0f, 100f);
                
                stats.Morale = (byte)math.round(newMorale);
                combatStatsLookup[entity] = stats;

                // Remove MoraleWaveTarget after application (one-time effect)
                ecb.RemoveComponent<MoraleWaveTarget>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}

