using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Updates villager needs (hunger, energy, health, morale) during the normal simulation loop.
    /// Mirrors deterministic logic from the legacy DOTS stack while remaining pause/rewind aware.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup))]
    public partial struct VillagerNeedsSystem : ISystem
    {
        private EntityQuery _villagerQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerId, VillagerNeeds>()
                .WithNone<VillagerDeadTag, PlaybackGuardTag>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate(_villagerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var job = new UpdateNeedsJob
            {
                DeltaTime = timeState.FixedDeltaTime,
                HungerIncreaseRate = 5f,
                EnergyDecreaseRate = 3f,
                HealthRegenRate = 1f,
                StarvationDamageRate = 10f
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct UpdateNeedsJob : IJobEntity
        {
            public float DeltaTime;
            public float HungerIncreaseRate;
            public float EnergyDecreaseRate;
            public float HealthRegenRate;
            public float StarvationDamageRate;

            public void Execute(ref VillagerNeeds needs, in VillagerAIState aiState)
            {
                needs.Hunger = math.min(100f, needs.Hunger + HungerIncreaseRate * DeltaTime);

                if (aiState.CurrentState == VillagerAIState.State.Working)
                {
                    needs.Energy = math.max(0f, needs.Energy - EnergyDecreaseRate * DeltaTime);
                }
                else if (aiState.CurrentState == VillagerAIState.State.Sleeping)
                {
                    needs.Energy = math.min(100f, needs.Energy + EnergyDecreaseRate * 2f * DeltaTime);
                }

                if (needs.Hunger >= 90f)
                {
                    needs.Health = math.max(0f, needs.Health - StarvationDamageRate * DeltaTime);
                    needs.Morale = math.max(0f, needs.Morale - 5f * DeltaTime);
                }
                else if (needs.Hunger < 50f && needs.Health < needs.MaxHealth)
                {
                    needs.Health = math.min(needs.MaxHealth, needs.Health + HealthRegenRate * DeltaTime);
                }

                var maxHealthSafe = math.max(1f, needs.MaxHealth);
                var satisfaction = (100f - needs.Hunger) * 0.5f + needs.Energy * 0.3f + (needs.Health / maxHealthSafe) * 100f * 0.2f;
                needs.Morale = math.lerp(needs.Morale, satisfaction, DeltaTime * 0.1f);
            }
        }
    }
}
