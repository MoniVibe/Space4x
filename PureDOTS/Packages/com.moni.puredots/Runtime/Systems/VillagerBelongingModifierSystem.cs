using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Converts the villager's top-N belonging entries into archetype modifiers using aggregate/culture profiles.
    /// Runs before archetype resolution so the resolved profile includes current loyalty effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(VillagerArchetypeResolutionSystem))]
    public partial struct VillagerBelongingModifierSystem : ISystem
    {
        private EntityQuery _villagerQuery;
        private BufferLookup<VillagerAggregateModifierProfile> _aggregateModifierLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _villagerQuery = SystemAPI.QueryBuilder()
                .WithAll<VillagerBelonging, VillagerArchetypeModifier>()
                .Build();

            _aggregateModifierLookup = state.GetBufferLookup<VillagerAggregateModifierProfile>(true);
            state.RequireForUpdate(_villagerQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _aggregateModifierLookup.Update(ref state);

            var job = new ApplyBelongingModifiersJob
            {
                AggregateModifierLookup = _aggregateModifierLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        private struct BelongingRank
        {
            public int Index;
            public short Loyalty;
        }

        [BurstCompile]
        public partial struct ApplyBelongingModifiersJob : IJobEntity
        {
            [ReadOnly] public BufferLookup<VillagerAggregateModifierProfile> AggregateModifierLookup;

            public void Execute(Entity entity,
                DynamicBuffer<VillagerBelonging> belongings,
                DynamicBuffer<VillagerArchetypeModifier> modifiers)
            {
                modifiers.Clear();

                if (!belongings.IsCreated || belongings.Length == 0)
                {
                    return;
                }

                var ranked = new FixedList64Bytes<BelongingRank>();

                for (var i = 0; i < belongings.Length; i++)
                {
                    var entry = belongings[i];
                    var loyalty = (short)math.clamp(entry.Loyalty,
                        VillagerBelongingLimits.MinLoyalty,
                        VillagerBelongingLimits.MaxLoyalty);

                    if (ranked.Length == VillagerBelongingLimits.MaxTrackedBelongings &&
                        ranked.Length > 0 &&
                        loyalty <= ranked[ranked.Length - 1].Loyalty)
                    {
                        continue;
                    }

                    var candidate = new BelongingRank
                    {
                        Index = i,
                        Loyalty = loyalty
                    };

                    var insertIndex = 0;
                    while (insertIndex < ranked.Length && loyalty <= ranked[insertIndex].Loyalty)
                    {
                        insertIndex++;
                    }

                    var oldLength = ranked.Length;
                    var newLength = math.min(oldLength + 1, VillagerBelongingLimits.MaxTrackedBelongings);
                    if (oldLength < newLength)
                    {
                        ranked.Add(default);
                    }
                    for (var shift = newLength - 1; shift > insertIndex && shift > 0; shift--)
                    {
                        ranked[shift] = ranked[shift - 1];
                    }
                    ranked[insertIndex] = candidate;
                    ranked.Length = newLength;
                }

                for (var r = 0; r < ranked.Length; r++)
                {
                    var rank = ranked[r];
                    var loyalty = rank.Loyalty;
                    if (loyalty == 0)
                    {
                        continue;
                    }

                    var belonging = belongings[rank.Index];
                    if (!AggregateModifierLookup.HasBuffer(belonging.AggregateEntity))
                    {
                        continue;
                    }

                    var aggregateModifiers = AggregateModifierLookup[belonging.AggregateEntity];
                    if (!aggregateModifiers.IsCreated || aggregateModifiers.Length == 0)
                    {
                        continue;
                    }

                    var loyaltyMagnitude = math.abs(loyalty);
                    var normalized = loyalty / (float)VillagerBelongingLimits.MaxLoyalty;

                    for (var m = 0; m < aggregateModifiers.Length; m++)
                    {
                        var profile = aggregateModifiers[m];
                        var threshold = math.max(1, math.abs(profile.LoyaltyThreshold));
                        var intensityRatio = math.saturate(loyaltyMagnitude / threshold);
                        var scaledIntensity = normalized >= 0f
                            ? intensityRatio
                            : -intensityRatio;

                        var scaledModifier = VillagerArchetypeDefaults.ScaleModifier(profile.Modifier, scaledIntensity);
                        scaledModifier.Source = (VillagerArchetypeModifierSource)belonging.Kind;
                        modifiers.Add(scaledModifier);
                    }
                }
            }
        }
    }
}
