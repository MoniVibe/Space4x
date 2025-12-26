using PureDOTS.Runtime.Profile;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Profile Mutation Config")]
    public sealed class Space4XProfileMutationConfigAuthoring : MonoBehaviour
    {
        [Header("Drift Scales")]
        [SerializeField, Range(0f, 1f)] private float alignmentScale = 0.08f;
        [SerializeField, Range(0f, 1f)] private float outlookScale = 0.1f;
        [Header("Per-Apply Caps")]
        [SerializeField, Range(0f, 1f)] private float alignmentMaxDelta = 0.12f;
        [SerializeField, Range(0f, 1f)] private float outlookMaxDelta = 0.15f;
        [Header("Accumulator")]
        [SerializeField, Range(0.5f, 1f)] private float accumulatorDecay = 0.85f;
        [SerializeField, Range(1f, 600f)] private float applyIntervalTicks = 30f;
        [Header("Context Multipliers")]
        [SerializeField, Range(0f, 1f)] private float coercedMultiplier = 0.35f;
        [SerializeField, Range(0f, 1f)] private float justifiedMultiplier = 0.5f;
        [SerializeField, Range(0.5f, 2f)] private float maliceMultiplier = 1.1f;
        [SerializeField, Range(0.5f, 2f)] private float benevolenceMultiplier = 0.9f;

        private void OnValidate()
        {
            alignmentScale = math.clamp(alignmentScale, 0f, 1f);
            outlookScale = math.clamp(outlookScale, 0f, 1f);
            alignmentMaxDelta = math.clamp(alignmentMaxDelta, 0f, 1f);
            outlookMaxDelta = math.clamp(outlookMaxDelta, 0f, 1f);
            accumulatorDecay = math.clamp(accumulatorDecay, 0.5f, 1f);
            applyIntervalTicks = math.clamp(applyIntervalTicks, 1f, 600f);
            coercedMultiplier = math.clamp(coercedMultiplier, 0f, 1f);
            justifiedMultiplier = math.clamp(justifiedMultiplier, 0f, 1f);
            maliceMultiplier = math.clamp(maliceMultiplier, 0.5f, 2f);
            benevolenceMultiplier = math.clamp(benevolenceMultiplier, 0.5f, 2f);
        }

        private sealed class Baker : Baker<Space4XProfileMutationConfigAuthoring>
        {
            public override void Bake(Space4XProfileMutationConfigAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ProfileMutationConfig
                {
                    AlignmentScale = math.clamp(authoring.alignmentScale, 0f, 1f),
                    OutlookScale = math.clamp(authoring.outlookScale, 0f, 1f),
                    AlignmentMaxDelta = math.clamp(authoring.alignmentMaxDelta, 0f, 1f),
                    OutlookMaxDelta = math.clamp(authoring.outlookMaxDelta, 0f, 1f),
                    AccumulatorDecay = math.clamp(authoring.accumulatorDecay, 0.5f, 1f),
                    ApplyIntervalTicks = (uint)math.max(1f, authoring.applyIntervalTicks),
                    CoercedMultiplier = math.clamp(authoring.coercedMultiplier, 0f, 1f),
                    JustifiedMultiplier = math.clamp(authoring.justifiedMultiplier, 0f, 1f),
                    MaliceMultiplier = math.clamp(authoring.maliceMultiplier, 0.5f, 2f),
                    BenevolenceMultiplier = math.clamp(authoring.benevolenceMultiplier, 0.5f, 2f)
                });
            }
        }
    }
}
