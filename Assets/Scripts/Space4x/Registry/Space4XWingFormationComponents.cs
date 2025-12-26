using PureDOTS.Runtime.Formation;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    public struct WingFormationDefaults
    {
        public FormationType TightFormation;
        public FormationType LooseFormation;
        public float TightSpacing;
        public float LooseSpacing;
        public float TightCohesion;
        public float LooseCohesion;
        public float TightFacingWeight;
        public float LooseFacingWeight;
        public byte MaxSplitGroups;
        public float SplitArcDegrees;
        public float SplitRadius;
        public float AckSuccessThreshold;
        public float DisciplineRequired;
        public byte RequireAckForTighten;
        public byte RequireAckForFlank;
    }

    public struct WingFormationConfig : IComponentData
    {
        public WingFormationDefaults StrikeDefaults;
        public WingFormationDefaults MiningDefaults;

        public static WingFormationDefaults DefaultStrike => new WingFormationDefaults
        {
            TightFormation = FormationType.Wedge,
            LooseFormation = FormationType.Skirmish,
            TightSpacing = 6f,
            LooseSpacing = 18f,
            TightCohesion = 0.85f,
            LooseCohesion = 0.4f,
            TightFacingWeight = 0.9f,
            LooseFacingWeight = 0.35f,
            MaxSplitGroups = 2,
            SplitArcDegrees = 120f,
            SplitRadius = 35f,
            AckSuccessThreshold = 0.6f,
            DisciplineRequired = 0.55f,
            RequireAckForTighten = 1,
            RequireAckForFlank = 1
        };

        public static WingFormationDefaults DefaultMining => new WingFormationDefaults
        {
            TightFormation = FormationType.Column,
            LooseFormation = FormationType.Scatter,
            TightSpacing = 10f,
            LooseSpacing = 24f,
            TightCohesion = 0.75f,
            LooseCohesion = 0.35f,
            TightFacingWeight = 0.7f,
            LooseFacingWeight = 0.25f,
            MaxSplitGroups = 2,
            SplitArcDegrees = 90f,
            SplitRadius = 45f,
            AckSuccessThreshold = 0.5f,
            DisciplineRequired = 0.45f,
            RequireAckForTighten = 1,
            RequireAckForFlank = 0
        };

        public static WingFormationConfig Default => new WingFormationConfig
        {
            StrikeDefaults = DefaultStrike,
            MiningDefaults = DefaultMining
        };
    }

    public struct WingFormationState : IComponentData
    {
        public uint LastDecisionTick;
        public byte LastTacticKind;
        public byte SplitCount;
        public float LastAckRatio;
    }

    [InternalBufferCapacity(4)]
    public struct WingFormationAnchorRef : IBufferElementData
    {
        public Entity Anchor;
    }

    public struct WingFormationAnchor : IComponentData
    {
        public Entity WingGroup;
        public byte AnchorIndex;
        public byte AnchorCount;
    }
}
