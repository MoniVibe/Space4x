using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Maintains registry entries for player abilities / miracles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(MiracleEffectSystemGroup), OrderFirst = true)]
    public partial struct AbilityRegistrySystem : ISystem
    {
        private EntityQuery _abilityQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _abilityQuery = SystemAPI.QueryBuilder()
                .WithAll<AbilityId, AbilityState>()
                .Build();

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<AbilityRegistry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) ||
                timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var registryEntity = SystemAPI.GetSingletonEntity<AbilityRegistry>();
            var registry = SystemAPI.GetComponentRW<AbilityRegistry>(registryEntity);
            ref var metadata = ref SystemAPI.GetComponentRW<RegistryMetadata>(registryEntity).ValueRW;
            var entries = state.EntityManager.GetBuffer<AbilityRegistryEntry>(registryEntity);

            var expectedCount = math.max(8, _abilityQuery.CalculateEntityCount());
            using var builder = new DeterministicRegistryBuilder<AbilityRegistryEntry>(expectedCount, Allocator.Temp);

            var totalAbilities = 0;
            var readyCount = 0;

            foreach (var (abilityId, abilityState, entity) in SystemAPI.Query<RefRO<AbilityId>, RefRO<AbilityState>>().WithEntityAccess())
            {
                builder.Add(new AbilityRegistryEntry
                {
                    AbilityEntity = entity,
                    AbilityId = abilityId.ValueRO.Value,
                    Owner = abilityState.ValueRO.Owner,
                    CooldownRemaining = abilityState.ValueRO.CooldownRemaining,
                    Charges = abilityState.ValueRO.Charges,
                    Flags = abilityState.ValueRO.Flags
                });

                totalAbilities++;

                var isReady = (abilityState.ValueRO.Flags & AbilityStatusFlags.Ready) != 0 && abilityState.ValueRO.CooldownRemaining <= 0f;
                if (isReady)
                {
                    readyCount++;
                }
            }

            var continuity = RegistryContinuitySnapshot.WithoutSpatialData(requireSync: false);
            builder.ApplyTo(ref entries, ref metadata, timeState.Tick, continuity);

            registry.ValueRW = new AbilityRegistry
            {
                TotalAbilities = totalAbilities,
                ReadyAbilityCount = readyCount,
                LastUpdateTick = timeState.Tick
            };
        }
    }
}
