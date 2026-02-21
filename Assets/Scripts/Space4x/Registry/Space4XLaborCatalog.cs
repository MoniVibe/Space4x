using System;
using PureDOTS.Runtime.Economy.Labor;
using UnityEngine;

namespace Space4X.Registry
{
    [CreateAssetMenu(fileName = "Space4XLaborCatalog", menuName = "Space4X/Registry/Labor Catalog")]
    public sealed class Space4XLaborCatalog : ScriptableObject
    {
        public const string ResourcePath = "Registry/Space4XLaborCatalog";

        [SerializeField] private WorkCalendarDefinition[] calendars = Array.Empty<WorkCalendarDefinition>();
        [SerializeField] private WorkShiftDefinition[] shifts = Array.Empty<WorkShiftDefinition>();
        [SerializeField] private WorkShiftScheduleDefinition[] schedules = Array.Empty<WorkShiftScheduleDefinition>();
        [SerializeField] private StaffingProfileDefinition[] staffingProfiles = Array.Empty<StaffingProfileDefinition>();
        [SerializeField] private RegimenScheduleDefinition[] regimenSchedules = Array.Empty<RegimenScheduleDefinition>();
        [SerializeField] private RegimenProfileDefinition[] regimenProfiles = Array.Empty<RegimenProfileDefinition>();
        [SerializeField] private RegimenAssignmentDefinition[] regimenAssignments = Array.Empty<RegimenAssignmentDefinition>();
        [SerializeField] private RegimenResolverDefinition[] regimenResolvers = Array.Empty<RegimenResolverDefinition>();

        public WorkCalendarDefinition[] Calendars => calendars;
        public WorkShiftDefinition[] Shifts => shifts;
        public WorkShiftScheduleDefinition[] Schedules => schedules;
        public StaffingProfileDefinition[] StaffingProfiles => staffingProfiles;
        public RegimenScheduleDefinition[] RegimenSchedules => regimenSchedules;
        public RegimenProfileDefinition[] RegimenProfiles => regimenProfiles;
        public RegimenAssignmentDefinition[] RegimenAssignments => regimenAssignments;
        public RegimenResolverDefinition[] RegimenResolvers => regimenResolvers;

        public static Space4XLaborCatalog LoadOrFallback()
        {
            var catalog = Resources.Load<Space4XLaborCatalog>(ResourcePath);
            if (catalog == null)
            {
                catalog = CreateRuntimeFallback();
            }

            return catalog;
        }

        public static Space4XLaborCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XLaborCatalog>();
            catalog.ApplyRuntimeDefaults();
            return catalog;
        }

        public void ApplyRuntimeDefaults()
        {
            calendars = new[]
            {
                new WorkCalendarDefinition
                {
                    Id = "calendar.standard",
                    DisplayName = "Standard Week",
                    DayLengthHours = WorkforceContractDefaults.DefaultDayLengthHours,
                    CycleLengthDays = WorkforceContractDefaults.DefaultCycleLengthDays,
                    OffDayMask = WorkforceContractDefaults.DefaultWeekendOff,
                    CycleStartOffsetHours = 0f,
                    Notes = "Baseline 24h/7d calendar."
                }
            };

            shifts = new[]
            {
                new WorkShiftDefinition
                {
                    Id = "shift.8h",
                    DisplayName = "8 Hour Shift",
                    ShiftLengthHours = 8f,
                    RestHours = 8f,
                    MaxConsecutiveShifts = 6,
                    MaxOvertimeHours = 2f,
                    AllowOvertime = true,
                    WageMultiplier = 1f,
                    PowerMultiplier = 1f,
                    FatigueGainPerHour = 0.08f,
                    FatigueRecoveryPerHour = 0.1f
                }
            };

            schedules = new[]
            {
                new WorkShiftScheduleDefinition
                {
                    Id = "schedule.3x8",
                    CalendarId = "calendar.standard",
                    ShiftId = "shift.8h",
                    ShiftsPerDay = 3,
                    ShiftOffsetHours = 0f,
                    ShiftSpacingHours = 8f,
                    CoverageTarget01 = 0.95f,
                    DowntimeWindows = new[]
                    {
                        new WorkDowntimeWindow
                        {
                            DayMask = WorkforceContractDefaults.DefaultWeekendOff,
                            StartHour = 0f,
                            DurationHours = 24f,
                            IsMaintenance = true,
                            Notes = "Weekend maintenance window."
                        }
                    }
                }
            };

            staffingProfiles = new[]
            {
                new StaffingProfileDefinition
                {
                    Id = "staffing.standard_3x8",
                    DisplayName = "Standard 3x8",
                    SiteType = WorkSiteType.Any,
                    CalendarId = "calendar.standard",
                    ScheduleId = "schedule.3x8",
                    Seats = new[]
                    {
                        new WorkSeatDefinition
                        {
                            Id = "seat.operator",
                            SeatType = WorkSeatType.Production,
                            RoleId = "operator",
                            SeatCount = 4,
                            CrewPerSeat = 1,
                            WagePerHour = 12f,
                            PowerCostPerHour = 0.2f,
                            MinSkill01 = 0.4f
                        }
                    },
                    Teams = new[]
                    {
                        new WorkTeamDefinition
                        {
                            Id = "team.alpha",
                            DisplayName = "Alpha",
                            SiteType = WorkSiteType.Any,
                            ScheduleId = "schedule.3x8",
                            SeatIds = new[] { "seat.operator" },
                            RotationPolicy = WorkRotationPolicy.Rotating,
                            MinCrew = 4,
                            MaxCrew = 6,
                            CoverageTarget01 = 0.9f,
                            OvertimeBias01 = 0.2f
                        }
                    },
                    PowerBudgetMultiplier = 1f,
                    PayrollBudgetMultiplier = 1f,
                    DowntimeTargetHoursPerCycle = 16f
                }
            };

            regimenSchedules = new[]
            {
                new RegimenScheduleDefinition
                {
                    Id = "regimen.standard_day",
                    DisplayName = "Standard Day",
                    CalendarId = "calendar.standard",
                    DefaultPatternId = "pattern.weekday",
                    Patterns = new[]
                    {
                        new RegimenDayPatternDefinition
                        {
                            Id = "pattern.weekday",
                            DayMask = WorkDayMask.Day1 | WorkDayMask.Day2 | WorkDayMask.Day3 | WorkDayMask.Day4 | WorkDayMask.Day5,
                            SlotCount = 24,
                            SlotLengthHours = 1f,
                            Slots = new[]
                            {
                                new RegimenSlotDefinition { SlotIndex = 0, Activity = RegimenActivityKind.Sleep },
                                new RegimenSlotDefinition { SlotIndex = 6, Activity = RegimenActivityKind.Work },
                                new RegimenSlotDefinition { SlotIndex = 12, Activity = RegimenActivityKind.FreeTime },
                                new RegimenSlotDefinition { SlotIndex = 13, Activity = RegimenActivityKind.Work },
                                new RegimenSlotDefinition { SlotIndex = 18, Activity = RegimenActivityKind.Leisure },
                                new RegimenSlotDefinition { SlotIndex = 22, Activity = RegimenActivityKind.Sleep }
                            },
                            Notes = "Workday cadence with flexible leisure blocks."
                        },
                        new RegimenDayPatternDefinition
                        {
                            Id = "pattern.weekend",
                            DayMask = WorkforceContractDefaults.DefaultWeekendOff,
                            SlotCount = 24,
                            SlotLengthHours = 1f,
                            Slots = new[]
                            {
                                new RegimenSlotDefinition { SlotIndex = 0, Activity = RegimenActivityKind.Sleep },
                                new RegimenSlotDefinition { SlotIndex = 8, Activity = RegimenActivityKind.Leisure },
                                new RegimenSlotDefinition { SlotIndex = 14, Activity = RegimenActivityKind.Social },
                                new RegimenSlotDefinition { SlotIndex = 20, Activity = RegimenActivityKind.FreeTime }
                            },
                            Notes = "Weekend leisure and social focus."
                        }
                    },
                    ComplianceTarget01 = 0.8f,
                    OverrideTolerance01 = 0.2f,
                    Notes = "Baseline regimen schedule for generic colonies."
                },
                new RegimenScheduleDefinition
                {
                    Id = "regimen.devout_day",
                    DisplayName = "Devout Day",
                    CalendarId = "calendar.standard",
                    DefaultPatternId = "pattern.devout_weekday",
                    Patterns = new[]
                    {
                        new RegimenDayPatternDefinition
                        {
                            Id = "pattern.devout_weekday",
                            DayMask = WorkDayMask.Day1 | WorkDayMask.Day2 | WorkDayMask.Day3 | WorkDayMask.Day4 | WorkDayMask.Day5,
                            SlotCount = 24,
                            SlotLengthHours = 1f,
                            Slots = new[]
                            {
                                new RegimenSlotDefinition { SlotIndex = 0, Activity = RegimenActivityKind.Sleep },
                                new RegimenSlotDefinition { SlotIndex = 5, Activity = RegimenActivityKind.Pray },
                                new RegimenSlotDefinition { SlotIndex = 6, Activity = RegimenActivityKind.Work },
                                new RegimenSlotDefinition { SlotIndex = 12, Activity = RegimenActivityKind.Pray },
                                new RegimenSlotDefinition { SlotIndex = 13, Activity = RegimenActivityKind.Work },
                                new RegimenSlotDefinition { SlotIndex = 19, Activity = RegimenActivityKind.Pray },
                                new RegimenSlotDefinition { SlotIndex = 20, Activity = RegimenActivityKind.Leisure },
                                new RegimenSlotDefinition { SlotIndex = 22, Activity = RegimenActivityKind.Sleep }
                            },
                            Notes = "Prayer cadence layered into the workday."
                        },
                        new RegimenDayPatternDefinition
                        {
                            Id = "pattern.devout_weekend",
                            DayMask = WorkforceContractDefaults.DefaultWeekendOff,
                            SlotCount = 24,
                            SlotLengthHours = 1f,
                            Slots = new[]
                            {
                                new RegimenSlotDefinition { SlotIndex = 0, Activity = RegimenActivityKind.Sleep },
                                new RegimenSlotDefinition { SlotIndex = 6, Activity = RegimenActivityKind.Pray },
                                new RegimenSlotDefinition { SlotIndex = 10, Activity = RegimenActivityKind.Social },
                                new RegimenSlotDefinition { SlotIndex = 16, Activity = RegimenActivityKind.Leisure },
                                new RegimenSlotDefinition { SlotIndex = 20, Activity = RegimenActivityKind.Pray }
                            },
                            Notes = "Weekend devotion and community focus."
                        }
                    },
                    ComplianceTarget01 = 0.85f,
                    OverrideTolerance01 = 0.15f,
                    Notes = "Devotional schedule with prayer emphasis."
                },
                new RegimenScheduleDefinition
                {
                    Id = "regimen.martial_day",
                    DisplayName = "Martial Day",
                    CalendarId = "calendar.standard",
                    DefaultPatternId = "pattern.martial_weekday",
                    Patterns = new[]
                    {
                        new RegimenDayPatternDefinition
                        {
                            Id = "pattern.martial_weekday",
                            DayMask = WorkDayMask.Day1 | WorkDayMask.Day2 | WorkDayMask.Day3 | WorkDayMask.Day4 | WorkDayMask.Day5,
                            SlotCount = 24,
                            SlotLengthHours = 1f,
                            Slots = new[]
                            {
                                new RegimenSlotDefinition { SlotIndex = 0, Activity = RegimenActivityKind.Sleep },
                                new RegimenSlotDefinition { SlotIndex = 5, Activity = RegimenActivityKind.Training },
                                new RegimenSlotDefinition { SlotIndex = 7, Activity = RegimenActivityKind.Work },
                                new RegimenSlotDefinition { SlotIndex = 12, Activity = RegimenActivityKind.Training },
                                new RegimenSlotDefinition { SlotIndex = 13, Activity = RegimenActivityKind.Work },
                                new RegimenSlotDefinition { SlotIndex = 18, Activity = RegimenActivityKind.Security },
                                new RegimenSlotDefinition { SlotIndex = 20, Activity = RegimenActivityKind.Leisure },
                                new RegimenSlotDefinition { SlotIndex = 22, Activity = RegimenActivityKind.Sleep }
                            },
                            Notes = "Training and security blocks woven into shifts."
                        },
                        new RegimenDayPatternDefinition
                        {
                            Id = "pattern.martial_weekend",
                            DayMask = WorkforceContractDefaults.DefaultWeekendOff,
                            SlotCount = 24,
                            SlotLengthHours = 1f,
                            Slots = new[]
                            {
                                new RegimenSlotDefinition { SlotIndex = 0, Activity = RegimenActivityKind.Sleep },
                                new RegimenSlotDefinition { SlotIndex = 8, Activity = RegimenActivityKind.Training },
                                new RegimenSlotDefinition { SlotIndex = 14, Activity = RegimenActivityKind.Security },
                                new RegimenSlotDefinition { SlotIndex = 20, Activity = RegimenActivityKind.FreeTime }
                            },
                            Notes = "Weekend drills and readiness."
                        }
                    },
                    ComplianceTarget01 = 0.9f,
                    OverrideTolerance01 = 0.12f,
                    Notes = "Martial schedule for security-forward cultures."
                }
            };

            regimenProfiles = new[]
            {
                new RegimenProfileDefinition
                {
                    Id = "profile.standard",
                    DisplayName = "Standard",
                    ScheduleId = "regimen.standard_day",
                    Priority = 0.3f,
                    FatigueBias01 = 0.6f,
                    MoraleBias01 = 0.4f,
                    DisciplineBias01 = 0.5f,
                    Strictness01 = 0.45f,
                    DeviationBias01 = 0.2f,
                    NightOwlShiftHours = 0f,
                    AfterHoursSocialBias01 = 0.15f,
                    ForcedComplianceMoodPenalty01 = 0.08f,
                    ForcedComplianceSleepPenalty01 = 0.1f,
                    Notes = "Balanced adherence; treats the regimen as guidance."
                },
                new RegimenProfileDefinition
                {
                    Id = "profile.night_owl",
                    DisplayName = "Night Owl",
                    ScheduleId = "regimen.standard_day",
                    Priority = 0.2f,
                    FatigueBias01 = 0.5f,
                    MoraleBias01 = 0.45f,
                    DisciplineBias01 = 0.3f,
                    Strictness01 = 0.25f,
                    DeviationBias01 = 0.35f,
                    NightOwlShiftHours = 3f,
                    AfterHoursSocialBias01 = 0.3f,
                    ForcedComplianceMoodPenalty01 = 0.12f,
                    ForcedComplianceSleepPenalty01 = 0.15f,
                    Notes = "Prefers late activity; more likely to ignore sleep blocks."
                },
                new RegimenProfileDefinition
                {
                    Id = "profile.lawful_strict",
                    DisplayName = "Lawful Strict",
                    ScheduleId = "regimen.standard_day",
                    Priority = 0.4f,
                    FatigueBias01 = 0.65f,
                    MoraleBias01 = 0.3f,
                    DisciplineBias01 = 0.8f,
                    Strictness01 = 0.85f,
                    DeviationBias01 = 0.1f,
                    NightOwlShiftHours = 0f,
                    AfterHoursSocialBias01 = 0.05f,
                    ForcedComplianceMoodPenalty01 = 0.18f,
                    ForcedComplianceSleepPenalty01 = 0.2f,
                    Notes = "High adherence with discipline-first behavior."
                },
                new RegimenProfileDefinition
                {
                    Id = "profile.chaotic_free",
                    DisplayName = "Chaotic Free",
                    ScheduleId = "regimen.standard_day",
                    Priority = 0.15f,
                    FatigueBias01 = 0.45f,
                    MoraleBias01 = 0.55f,
                    DisciplineBias01 = 0.2f,
                    Strictness01 = 0.2f,
                    DeviationBias01 = 0.45f,
                    NightOwlShiftHours = 1.5f,
                    AfterHoursSocialBias01 = 0.4f,
                    ForcedComplianceMoodPenalty01 = 0.05f,
                    ForcedComplianceSleepPenalty01 = 0.08f,
                    Notes = "Loose adherence; treats regimen as a suggestion."
                },
                new RegimenProfileDefinition
                {
                    Id = "profile.devout",
                    DisplayName = "Devout",
                    ScheduleId = "regimen.devout_day",
                    Priority = 0.35f,
                    FatigueBias01 = 0.55f,
                    MoraleBias01 = 0.5f,
                    DisciplineBias01 = 0.6f,
                    Strictness01 = 0.65f,
                    DeviationBias01 = 0.2f,
                    NightOwlShiftHours = 0f,
                    AfterHoursSocialBias01 = 0.1f,
                    ForcedComplianceMoodPenalty01 = 0.12f,
                    ForcedComplianceSleepPenalty01 = 0.12f,
                    Notes = "Prayer-forward schedule with moderate adherence."
                },
                new RegimenProfileDefinition
                {
                    Id = "profile.martial",
                    DisplayName = "Martial",
                    ScheduleId = "regimen.martial_day",
                    Priority = 0.4f,
                    FatigueBias01 = 0.6f,
                    MoraleBias01 = 0.35f,
                    DisciplineBias01 = 0.75f,
                    Strictness01 = 0.75f,
                    DeviationBias01 = 0.15f,
                    NightOwlShiftHours = 0f,
                    AfterHoursSocialBias01 = 0.05f,
                    ForcedComplianceMoodPenalty01 = 0.16f,
                    ForcedComplianceSleepPenalty01 = 0.18f,
                    Notes = "Training-centric routine with high discipline."
                }
            };

            regimenAssignments = new[]
            {
                new RegimenAssignmentDefinition
                {
                    Id = "assign.global.standard",
                    TargetKind = RegimenTargetKind.Global,
                    TargetId = "global",
                    ProfileId = "profile.standard",
                    ResolverId = "resolver.standard",
                    Priority = 0.1f,
                    Weight = 1f
                }
            };

            regimenResolvers = new[]
            {
                new RegimenResolverDefinition
                {
                    Id = "resolver.standard",
                    DisplayName = "Standard Resolver",
                    Mode = RegimenResolutionMode.WeightedBlend,
                    ScheduleWeight01 = 0.6f,
                    NeedsWeight01 = 0.7f,
                    SocialWeight01 = 0.4f,
                    HabitWeight01 = 0.5f,
                    StrictnessThreshold01 = 0.7f,
                    DeviationPenalty01 = 0.15f,
                    CompliancePenaltyMultiplier01 = 1f,
                    TargetWeights = new[]
                    {
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Global, Weight = 0.5f, PriorityBonus = 0f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Site, Weight = 0.6f, PriorityBonus = 0.05f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Role, Weight = 0.7f, PriorityBonus = 0.1f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Seat, Weight = 0.85f, PriorityBonus = 0.15f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Entity, Weight = 1f, PriorityBonus = 0.2f }
                    },
                    Notes = "Balanced advisory resolver with preference for specific targets."
                },
                new RegimenResolverDefinition
                {
                    Id = "resolver.strict",
                    DisplayName = "Strict Resolver",
                    Mode = RegimenResolutionMode.PriorityWins,
                    ScheduleWeight01 = 0.9f,
                    NeedsWeight01 = 0.4f,
                    SocialWeight01 = 0.2f,
                    HabitWeight01 = 0.6f,
                    StrictnessThreshold01 = 0.5f,
                    DeviationPenalty01 = 0.4f,
                    CompliancePenaltyMultiplier01 = 1.2f,
                    TargetWeights = new[]
                    {
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Global, Weight = 0.7f, PriorityBonus = 0.1f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Site, Weight = 0.8f, PriorityBonus = 0.15f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Role, Weight = 0.85f, PriorityBonus = 0.2f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Seat, Weight = 0.9f, PriorityBonus = 0.25f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Entity, Weight = 1f, PriorityBonus = 0.3f }
                    },
                    Notes = "Priority-driven resolver for disciplined cultures."
                },
                new RegimenResolverDefinition
                {
                    Id = "resolver.lax",
                    DisplayName = "Lax Resolver",
                    Mode = RegimenResolutionMode.WeightedBlend,
                    ScheduleWeight01 = 0.35f,
                    NeedsWeight01 = 0.85f,
                    SocialWeight01 = 0.6f,
                    HabitWeight01 = 0.4f,
                    StrictnessThreshold01 = 0.85f,
                    DeviationPenalty01 = 0.05f,
                    CompliancePenaltyMultiplier01 = 0.6f,
                    TargetWeights = new[]
                    {
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Global, Weight = 0.35f, PriorityBonus = 0f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Site, Weight = 0.45f, PriorityBonus = 0.02f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Role, Weight = 0.5f, PriorityBonus = 0.05f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Seat, Weight = 0.6f, PriorityBonus = 0.08f },
                        new RegimenTargetWeightDefinition { TargetKind = RegimenTargetKind.Entity, Weight = 0.75f, PriorityBonus = 0.1f }
                    },
                    Notes = "Loose resolver that favors needs and personal preference."
                }
            };
        }
    }
}
