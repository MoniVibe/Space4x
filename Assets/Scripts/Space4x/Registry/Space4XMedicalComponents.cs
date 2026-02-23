using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum MedicalFacilityType : byte
    {
        Clinic = 0,
        Surgery = 1,
        Recovery = 2,
        AugmentLab = 3,
        ResearchLab = 4
    }

    public enum MedicalStaffRoleType : byte
    {
        Doctor = 0,
        Surgeon = 1,
        Assistant = 2,
        Researcher = 3
    }

    public enum MedicalUnlockKind : byte
    {
        Augmentation = 0,
        FacilityLimb = 1,
        FacilityProcess = 2,
        TechFlag = 3
    }

    /// <summary>
    /// Per-limb medical facility profile attached to installed modules.
    /// Rates are in "per-second" space unless otherwise noted.
    /// </summary>
    public struct MedicalFacilityLimb : IComponentData
    {
        public MedicalFacilityType Type;
        public float TreatmentRate;
        public float SurgeryRate;
        public float RecoveryRate;
        public float ResearchRate;
        public float AugmentRate;
        public float AugmentQualityBonus;
        public float InfectionRisk;
        public float MalpracticeRisk;
        public float Sterility;
    }

    /// <summary>
    /// Shift policy for medical facilities (hour-of-day windows).
    /// </summary>
    public struct MedicalShiftPolicy : IComponentData
    {
        public float DayLengthHours;
        public float ClinicStartHour;
        public float ClinicDurationHours;
        public float SurgeryStartHour;
        public float SurgeryDurationHours;
        public float ResearchStartHour;
        public float ResearchDurationHours;
        public half OffHoursEfficiency;

        public static MedicalShiftPolicy Default => new MedicalShiftPolicy
        {
            DayLengthHours = 24f,
            ClinicStartHour = 7f,
            ClinicDurationHours = 10f,
            SurgeryStartHour = 9f,
            SurgeryDurationHours = 8f,
            ResearchStartHour = 12f,
            ResearchDurationHours = 6f,
            OffHoursEfficiency = (half)0.2f
        };
    }

    /// <summary>
    /// Staffing policy for a medical facility (seat targets).
    /// </summary>
    public struct MedicalStaffingPolicy : IComponentData
    {
        public byte DoctorsRequired;
        public byte SurgeonsRequired;
        public byte AssistantsRequired;
        public byte ResearchersRequired;

        public static MedicalStaffingPolicy Default => new MedicalStaffingPolicy
        {
            DoctorsRequired = 1,
            SurgeonsRequired = 1,
            AssistantsRequired = 2,
            ResearchersRequired = 1
        };
    }

    /// <summary>
    /// Medical staff role profile attached to an individual entity.
    /// </summary>
    public struct MedicalStaffRole : IComponentData
    {
        public MedicalStaffRoleType Role;
        public half Skill01;
        public byte IsLicensed;
        public byte IsCertified;
    }

    /// <summary>
    /// Assignment of a medical staff entity to a facility.
    /// </summary>
    public struct MedicalStaffAssignment : IComponentData
    {
        public Entity FacilityEntity;
        public byte ShiftIndex;
        public byte IsActive;
    }

    /// <summary>
    /// Aggregated medical facility capability snapshot for UI/telemetry.
    /// </summary>
    public struct MedicalFacilityAggregate : IComponentData
    {
        public float TreatmentRate;
        public float SurgeryRate;
        public float RecoveryRate;
        public float ResearchRate;
        public float AugmentRate;
        public float AugmentQualityBonus;
        public float InfectionRisk;
        public float MalpracticeRisk;
        public float Sterility;
    }

    /// <summary>
    /// Summary staffing snapshot for medical operations.
    /// </summary>
    public struct MedicalStaffingAggregate : IComponentData
    {
        public byte Doctors;
        public byte Surgeons;
        public byte Assistants;
        public byte Researchers;
        public half SkillMean;
        public half Coverage01;
        public half ResearchCoverage01;
        public half ClinicCoverage01;
        public half SurgeryCoverage01;
        public half RecoveryCoverage01;
    }

    /// <summary>
    /// Tuning knobs for how medical output is applied.
    /// </summary>
    public struct MedicalCarePolicy : IComponentData
    {
        public float ConditionHealScalar;
        public float ModuleRepairScalar;
        public float SurgeryBonusScalar;
        public float ResearchScalar;

        public static MedicalCarePolicy Default => new MedicalCarePolicy
        {
            ConditionHealScalar = 0.06f,
            ModuleRepairScalar = 0.4f,
            SurgeryBonusScalar = 1.35f,
            ResearchScalar = 1f
        };
    }

    /// <summary>
    /// Medical research progress and knowledge output.
    /// </summary>
    public struct MedicalResearchState : IComponentData
    {
        public float KnowledgeProgress;
        public float LastOutput;
        public float LifetimeKnowledge;
        public byte Tier;
        public uint LastUpdateTick;
    }

    [InternalBufferCapacity(8)]
    public struct MedicalResearchUnlock : IBufferElementData
    {
        public FixedString64Bytes UnlockId;
        public MedicalUnlockKind Kind;
        public FixedString64Bytes TargetId;
        public float RequiredKnowledge;
        public uint UnlockedTick;
    }
}
