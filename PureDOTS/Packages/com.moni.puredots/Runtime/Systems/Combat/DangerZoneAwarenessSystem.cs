using PureDOTS.Runtime.Combat;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Combat
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatLoopSystem))]
    public partial struct DangerZoneAwarenessSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PilotAwareness>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (awareness, experience, attributes, instruments) in SystemAPI
                         .Query<RefRW<PilotAwareness>, RefRO<PilotExperience>, RefRO<PilotAttributes>, RefRO<InstrumentTechLevel>>())
            {
                var xpFactor = 1f + experience.ValueRO.Experience * 0.01f;
                var sensorFactor = attributes.ValueRO.Perception * 0.6f + instruments.ValueRO.TechLevel * 0.4f;
                var intellectFactor = (attributes.ValueRO.Intelligence + attributes.ValueRO.Finesse) * 0.05f;
                var total = (sensorFactor + intellectFactor) * xpFactor;
                awareness.ValueRW.ArcSensitivity = math.saturate(total * 0.05f);
                awareness.ValueRW.ThreatSensitivity = math.saturate(total * 0.04f);
            }
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }
    }
}
