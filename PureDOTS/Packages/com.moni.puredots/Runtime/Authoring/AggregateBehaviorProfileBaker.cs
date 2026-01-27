using PureDOTS.Runtime.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring
{
    public class AggregateBehaviorProfileAuthoring : MonoBehaviour
    {
        [Tooltip("Optional ScriptableObject source. If assigned, fields are pulled from the asset.")]
        public AggregateBehaviorProfileAsset profileAsset;

        [Header("Cadence")]
        public uint initiativeIntervalTicks = 240u;
        public uint initiativeJitterTicks = 12u;

        [Header("Collective vs Individual Weights")]
        public float collectiveNeedWeight = 1f;
        public float personalAmbitionWeight = 1f;
        public float emergencyOverrideWeight = 2f;

        [Header("Alignment / Outlook Multipliers")]
        public AnimationCurve lawfulnessComplianceCurve = AnimationCurve.Linear(-1f, 0.5f, 1f, 1.5f);
        public AnimationCurve chaosFreedomCurve = AnimationCurve.Linear(-1f, 0.5f, 1f, 1.5f);

        [Header("Discipline Preferences")]
        public float disciplineResistanceWeight = 1.25f;
        public float shortageThreshold = 0.35f;

        [Header("Emergency Flags")]
        public bool allowConscriptionOverrides = true;
        public float conscriptionWeight = 3f;
        public float defenseEmergencyWeight = 2.5f;

        public AggregateBehaviorProfileBlob.BuildData ToBuildData()
        {
            if (profileAsset != null)
            {
                return profileAsset.ToBuildData();
            }

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

    public class AggregateBehaviorProfileBaker : Baker<AggregateBehaviorProfileAuthoring>
    {
        public override void Bake(AggregateBehaviorProfileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            var buildData = authoring.ToBuildData();
            var blob = BuildBlob(buildData);
            AddComponent(entity, new AggregateBehaviorProfile { Blob = blob });
        }

        private static BlobAssetReference<AggregateBehaviorProfileBlob> BuildBlob(AggregateBehaviorProfileBlob.BuildData data)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var profile = ref builder.ConstructRoot<AggregateBehaviorProfileBlob>();

            profile.CollectiveNeedWeight = data.CollectiveNeedWeight;
            profile.PersonalAmbitionWeight = data.PersonalAmbitionWeight;
            profile.EmergencyOverrideWeight = data.EmergencyOverrideWeight;
            profile.DisciplineResistanceWeight = data.DisciplineResistanceWeight;
            profile.ShortageThreshold = data.ShortageThreshold;
            profile.ConscriptionWeight = data.ConscriptionWeight;
            profile.DefenseEmergencyWeight = data.DefenseEmergencyWeight;
            profile.InitiativeIntervalTicks = data.InitiativeIntervalTicks;
            profile.InitiativeJitterTicks = data.InitiativeJitterTicks;
            profile.AllowConscriptionOverrides = data.AllowConscriptionOverrides;

            var lawKeys = data.LawfulnessComplianceCurve != null && data.LawfulnessComplianceCurve.length > 0
                ? data.LawfulnessComplianceCurve.keys
                : new[] { new Keyframe(-1f, 1f), new Keyframe(1f, 1f) };
            var lawArray = builder.Allocate(ref profile.LawfulnessComplianceCurve.Keys, lawKeys.Length);
            for (int i = 0; i < lawKeys.Length; i++)
            {
                lawArray[i] = new float2(lawKeys[i].time, lawKeys[i].value);
            }

            var chaosKeys = data.ChaosFreedomCurve != null && data.ChaosFreedomCurve.length > 0
                ? data.ChaosFreedomCurve.keys
                : new[] { new Keyframe(-1f, 1f), new Keyframe(1f, 1f) };
            var chaosArray = builder.Allocate(ref profile.ChaosFreedomCurve.Keys, chaosKeys.Length);
            for (int i = 0; i < chaosKeys.Length; i++)
            {
                chaosArray[i] = new float2(chaosKeys[i].time, chaosKeys[i].value);
            }

            var blobRef = builder.CreateBlobAssetReference<AggregateBehaviorProfileBlob>(Allocator.Persistent);
            builder.Dispose();
            return blobRef;
        }
    }
}
