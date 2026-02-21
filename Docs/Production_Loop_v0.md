# Production Loop v0 (Space4x adapter notes)

**Goal**: Share a minimal extraction + production loop via PureDOTS, then adapt to Space4x modules/ship parts and shipyards.

- **Extraction**: keep current mining loop; map output to production inputs.
- **Facilities**: module fabs (non-shipyard) + shipyards. Ships are built **only** in shipyards; modules/resources are produced in other facilities.
- **Recipes**: ore → ingot/alloy; parts → module items; shipyard consumes parts/alloy/ingots to produce hull items.
- **Essentials**: supplies → food/water/fuel (placeholder); parts + supplies → trade goods for early economy scaffolding.
- **Facilities**: facility hulls + limbs + organs; limbs can be production/power/cargo/automation/relations/legal/executive/etc.; processes can accept byproducts, salvage, or gas-scooped inputs; output feeds stock or markets.
- **Investment**: facility build costs, payroll budgets, and tax withholding are data-only hooks for now.
- **Policies/Edicts**: owners (business/guild/faction/empire) can enact facility policies that adjust throughput, seats, costs, byproducts, relations, and compliance (data-only hooks).
- **Staffing profiles**: teams + shifts + seats define staffing cycles, downtime, wages, and power budgets (data-only hooks for facilities/ships).
- **Regimens**: per-entity or per-seat schedules define hourly activity intent (work/relax/pray/etc.) as data-only hooks; adherence is profile-weighted, not hard enforcement.
- **Stockpile**: carrier cargo or station storage as `ResourceStockpileRef`; module outputs are items until equipped.
- **Shipyard MVP**: `BusinessType.Builder` stands in for shipyards; module fabs use `BusinessType.Blacksmith` until we split explicit facility classes.
- **Telemetry**: slot utilization + throughput; use for headless validation.
- **Inputs**: allow tag-based “any fuel/any food” inputs so multiple resource types can satisfy recipes.
- **Outputs**: optional tag-based outputs for generic resource variants (e.g., any fuel). Hull outputs use hull IDs (e.g., `lcv-sparrow`).
- **Ship models**: chassis + hull segments compose a model; models point to default segments/modules until the customization pass lands.
- **Crew**: production uses abstract crew pools (planets = millions; ships/stations = pooled crew) for throughput modifiers later.
- **Permissions**: default generic (owner/contracted) with a later pass for faction policy nuance.

Regimens are advisory by default: entities blend regimen intent with their profile/outlook. High strictness favors compliance; night-owl shifts bias late hours; forced compliance can apply mood/sleep effectiveness penalties. Resolver presets control how global/seat/entity regimens blend, while profile presets (lawful/chaotic/devout/martial) set default adherence behavior.
Entity-side resolver is a lightweight helper (`RegimenActivityResolver`) plus cached `RegimenIntent`; update on schedule boundaries to avoid per-frame overhead.

Blueprints, Quality, Sabotage (draft hooks)
--------------------------------------------
- Blueprints are dual: knowledge (entity memory) + physical copies (business artifacts).
- Research labs can reverse engineer modules to extract blueprints or reveal hidden quality modifiers.
- Inspection outcomes are opposed by concealment; perception/analysis stats influence detection.
- Pirated or forged copies can be degraded or (rarely) improved by genius operators.
- Sabotage fronts can distribute disguised items with hidden defects and delayed triggers.

Research Tree (draft hooks)
---------------------------
- Research nodes are organized by discipline (combat, production, extraction, society, diplomacy, colonization, exploration, construction, physics).
- Nodes can be pursued without prerequisites at escalating penalties (missing-link multipliers and minimum link coverage).
- Research knowledge is a non-physical asset; entities/businesses can sell, trade, steal, or mutate it.
- Outlook restrictions are data-only: nodes can require or forbid certain outlooks without hard locks.
- Reverse engineering can unlock tech flags or blueprint knowledge via research labs.
- `Space4XResearchSeedSystem` can seed `ResearchProject` buffers from `Space4XResearchCatalog` when a `Space4XResearchSeedRequest` tag is present (auto-attached to colonies/stations when scenario + economy are enabled).

Genetics & Genealogy (draft hooks)
----------------------------------
- Genetics define inherited inclinations (violence<->diplomacy, might<->magic) and baseline habitat preferences.
- Culture defines mutable axes (spiritual<->material, corrupt<->pure, lawful<->chaotic, xenophobe<->xenophile); "fanatic" is a threshold, not a hard lock.
- Genealogy is mutable only via special sources: research facility limbs, events/ruins, miracles, or forced manipulation.
- Gene/culture axes can be stored as trait axes with `TraitAxisTag.Gene` / `TraitAxisTag.Culture` for filtering; `Space4XGeneticsTraitAxisBridgeSystem` mirrors them into `TraitAxisValue`.
- Habitat preferences map to planet/species preference components (`SpeciesPlanetPreference`, `PreferredPlanetFlavor`, `PreferredBiome`) via `Space4XSpeciesPreferenceSeedSystem`.
- Contracts live in `PureDOTS.Runtime.Genetics` (`GeneticProfile`, `GeneticInclination`, `CultureProfile`, `GenealogyLineage`, `GeneticModificationRecord`).
- `Space4XGeneticsCatalog` provides default axis definitions and seed profiles (data-only).
- `Space4XGeneticsSeedSystem` seeds baseline genetic/culture components onto individuals missing them.
- `Space4XGeneticsGroupSeedSystem` seeds baseline genetic/culture components onto colonies and factions.

Sample data (contracts only)
-----------------------------
```csharp
var blueprintCopy = new BlueprintCopyDefinition
{
    CopyId = "bp_copy_001",
    BlueprintId = "bp_laser_s_1",
    Kind = BlueprintCopyKind.Licensed,
    CopyQuality01 = 0.72f,
    Underclock01 = 0.15f,
    Concealment01 = 0.25f,
    License = new BlueprintLicenseDefinition
    {
        Kind = BlueprintLicenseKind.NonExclusive,
        LicensorId = "biz_orion",
        LicenseeId = "biz_aegis",
        SharingPolicy = BlueprintSharingPolicy.GroupLimited,
        AllowCopy = true,
        AllowSubLicense = false,
        RoyaltyRate01 = 0.08f
    }
};

var organInstance = new OrganInstanceDefinition
{
    InstanceId = "org_kit_778",
    OrganId = "cooling.orion.m1",
    BlueprintCopyId = blueprintCopy.CopyId,
    ManufacturerId = "orion_coilworks",
    QualityProfile = new QualityProfile
    {
        MaterialQuality01 = 0.68f,
        BlueprintQuality01 = 0.72f,
        ManufacturingQuality01 = 0.70f,
        IntegrationQuality01 = 0.66f,
        FinalQuality01 = 0.69f,
        Reliability01 = 0.74f,
        FaultRate01 = 0.12f
    }
};

var inspection = new ResearchInspectionDefinition
{
    InspectionId = "lab_scan_004",
    LabId = "lab_helios",
    TargetKind = ResearchInspectionTargetKind.ModuleInstance,
    TargetId = "mod_laser_553",
    Focus = ResearchInspectionFocus.HiddenModifierScan,
    AnalystSkill01 = 0.72f,
    AnalystPerception01 = 0.78f,
    AnalysisQuality01 = 0.65f,
    PowerAllocation01 = 0.6f
};

var sabotage = new SabotageMaskDefinition
{
    Id = "sabotage_mask_02",
    Label = "Sleeper overload",
    Intent = SabotageIntent.DelayFailure,
    FrontId = "front_aurora_trade",
    OperatorId = "cell_mauve",
    Concealment01 = 0.82f,
    DetectionDifficulty01 = 0.75f,
    DefectSeverity01 = 0.55f,
    TriggerRisk01 = 0.35f,
    Triggers = new[]
    {
        new FaultTriggerDefinition
        {
            Kind = SabotageTriggerKind.Overheat,
            Threshold01 = 0.85f,
            CooldownSeconds = 90f,
            MinTicks = 240
        }
    },
    HiddenModifiers = new[]
    {
        new QualityModifier
        {
            Id = "mod_hidden_heat",
            Label = "Thermal bleed",
            Target = QualityStage.Reliability,
            Mode = QualityModifierMode.Additive,
            Value = -0.12f,
            IsHidden = true,
            SourceId = "sabotage_mask_02",
            Tag = "heat"
        }
    }
};

var consumableInstance = new ConsumableInstanceDefinition
{
    InstanceId = "cons_shieldpatch_12",
    ConsumableId = "shield_patch",
    BlueprintCopyId = "bp_copy_shield_02",
    ManufacturerId = "lumen_covenant",
    Charges = 1,
    QualityProfile = new QualityProfile
    {
        MaterialQuality01 = 0.64f,
        BlueprintQuality01 = 0.7f,
        ManufacturingQuality01 = 0.66f,
        IntegrationQuality01 = 0.62f,
        FinalQuality01 = 0.65f,
        Reliability01 = 0.7f,
        FaultRate01 = 0.08f
    },
    SabotageMasks = new[] { sabotage }
};

var chassis = new ShipChassisDefinition
{
    Id = "lcv-sparrow",
    DisplayName = "LCV Sparrow",
    ManufacturerId = "orion_coilworks",
    Role = "Light Courier",
    SegmentSlotCount = 3,
    ModuleSocketCount = 4,
    BaseMassTons = 140f,
    BaseIntegrity = 120f
};

var segment = new HullSegmentDefinition
{
    Id = "hull_bastion_ring",
    DisplayName = "Bastion Ring",
    SegmentType = "ring",
    ManufacturerId = "aegis_forge",
    ModuleSocketCount = 2,
    MassTons = 48f,
    IntegrityBonus = 55f,
    TurnRateMultiplier = 0.9f,
    AccelerationMultiplier = 0.95f,
    DecelerationMultiplier = 0.95f,
    MaxSpeedMultiplier = 0.9f
};

var model = new ShipModelDefinition
{
    Id = "model.lcv.sparrow",
    DisplayName = "LCV Sparrow",
    ChassisId = chassis.Id,
    ManufacturerId = "orion_coilworks",
    BlueprintId = "bp_ship_lcv_sparrow",
    DefaultSegmentIds = new[] { segment.Id },
    DefaultModuleIds = new[] { "reactor-mk1", "engine-mk1", "laser-s-1" },
    DefaultStaffingProfileId = "staffing.standard_3x8"
};

var facilityProcess = new FacilityProcessDefinition
{
    Id = "proc.desalinate_brine",
    DisplayName = "Desalinate Brine",
    Stage = ProductionStage.Refining,
    Inputs = new[]
    {
        new FacilityProcessInputDefinition { ResourceId = "space4x_byproduct_brine", Quantity = 10f }
    },
    Outputs = new[]
    {
        new FacilityProcessOutputDefinition { ResourceId = "space4x_water", Quantity = 8f, QualityFloor01 = 0.4f }
    }
};

var facilityModel = new FacilityModelDefinition
{
    Id = "facility.desalination_plant",
    DisplayName = "Desalination Plant",
    FacilityFamilyId = "desalination",
    ManufacturerId = "aegis_forge",
    HullId = "hull.desalination.core",
    BlueprintId = "bp_facility_desalination",
    DefaultLimbIds = new[] { "limb.desalination_stack" },
    DefaultProcessIds = new[] { facilityProcess.Id },
    DefaultStaffingProfileId = "staffing.standard_3x8",
    Investment = new FacilityInvestmentDefinition
    {
        InitialCapitalCredits = 4800f,
        PermitCostCredits = 400f,
        ConstructionTimeSeconds = 220f,
        ResourceCosts = new[]
        {
            new FacilityConstructionCostDefinition { ResourceId = "space4x_parts", UnitsRequired = 24f },
            new FacilityConstructionCostDefinition { ResourceId = "space4x_alloy", UnitsRequired = 18f }
        },
        PayrollBudgetPerSecond = 0.4f,
        MaintenanceBudgetPerSecond = 0.18f
    }
};

var policy = new FacilityPolicyDefinition
{
    Id = "policy.autonomy_boost",
    DisplayName = "Automation Boost",
    Kind = FacilityPolicyKind.Policy,
    Scope = FacilityPolicyScope.Business,
    Target = FacilityPolicyTarget.Limb,
    DecisionMode = FacilityPolicyDecisionMode.BoardVote,
    AllowedLimbTypes = new[] { FacilityLimbTypeIds.Automation },
    Effects = new[]
    {
        new FacilityPolicyEffect
        {
            Kind = FacilityPolicyEffectKind.ThroughputMultiplier,
            Value = 1.1f,
            Tag = "automation"
        }
    }
};

var shiftSchedule = new WorkShiftScheduleDefinition
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
            IsMaintenance = true
        }
    }
};

var staffingProfile = new StaffingProfileDefinition
{
    Id = "staffing.standard_3x8",
    DisplayName = "Standard 3x8",
    SiteType = WorkSiteType.Any,
    CalendarId = "calendar.standard",
    ScheduleId = shiftSchedule.Id,
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
            ScheduleId = shiftSchedule.Id,
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
};

var regimenSchedule = new RegimenScheduleDefinition
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
                new RegimenSlotDefinition { SlotIndex = 12, Activity = RegimenActivityKind.Leisure },
                new RegimenSlotDefinition { SlotIndex = 13, Activity = RegimenActivityKind.Work },
                new RegimenSlotDefinition { SlotIndex = 20, Activity = RegimenActivityKind.Leisure },
                new RegimenSlotDefinition { SlotIndex = 22, Activity = RegimenActivityKind.Sleep }
            }
        }
    },
    ComplianceTarget01 = 0.8f,
    OverrideTolerance01 = 0.2f
};

var regimenProfile = new RegimenProfileDefinition
{
    Id = "profile.standard",
    DisplayName = "Standard",
    ScheduleId = regimenSchedule.Id,
    Priority = 0.3f,
    FatigueBias01 = 0.6f,
    MoraleBias01 = 0.4f,
    DisciplineBias01 = 0.5f,
    Strictness01 = 0.45f,
    DeviationBias01 = 0.2f,
    NightOwlShiftHours = 0f,
    AfterHoursSocialBias01 = 0.15f,
    ForcedComplianceMoodPenalty01 = 0.08f,
    ForcedComplianceSleepPenalty01 = 0.1f
};

var regimenAssignment = new RegimenAssignmentDefinition
{
    Id = "assign.global.standard",
    TargetKind = RegimenTargetKind.Global,
    TargetId = "global",
    ProfileId = regimenProfile.Id,
    ResolverId = "resolver.standard",
    Priority = 0.1f,
    Weight = 1f
};

var regimenResolver = new RegimenResolverDefinition
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
    CompliancePenaltyMultiplier01 = 1f
};

var discipline = new ResearchDisciplineDefinition
{
    Id = "physics",
    DisplayName = "Physics",
    Description = "Fundamental science and exotic systems.",
    Kind = ResearchDisciplineKind.Physics,
    BaseCostMultiplier = 1.15f
};

var unlock = new ResearchUnlockDefinition
{
    Id = "unlock.bp.anti_grav_drive",
    Kind = ResearchUnlockKind.Blueprint,
    TargetId = "bp_anti_grav_drive",
    Quantity = 1f,
    QualityFloor01 = 0.62f
};

var node = new ResearchNodeDefinition
{
    Id = "tech.anti_gravity",
    DisplayName = "Anti-Gravity",
    DisciplineId = "physics",
    Tier = 3,
    BaseResearchCost = 420f,
    BaseTimeSeconds = 420f,
    BaseDifficulty01 = 0.7f,
    PrerequisiteIds = new[] { "tech.field_inertia" },
    OptionalPrerequisiteIds = new[] { "tech.zero_g_engineering" },
    MissingPrereqPenaltyMultiplier = 1.8f,
    UnlinkedPenaltyMultiplier = 2.1f,
    MinPrerequisiteLinks = 1,
    UnlockIds = new[] { unlock.Id },
    ForbiddenOutlookIds = new[] { "materialist_pure" },
    ForbiddenOutlookMaximum01 = 0.6f
};
```

Suggested next docs: `C:/Dev/unity_clean/puredots/Docs/Production_Loop_v0.md`.
