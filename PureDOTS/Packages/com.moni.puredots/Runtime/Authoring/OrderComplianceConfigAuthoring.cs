using PureDOTS.Runtime.Groups;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("PureDOTS/Orders/Order Compliance Config")]
    public sealed class OrderComplianceConfigAuthoring : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField, Range(0f, 1f)] private float tightenThreshold = 0.55f;
        [SerializeField, Range(0f, 1f)] private float loosenThreshold = 0.45f;
        [SerializeField, Range(0f, 1f)] private float flankThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float collapseThreshold = 0.58f;
        [SerializeField, Range(0f, 1f)] private float retreatThreshold = 0.5f;

        [Header("Claim Weights")]
        [SerializeField, Range(0f, 1f)] private float pressureWeight = 0.45f;
        [SerializeField, Range(0f, 1f)] private float legitimacyWeight = 0.2f;
        [SerializeField, Range(0f, 1f)] private float consentWeight = 0.2f;
        [SerializeField, Range(0f, 1f)] private float hostilityWeight = 0.25f;
        [SerializeField, Range(0f, 1f)] private float dispositionWeight = 0.35f;
        [SerializeField, Range(0f, 0.2f)] private float deterministicBias = 0.05f;

        [Header("Escalation")]
        [SerializeField, Range(0f, 1f)] private float escalationPressureBonus = 0.25f;
        [SerializeField, Range(0f, 1f)] private float escalationLegitimacyBonus = 0.15f;
        [SerializeField, Range(0f, 3f)] private float maxEscalationAttempts = 1f;
        [SerializeField, Range(0f, 300f)] private float escalationCooldownTicks = 30f;

        private void OnValidate()
        {
            tightenThreshold = math.clamp(tightenThreshold, 0f, 1f);
            loosenThreshold = math.clamp(loosenThreshold, 0f, 1f);
            flankThreshold = math.clamp(flankThreshold, 0f, 1f);
            collapseThreshold = math.clamp(collapseThreshold, 0f, 1f);
            retreatThreshold = math.clamp(retreatThreshold, 0f, 1f);
            pressureWeight = math.clamp(pressureWeight, 0f, 1f);
            legitimacyWeight = math.clamp(legitimacyWeight, 0f, 1f);
            consentWeight = math.clamp(consentWeight, 0f, 1f);
            hostilityWeight = math.clamp(hostilityWeight, 0f, 1f);
            dispositionWeight = math.clamp(dispositionWeight, 0f, 1f);
            deterministicBias = math.clamp(deterministicBias, 0f, 0.2f);
            escalationPressureBonus = math.clamp(escalationPressureBonus, 0f, 1f);
            escalationLegitimacyBonus = math.clamp(escalationLegitimacyBonus, 0f, 1f);
            maxEscalationAttempts = math.clamp(maxEscalationAttempts, 0f, 3f);
            escalationCooldownTicks = math.clamp(escalationCooldownTicks, 0f, 300f);
        }

        private sealed class Baker : Baker<OrderComplianceConfigAuthoring>
        {
            public override void Bake(OrderComplianceConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new OrderComplianceConfig
                {
                    TightenThreshold = math.clamp(authoring.tightenThreshold, 0f, 1f),
                    LoosenThreshold = math.clamp(authoring.loosenThreshold, 0f, 1f),
                    FlankThreshold = math.clamp(authoring.flankThreshold, 0f, 1f),
                    CollapseThreshold = math.clamp(authoring.collapseThreshold, 0f, 1f),
                    RetreatThreshold = math.clamp(authoring.retreatThreshold, 0f, 1f),
                    PressureWeight = math.clamp(authoring.pressureWeight, 0f, 1f),
                    LegitimacyWeight = math.clamp(authoring.legitimacyWeight, 0f, 1f),
                    ConsentWeight = math.clamp(authoring.consentWeight, 0f, 1f),
                    HostilityWeight = math.clamp(authoring.hostilityWeight, 0f, 1f),
                    DispositionWeight = math.clamp(authoring.dispositionWeight, 0f, 1f),
                    DeterministicBias = math.clamp(authoring.deterministicBias, 0f, 0.2f),
                    EscalationPressureBonus = math.clamp(authoring.escalationPressureBonus, 0f, 1f),
                    EscalationLegitimacyBonus = math.clamp(authoring.escalationLegitimacyBonus, 0f, 1f),
                    MaxEscalationAttempts = (byte)math.clamp(math.round(authoring.maxEscalationAttempts), 0f, 3f),
                    EscalationCooldownTicks = (uint)math.clamp(authoring.escalationCooldownTicks, 0f, 300f)
                });
            }
        }
    }
}
