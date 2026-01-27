using PureDOTS.Runtime.Shared;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Shared
{
    /// <summary>
    /// Shared system that calculates InstanceQuality for items/modules.
    /// Runs in FixedStep simulation group and is used by both Godgame and Space4X.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct InstanceQualityCalculationSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Get formula blob singleton
            if (!SystemAPI.TryGetSingleton<QualityFormulaBlobRef>(out var formulaRef) ||
                !formulaRef.Blob.IsCreated)
            {
                return; // No formula configured yet
            }

            new CalculateInstanceQualityJob { Formula = formulaRef.Blob }.ScheduleParallel();
        }

        [BurstCompile]
        public partial struct CalculateInstanceQualityJob : IJobEntity
        {
            [ReadOnly]
            public BlobAssetReference<QualityFormulaBlob> Formula;

            void Execute(
                Entity entity,
                ref InstanceQuality quality,
                in QualityInputs inputs)
            {
                // Calculate Score01 from inputs
                ref var formula = ref Formula.Value;
                float score01 = QualityEval.Score01(
                    ref formula,
                    inputs.Purity01,
                    inputs.Skill01,
                    inputs.Station01,
                    inputs.Recipe01);

                // Determine tier from cutoffs
                QualityTier tier = QualityEval.Tier(ref formula, score01);

                // Compute provenance hash
                uint provenanceHash = QualityEval.ComputeProvenanceHash(
                    inputs.MaterialIdHash,
                    inputs.Purity01,
                    inputs.CrafterIdHash,
                    inputs.Skill01,
                    inputs.StationIdHash,
                    inputs.Station01,
                    inputs.RecipeIdHash,
                    inputs.Recipe01);

                // Update instance quality
                quality.Score01 = score01;
                quality.Tier = tier;
                quality.ProvenanceHash = provenanceHash;
                // Flags are set separately (e.g., by special crafting events)
            }
        }
    }

    /// <summary>
    /// Input component for quality calculation. Attach to items/modules that need quality computed.
    /// </summary>
    public struct QualityInputs : IComponentData
    {
        /// <summary>
        /// Material purity (0-1).
        /// </summary>
        public float Purity01;

        /// <summary>
        /// Crafter/crew skill (0-1, normalized).
        /// </summary>
        public float Skill01;

        /// <summary>
        /// Station/workstation rating (0-1, normalized).
        /// </summary>
        public float Station01;

        /// <summary>
        /// Recipe difficulty influence (0-1, normalized).
        /// </summary>
        public float Recipe01;

        /// <summary>
        /// Material ID hash for provenance.
        /// </summary>
        public uint MaterialIdHash;

        /// <summary>
        /// Crafter ID hash for provenance.
        /// </summary>
        public uint CrafterIdHash;

        /// <summary>
        /// Station ID hash for provenance.
        /// </summary>
        public uint StationIdHash;

        /// <summary>
        /// Recipe ID hash for provenance.
        /// </summary>
        public uint RecipeIdHash;
    }

    /// <summary>
    /// Singleton reference to QualityFormulaBlob.
    /// </summary>
    public struct QualityFormulaBlobRef : IComponentData
    {
        public BlobAssetReference<QualityFormulaBlob> Blob;
    }

    /// <summary>
    /// Singleton reference to QualityCurveBlob.
    /// </summary>
    public struct QualityCurveBlobRef : IComponentData
    {
        public BlobAssetReference<QualityCurveBlob> Blob;
    }
}

