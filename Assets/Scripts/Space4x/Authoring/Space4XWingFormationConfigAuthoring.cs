using PureDOTS.Runtime.Formation;
using Space4X.Registry;
using Unity.Entities;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Wing Formation Config")]
    public sealed class Space4XWingFormationConfigAuthoring : MonoBehaviour
    {
        [Header("Strike Wing Formations")]
        public FormationType strikeTightFormation = FormationType.Wedge;
        public FormationType strikeLooseFormation = FormationType.Skirmish;
        [Min(0.5f)] public float strikeTightSpacing = 6f;
        [Min(0.5f)] public float strikeLooseSpacing = 18f;
        [Range(0f, 1f)] public float strikeTightCohesion = 0.85f;
        [Range(0f, 1f)] public float strikeLooseCohesion = 0.4f;
        [Range(0f, 1f)] public float strikeTightFacingWeight = 0.9f;
        [Range(0f, 1f)] public float strikeLooseFacingWeight = 0.35f;
        [Range(1f, 8f)] public float strikeMaxSplitGroups = 2f;
        [Range(0f, 180f)] public float strikeSplitArcDegrees = 120f;
        [Min(0f)] public float strikeSplitRadius = 35f;
        [Range(0f, 1f)] public float strikeAckSuccessThreshold = 0.6f;
        [Range(0f, 1f)] public float strikeDisciplineRequired = 0.55f;
        public bool strikeRequireAckForTighten = true;
        public bool strikeRequireAckForFlank = true;

        [Header("Mining Wing Formations")]
        public FormationType miningTightFormation = FormationType.Column;
        public FormationType miningLooseFormation = FormationType.Scatter;
        [Min(0.5f)] public float miningTightSpacing = 10f;
        [Min(0.5f)] public float miningLooseSpacing = 24f;
        [Range(0f, 1f)] public float miningTightCohesion = 0.75f;
        [Range(0f, 1f)] public float miningLooseCohesion = 0.35f;
        [Range(0f, 1f)] public float miningTightFacingWeight = 0.7f;
        [Range(0f, 1f)] public float miningLooseFacingWeight = 0.25f;
        [Range(1f, 8f)] public float miningMaxSplitGroups = 2f;
        [Range(0f, 180f)] public float miningSplitArcDegrees = 90f;
        [Min(0f)] public float miningSplitRadius = 45f;
        [Range(0f, 1f)] public float miningAckSuccessThreshold = 0.5f;
        [Range(0f, 1f)] public float miningDisciplineRequired = 0.45f;
        public bool miningRequireAckForTighten = true;
        public bool miningRequireAckForFlank = false;

        public sealed class Baker : Baker<Space4XWingFormationConfigAuthoring>
        {
            public override void Bake(Space4XWingFormationConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WingFormationConfig
                {
                    StrikeDefaults = new WingFormationDefaults
                    {
                        TightFormation = authoring.strikeTightFormation,
                        LooseFormation = authoring.strikeLooseFormation,
                        TightSpacing = Mathf.Max(0.5f, authoring.strikeTightSpacing),
                        LooseSpacing = Mathf.Max(0.5f, authoring.strikeLooseSpacing),
                        TightCohesion = Mathf.Clamp01(authoring.strikeTightCohesion),
                        LooseCohesion = Mathf.Clamp01(authoring.strikeLooseCohesion),
                        TightFacingWeight = Mathf.Clamp01(authoring.strikeTightFacingWeight),
                        LooseFacingWeight = Mathf.Clamp01(authoring.strikeLooseFacingWeight),
                        MaxSplitGroups = (byte)Mathf.Clamp(Mathf.RoundToInt(authoring.strikeMaxSplitGroups), 1, 8),
                        SplitArcDegrees = Mathf.Clamp(authoring.strikeSplitArcDegrees, 0f, 180f),
                        SplitRadius = Mathf.Max(0f, authoring.strikeSplitRadius),
                        AckSuccessThreshold = Mathf.Clamp01(authoring.strikeAckSuccessThreshold),
                        DisciplineRequired = Mathf.Clamp01(authoring.strikeDisciplineRequired),
                        RequireAckForTighten = (byte)(authoring.strikeRequireAckForTighten ? 1 : 0),
                        RequireAckForFlank = (byte)(authoring.strikeRequireAckForFlank ? 1 : 0)
                    },
                    MiningDefaults = new WingFormationDefaults
                    {
                        TightFormation = authoring.miningTightFormation,
                        LooseFormation = authoring.miningLooseFormation,
                        TightSpacing = Mathf.Max(0.5f, authoring.miningTightSpacing),
                        LooseSpacing = Mathf.Max(0.5f, authoring.miningLooseSpacing),
                        TightCohesion = Mathf.Clamp01(authoring.miningTightCohesion),
                        LooseCohesion = Mathf.Clamp01(authoring.miningLooseCohesion),
                        TightFacingWeight = Mathf.Clamp01(authoring.miningTightFacingWeight),
                        LooseFacingWeight = Mathf.Clamp01(authoring.miningLooseFacingWeight),
                        MaxSplitGroups = (byte)Mathf.Clamp(Mathf.RoundToInt(authoring.miningMaxSplitGroups), 1, 8),
                        SplitArcDegrees = Mathf.Clamp(authoring.miningSplitArcDegrees, 0f, 180f),
                        SplitRadius = Mathf.Max(0f, authoring.miningSplitRadius),
                        AckSuccessThreshold = Mathf.Clamp01(authoring.miningAckSuccessThreshold),
                        DisciplineRequired = Mathf.Clamp01(authoring.miningDisciplineRequired),
                        RequireAckForTighten = (byte)(authoring.miningRequireAckForTighten ? 1 : 0),
                        RequireAckForFlank = (byte)(authoring.miningRequireAckForFlank ? 1 : 0)
                    }
                });
            }
        }
    }
}
