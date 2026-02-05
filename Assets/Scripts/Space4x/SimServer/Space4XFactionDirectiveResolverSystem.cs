using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSimServerHttpSystem))]
    [UpdateBefore(typeof(Space4XFactionGoalSystem))]
    public partial struct Space4XFactionDirectiveResolverSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XSimServerConfig>();
            state.RequireForUpdate<Space4XFaction>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            var entityManager = state.EntityManager;

            foreach (var (faction, entity) in SystemAPI.Query<RefRW<Space4XFaction>>().WithEntityAccess())
            {
                EnsureBaseline(entityManager, entity, faction.ValueRO);

                if (!entityManager.HasBuffer<Space4XFactionOrder>(entity))
                {
                    if (entityManager.HasComponent<Space4XFactionDirective>(entity))
                    {
                        var directive = entityManager.GetComponentData<Space4XFactionDirective>(entity);
                        if (directive.ExpiresAtTick != 0 && tick >= directive.ExpiresAtTick)
                        {
                            ApplyBaseline(entityManager, entity, faction, tick);
                            continue;
                        }
                    }

                    EnsureDirectiveDefault(entityManager, entity, faction.ValueRO, tick);
                    continue;
                }

                var orders = entityManager.GetBuffer<Space4XFactionOrder>(entity);
                if (orders.Length == 0)
                {
                    ApplyBaseline(entityManager, entity, faction, tick);
                    continue;
                }

                ResolveOrders(entityManager, entity, faction, ref orders, tick);
            }
        }

        private static void EnsureBaseline(EntityManager entityManager, Entity entity, in Space4XFaction faction)
        {
            if (entityManager.HasComponent<Space4XFactionDirectiveBaseline>(entity))
            {
                return;
            }

            entityManager.AddComponentData(entity, new Space4XFactionDirectiveBaseline
            {
                Security = (float)faction.MilitaryFocus,
                Economy = (float)faction.TradeFocus,
                Research = (float)faction.ResearchFocus,
                Expansion = (float)faction.ExpansionDrive,
                Diplomacy = 0.5f,
                Production = (float)faction.TradeFocus,
                Food = 0.5f,
                Aggression = (float)faction.Aggression,
                RiskTolerance = (float)faction.RiskTolerance
            });
        }

        private static void EnsureDirectiveDefault(EntityManager entityManager, Entity entity, in Space4XFaction faction, uint tick)
        {
            if (!entityManager.HasComponent<Space4XFactionDirective>(entity))
            {
                entityManager.AddComponentData(entity, new Space4XFactionDirective
                {
                    Security = (float)faction.MilitaryFocus,
                    Economy = (float)faction.TradeFocus,
                    Research = (float)faction.ResearchFocus,
                    Expansion = (float)faction.ExpansionDrive,
                    Diplomacy = 0.5f,
                    Production = (float)faction.TradeFocus,
                    Food = 0.5f,
                    Priority = 0.25f,
                    LastUpdatedTick = tick,
                    ExpiresAtTick = 0,
                    DirectiveId = new FixedString64Bytes("default")
                });
            }
        }

        private static void ApplyBaseline(EntityManager entityManager, Entity entity, RefRW<Space4XFaction> faction, uint tick)
        {
            if (!entityManager.HasComponent<Space4XFactionDirectiveBaseline>(entity))
            {
                return;
            }

            var baseline = entityManager.GetComponentData<Space4XFactionDirectiveBaseline>(entity);
            ApplyResolvedWeights(entityManager, entity, ref faction.ValueRW, baseline, tick, new FixedString64Bytes("default"), 0f, 0);
        }

        private static void ResolveOrders(
            EntityManager entityManager,
            Entity entity,
            RefRW<Space4XFaction> faction,
            ref DynamicBuffer<Space4XFactionOrder> orders,
            uint tick)
        {
            var baseline = entityManager.GetComponentData<Space4XFactionDirectiveBaseline>(entity);
            var resolved = new DirectiveWeights(baseline);

            for (int i = orders.Length - 1; i >= 0; i--)
            {
                var order = orders[i];
                if (order.ExpiresAtTick != 0 && tick >= order.ExpiresAtTick)
                {
                    orders.RemoveAt(i);
                    continue;
                }
            }

            if (orders.Length == 0)
            {
                ApplyBaseline(entityManager, entity, faction, tick);
                return;
            }

            FixedString64Bytes topId = new FixedString64Bytes("default");
            float topPriority = -1f;
            uint topExpiry = 0u;

            for (int i = 0; i < orders.Length; i++)
            {
                var order = orders[i];
                if (order.Priority > topPriority)
                {
                    topPriority = order.Priority;
                    topId = order.OrderId;
                    topExpiry = order.ExpiresAtTick;
                }
            }

            using var processed = new NativeArray<byte>(orders.Length, Allocator.Temp);
            for (int applied = 0; applied < orders.Length; applied++)
            {
                var selectedIndex = -1;
                var selectedPriority = float.MaxValue;

                for (int i = 0; i < orders.Length; i++)
                {
                    if (processed[i] != 0)
                    {
                        continue;
                    }

                    var priority = orders[i].Priority;
                    if (priority < selectedPriority)
                    {
                        selectedPriority = priority;
                        selectedIndex = i;
                    }
                }

                if (selectedIndex < 0)
                {
                    break;
                }

                processed[selectedIndex] = 1;
                var order = orders[selectedIndex];
                float weight = math.saturate(order.Priority);
                ApplyOrder(ref resolved, order, weight);
            }

            ApplyResolvedWeights(entityManager, entity, ref faction.ValueRW, resolved, tick, topId, topPriority, topExpiry);
        }

        private static void ApplyOrder(ref DirectiveWeights resolved, in Space4XFactionOrder order, float weight)
        {
            if (order.Mode == Space4XDirectiveMode.Override)
            {
                OverrideField(order.Security, ref resolved.Security);
                OverrideField(order.Economy, ref resolved.Economy);
                OverrideField(order.Research, ref resolved.Research);
                OverrideField(order.Expansion, ref resolved.Expansion);
                OverrideField(order.Diplomacy, ref resolved.Diplomacy);
                OverrideField(order.Production, ref resolved.Production);
                OverrideField(order.Food, ref resolved.Food);
                OverrideField(order.Aggression, ref resolved.Aggression);
                OverrideField(order.RiskTolerance, ref resolved.RiskTolerance);
                return;
            }

            BlendField(order.Security, weight, ref resolved.Security);
            BlendField(order.Economy, weight, ref resolved.Economy);
            BlendField(order.Research, weight, ref resolved.Research);
            BlendField(order.Expansion, weight, ref resolved.Expansion);
            BlendField(order.Diplomacy, weight, ref resolved.Diplomacy);
            BlendField(order.Production, weight, ref resolved.Production);
            BlendField(order.Food, weight, ref resolved.Food);
            BlendField(order.Aggression, weight, ref resolved.Aggression);
            BlendField(order.RiskTolerance, weight, ref resolved.RiskTolerance);
        }

        private static void ApplyResolvedWeights(
            EntityManager entityManager,
            Entity entity,
            ref Space4XFaction faction,
            DirectiveWeights resolved,
            uint tick,
            FixedString64Bytes directiveId,
            float priority,
            uint expiresAt)
        {
            faction.MilitaryFocus = (half)math.saturate(resolved.Security);
            faction.TradeFocus = (half)math.saturate(resolved.Economy);
            faction.ResearchFocus = (half)math.saturate(resolved.Research);
            faction.ExpansionDrive = (half)math.saturate(resolved.Expansion);
            faction.Aggression = (half)math.saturate(resolved.Aggression);
            faction.RiskTolerance = (half)math.saturate(resolved.RiskTolerance);

            if (!entityManager.HasComponent<Space4XFactionDirective>(entity))
            {
                entityManager.AddComponent<Space4XFactionDirective>(entity);
            }

            entityManager.SetComponentData(entity, new Space4XFactionDirective
            {
                Security = math.saturate(resolved.Security),
                Economy = math.saturate(resolved.Economy),
                Research = math.saturate(resolved.Research),
                Expansion = math.saturate(resolved.Expansion),
                Diplomacy = math.saturate(resolved.Diplomacy),
                Production = math.saturate(resolved.Production),
                Food = math.saturate(resolved.Food),
                Priority = math.saturate(priority >= 0f ? priority : 0f),
                LastUpdatedTick = tick,
                ExpiresAtTick = expiresAt,
                DirectiveId = directiveId
            });

            var leader = ResolveLeader(entityManager, entity);
            ApplyDirectiveProfile(entityManager, leader != Entity.Null ? leader : entity,
                resolved.Security,
                resolved.Economy,
                resolved.Research,
                resolved.Expansion,
                resolved.Diplomacy,
                resolved.Aggression,
                resolved.RiskTolerance,
                resolved.Food);
        }

        private static void BlendField(float candidate, float weight, ref float target)
        {
            if (candidate < 0f)
            {
                return;
            }

            target = math.lerp(target, math.saturate(candidate), weight);
        }

        private static void OverrideField(float candidate, ref float target)
        {
            if (candidate < 0f)
            {
                return;
            }

            target = math.saturate(candidate);
        }

        private static void ApplyDirectiveProfile(
            EntityManager entityManager,
            Entity entity,
            float security,
            float economy,
            float research,
            float expansion,
            float diplomacy,
            float aggression,
            float risk,
            float food)
        {
            var target = Space4XSimServerProfileUtility.BuildLeaderDisposition(
                security,
                economy,
                research,
                expansion,
                diplomacy,
                aggression,
                risk,
                food);

            if (entityManager.HasComponent<BehaviorDisposition>(entity))
            {
                var current = entityManager.GetComponentData<BehaviorDisposition>(entity);
                var blended = Space4XSimServerProfileUtility.LerpDisposition(current, target, 0.35f);
                entityManager.SetComponentData(entity, blended);
                return;
            }

            entityManager.AddComponentData(entity, target);
        }

        private static Entity ResolveLeader(EntityManager entityManager, Entity factionEntity)
        {
            if (!entityManager.HasComponent<AuthorityBody>(factionEntity))
            {
                return Entity.Null;
            }

            var body = entityManager.GetComponentData<AuthorityBody>(factionEntity);
            if (body.ExecutiveSeat == Entity.Null || !entityManager.HasComponent<AuthoritySeatOccupant>(body.ExecutiveSeat))
            {
                return Entity.Null;
            }

            var occupant = entityManager.GetComponentData<AuthoritySeatOccupant>(body.ExecutiveSeat).OccupantEntity;
            return entityManager.Exists(occupant) ? occupant : Entity.Null;
        }

        private struct DirectiveWeights
        {
            public float Security;
            public float Economy;
            public float Research;
            public float Expansion;
            public float Diplomacy;
            public float Production;
            public float Food;
            public float Aggression;
            public float RiskTolerance;

            public DirectiveWeights(in Space4XFactionDirectiveBaseline baseline)
            {
                Security = baseline.Security;
                Economy = baseline.Economy;
                Research = baseline.Research;
                Expansion = baseline.Expansion;
                Diplomacy = baseline.Diplomacy;
                Production = baseline.Production;
                Food = baseline.Food;
                Aggression = baseline.Aggression;
                RiskTolerance = baseline.RiskTolerance;
            }
        }
    }
}
