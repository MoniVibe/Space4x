using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Resource families for the economy.
    /// </summary>
    public enum ResourceFamily : byte
    {
        None = 0,
        Metals = 1,        // Iron -> Steel
        AdvancedMetals = 2, // Titanium -> Plasteel
        Organics = 3,      // Biomass -> Nutrients -> Biopolymers
        Petrochemicals = 4, // Hydrocarbon -> Fuels -> Polymers
        Electronics = 5     // Rare Earths -> Conductors -> Quantum Cores
    }

    /// <summary>
    /// Processing tier for resources.
    /// </summary>
    public enum ProcessingTier : byte
    {
        Raw = 0,       // Mined directly
        Refined = 1,   // First processing stage
        Composite = 2, // Combined materials
        Advanced = 3   // High-tech outputs
    }

    /// <summary>
    /// Resource definition with family and tier.
    /// </summary>
    public struct ResourceDefinition
    {
        /// <summary>
        /// Unique ID for this resource.
        /// </summary>
        public FixedString32Bytes Id;

        /// <summary>
        /// Display name.
        /// </summary>
        public FixedString64Bytes Name;

        /// <summary>
        /// Resource family.
        /// </summary>
        public ResourceFamily Family;

        /// <summary>
        /// Processing tier.
        /// </summary>
        public ProcessingTier Tier;

        /// <summary>
        /// Base value per unit.
        /// </summary>
        public float BaseValue;

        /// <summary>
        /// Mass per unit (affects cargo).
        /// </summary>
        public float MassPerUnit;

        public static ResourceDefinition Create(string id, string name, ResourceFamily family, ProcessingTier tier, float value, float mass)
        {
            return new ResourceDefinition
            {
                Id = new FixedString32Bytes(id),
                Name = new FixedString64Bytes(name),
                Family = family,
                Tier = tier,
                BaseValue = value,
                MassPerUnit = mass
            };
        }
    }

    /// <summary>
    /// Input resource for a recipe.
    /// </summary>
    public struct RecipeInput
    {
        /// <summary>
        /// Resource ID.
        /// </summary>
        public FixedString32Bytes ResourceId;

        /// <summary>
        /// Amount required per batch.
        /// </summary>
        public float Amount;

        public static RecipeInput Create(string resourceId, float amount)
        {
            return new RecipeInput
            {
                ResourceId = new FixedString32Bytes(resourceId),
                Amount = amount
            };
        }
    }

    /// <summary>
    /// Output resource from a recipe.
    /// </summary>
    public struct RecipeOutput
    {
        /// <summary>
        /// Resource ID.
        /// </summary>
        public FixedString32Bytes ResourceId;

        /// <summary>
        /// Amount produced per batch.
        /// </summary>
        public float Amount;

        public static RecipeOutput Create(string resourceId, float amount)
        {
            return new RecipeOutput
            {
                ResourceId = new FixedString32Bytes(resourceId),
                Amount = amount
            };
        }
    }

    /// <summary>
    /// Processing recipe definition.
    /// </summary>
    public struct ResourceRecipe
    {
        /// <summary>
        /// Unique recipe ID.
        /// </summary>
        public FixedString32Bytes Id;

        /// <summary>
        /// Display name.
        /// </summary>
        public FixedString64Bytes Name;

        /// <summary>
        /// Primary input resource.
        /// </summary>
        public RecipeInput PrimaryInput;

        /// <summary>
        /// Secondary input resource (optional).
        /// </summary>
        public RecipeInput SecondaryInput;

        /// <summary>
        /// Whether secondary input is required.
        /// </summary>
        public byte HasSecondaryInput;

        /// <summary>
        /// Primary output resource.
        /// </summary>
        public RecipeOutput PrimaryOutput;

        /// <summary>
        /// Secondary output resource (byproduct, optional).
        /// </summary>
        public RecipeOutput SecondaryOutput;

        /// <summary>
        /// Whether there's a secondary output.
        /// </summary>
        public byte HasSecondaryOutput;

        /// <summary>
        /// Base processing time in seconds.
        /// </summary>
        public float ProcessingTime;

        /// <summary>
        /// Energy cost per batch.
        /// </summary>
        public float EnergyCost;

        /// <summary>
        /// Minimum facility tier required.
        /// </summary>
        public byte MinFacilityTier;

        /// <summary>
        /// Skill bonus multiplier from crew skill.
        /// </summary>
        public half SkillBonusMultiplier;

        public static ResourceRecipe CreateSimple(string id, string name, string inputId, float inputAmount, string outputId, float outputAmount, float time, float energy)
        {
            return new ResourceRecipe
            {
                Id = new FixedString32Bytes(id),
                Name = new FixedString64Bytes(name),
                PrimaryInput = RecipeInput.Create(inputId, inputAmount),
                SecondaryInput = default,
                HasSecondaryInput = 0,
                PrimaryOutput = RecipeOutput.Create(outputId, outputAmount),
                SecondaryOutput = default,
                HasSecondaryOutput = 0,
                ProcessingTime = time,
                EnergyCost = energy,
                MinFacilityTier = 1,
                SkillBonusMultiplier = (half)0.2f
            };
        }

        public static ResourceRecipe CreateCombination(string id, string name, string input1Id, float input1Amount, string input2Id, float input2Amount, string outputId, float outputAmount, float time, float energy, byte facilityTier)
        {
            return new ResourceRecipe
            {
                Id = new FixedString32Bytes(id),
                Name = new FixedString64Bytes(name),
                PrimaryInput = RecipeInput.Create(input1Id, input1Amount),
                SecondaryInput = RecipeInput.Create(input2Id, input2Amount),
                HasSecondaryInput = 1,
                PrimaryOutput = RecipeOutput.Create(outputId, outputAmount),
                SecondaryOutput = default,
                HasSecondaryOutput = 0,
                ProcessingTime = time,
                EnergyCost = energy,
                MinFacilityTier = facilityTier,
                SkillBonusMultiplier = (half)0.25f
            };
        }
    }

    /// <summary>
    /// Blob asset containing all resource definitions and recipes.
    /// </summary>
    public struct ResourceChainCatalogBlob
    {
        /// <summary>
        /// All resource definitions.
        /// </summary>
        public BlobArray<ResourceDefinition> Resources;

        /// <summary>
        /// All processing recipes.
        /// </summary>
        public BlobArray<ResourceRecipe> Recipes;
    }

    /// <summary>
    /// Reference to the resource chain catalog blob asset.
    /// </summary>
    public struct ResourceChainCatalog : IComponentData
    {
        /// <summary>
        /// Reference to the blob asset.
        /// </summary>
        public BlobAssetReference<ResourceChainCatalogBlob> BlobReference;
    }

    /// <summary>
    /// Active processing job on a facility.
    /// </summary>
    public struct ProcessingJob : IComponentData
    {
        /// <summary>
        /// Recipe being processed.
        /// </summary>
        public FixedString32Bytes RecipeId;

        /// <summary>
        /// Progress [0, 1].
        /// </summary>
        public half Progress;

        /// <summary>
        /// Remaining time in seconds.
        /// </summary>
        public float RemainingTime;

        /// <summary>
        /// Tick when processing started.
        /// </summary>
        public uint StartTick;

        /// <summary>
        /// Number of batches queued.
        /// </summary>
        public int BatchesQueued;

        /// <summary>
        /// Number of batches completed.
        /// </summary>
        public int BatchesCompleted;
    }

    /// <summary>
    /// Buffer of queued processing jobs.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ProcessingQueueEntry : IBufferElementData
    {
        /// <summary>
        /// Recipe to process.
        /// </summary>
        public FixedString32Bytes RecipeId;

        /// <summary>
        /// Number of batches to process.
        /// </summary>
        public int BatchCount;

        /// <summary>
        /// Priority (lower = higher priority).
        /// </summary>
        public byte Priority;

        /// <summary>
        /// Tick when added to queue.
        /// </summary>
        public uint QueuedTick;
    }

    /// <summary>
    /// Processing facility component.
    /// </summary>
    public struct ProcessingFacility : IComponentData
    {
        /// <summary>
        /// Facility tier (1-3).
        /// </summary>
        public byte Tier;

        /// <summary>
        /// Processing speed multiplier.
        /// </summary>
        public half SpeedMultiplier;

        /// <summary>
        /// Energy efficiency multiplier (lower = less energy used).
        /// </summary>
        public half EnergyEfficiency;

        /// <summary>
        /// Whether facility is currently active.
        /// </summary>
        public byte IsActive;

        /// <summary>
        /// Last tick when facility processed.
        /// </summary>
        public uint LastProcessTick;

        public static ProcessingFacility Tier1 => new ProcessingFacility
        {
            Tier = 1,
            SpeedMultiplier = (half)1f,
            EnergyEfficiency = (half)1f,
            IsActive = 1,
            LastProcessTick = 0
        };

        public static ProcessingFacility Tier2 => new ProcessingFacility
        {
            Tier = 2,
            SpeedMultiplier = (half)1.5f,
            EnergyEfficiency = (half)0.9f,
            IsActive = 1,
            LastProcessTick = 0
        };

        public static ProcessingFacility Tier3 => new ProcessingFacility
        {
            Tier = 3,
            SpeedMultiplier = (half)2f,
            EnergyEfficiency = (half)0.75f,
            IsActive = 1,
            LastProcessTick = 0
        };
    }

    /// <summary>
    /// Standard resource definitions.
    /// </summary>
    public static class StandardResources
    {
        // Raw resources
        public static ResourceDefinition IronOre => ResourceDefinition.Create("iron_ore", "Iron Ore", ResourceFamily.Metals, ProcessingTier.Raw, 1f, 2f);
        public static ResourceDefinition TitaniumOre => ResourceDefinition.Create("titanium_ore", "Titanium Ore", ResourceFamily.AdvancedMetals, ProcessingTier.Raw, 3f, 2.5f);
        public static ResourceDefinition Biomass => ResourceDefinition.Create("biomass", "Biomass", ResourceFamily.Organics, ProcessingTier.Raw, 0.5f, 0.5f);
        public static ResourceDefinition HydrocarbonIce => ResourceDefinition.Create("hydrocarbon_ice", "Hydrocarbon Ice", ResourceFamily.Petrochemicals, ProcessingTier.Raw, 1.5f, 1f);
        public static ResourceDefinition RareEarths => ResourceDefinition.Create("rare_earths", "Rare Earths", ResourceFamily.Electronics, ProcessingTier.Raw, 5f, 1.5f);
        public static ResourceDefinition Carbon => ResourceDefinition.Create("carbon", "Carbon", ResourceFamily.Metals, ProcessingTier.Raw, 0.8f, 0.8f);

        // Refined resources
        public static ResourceDefinition IronIngots => ResourceDefinition.Create("iron_ingots", "Iron Ingots", ResourceFamily.Metals, ProcessingTier.Refined, 2f, 1.8f);
        public static ResourceDefinition TitaniumIngots => ResourceDefinition.Create("titanium_ingots", "Titanium Ingots", ResourceFamily.AdvancedMetals, ProcessingTier.Refined, 8f, 2f);
        public static ResourceDefinition Nutrients => ResourceDefinition.Create("nutrients", "Nutrients", ResourceFamily.Organics, ProcessingTier.Refined, 1.5f, 0.3f);
        public static ResourceDefinition RefinedFuels => ResourceDefinition.Create("refined_fuels", "Refined Fuels", ResourceFamily.Petrochemicals, ProcessingTier.Refined, 4f, 0.8f);
        public static ResourceDefinition Conductors => ResourceDefinition.Create("conductors", "Conductors", ResourceFamily.Electronics, ProcessingTier.Refined, 12f, 0.5f);

        // Composite resources
        public static ResourceDefinition Steel => ResourceDefinition.Create("steel", "Steel", ResourceFamily.Metals, ProcessingTier.Composite, 5f, 1.5f);
        public static ResourceDefinition Polymers => ResourceDefinition.Create("polymers", "Polymers", ResourceFamily.Petrochemicals, ProcessingTier.Composite, 6f, 0.4f);
        public static ResourceDefinition Biopolymers => ResourceDefinition.Create("biopolymers", "Biopolymers", ResourceFamily.Organics, ProcessingTier.Composite, 8f, 0.3f);

        // Advanced resources
        public static ResourceDefinition Plasteel => ResourceDefinition.Create("plasteel", "Plasteel", ResourceFamily.AdvancedMetals, ProcessingTier.Advanced, 20f, 1.2f);
        public static ResourceDefinition QuantumCores => ResourceDefinition.Create("quantum_cores", "Quantum Cores", ResourceFamily.Electronics, ProcessingTier.Advanced, 50f, 0.2f);
        public static ResourceDefinition CompositeAlloys => ResourceDefinition.Create("composite_alloys", "Composite Alloys", ResourceFamily.AdvancedMetals, ProcessingTier.Advanced, 40f, 1f);
    }

    /// <summary>
    /// Standard processing recipes.
    /// </summary>
    public static class StandardRecipes
    {
        // Basic refining
        public static ResourceRecipe RefineIron => ResourceRecipe.CreateSimple("refine_iron", "Refine Iron", "iron_ore", 2f, "iron_ingots", 1f, 5f, 10f);
        public static ResourceRecipe RefineTitanium => ResourceRecipe.CreateSimple("refine_titanium", "Refine Titanium", "titanium_ore", 2f, "titanium_ingots", 1f, 8f, 20f);
        public static ResourceRecipe ProcessBiomass => ResourceRecipe.CreateSimple("process_biomass", "Process Biomass", "biomass", 3f, "nutrients", 1f, 4f, 5f);
        public static ResourceRecipe RefineHydrocarbons => ResourceRecipe.CreateSimple("refine_hydrocarbons", "Refine Hydrocarbons", "hydrocarbon_ice", 2f, "refined_fuels", 1f, 6f, 15f);
        public static ResourceRecipe ExtractConductors => ResourceRecipe.CreateSimple("extract_conductors", "Extract Conductors", "rare_earths", 2f, "conductors", 1f, 10f, 25f);

        // Combination recipes
        public static ResourceRecipe SmeltSteel => ResourceRecipe.CreateCombination("smelt_steel", "Smelt Steel", "iron_ingots", 2f, "carbon", 1f, "steel", 1f, 8f, 20f, 1);
        public static ResourceRecipe SynthesizePolymers => ResourceRecipe.CreateCombination("synthesize_polymers", "Synthesize Polymers", "refined_fuels", 2f, "nutrients", 1f, "polymers", 1f, 10f, 15f, 1);
        public static ResourceRecipe CreateBiopolymers => ResourceRecipe.CreateCombination("create_biopolymers", "Create Biopolymers", "nutrients", 2f, "polymers", 1f, "biopolymers", 1f, 12f, 20f, 2);

        // Advanced recipes
        public static ResourceRecipe ForgePlasteel => ResourceRecipe.CreateCombination("forge_plasteel", "Forge Plasteel", "steel", 2f, "polymers", 1f, "plasteel", 1f, 15f, 40f, 2);
        public static ResourceRecipe AssembleQuantumCores => ResourceRecipe.CreateCombination("assemble_quantum_cores", "Assemble Quantum Cores", "conductors", 3f, "rare_earths", 2f, "quantum_cores", 1f, 20f, 60f, 3);
        public static ResourceRecipe CreateCompositeAlloys => ResourceRecipe.CreateCombination("create_composite_alloys", "Create Composite Alloys", "plasteel", 2f, "quantum_cores", 1f, "composite_alloys", 1f, 25f, 80f, 3);
    }
}

