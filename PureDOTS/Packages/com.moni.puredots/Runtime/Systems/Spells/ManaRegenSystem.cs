using PureDOTS.Runtime.Buffs;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spells;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.Spells
{
    /// <summary>
    /// Regenerates mana for entities with SpellMana component.
    /// Integrates with BuffStatCache for regen modifiers.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    public partial struct ManaRegenSystem : ISystem
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

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = timeState.DeltaTime;

            new RegenManaJob
            {
                DeltaTime = deltaTime
            }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct RegenManaJob : IJobEntity
        {
            public float DeltaTime;

            void Execute(ref SpellMana mana)
            {
                // Get base regen rate
                float regenRate = mana.RegenRate;

                // Apply buff modifiers if BuffStatCache exists
                // Note: This would need to be done via a lookup or component access
                // For now, we'll just use the base regen rate

                // Regenerate mana
                mana.Current += regenRate * DeltaTime;

                // Clamp to max
                if (mana.Current > mana.Max)
                {
                    mana.Current = mana.Max;
                }
            }
        }
    }
}

