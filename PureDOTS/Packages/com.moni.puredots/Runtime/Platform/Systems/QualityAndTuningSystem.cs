using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Applies crew skill, maintenance, wear, and reliability calculations.
    /// Reduces MaintenanceDebt over time. Applies wear from operations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct QualityAndTuningSystem : ISystem
    {
        private const float CombatWearMultiplier = 5f;
        private const float BaseMaintenanceRate = 0.01f;
        private const float BaseWearRate = 0.001f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (tuningState, manufacturer, crewMembers, entity) in SystemAPI.Query<RefRW<PlatformTuningState>, RefRO<PlatformManufacturer>, DynamicBuffer<PlatformCrewMember>>().WithEntityAccess())
            {
                UpdateTuningState(
                    ref state,
                    ref ecb,
                    entity,
                    ref tuningState.ValueRW,
                    in manufacturer.ValueRO,
                    in crewMembers,
                    timeState.Tick);
            }
        }

        [BurstCompile]
        private void UpdateTuningState(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity platformEntity,
            ref PlatformTuningState tuningState,
            in PlatformManufacturer manufacturer,
            in DynamicBuffer<PlatformCrewMember> crewMembers,
            uint currentTick)
        {
            var averageCrewSkill = CalculateAverageCrewSkill(ref state, in crewMembers);
            var maintenanceEffectiveness = 0.5f + (averageCrewSkill / 200f);

            tuningState.MaintenanceDebt = math.max(0f, tuningState.MaintenanceDebt - (BaseMaintenanceRate * maintenanceEffectiveness));

            var wearRate = BaseWearRate;
            var platformEntityRef = platformEntity;
            if (IsInCombat(ref state, ref platformEntityRef))
            {
                wearRate *= CombatWearMultiplier;
            }

            tuningState.WearLevel = math.min(1f, tuningState.WearLevel + wearRate);

            var baseReliability = 1f - tuningState.WearLevel;
            var maintenanceBonus = (1f - tuningState.MaintenanceDebt) * 0.3f;
            var qualityBonus = (manufacturer.BaseQualityTier / 100f) * 0.2f;
            var crewBonus = (averageCrewSkill / 100f) * 0.1f;

            tuningState.Reliability = math.clamp(baseReliability + maintenanceBonus + qualityBonus + crewBonus, 0f, 1f);
            tuningState.PerformanceFactor = math.clamp(0.5f + (tuningState.Reliability * 0.5f), 0.5f, 1f);

            if (tuningState.Reliability < 0.3f && SystemAPI.HasBuffer<PlatformModuleSlot>(platformEntity))
            {
                var modules = SystemAPI.GetBuffer<PlatformModuleSlot>(platformEntity);
                var random = new Unity.Mathematics.Random((uint)(platformEntity.Index + currentTick));
                for (int i = 0; i < modules.Length; i++)
                {
                    var slot = modules[i];
                    if (slot.State == ModuleSlotState.Installed && random.NextFloat() < 0.01f)
                    {
                        slot.State = ModuleSlotState.Offline;
                        modules[i] = slot;
                    }
                }
            }
        }

        [BurstCompile]
        private static float CalculateAverageCrewSkill(ref SystemState state, in DynamicBuffer<PlatformCrewMember> crewMembers)
        {
            if (crewMembers.Length == 0)
            {
                return 50f;
            }

            float totalSkill = 0f;
            int count = 0;

            var entityManager = state.EntityManager;
            for (int i = 0; i < crewMembers.Length; i++)
            {
                var crewEntity = crewMembers[i].CrewEntity;
                if (entityManager.Exists(crewEntity))
                {
                    totalSkill += 50f;
                    count++;
                }
            }

            return count > 0 ? totalSkill / count : 50f;
        }

        [BurstCompile]
        private static bool IsInCombat(ref SystemState state, ref Entity platformEntity)
        {
            return false;
        }
    }
}

