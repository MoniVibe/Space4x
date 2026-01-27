using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Runtime.Components
{
    public struct AggregateBehaviorProfile : IComponentData
    {
        public BlobAssetReference<AggregateBehaviorProfileBlob> Blob;
    }

    public struct AggregateBehaviorProfileBlob
    {
        public float CollectiveNeedWeight;
        public float PersonalAmbitionWeight;
        public float EmergencyOverrideWeight;
        public float DisciplineResistanceWeight;
        public float ShortageThreshold;
        public float ConscriptionWeight;
        public float DefenseEmergencyWeight;
        public uint InitiativeIntervalTicks;
        public uint InitiativeJitterTicks;
        public bool AllowConscriptionOverrides;
        public AggregateCurve LawfulnessComplianceCurve;
        public AggregateCurve ChaosFreedomCurve;

        public struct BuildData
        {
            public float CollectiveNeedWeight;
            public float PersonalAmbitionWeight;
            public float EmergencyOverrideWeight;
            public float DisciplineResistanceWeight;
            public float ShortageThreshold;
            public float ConscriptionWeight;
            public float DefenseEmergencyWeight;
            public uint InitiativeIntervalTicks;
            public uint InitiativeJitterTicks;
            public bool AllowConscriptionOverrides;
            public AnimationCurve LawfulnessComplianceCurve;
            public AnimationCurve ChaosFreedomCurve;
        }
    }

    public struct AggregateCurve
    {
        public BlobArray<float2> Keys;

        public float Evaluate(float x)
        {
            if (Keys.Length == 0)
            {
                return 1f;
            }

            if (x <= Keys[0].x)
            {
                return Keys[0].y;
            }

            if (x >= Keys[Keys.Length - 1].x)
            {
                return Keys[Keys.Length - 1].y;
            }

            for (int i = 0; i < Keys.Length - 1; i++)
            {
                var a = Keys[i];
                var b = Keys[i + 1];
                if (x >= a.x && x <= b.x)
                {
                    var t = math.saturate((x - a.x) / math.max(1e-5f, b.x - a.x));
                    return math.lerp(a.y, b.y, t);
                }
            }

            return Keys[Keys.Length - 1].y;
        }
    }
}
