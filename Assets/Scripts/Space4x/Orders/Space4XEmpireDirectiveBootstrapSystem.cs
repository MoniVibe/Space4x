using Space4X.Registry;
using TimeState = PureDOTS.Runtime.Components.TimeState;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Orders
{
    /// <summary>
    /// Ensures empire factions have directive buffers with baseline directives.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XEmpireDirectiveBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XFaction>();
            state.RequireForUpdate<TimeState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            uint currentTick = SystemAPI.GetSingleton<TimeState>().Tick;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                if (faction.ValueRO.Type != FactionType.Empire && faction.ValueRO.Type != FactionType.Player)
                    continue;

                if (!state.EntityManager.HasBuffer<EmpireDirective>(entity))
                {
                    var directives = ecb.AddBuffer<EmpireDirective>(entity);
                    AddDirective(ref directives, EmpireDirectiveType.SecureResources, math.clamp((float)faction.ValueRO.TradeFocus * 100f, 0f, 100f), currentTick);
                    AddDirective(ref directives, EmpireDirectiveType.Expand, math.clamp((float)faction.ValueRO.ExpansionDrive * 100f, 0f, 100f), currentTick);
                    AddDirective(ref directives, EmpireDirectiveType.ResearchFocus, math.clamp((float)faction.ValueRO.ResearchFocus * 100f, 0f, 100f), currentTick);
                    AddDirective(ref directives, EmpireDirectiveType.MilitaryPosture, math.clamp((float)faction.ValueRO.MilitaryFocus * 100f, 0f, 100f), currentTick);
                    AddDirective(ref directives, EmpireDirectiveType.TradeBias, math.clamp((float)faction.ValueRO.TradeFocus * 90f, 0f, 100f), currentTick);
                    continue;
                }

                var existingDirectives = state.EntityManager.GetBuffer<EmpireDirective>(entity);
                if (existingDirectives.Length > 0)
                    continue;

                AddDirective(ref existingDirectives, EmpireDirectiveType.SecureResources, math.clamp((float)faction.ValueRO.TradeFocus * 100f, 0f, 100f), currentTick);
                AddDirective(ref existingDirectives, EmpireDirectiveType.Expand, math.clamp((float)faction.ValueRO.ExpansionDrive * 100f, 0f, 100f), currentTick);
                AddDirective(ref existingDirectives, EmpireDirectiveType.ResearchFocus, math.clamp((float)faction.ValueRO.ResearchFocus * 100f, 0f, 100f), currentTick);
                AddDirective(ref existingDirectives, EmpireDirectiveType.MilitaryPosture, math.clamp((float)faction.ValueRO.MilitaryFocus * 100f, 0f, 100f), currentTick);
                AddDirective(ref existingDirectives, EmpireDirectiveType.TradeBias, math.clamp((float)faction.ValueRO.TradeFocus * 90f, 0f, 100f), currentTick);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static void AddDirective(ref DynamicBuffer<EmpireDirective> directives, EmpireDirectiveType directiveType, float basePriority, uint currentTick)
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
