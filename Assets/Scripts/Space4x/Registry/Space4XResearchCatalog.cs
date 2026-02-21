using System;
using PureDOTS.Runtime.Technology;
using UnityEngine;

namespace Space4X.Registry
{
    [CreateAssetMenu(fileName = "Space4XResearchCatalog", menuName = "Space4X/Registry/Research Catalog")]
    public sealed class Space4XResearchCatalog : ScriptableObject
    {
        public const string ResourcePath = "Registry/Space4XResearchCatalog";

        [SerializeField] private ResearchDisciplineDefinition[] disciplines = Array.Empty<ResearchDisciplineDefinition>();
        [SerializeField] private ResearchNodeDefinition[] nodes = Array.Empty<ResearchNodeDefinition>();
        [SerializeField] private ResearchUnlockDefinition[] unlocks = Array.Empty<ResearchUnlockDefinition>();
        [SerializeField] private ResearchKnowledgeDefinition[] knowledgeSeeds = Array.Empty<ResearchKnowledgeDefinition>();

        public ResearchDisciplineDefinition[] Disciplines => disciplines;
        public ResearchNodeDefinition[] Nodes => nodes;
        public ResearchUnlockDefinition[] Unlocks => unlocks;
        public ResearchKnowledgeDefinition[] KnowledgeSeeds => knowledgeSeeds;

        public static Space4XResearchCatalog LoadOrFallback()
        {
            var catalog = Resources.Load<Space4XResearchCatalog>(ResourcePath);
            if (catalog == null)
            {
                catalog = CreateRuntimeFallback();
            }

            return catalog;
        }

        public static Space4XResearchCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XResearchCatalog>();
            catalog.ApplyRuntimeDefaults();
            return catalog;
        }

        public void ApplyRuntimeDefaults()
        {
            disciplines = new[]
            {
                new ResearchDisciplineDefinition { Id = "combat", DisplayName = "Combat", Description = "Weaponry, tactics, and defensive doctrine.", Kind = ResearchDisciplineKind.Combat, BaseCostMultiplier = 1.1f, Tags = new[] { "military" } },
                new ResearchDisciplineDefinition { Id = "production", DisplayName = "Production", Description = "Manufacturing, automation, and industrial upgrades.", Kind = ResearchDisciplineKind.Production, BaseCostMultiplier = 1.0f, Tags = new[] { "industry" } },
                new ResearchDisciplineDefinition { Id = "extraction", DisplayName = "Extraction", Description = "Mining, salvage, and resource recovery.", Kind = ResearchDisciplineKind.Extraction, BaseCostMultiplier = 0.95f, Tags = new[] { "resource" } },
                new ResearchDisciplineDefinition { Id = "society", DisplayName = "Society", Description = "Culture, governance, and social engineering.", Kind = ResearchDisciplineKind.Society, BaseCostMultiplier = 1.05f, Tags = new[] { "culture" } },
                new ResearchDisciplineDefinition { Id = "diplomacy", DisplayName = "Diplomacy", Description = "Relations, negotiation, and influence.", Kind = ResearchDisciplineKind.Diplomacy, BaseCostMultiplier = 1.0f, Tags = new[] { "relations" } },
                new ResearchDisciplineDefinition { Id = "colonization", DisplayName = "Colonization", Description = "Colony development and life support.", Kind = ResearchDisciplineKind.Colonization, BaseCostMultiplier = 1.0f, Tags = new[] { "habitat" } },
                new ResearchDisciplineDefinition { Id = "exploration", DisplayName = "Exploration", Description = "Surveying, scouting, and deep-space logistics.", Kind = ResearchDisciplineKind.Exploration, BaseCostMultiplier = 1.0f, Tags = new[] { "scout" } },
                new ResearchDisciplineDefinition { Id = "construction", DisplayName = "Construction", Description = "Shipyards, facilities, and structural design.", Kind = ResearchDisciplineKind.Construction, BaseCostMultiplier = 1.0f, Tags = new[] { "build" } },
                new ResearchDisciplineDefinition { Id = "physics", DisplayName = "Physics", Description = "Fundamental science and exotic systems.", Kind = ResearchDisciplineKind.Physics, BaseCostMultiplier = 1.15f, Tags = new[] { "science" } }
            };

            unlocks = new[]
            {
                new ResearchUnlockDefinition { Id = "unlock.proc.scrap_to_supplies", Kind = ResearchUnlockKind.FacilityProcess, TargetId = "proc.scrap_to_supplies", Quantity = 1f, QualityFloor01 = 0.3f, Tags = new[] { "salvage" } },
                new ResearchUnlockDefinition { Id = "unlock.proc.raw_gas_to_fuel", Kind = ResearchUnlockKind.FacilityProcess, TargetId = "proc.raw_gas_to_fuel", Quantity = 1f, QualityFloor01 = 0.3f, Tags = new[] { "fuel" } },
                new ResearchUnlockDefinition { Id = "unlock.proc.desalinate_brine", Kind = ResearchUnlockKind.FacilityProcess, TargetId = "proc.desalinate_brine", Quantity = 1f, QualityFloor01 = 0.4f, Tags = new[] { "water" } },
                new ResearchUnlockDefinition { Id = "unlock.proc.algae_food", Kind = ResearchUnlockKind.FacilityProcess, TargetId = "proc.algae_food", Quantity = 1f, QualityFloor01 = 0.45f, Tags = new[] { "food" } },
                new ResearchUnlockDefinition { Id = "unlock.proc.printed_meat", Kind = ResearchUnlockKind.FacilityProcess, TargetId = "proc.printed_meat", Quantity = 1f, QualityFloor01 = 0.5f, Tags = new[] { "food" } },
                new ResearchUnlockDefinition { Id = "unlock.limb.relations_office", Kind = ResearchUnlockKind.FacilityLimb, TargetId = "limb.relations_office", Quantity = 1f, QualityFloor01 = 0.5f, Tags = new[] { "relations" } },
                new ResearchUnlockDefinition { Id = "unlock.tech.reverse_engineering", Kind = ResearchUnlockKind.TechFlag, TargetId = "reverse_engineering", Quantity = 1f, QualityFloor01 = 0f, Tags = new[] { "research" } },
                new ResearchUnlockDefinition { Id = "unlock.bp.ballistics_core", Kind = ResearchUnlockKind.Blueprint, TargetId = "bp_weapon_ballistics_core", Quantity = 1f, QualityFloor01 = 0.6f, Tags = new[] { "combat" } },
                new ResearchUnlockDefinition { Id = "unlock.bp.shredder_cannon", Kind = ResearchUnlockKind.Blueprint, TargetId = "bp_weapon_shredder_cannon", Quantity = 1f, QualityFloor01 = 0.55f, Tags = new[] { "combat" } },
                new ResearchUnlockDefinition { Id = "unlock.bp.zero_g_harness", Kind = ResearchUnlockKind.Blueprint, TargetId = "bp_zero_g_harness", Quantity = 1f, QualityFloor01 = 0.6f, Tags = new[] { "physics" } },
                new ResearchUnlockDefinition { Id = "unlock.bp.inertia_field", Kind = ResearchUnlockKind.Blueprint, TargetId = "bp_inertia_field", Quantity = 1f, QualityFloor01 = 0.6f, Tags = new[] { "physics" } },
                new ResearchUnlockDefinition { Id = "unlock.bp.anti_grav_drive", Kind = ResearchUnlockKind.Blueprint, TargetId = "bp_anti_grav_drive", Quantity = 1f, QualityFloor01 = 0.62f, Tags = new[] { "physics" } },
                new ResearchUnlockDefinition { Id = "unlock.bp.genetic_forge", Kind = ResearchUnlockKind.Blueprint, TargetId = "bp_genetic_forge", Quantity = 1f, QualityFloor01 = 0.58f, Tags = new[] { "society" } }
            };

            nodes = new[]
            {
                new ResearchNodeDefinition
                {
                    Id = "tech.salvage_refinement",
                    DisplayName = "Salvage Refinement",
                    Description = "Refine wreckage into usable supplies.",
                    DisciplineId = "extraction",
                    Tier = 1,
                    BaseResearchCost = 120f,
                    BaseTimeSeconds = 180f,
                    BaseDifficulty01 = 0.35f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.2f,
                    UnlinkedPenaltyMultiplier = 1.3f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.proc.scrap_to_supplies" },
                    Tags = new[] { "salvage" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.gas_scooping",
                    DisplayName = "Gas Scooping",
                    Description = "Harvest gas giants for raw fuel.",
                    DisciplineId = "exploration",
                    Tier = 1,
                    BaseResearchCost = 140f,
                    BaseTimeSeconds = 200f,
                    BaseDifficulty01 = 0.4f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.25f,
                    UnlinkedPenaltyMultiplier = 1.35f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.proc.raw_gas_to_fuel" },
                    Tags = new[] { "fuel", "exploration" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.desalination",
                    DisplayName = "Desalination",
                    Description = "Recover water from brine byproducts.",
                    DisciplineId = "colonization",
                    Tier = 1,
                    BaseResearchCost = 110f,
                    BaseTimeSeconds = 160f,
                    BaseDifficulty01 = 0.3f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.2f,
                    UnlinkedPenaltyMultiplier = 1.3f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.proc.desalinate_brine" },
                    Tags = new[] { "water" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.hydroponics",
                    DisplayName = "Hydroponics",
                    Description = "Algae growth and nutrient recovery.",
                    DisciplineId = "colonization",
                    Tier = 1,
                    BaseResearchCost = 130f,
                    BaseTimeSeconds = 190f,
                    BaseDifficulty01 = 0.35f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.25f,
                    UnlinkedPenaltyMultiplier = 1.35f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.proc.algae_food" },
                    Tags = new[] { "food" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.printed_meat",
                    DisplayName = "Printed Meat",
                    Description = "Synthetic food printing at scale.",
                    DisciplineId = "production",
                    Tier = 2,
                    BaseResearchCost = 220f,
                    BaseTimeSeconds = 260f,
                    BaseDifficulty01 = 0.45f,
                    PrerequisiteIds = new[] { "tech.hydroponics" },
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.4f,
                    UnlinkedPenaltyMultiplier = 1.6f,
                    MinPrerequisiteLinks = 1,
                    UnlockIds = new[] { "unlock.proc.printed_meat" },
                    Tags = new[] { "food", "biotech" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.ballistics",
                    DisplayName = "Ballistics",
                    Description = "Baseline projectile weapon theory.",
                    DisciplineId = "combat",
                    Tier = 1,
                    BaseResearchCost = 150f,
                    BaseTimeSeconds = 210f,
                    BaseDifficulty01 = 0.4f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.25f,
                    UnlinkedPenaltyMultiplier = 1.35f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.bp.ballistics_core" },
                    Tags = new[] { "combat" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.shredder_cannons",
                    DisplayName = "Shredder Cannons",
                    Description = "High-velocity fragmentation weapons.",
                    DisciplineId = "combat",
                    Tier = 2,
                    BaseResearchCost = 260f,
                    BaseTimeSeconds = 320f,
                    BaseDifficulty01 = 0.55f,
                    PrerequisiteIds = new[] { "tech.ballistics" },
                    OptionalPrerequisiteIds = new[] { "tech.field_inertia" },
                    MissingPrereqPenaltyMultiplier = 1.5f,
                    UnlinkedPenaltyMultiplier = 1.8f,
                    MinPrerequisiteLinks = 1,
                    UnlockIds = new[] { "unlock.bp.shredder_cannon" },
                    Tags = new[] { "combat", "warlike" },
                    RequiredOutlookIds = new[] { "warlike" },
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0.35f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.zero_g_engineering",
                    DisplayName = "Zero-G Engineering",
                    Description = "Foundation for microgravity operations.",
                    DisciplineId = "physics",
                    Tier = 1,
                    BaseResearchCost = 180f,
                    BaseTimeSeconds = 220f,
                    BaseDifficulty01 = 0.45f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.3f,
                    UnlinkedPenaltyMultiplier = 1.4f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.bp.zero_g_harness" },
                    Tags = new[] { "physics" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.field_inertia",
                    DisplayName = "Inertia Field Theory",
                    Description = "Field shaping for inertial control.",
                    DisciplineId = "physics",
                    Tier = 2,
                    BaseResearchCost = 260f,
                    BaseTimeSeconds = 300f,
                    BaseDifficulty01 = 0.55f,
                    PrerequisiteIds = new[] { "tech.zero_g_engineering" },
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.5f,
                    UnlinkedPenaltyMultiplier = 1.7f,
                    MinPrerequisiteLinks = 1,
                    UnlockIds = new[] { "unlock.bp.inertia_field" },
                    Tags = new[] { "physics" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.anti_gravity",
                    DisplayName = "Anti-Gravity",
                    Description = "Exotic propulsion without reaction mass.",
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
                    UnlockIds = new[] { "unlock.bp.anti_grav_drive" },
                    Tags = new[] { "physics", "exotic" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = new[] { "materialist_pure" },
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 0.6f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.diplomatic_channels",
                    DisplayName = "Diplomatic Channels",
                    Description = "Formalize external relations and mediation.",
                    DisciplineId = "diplomacy",
                    Tier = 1,
                    BaseResearchCost = 120f,
                    BaseTimeSeconds = 160f,
                    BaseDifficulty01 = 0.3f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.2f,
                    UnlinkedPenaltyMultiplier = 1.3f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.limb.relations_office" },
                    Tags = new[] { "relations" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = new[] { "fanatic_xenophobe" },
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 0.4f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.reverse_engineering",
                    DisplayName = "Reverse Engineering",
                    Description = "Extract blueprints and hidden modifiers from equipment.",
                    DisciplineId = "construction",
                    Tier = 1,
                    BaseResearchCost = 160f,
                    BaseTimeSeconds = 200f,
                    BaseDifficulty01 = 0.4f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.25f,
                    UnlinkedPenaltyMultiplier = 1.4f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.tech.reverse_engineering" },
                    Tags = new[] { "research", "inspection" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = Array.Empty<string>(),
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 1f
                },
                new ResearchNodeDefinition
                {
                    Id = "tech.genetic_manipulation",
                    DisplayName = "Genetic Manipulation",
                    Description = "Directed genetic editing for biome adaptation.",
                    DisciplineId = "society",
                    Tier = 2,
                    BaseResearchCost = 280f,
                    BaseTimeSeconds = 320f,
                    BaseDifficulty01 = 0.55f,
                    PrerequisiteIds = Array.Empty<string>(),
                    OptionalPrerequisiteIds = Array.Empty<string>(),
                    MissingPrereqPenaltyMultiplier = 1.6f,
                    UnlinkedPenaltyMultiplier = 1.9f,
                    MinPrerequisiteLinks = 0,
                    UnlockIds = new[] { "unlock.bp.genetic_forge" },
                    Tags = new[] { "society", "biotech" },
                    RequiredOutlookIds = Array.Empty<string>(),
                    ForbiddenOutlookIds = new[] { "spiritual" },
                    RequiredOutlookMinimum01 = 0f,
                    ForbiddenOutlookMaximum01 = 0.4f
                }
            };

            knowledgeSeeds = new[]
            {
                new ResearchKnowledgeDefinition
                {
                    KnowledgeId = "knowledge.seed.salvage",
                    NodeId = "tech.salvage_refinement",
                    OwnerEntityId = "seed.standard",
                    SourceId = "seed",
                    State = ResearchKnowledgeState.Stable,
                    KnowledgeQuality01 = 0.6f,
                    Drift01 = 0.05f,
                    Confidence01 = 0.6f,
                    MutationVariance01 = 0.1f,
                    GeniusPotential01 = 0.2f,
                    SharingPolicy = ResearchSharingPolicy.GroupLimited,
                    Tags = new[] { "starter" }
                },
                new ResearchKnowledgeDefinition
                {
                    KnowledgeId = "knowledge.seed.desalination",
                    NodeId = "tech.desalination",
                    OwnerEntityId = "seed.standard",
                    SourceId = "seed",
                    State = ResearchKnowledgeState.Stable,
                    KnowledgeQuality01 = 0.58f,
                    Drift01 = 0.06f,
                    Confidence01 = 0.55f,
                    MutationVariance01 = 0.12f,
                    GeniusPotential01 = 0.18f,
                    SharingPolicy = ResearchSharingPolicy.GroupLimited,
                    Tags = new[] { "starter" }
                }
            };
        }
    }
}
