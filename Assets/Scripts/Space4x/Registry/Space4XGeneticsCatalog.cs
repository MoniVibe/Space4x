using System;
using PureDOTS.Runtime.Genetics;
using UnityEngine;

namespace Space4X.Registry
{
    [CreateAssetMenu(fileName = "Space4XGeneticsCatalog", menuName = "Space4X/Registry/Genetics Catalog")]
    public sealed class Space4XGeneticsCatalog : ScriptableObject
    {
        public const string ResourcePath = "Registry/Space4XGeneticsCatalog";

        [SerializeField] private GeneticAxisDefinition[] geneticAxes = Array.Empty<GeneticAxisDefinition>();
        [SerializeField] private CultureAxisDefinition[] cultureAxes = Array.Empty<CultureAxisDefinition>();
        [SerializeField] private GeneticProfileDefinition[] geneticProfiles = Array.Empty<GeneticProfileDefinition>();
        [SerializeField] private CultureProfileDefinition[] cultureProfiles = Array.Empty<CultureProfileDefinition>();

        public GeneticAxisDefinition[] GeneticAxes => geneticAxes;
        public CultureAxisDefinition[] CultureAxes => cultureAxes;
        public GeneticProfileDefinition[] GeneticProfiles => geneticProfiles;
        public CultureProfileDefinition[] CultureProfiles => cultureProfiles;

        public static Space4XGeneticsCatalog LoadOrFallback()
        {
            var catalog = Resources.Load<Space4XGeneticsCatalog>(ResourcePath);
            if (catalog == null)
            {
                catalog = CreateRuntimeFallback();
            }

            return catalog;
        }

        public static Space4XGeneticsCatalog CreateRuntimeFallback()
        {
            var catalog = CreateInstance<Space4XGeneticsCatalog>();
            catalog.ApplyRuntimeDefaults();
            return catalog;
        }

        public void ApplyRuntimeDefaults()
        {
            geneticAxes = new[]
            {
                new GeneticAxisDefinition
                {
                    Id = "gene.violence_diplomacy",
                    DisplayName = "Violence <-> Diplomacy",
                    MinValue = -100f,
                    MaxValue = 100f,
                    DefaultValue = 0f,
                    NegativePoleLabel = "Diplomatic",
                    NeutralLabel = "Balanced",
                    PositivePoleLabel = "Violent",
                    Tags = new[] { "gene", "social" }
                },
                new GeneticAxisDefinition
                {
                    Id = "gene.might_magic",
                    DisplayName = "Might <-> Magic",
                    MinValue = -100f,
                    MaxValue = 100f,
                    DefaultValue = 0f,
                    NegativePoleLabel = "Magic",
                    NeutralLabel = "Balanced",
                    PositivePoleLabel = "Might",
                    Tags = new[] { "gene", "power" }
                }
            };

            cultureAxes = new[]
            {
                new CultureAxisDefinition
                {
                    Id = "culture.spiritual_material",
                    DisplayName = "Spiritual <-> Material",
                    MinValue = -100f,
                    MaxValue = 100f,
                    DefaultValue = 0f,
                    NegativePoleLabel = "Spiritual",
                    NeutralLabel = "Balanced",
                    PositivePoleLabel = "Material",
                    Tags = new[] { "culture", "belief" }
                },
                new CultureAxisDefinition
                {
                    Id = "culture.corrupt_pure",
                    DisplayName = "Corrupt <-> Pure",
                    MinValue = -100f,
                    MaxValue = 100f,
                    DefaultValue = 0f,
                    NegativePoleLabel = "Corrupt",
                    NeutralLabel = "Neutral",
                    PositivePoleLabel = "Pure",
                    Tags = new[] { "culture", "ethic" }
                },
                new CultureAxisDefinition
                {
                    Id = "culture.lawful_chaotic",
                    DisplayName = "Lawful <-> Chaotic",
                    MinValue = -100f,
                    MaxValue = 100f,
                    DefaultValue = 0f,
                    NegativePoleLabel = "Chaotic",
                    NeutralLabel = "Neutral",
                    PositivePoleLabel = "Lawful",
                    Tags = new[] { "culture", "order" }
                },
                new CultureAxisDefinition
                {
                    Id = "culture.xenophile_xenophobe",
                    DisplayName = "Xenophile <-> Xenophobe",
                    MinValue = -100f,
                    MaxValue = 100f,
                    DefaultValue = 0f,
                    NegativePoleLabel = "Xenophobe",
                    NeutralLabel = "Neutral",
                    PositivePoleLabel = "Xenophile",
                    Tags = new[] { "culture", "relations" }
                }
            };

            geneticProfiles = new[]
            {
                new GeneticProfileDefinition
                {
                    Id = "genetic.baseline",
                    ViolenceDiplomacyAxis = 0f,
                    MightMagicAxis = 0f,
                    PrimaryHabitatId = "planet.continental",
                    SecondaryHabitatId = "planet.oceanic",
                    PreferredBiomeId = "biome.forest",
                    PreferredGravity = 1f,
                    GravityTolerancePercent = 20f,
                    ToleratesExtremeEnvironments = false,
                    MutationVariance01 = 0.1f,
                    Mutability01 = 0.4f,
                    AllowedMutationSources = GeneticMutationSourceFlags.Natural |
                                             GeneticMutationSourceFlags.ResearchFacility |
                                             GeneticMutationSourceFlags.Event |
                                             GeneticMutationSourceFlags.Miracle,
                    Tags = new[] { "starter" }
                },
                new GeneticProfileDefinition
                {
                    Id = "genetic.warlike",
                    ViolenceDiplomacyAxis = 55f,
                    MightMagicAxis = 20f,
                    PrimaryHabitatId = "planet.arid",
                    SecondaryHabitatId = "planet.desert",
                    PreferredBiomeId = "biome.savanna",
                    PreferredGravity = 1.1f,
                    GravityTolerancePercent = 25f,
                    ToleratesExtremeEnvironments = true,
                    MutationVariance01 = 0.12f,
                    Mutability01 = 0.35f,
                    AllowedMutationSources = GeneticMutationSourceFlags.Natural |
                                             GeneticMutationSourceFlags.ResearchFacility |
                                             GeneticMutationSourceFlags.Forced,
                    Tags = new[] { "warlike" }
                },
                new GeneticProfileDefinition
                {
                    Id = "genetic.diplomatic",
                    ViolenceDiplomacyAxis = -40f,
                    MightMagicAxis = -10f,
                    PrimaryHabitatId = "planet.oceanic",
                    SecondaryHabitatId = "planet.tropical",
                    PreferredBiomeId = "biome.rainforest",
                    PreferredGravity = 0.95f,
                    GravityTolerancePercent = 18f,
                    ToleratesExtremeEnvironments = false,
                    MutationVariance01 = 0.08f,
                    Mutability01 = 0.45f,
                    AllowedMutationSources = GeneticMutationSourceFlags.Natural |
                                             GeneticMutationSourceFlags.ResearchFacility |
                                             GeneticMutationSourceFlags.Event,
                    Tags = new[] { "diplomatic" }
                }
            };

            cultureProfiles = new[]
            {
                new CultureProfileDefinition
                {
                    Id = "culture.baseline",
                    SpiritualMaterialAxis = 0f,
                    LawfulChaoticAxis = 0f,
                    CorruptPureAxis = 0f,
                    XenophileXenophobeAxis = 0f,
                    Mutability01 = 0.5f,
                    DriftRate01 = 0.02f,
                    Cohesion01 = 0.5f,
                    Tags = new[] { "starter" }
                },
                new CultureProfileDefinition
                {
                    Id = "culture.spiritual_devout",
                    SpiritualMaterialAxis = -65f,
                    LawfulChaoticAxis = 20f,
                    CorruptPureAxis = 25f,
                    XenophileXenophobeAxis = 10f,
                    Mutability01 = 0.4f,
                    DriftRate01 = 0.015f,
                    Cohesion01 = 0.6f,
                    Tags = new[] { "spiritual" }
                },
                new CultureProfileDefinition
                {
                    Id = "culture.materialist_pragmatic",
                    SpiritualMaterialAxis = 60f,
                    LawfulChaoticAxis = 10f,
                    CorruptPureAxis = -10f,
                    XenophileXenophobeAxis = 20f,
                    Mutability01 = 0.45f,
                    DriftRate01 = 0.02f,
                    Cohesion01 = 0.55f,
                    Tags = new[] { "materialist" }
                },
                new CultureProfileDefinition
                {
                    Id = "culture.xenophobic_militarist",
                    SpiritualMaterialAxis = 5f,
                    LawfulChaoticAxis = 30f,
                    CorruptPureAxis = -20f,
                    XenophileXenophobeAxis = -70f,
                    Mutability01 = 0.35f,
                    DriftRate01 = 0.01f,
                    Cohesion01 = 0.65f,
                    Tags = new[] { "xenophobic", "militarist" }
                }
            };
        }
    }
}
