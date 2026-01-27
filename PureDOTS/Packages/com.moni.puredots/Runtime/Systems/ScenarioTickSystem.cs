using Unity.Burst;
using Unity.Entities;
using PureDOTS.Runtime.Scenarios;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Maintains the shared ScenarioTick singleton so telemetry systems can read a consistent tick index.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct ScenarioTickSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<ScenarioTick>(out _))
            {
                var entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponentData(entity, new ScenarioTick { Value = 0 });
            }
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingletonRW<ScenarioTick>();
            tick.ValueRW.Value++;
        }
    }
}
