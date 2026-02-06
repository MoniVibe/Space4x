using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Aggregates installed augmentations into summary stats for entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XAugmentationAggregationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InstalledAugmentation>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) &&
                rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (augments, entity) in SystemAPI.Query<DynamicBuffer<InstalledAugmentation>>().WithEntityAccess())
            {
                var summary = AggregateAugments(augments);

                if (SystemAPI.HasComponent<AugmentationStats>(entity))
                {
                    var stats = SystemAPI.GetComponentRW<AugmentationStats>(entity);
                    stats.ValueRW.PhysiqueModifier = summary.PhysiqueModifier;
                    stats.ValueRW.FinesseModifier = summary.FinesseModifier;
                    stats.ValueRW.WillModifier = summary.WillModifier;
                    stats.ValueRW.GeneralModifier = summary.GeneralModifier;
                    stats.ValueRW.TotalUpkeepCost = summary.TotalUpkeepCost;
                    stats.ValueRW.AggregatedRiskFactor = summary.RiskFactor;
                }
                else
                {
                    ecb.AddComponent(entity, new AugmentationStats
                    {
                        PhysiqueModifier = summary.PhysiqueModifier,
                        FinesseModifier = summary.FinesseModifier,
                        WillModifier = summary.WillModifier,
                        GeneralModifier = summary.GeneralModifier,
                        TotalUpkeepCost = summary.TotalUpkeepCost,
                        AggregatedRiskFactor = summary.RiskFactor
                    });
                }

                if (SystemAPI.HasComponent<AugmentationSummary>(entity))
                {
                    ecb.SetComponent(entity, summary);
                }
                else
                {
                    ecb.AddComponent(entity, summary);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static AugmentationSummary AggregateAugments(DynamicBuffer<InstalledAugmentation> augments)
        {
            var summary = new AugmentationSummary
            {
                PhysiqueModifier = 0f,
                FinesseModifier = 0f,
                WillModifier = 0f,
                GeneralModifier = 0f,
                TotalUpkeepCost = 0f,
                RiskFactor = 0f,
                AugmentCount = (byte)math.min(255, augments.Length),
                BionicsCount = 0
            };

            for (int i = 0; i < augments.Length; i++)
            {
                var augment = augments[i];
                var quality = math.clamp(augment.Quality, 0f, 1f);
                var tierFactor = math.clamp(augment.Tier / 10f, 0f, 3f);
                var baseMod = quality * (0.03f + tierFactor * 0.02f);

                summary.GeneralModifier += baseMod;
                summary.TotalUpkeepCost += 1f + tierFactor * 0.5f;
                summary.RiskFactor += math.saturate((1f - quality) * 0.1f + tierFactor * 0.02f);

                var augmentId = augment.AugmentId.ToString().ToLowerInvariant();
                if (augmentId.Contains("phys") || augmentId.Contains("strength"))
                {
                    summary.PhysiqueModifier += baseMod;
                }
                else if (augmentId.Contains("fin") || augmentId.Contains("dex"))
                {
                    summary.FinesseModifier += baseMod;
                }
                else if (augmentId.Contains("will") || augmentId.Contains("psi"))
                {
                    summary.WillModifier += baseMod;
                }

                if (augmentId.Contains("bionic") || augmentId.Contains("prosthetic") || augmentId.Contains("augment"))
                {
                    summary.BionicsCount = (byte)math.min(255, summary.BionicsCount + 1);
                }
            }

            summary.RiskFactor = math.saturate(summary.RiskFactor);
            return summary;
        }
    }
}
