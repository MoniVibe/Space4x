using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(menuName = "PureDOTS/Aggregate Behavior Profile", fileName = "AggregateBehaviorProfile")]
    public class AggregateBehaviorProfileAsset : ScriptableObject
    {
        [Header("Cadence")]
        [Tooltip("Ticks between initiative checks (bi-daily by default).")]
        public uint initiativeIntervalTicks = 240u;
        [Tooltip("Random jitter applied to initiative interval to avoid synchronization.")]
        public uint initiativeJitterTicks = 12u;

        [Header("Collective vs Individual Weights")]
        [Range(0f, 5f)] public float collectiveNeedWeight = 1f;
        [Range(0f, 5f)] public float personalAmbitionWeight = 1f;
        [Range(0f, 5f)] public float emergencyOverrideWeight = 2f;

        [Header("Alignment / Outlook Multipliers")]
        [Tooltip("Multipliers applied based on village lawfulness (L axis). Higher values force compliance.")]
        public AnimationCurve lawfulnessComplianceCurve = AnimationCurve.Linear(-1f, 0.5f, 1f, 1.5f);
        [Tooltip("Multipliers applied based on village chaos/materialism (C axis).")]
        public AnimationCurve chaosFreedomCurve = AnimationCurve.Linear(-1f, 0.5f, 1f, 1.5f);

        [Header("Discipline Preferences")]
        [Tooltip("Additional weight applied when a villager switches away from their discipline.")]
        [Range(0f, 5f)] public float disciplineResistanceWeight = 1.25f;
        [Tooltip("Minimum shortage severity required before forcing cross-discipline reassignment.")]
        [Range(0f, 1f)] public float shortageThreshold = 0.35f;

        [Header("Emergency Flags")]
        public bool allowConscriptionOverrides = true;
        [Range(0f, 5f)] public float conscriptionWeight = 3f;
        [Range(0f, 5f)] public float defenseEmergencyWeight = 2.5f;

        public AggregateBehaviorProfileBlob.BuildData ToBuildData()
        {
            return new AggregateBehaviorProfileBlob.BuildData
            {
                InitiativeIntervalTicks = initiativeIntervalTicks,
                InitiativeJitterTicks = initiativeJitterTicks,
                CollectiveNeedWeight = collectiveNeedWeight,
                PersonalAmbitionWeight = personalAmbitionWeight,
                EmergencyOverrideWeight = emergencyOverrideWeight,
                LawfulnessComplianceCurve = lawfulnessComplianceCurve,
                ChaosFreedomCurve = chaosFreedomCurve,
                DisciplineResistanceWeight = disciplineResistanceWeight,
                ShortageThreshold = shortageThreshold,
                AllowConscriptionOverrides = allowConscriptionOverrides,
                ConscriptionWeight = conscriptionWeight,
                DefenseEmergencyWeight = defenseEmergencyWeight
            };
        }
    }
}
