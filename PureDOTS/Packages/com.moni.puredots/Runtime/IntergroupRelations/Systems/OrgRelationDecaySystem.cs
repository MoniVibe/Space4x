#if TRI_ENABLE_INTERGROUP_RELATIONS
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;

namespace PureDOTS.Runtime.IntergroupRelations
{
    /// <summary>
    /// COLD path: Slow decay for org relations (every 100-200 ticks, only on active edges).
    /// Maintains sparse graph by pruning inactive relations.
    /// Decays extreme attitudes toward baseline over time.
    /// Rate based on OrgPersona (vengeful orgs decay slower, forgiving faster).
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(OrgRelationInitSystem))]
    public partial struct OrgRelationDecaySystem : ISystem
    {
        private const float DECAY_RATE_PER_TICK = 0.0001f;
        private const uint DECAY_CHECK_INTERVAL = 100;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
                return;
            
            var currentTick = timeState.Tick;

            foreach (var (relation, entity) in SystemAPI.Query<RefRW<OrgRelation>>()
                .WithAll<OrgRelationTag>()
                .WithEntityAccess())
            {
                if (currentTick - relation.ValueRO.LastUpdateTick < DECAY_CHECK_INTERVAL)
                    continue;

                float decayRate = DECAY_RATE_PER_TICK;
                
                if (SystemAPI.HasComponent<OrgPersona>(relation.ValueRO.OrgA))
                {
                    var personaA = SystemAPI.GetComponent<OrgPersona>(relation.ValueRO.OrgA);
                    float vengefulFactor = 1f - personaA.VengefulForgiving;
                    decayRate *= (0.5f + vengefulFactor * 0.5f);
                }

                float baselineAttitude = 0f;
                
                float attitudeDelta = relation.ValueRO.Attitude - baselineAttitude;
                float decayAmount = attitudeDelta * decayRate * DECAY_CHECK_INTERVAL;
                
                relation.ValueRW.Attitude = math.clamp(relation.ValueRO.Attitude - decayAmount, -100f, 100f);
                
                relation.ValueRW.Trust = math.clamp(relation.ValueRO.Trust - 0.001f * DECAY_CHECK_INTERVAL, 0f, 1f);
                relation.ValueRW.Fear = math.clamp(relation.ValueRO.Fear - 0.001f * DECAY_CHECK_INTERVAL, 0f, 1f);
                relation.ValueRW.Respect = math.clamp(relation.ValueRO.Respect - 0.001f * DECAY_CHECK_INTERVAL, 0f, 1f);
                
                relation.ValueRW.LastUpdateTick = currentTick;
            }
        }
    }
}
#else
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Runtime.IntergroupRelations
{
    // [TRI-STUB] Disabled in MVP baseline.
    [BurstCompile]
    public partial struct OrgRelationDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state) { }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            return;
        }
    }
}
#endif
