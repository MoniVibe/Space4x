using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Types of departments on a carrier/vessel.
    /// </summary>
    public enum DepartmentType : byte
    {
        /// <summary>
        /// Hangar operations, strike craft launch, torpedo management.
        /// </summary>
        Flight = 0,

        /// <summary>
        /// Reactor tuning, hull repairs, system maintenance.
        /// </summary>
        Engineering = 1,

        /// <summary>
        /// Tactical coordination, sensor fusion, targeting (CIC).
        /// </summary>
        Combat = 2,

        /// <summary>
        /// Cargo handling, resource routing, environmental control.
        /// </summary>
        Logistics = 3,

        /// <summary>
        /// Bridge crew, communications, diplomacy liaisons.
        /// </summary>
        Command = 4,

        Count = 5
    }

    /// <summary>
    /// Stats for a single department.
    /// </summary>
    public struct DepartmentStats : IComponentData
    {
        /// <summary>
        /// Which department these stats belong to.
        /// </summary>
        public DepartmentType Type;

        /// <summary>
        /// Current fatigue level [0, 1]. Higher = more tired, reduced efficiency.
        /// </summary>
        public half Fatigue;

        /// <summary>
        /// Team cohesion [0, 1]. Higher = better teamwork, service trait unlocks.
        /// </summary>
        public half Cohesion;

        /// <summary>
        /// Current stress level [0, 1]. High stress triggers mistakes.
        /// </summary>
        public half Stress;

        /// <summary>
        /// Average skill level of department crew [0, 1].
        /// </summary>
        public half SkillLevel;

        /// <summary>
        /// Efficiency multiplier based on stats [0.5, 1.5].
        /// </summary>
        public half Efficiency;

        /// <summary>
        /// Last tick when stats were updated.
        /// </summary>
        public uint LastUpdateTick;

        public static DepartmentStats Create(DepartmentType type, float skillLevel = 0.5f)
        {
            return new DepartmentStats
            {
                Type = type,
                Fatigue = (half)0f,
                Cohesion = (half)0.5f,
                Stress = (half)0f,
                SkillLevel = (half)math.clamp(skillLevel, 0f, 1f),
                Efficiency = (half)1f,
                LastUpdateTick = 0
            };
        }

        public static DepartmentStats Default(DepartmentType type) => Create(type, 0.5f);
    }

    /// <summary>
    /// Buffer element for per-department stats on a carrier.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct DepartmentStatsBuffer : IBufferElementData
    {
        public DepartmentStats Stats;
    }

    /// <summary>
    /// Staffing information for a department.
    /// </summary>
    public struct DepartmentStaffing
    {
        /// <summary>
        /// Which department.
        /// </summary>
        public DepartmentType Type;

        /// <summary>
        /// Required crew for full efficiency.
        /// </summary>
        public int RequiredCrew;

        /// <summary>
        /// Currently assigned crew.
        /// </summary>
        public int CurrentCrew;

        /// <summary>
        /// Staffing ratio [0, 2+]. Below 1 = understaffed, above 1 = overstaffed.
        /// </summary>
        public float StaffingRatio => RequiredCrew > 0 ? (float)CurrentCrew / RequiredCrew : 1f;

        /// <summary>
        /// Whether department is critically understaffed (< 50%).
        /// </summary>
        public bool IsCriticallyUnderstaffed => StaffingRatio < 0.5f;

        public static DepartmentStaffing Create(DepartmentType type, int required, int current)
        {
            return new DepartmentStaffing
            {
                Type = type,
                RequiredCrew = required,
                CurrentCrew = current
            };
        }
    }

    /// <summary>
    /// Buffer element for department staffing on a carrier.
    /// </summary>
    [InternalBufferCapacity(5)]
    public struct DepartmentStaffingBuffer : IBufferElementData
    {
        public DepartmentStaffing Staffing;
    }

    /// <summary>
    /// Aggregate department state for a carrier/vessel.
    /// </summary>
    public struct CarrierDepartmentState : IComponentData
    {
        /// <summary>
        /// Average fatigue across all departments.
        /// </summary>
        public half AverageFatigue;

        /// <summary>
        /// Average cohesion across all departments.
        /// </summary>
        public half AverageCohesion;

        /// <summary>
        /// Average stress across all departments.
        /// </summary>
        public half AverageStress;

        /// <summary>
        /// Overall staffing ratio.
        /// </summary>
        public half OverallStaffingRatio;

        /// <summary>
        /// Overall efficiency modifier.
        /// </summary>
        public half OverallEfficiency;

        /// <summary>
        /// Number of departments in critical state.
        /// </summary>
        public byte CriticalDepartmentCount;

        /// <summary>
        /// Last tick when aggregate was computed.
        /// </summary>
        public uint LastUpdateTick;

        public static CarrierDepartmentState Default => new CarrierDepartmentState
        {
            AverageFatigue = (half)0f,
            AverageCohesion = (half)0.5f,
            AverageStress = (half)0f,
            OverallStaffingRatio = (half)1f,
            OverallEfficiency = (half)1f,
            CriticalDepartmentCount = 0,
            LastUpdateTick = 0
        };
    }

    /// <summary>
    /// Thresholds for department stat effects.
    /// </summary>
    public static class DepartmentThresholds
    {
        /// <summary>
        /// Fatigue above this reduces efficiency.
        /// </summary>
        public const float HighFatigue = 0.6f;

        /// <summary>
        /// Fatigue above this triggers errors and morale penalties.
        /// </summary>
        public const float CriticalFatigue = 0.85f;

        /// <summary>
        /// Stress above this triggers mistakes.
        /// </summary>
        public const float HighStress = 0.7f;

        /// <summary>
        /// Stress above this triggers breakdowns.
        /// </summary>
        public const float CriticalStress = 0.9f;

        /// <summary>
        /// Cohesion below this causes teamwork penalties.
        /// </summary>
        public const float LowCohesion = 0.3f;

        /// <summary>
        /// Cohesion above this grants teamwork bonuses.
        /// </summary>
        public const float HighCohesion = 0.7f;

        /// <summary>
        /// Staffing ratio below this is critical.
        /// </summary>
        public const float CriticalStaffing = 0.5f;
    }

    /// <summary>
    /// Utility functions for department calculations.
    /// </summary>
    public static class DepartmentUtility
    {
        /// <summary>
        /// Calculates efficiency modifier based on department stats.
        /// </summary>
        public static float CalculateEfficiency(in DepartmentStats stats, float staffingRatio)
        {
            float efficiency = 1f;

            // Staffing impact
            efficiency *= math.clamp(staffingRatio, 0.3f, 1.2f);

            // Fatigue penalty
            float fatigue = (float)stats.Fatigue;
            if (fatigue > DepartmentThresholds.HighFatigue)
            {
                float fatiguePenalty = (fatigue - DepartmentThresholds.HighFatigue) * 0.5f;
                efficiency -= fatiguePenalty;
            }

            // Stress penalty
            float stress = (float)stats.Stress;
            if (stress > DepartmentThresholds.HighStress)
            {
                float stressPenalty = (stress - DepartmentThresholds.HighStress) * 0.3f;
                efficiency -= stressPenalty;
            }

            // Cohesion bonus/penalty
            float cohesion = (float)stats.Cohesion;
            if (cohesion < DepartmentThresholds.LowCohesion)
            {
                efficiency -= (DepartmentThresholds.LowCohesion - cohesion) * 0.2f;
            }
            else if (cohesion > DepartmentThresholds.HighCohesion)
            {
                efficiency += (cohesion - DepartmentThresholds.HighCohesion) * 0.15f;
            }

            // Skill bonus
            float skill = (float)stats.SkillLevel;
            efficiency += (skill - 0.5f) * 0.2f;

            return math.clamp(efficiency, 0.3f, 1.5f);
        }

        /// <summary>
        /// Gets fatigue recovery rate based on current state.
        /// </summary>
        public static float GetFatigueRecoveryRate(float currentFatigue, bool isResting)
        {
            if (isResting)
            {
                return 0.02f; // 2% per second when resting
            }
            return currentFatigue > 0.3f ? -0.005f : 0f; // Slow recovery when active and not too tired
        }

        /// <summary>
        /// Gets fatigue accumulation rate based on activity.
        /// </summary>
        public static float GetFatigueAccumulationRate(bool isActive, bool isInCombat)
        {
            if (isInCombat)
            {
                return 0.01f; // 1% per second in combat
            }
            if (isActive)
            {
                return 0.003f; // 0.3% per second during normal operations
            }
            return 0f;
        }
    }
}

