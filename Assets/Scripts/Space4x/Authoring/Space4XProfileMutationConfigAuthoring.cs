using PureDOTS.Runtime.Profile;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace Space4X.Authoring
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Profile Mutation Config")]
    public sealed class Space4XProfileMutationConfigAuthoring : MonoBehaviour
    {
        [Header("Drift Scales")]
        [SerializeField, Range(0f, 1f)] private float alignmentScale = 0.08f;
        [FormerlySerializedAs("outlookScale")]
        [SerializeField, Range(0f, 1f)] private float stanceScale = 0.1f;
        [SerializeField, Range(0f, 1f)] private float dispositionScale = 0.07f;
        [Header("Per-Apply Caps")]
        [SerializeField, Range(0f, 1f)] private float alignmentMaxDelta = 0.12f;
        [FormerlySerializedAs("outlookMaxDelta")]
        [SerializeField, Range(0f, 1f)] private float stanceMaxDelta = 0.15f;
        [SerializeField, Range(0f, 1f)] private float dispositionMaxDelta = 0.1f;
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
            stanceScale = math.clamp(stanceScale, 0f, 1f);
            dispositionScale = math.clamp(dispositionScale, 0f, 1f);
            alignmentMaxDelta = math.clamp(alignmentMaxDelta, 0f, 1f);
            stanceMaxDelta = math.clamp(stanceMaxDelta, 0f, 1f);
            dispositionMaxDelta = math.clamp(dispositionMaxDelta, 0f, 1f);
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
                    StanceScale = math.clamp(authoring.stanceScale, 0f, 1f),
                    DispositionScale = math.clamp(authoring.dispositionScale, 0f, 1f),
                    AlignmentMaxDelta = math.clamp(authoring.alignmentMaxDelta, 0f, 1f),
                    StanceMaxDelta = math.clamp(authoring.stanceMaxDelta, 0f, 1f),
                    DispositionMaxDelta = math.clamp(authoring.dispositionMaxDelta, 0f, 1f),
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
