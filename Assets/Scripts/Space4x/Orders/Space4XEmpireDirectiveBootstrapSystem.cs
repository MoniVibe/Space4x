using Space4X.Registry;
using TimeState = PureDOTS.Runtime.Components.TimeState;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Orders
{
    /// <summary>
    /// Ensures empire factions have directive buffers with baseline directives.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XEmpireDirectiveBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            uint currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                if (faction.ValueRO.Type != FactionType.Empire && faction.ValueRO.Type != FactionType.Player)
                    continue;

                if (!state.EntityManager.HasBuffer<EmpireDirective>(entity))
                {
                    state.EntityManager.AddBuffer<EmpireDirective>(entity);
                }

                var directives = state.EntityManager.GetBuffer<EmpireDirective>(entity);
                if (directives.Length > 0)
                    continue;

                AddDirective(ref directives, EmpireDirectiveKeys.SecureResources, math.clamp((float)faction.ValueRO.TradeFocus * 100f, 0f, 100f), currentTick);
                AddDirective(ref directives, EmpireDirectiveKeys.Expand, math.clamp((float)faction.ValueRO.ExpansionDrive * 100f, 0f, 100f), currentTick);
                AddDirective(ref directives, EmpireDirectiveKeys.ResearchFocus, math.clamp((float)faction.ValueRO.ResearchFocus * 100f, 0f, 100f), currentTick);
                AddDirective(ref directives, EmpireDirectiveKeys.MilitaryPosture, math.clamp((float)faction.ValueRO.MilitaryFocus * 100f, 0f, 100f), currentTick);
                AddDirective(ref directives, EmpireDirectiveKeys.TradeBias, math.clamp((float)faction.ValueRO.TradeFocus * 90f, 0f, 100f), currentTick);
            }
        }

        private static void AddDirective(ref DynamicBuffer<EmpireDirective> directives, FixedString32Bytes directiveType, float basePriority, uint currentTick)
        {
            if (basePriority <= 0f)
                return;

            directives.Add(new EmpireDirective
            {
                DirectiveType = directiveType,
                BasePriority = basePriority,
                PriorityWeight = 1f,
                IssuedTick = currentTick,
                ExpiryTick = 0,
                TargetEntity = Entity.Null,
                Issuer = Entity.Null,
                Scope = DirectiveScope.Empire,
                Source = DirectiveSource.AI,
                Flags = DirectiveFlags.Persistent
            });
        }
    }
}
