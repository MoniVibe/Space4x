using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Runtime.Time;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Systems.Social.Companions
{
    /// <summary>
    /// System that detects companion bond endings: death and separation.
    /// Transitions bonds to EndedByDeath or Broken states.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(CompanionEventSystem))]
    public partial struct CompanionEndSystem : ISystem
    {
        ComponentLookup<DeathState> _deathLookup;
        BufferLookup<CompanionLink> _companionLinkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _deathLookup = state.GetComponentLookup<DeathState>(true);
            _companionLinkLookup = state.GetBufferLookup<CompanionLink>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                rewindState.Mode != RewindMode.Record)
                return;

            _deathLookup.Update(ref state);
            _companionLinkLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            var job = new EndDetectionJob
            {
                CurrentTick = currentTick,
                DeathLookup = _deathLookup,
                CompanionLinkLookup = _companionLinkLookup
            };
            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct EndDetectionJob : IJobEntity
        {
            public uint CurrentTick;
            [ReadOnly] public ComponentLookup<DeathState> DeathLookup;
            [ReadOnly] public BufferLookup<CompanionLink> CompanionLinkLookup;

            void Execute(ref CompanionBond bond)
            {
                // Skip if already ended
                if (bond.State == CompanionState.Broken || bond.State == CompanionState.EndedByDeath)
                    return;

                // Check for death
                bool aDead = bond.A == Entity.Null || DeathLookup.HasComponent(bond.A);
                bool bDead = bond.B == Entity.Null || DeathLookup.HasComponent(bond.B);

                if (aDead || bDead)
                {
                    bond.State = CompanionState.EndedByDeath;
                    return;
                }

                // Check for separation (no interaction for long period)
                // This is simplified - in practice you'd track last interaction tick
                // For now, check if intensity has decayed too low
                if (bond.Intensity < 0.1f && bond.State == CompanionState.Strained)
                {
                    bond.State = CompanionState.Broken;
                    return;
                }

                // Check if entities are in different groups (separation)
                // Simplified check - if both have companion links but bond intensity is decaying
                bool aHasLinks = CompanionLinkLookup.HasBuffer(bond.A);
                bool bHasLinks = CompanionLinkLookup.HasBuffer(bond.B);

                // If entities exist but bond is decaying and no recent interaction
                uint ticksSinceUpdate = CurrentTick - bond.LastUpdateTick;
                if (ticksSinceUpdate > 10000 && bond.Intensity < 0.2f) // ~2.7 minutes at 60tps
                {
                    bond.State = CompanionState.Broken;
                }
            }
        }
    }
}

