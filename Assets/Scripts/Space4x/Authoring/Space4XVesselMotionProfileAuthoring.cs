using Space4X.Runtime;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Vessel Motion Profile Config")]
    public sealed class Space4XVesselMotionProfileAuthoring : MonoBehaviour
    {
        [Header("Deliberate / Economic")]
        [Range(0.2f, 1f)] public float deliberateSpeedMultiplier = 0.78f;
        [Range(0.2f, 1f)] public float deliberateTurnMultiplier = 0.65f;
        [Range(0.8f, 2f)] public float deliberateSlowdownMultiplier = 1.25f;
        [Range(0.2f, 1f)] public float economyAccelerationMultiplier = 0.7f;
        [Range(0.2f, 1.5f)] public float economyDecelerationMultiplier = 0.85f;

        [Header("Chaotic")]
        [Range(0.8f, 2f)] public float chaoticSpeedMultiplier = 1.2f;
        [Range(0.8f, 2f)] public float chaoticTurnMultiplier = 1.4f;
        [Range(0.8f, 2f)] public float chaoticAccelerationMultiplier = 1.3f;
        [Range(0.8f, 2f)] public float chaoticDecelerationMultiplier = 1.15f;
        [Range(0.5f, 1.5f)] public float chaoticSlowdownMultiplier = 0.8f;
        [Range(0f, 1f)] public float chaoticDeviationStrength = 0.35f;
        [Range(0f, 50f)] public float chaoticDeviationMinDistance = 6f;

        [Header("Intelligent")]
        [Range(0.8f, 1.5f)] public float intelligentTurnMultiplier = 1.15f;
        [Range(0.5f, 1.2f)] public float intelligentSlowdownMultiplier = 0.9f;

        [Header("Capital Ship Baseline")]
        [Range(0.3f, 1f)] public float capitalShipSpeedMultiplier = 0.85f;
        [Range(0.3f, 1f)] public float capitalShipTurnMultiplier = 0.8f;
        [Range(0.3f, 1f)] public float capitalShipAccelerationMultiplier = 0.75f;
        [Range(0.3f, 1f)] public float capitalShipDecelerationMultiplier = 0.85f;

        [Header("Mining Phase Speeds")]
        [Range(0.1f, 1f)] public float miningUndockSpeedMultiplier = 0.35f;
        [Range(0.1f, 1.5f)] public float miningApproachSpeedMultiplier = 0.8f;
        [Range(0.1f, 1f)] public float miningLatchSpeedMultiplier = 0.45f;
        [Range(0.1f, 1f)] public float miningDetachSpeedMultiplier = 0.55f;
        [Range(0.1f, 1.5f)] public float miningReturnSpeedMultiplier = 0.95f;
        [Range(0.1f, 1f)] public float miningDockSpeedMultiplier = 0.4f;

        [Header("Miner Risk / Fast Haul")]
        [Range(0.8f, 2f)] public float minerRiskSpeedMultiplier = 1.25f;
        [Range(0.8f, 2f)] public float minerRiskDeviationMultiplier = 1.4f;
        [Range(0.4f, 1f)] public float minerRiskSlowdownMultiplier = 0.8f;
        [Range(0.4f, 1f)] public float minerRiskArrivalMultiplier = 0.7f;

        [Header("Brake Lead")]
        [Tooltip("0 disables early braking.")]
        [Range(0f, 2f)] public float brakeLeadFactor = 0f;

        private void OnValidate()
        {
            deliberateSpeedMultiplier = math.clamp(deliberateSpeedMultiplier, 0.2f, 1f);
            deliberateTurnMultiplier = math.clamp(deliberateTurnMultiplier, 0.2f, 1f);
            deliberateSlowdownMultiplier = math.clamp(deliberateSlowdownMultiplier, 0.8f, 2f);
            economyAccelerationMultiplier = math.clamp(economyAccelerationMultiplier, 0.2f, 1f);
            economyDecelerationMultiplier = math.clamp(economyDecelerationMultiplier, 0.2f, 1.5f);
            chaoticSpeedMultiplier = math.clamp(chaoticSpeedMultiplier, 0.8f, 2f);
            chaoticTurnMultiplier = math.clamp(chaoticTurnMultiplier, 0.8f, 2f);
            chaoticAccelerationMultiplier = math.clamp(chaoticAccelerationMultiplier, 0.8f, 2f);
            chaoticDecelerationMultiplier = math.clamp(chaoticDecelerationMultiplier, 0.8f, 2f);
            chaoticSlowdownMultiplier = math.clamp(chaoticSlowdownMultiplier, 0.5f, 1.5f);
            chaoticDeviationStrength = math.clamp(chaoticDeviationStrength, 0f, 1f);
            chaoticDeviationMinDistance = math.max(0f, chaoticDeviationMinDistance);
            intelligentTurnMultiplier = math.clamp(intelligentTurnMultiplier, 0.8f, 1.5f);
            intelligentSlowdownMultiplier = math.clamp(intelligentSlowdownMultiplier, 0.5f, 1.2f);
            capitalShipSpeedMultiplier = math.clamp(capitalShipSpeedMultiplier, 0.3f, 1f);
            capitalShipTurnMultiplier = math.clamp(capitalShipTurnMultiplier, 0.3f, 1f);
            capitalShipAccelerationMultiplier = math.clamp(capitalShipAccelerationMultiplier, 0.3f, 1f);
            capitalShipDecelerationMultiplier = math.clamp(capitalShipDecelerationMultiplier, 0.3f, 1f);
            miningUndockSpeedMultiplier = math.clamp(miningUndockSpeedMultiplier, 0.1f, 1f);
            miningApproachSpeedMultiplier = math.clamp(miningApproachSpeedMultiplier, 0.1f, 1.5f);
            miningLatchSpeedMultiplier = math.clamp(miningLatchSpeedMultiplier, 0.1f, 1f);
            miningDetachSpeedMultiplier = math.clamp(miningDetachSpeedMultiplier, 0.1f, 1f);
            miningReturnSpeedMultiplier = math.clamp(miningReturnSpeedMultiplier, 0.1f, 1.5f);
            miningDockSpeedMultiplier = math.clamp(miningDockSpeedMultiplier, 0.1f, 1f);
            minerRiskSpeedMultiplier = math.clamp(minerRiskSpeedMultiplier, 0.8f, 2f);
            minerRiskDeviationMultiplier = math.clamp(minerRiskDeviationMultiplier, 0.8f, 2f);
            minerRiskSlowdownMultiplier = math.clamp(minerRiskSlowdownMultiplier, 0.4f, 1f);
            minerRiskArrivalMultiplier = math.clamp(minerRiskArrivalMultiplier, 0.4f, 1f);
            brakeLeadFactor = math.clamp(brakeLeadFactor, 0f, 2f);
        }

        private sealed class Baker : Baker<Space4XVesselMotionProfileAuthoring>
        {
            public override void Bake(Space4XVesselMotionProfileAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new VesselMotionProfileConfig
                {
                    DeliberateSpeedMultiplier = authoring.deliberateSpeedMultiplier,
                    DeliberateTurnMultiplier = authoring.deliberateTurnMultiplier,
                    DeliberateSlowdownMultiplier = authoring.deliberateSlowdownMultiplier,
                    EconomyAccelerationMultiplier = authoring.economyAccelerationMultiplier,
                    EconomyDecelerationMultiplier = authoring.economyDecelerationMultiplier,
                    ChaoticSpeedMultiplier = authoring.chaoticSpeedMultiplier,
                    ChaoticTurnMultiplier = authoring.chaoticTurnMultiplier,
                    ChaoticAccelerationMultiplier = authoring.chaoticAccelerationMultiplier,
                    ChaoticDecelerationMultiplier = authoring.chaoticDecelerationMultiplier,
                    ChaoticSlowdownMultiplier = authoring.chaoticSlowdownMultiplier,
                    ChaoticDeviationStrength = authoring.chaoticDeviationStrength,
                    ChaoticDeviationMinDistance = authoring.chaoticDeviationMinDistance,
                    IntelligentTurnMultiplier = authoring.intelligentTurnMultiplier,
                    IntelligentSlowdownMultiplier = authoring.intelligentSlowdownMultiplier,
                    CapitalShipSpeedMultiplier = authoring.capitalShipSpeedMultiplier,
                    CapitalShipTurnMultiplier = authoring.capitalShipTurnMultiplier,
                    CapitalShipAccelerationMultiplier = authoring.capitalShipAccelerationMultiplier,
                    CapitalShipDecelerationMultiplier = authoring.capitalShipDecelerationMultiplier,
                    MiningUndockSpeedMultiplier = authoring.miningUndockSpeedMultiplier,
                    MiningApproachSpeedMultiplier = authoring.miningApproachSpeedMultiplier,
                    MiningLatchSpeedMultiplier = authoring.miningLatchSpeedMultiplier,
                    MiningDetachSpeedMultiplier = authoring.miningDetachSpeedMultiplier,
                    MiningReturnSpeedMultiplier = authoring.miningReturnSpeedMultiplier,
                    MiningDockSpeedMultiplier = authoring.miningDockSpeedMultiplier,
                    MinerRiskSpeedMultiplier = authoring.minerRiskSpeedMultiplier,
                    MinerRiskDeviationMultiplier = authoring.minerRiskDeviationMultiplier,
                    MinerRiskSlowdownMultiplier = authoring.minerRiskSlowdownMultiplier,
                    MinerRiskArrivalMultiplier = authoring.minerRiskArrivalMultiplier,
                    BrakeLeadFactor = authoring.brakeLeadFactor
                });
            }
        }
    }
}
