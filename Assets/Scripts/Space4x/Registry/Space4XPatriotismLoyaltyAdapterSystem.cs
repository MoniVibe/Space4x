using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Systems.Stats;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Adapts Space4X patriotism (BelongingEntry) into PureDOTS loyalty components
    /// so trait drift can use shared baselines.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TraitDriftSystem))]
    public partial struct Space4XPatriotismLoyaltyAdapterSystem : ISystem
    {
        private const uint UpdateIntervalTicks = 10;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;
            if (currentTick % UpdateIntervalTicks != 0)
            {
                return;
            }

            var config = SystemAPI.TryGetSingleton<LoyaltyConfig>(out var configSingleton)
                ? configSingleton
                : LoyaltyHelpers.DefaultConfig;

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (belongings, entity) in SystemAPI.Query<DynamicBuffer<BelongingEntry>>().WithEntityAccess())
            {
                if (belongings.Length == 0)
                {
                    continue;
                }

                int primaryIndex = -1;
                bool hasPrimaryFlag = false;
                byte bestLoyalty = 0;
                byte bestPriority = 0;

                for (int i = 0; i < belongings.Length; i++)
                {
                    var entry = belongings[i];
                    if (entry.IsPrimaryIdentity != 0)
                    {
                        if (!hasPrimaryFlag || entry.Loyalty > bestLoyalty || (entry.Loyalty == bestLoyalty && entry.Priority > bestPriority))
                        {
                            hasPrimaryFlag = true;
                            primaryIndex = i;
                            bestLoyalty = entry.Loyalty;
                            bestPriority = entry.Priority;
                        }
                    }
                    else if (!hasPrimaryFlag)
                    {
                        if (primaryIndex < 0 || entry.Loyalty > bestLoyalty || (entry.Loyalty == bestLoyalty && entry.Priority > bestPriority))
                        {
                            primaryIndex = i;
                            bestLoyalty = entry.Loyalty;
                            bestPriority = entry.Priority;
                        }
                    }
                }

                if (primaryIndex < 0)
                {
                    continue;
                }

                var primary = belongings[primaryIndex];
                var targetType = MapTier(primary.Tier);

                if (!SystemAPI.HasComponent<EntityLoyalty>(entity))
                {
                    ecb.AddComponent(entity, new EntityLoyalty
                    {
                        PrimaryTarget = primary.Target,
                        TargetType = targetType,
                        Loyalty = primary.Loyalty,
                        State = LoyaltyHelpers.GetState(primary.Loyalty),
                        NaturalLoyalty = primary.Loyalty,
                        DesertionRisk = LoyaltyHelpers.GetDesertionRisk(primary.Loyalty, config),
                        LastLoyaltyChangeTick = currentTick
                    });
                }
                else
                {
                    var loyalty = SystemAPI.GetComponentRW<EntityLoyalty>(entity);
                    bool changed = loyalty.ValueRO.PrimaryTarget != primary.Target ||
                                   loyalty.ValueRO.Loyalty != primary.Loyalty ||
                                   loyalty.ValueRO.TargetType != targetType;

                    loyalty.ValueRW.PrimaryTarget = primary.Target;
                    loyalty.ValueRW.TargetType = targetType;
                    loyalty.ValueRW.Loyalty = primary.Loyalty;
                    loyalty.ValueRW.State = LoyaltyHelpers.GetState(primary.Loyalty);
                    loyalty.ValueRW.DesertionRisk = LoyaltyHelpers.GetDesertionRisk(primary.Loyalty, config);

                    if (changed)
                    {
                        loyalty.ValueRW.LastLoyaltyChangeTick = currentTick;
                    }
                }

                if (SystemAPI.HasBuffer<SecondaryLoyalty>(entity))
                {
                    var secondaryBuffer = SystemAPI.GetBuffer<SecondaryLoyalty>(entity);
                    secondaryBuffer.Clear();
                    FillSecondaryLoyalties(ref secondaryBuffer, in belongings, primaryIndex);
                }
                else
                {
                    var secondaryBuffer = ecb.AddBuffer<SecondaryLoyalty>(entity);
                    AppendSecondaryLoyalties(ref ecb, entity, in belongings, primaryIndex);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static LoyaltyTarget MapTier(BelongingTier tier)
        {
            return tier switch
            {
                BelongingTier.Family => LoyaltyTarget.Family,
                BelongingTier.Dynasty => LoyaltyTarget.Family,
                BelongingTier.Guild => LoyaltyTarget.Guild,
                BelongingTier.Colony => LoyaltyTarget.Colony,
                BelongingTier.Faction => LoyaltyTarget.Faction,
                BelongingTier.Empire => LoyaltyTarget.Faction,
                BelongingTier.Ideology => LoyaltyTarget.Ideology,
                BelongingTier.Species => LoyaltyTarget.Tradition,
                _ => LoyaltyTarget.None
            };
        }

        private static void FillSecondaryLoyalties(
            ref DynamicBuffer<SecondaryLoyalty> buffer,
            in DynamicBuffer<BelongingEntry> belongings,
            int primaryIndex)
        {
            for (int i = 0; i < belongings.Length; i++)
            {
                if (i == primaryIndex)
                {
                    continue;
                }

                var entry = belongings[i];
                if (entry.Target == Entity.Null || entry.Loyalty == 0)
                {
                    continue;
                }

                buffer.Add(new SecondaryLoyalty
                {
                    Target = entry.Target,
                    TargetType = MapTier(entry.Tier),
                    Loyalty = entry.Loyalty,
                    State = LoyaltyHelpers.GetState(entry.Loyalty)
                });
            }
        }

        private static void AppendSecondaryLoyalties(
            ref EntityCommandBuffer ecb,
            Entity entity,
            in DynamicBuffer<BelongingEntry> belongings,
            int primaryIndex)
        {
            for (int i = 0; i < belongings.Length; i++)
            {
                if (i == primaryIndex)
                {
                    continue;
                }

                var entry = belongings[i];
                if (entry.Target == Entity.Null || entry.Loyalty == 0)
                {
                    continue;
                }

                ecb.AppendToBuffer(entity, new SecondaryLoyalty
                {
                    Target = entry.Target,
                    TargetType = MapTier(entry.Tier),
                    Loyalty = entry.Loyalty,
                    State = LoyaltyHelpers.GetState(entry.Loyalty)
                });
            }
        }
    }
}
