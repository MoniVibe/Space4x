using PureDOTS.Runtime.Telemetry;
using PureDOTS.Systems;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Publishes aggregated crew skill metrics for debug and HUD bindings.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XMiningTelemetrySystem))]
    public partial struct Space4XCrewSkillTelemetrySystem : ISystem
    {
        private EntityQuery _telemetryQuery;
        private EntityQuery _skillQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TelemetryStream>();

            _telemetryQuery = SystemAPI.QueryBuilder()
                .WithAll<TelemetryStream>()
                .Build();

            _skillQuery = SystemAPI.QueryBuilder()
                .WithAll<CrewSkills>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_skillQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var telemetryEntity = _telemetryQuery.GetSingletonEntity();
            var buffer = state.EntityManager.GetBuffer<TelemetryMetric>(telemetryEntity);

            float mining = 0f;
            float hauling = 0f;
            float combat = 0f;
            float repair = 0f;
            float exploration = 0f;
            var count = 0;

            foreach (var skills in SystemAPI.Query<RefRO<CrewSkills>>())
            {
                mining += skills.ValueRO.MiningSkill;
                hauling += skills.ValueRO.HaulingSkill;
                combat += skills.ValueRO.CombatSkill;
                repair += skills.ValueRO.RepairSkill;
                exploration += skills.ValueRO.ExplorationSkill;
                count++;
            }

            if (count == 0)
            {
                return;
            }

            var inv = 1f / count;
            buffer.AddMetric("space4x.skills.mining.avg", mining * inv);
            buffer.AddMetric("space4x.skills.hauling.avg", hauling * inv);
            buffer.AddMetric("space4x.skills.combat.avg", combat * inv);
            buffer.AddMetric("space4x.skills.repair.avg", repair * inv);
            buffer.AddMetric("space4x.skills.exploration.avg", exploration * inv);
        }
    }
}
