using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Combat.DamageApplicationSystem))]
    public partial struct Space4XCombatOutcomeCollectionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<DeathEvent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var streamEntity = EnsureStreamEntity(ref state);
            var outcomeBuffer = state.EntityManager.GetBuffer<Space4XCombatOutcomeEvent>(streamEntity);

            foreach (var (deathEvents, entity) in SystemAPI.Query<DynamicBuffer<DeathEvent>>()
                         .WithEntityAccess())
            {
                if (deathEvents.Length == 0)
                {
                    continue;
                }

                for (int i = 0; i < deathEvents.Length; i++)
                {
                    var death = deathEvents[i];
                    outcomeBuffer.Add(new Space4XCombatOutcomeEvent
                    {
                        Attacker = death.KillerEntity,
                        Victim = entity,
                        AttackerFactionId = 0,
                        VictimFactionId = 0,
                        Outcome = Space4XCombatOutcomeType.Destroyed,
                        Tick = death.DeathTick
                    });
                }

                deathEvents.Clear();
            }
        }

        private static Entity EnsureStreamEntity(ref SystemState state)
        {
            using var query = state.GetEntityQuery(ComponentType.ReadOnly<Space4XCombatOutcomeStream>());
            if (query.TryGetSingletonEntity<Space4XCombatOutcomeStream>(out var streamEntity))
            {
                if (!state.EntityManager.HasBuffer<Space4XCombatOutcomeEvent>(streamEntity))
                {
                    state.EntityManager.AddBuffer<Space4XCombatOutcomeEvent>(streamEntity);
                }

                return streamEntity;
            }

            streamEntity = state.EntityManager.CreateEntity(typeof(Space4XCombatOutcomeStream));
            state.EntityManager.AddBuffer<Space4XCombatOutcomeEvent>(streamEntity);
            return streamEntity;
        }
    }
}
