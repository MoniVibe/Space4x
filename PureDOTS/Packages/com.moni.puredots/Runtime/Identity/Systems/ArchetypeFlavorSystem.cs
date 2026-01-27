using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Identity
{
    using IndividualAlignmentTriplet = PureDOTS.Runtime.Individual.AlignmentTriplet;
    using IndividualBehaviorTuning = PureDOTS.Runtime.Individual.BehaviorTuning;
    using IndividualMightMagicAlignment = PureDOTS.Runtime.Individual.MightMagicAlignment;
    using IndividualPersonalityAxes = PureDOTS.Runtime.Individual.PersonalityAxes;

    /// <summary>
    /// Derives archetype flavor vectors from identity/outlook/personality inputs.
    /// Values remain semantic only; game-level systems map them to visuals.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(AggregateIdentitySystem))]
    public partial struct ArchetypeFlavorSystem : ISystem
    {
        private ComponentLookup<EntityOutlook> _outlookLookup;
        private ComponentLookup<PersonalityAxes> _identityPersonalityLookup;
        private ComponentLookup<MightMagicAffinity> _affinityLookup;
        private ComponentLookup<AggregateOutlook> _aggregateOutlookLookup;
        private ComponentLookup<GroupPersona> _aggregatePersonaLookup;
        private ComponentLookup<AggregatePowerProfile> _aggregatePowerLookup;
        private ComponentLookup<IndividualPersonalityAxes> _individualPersonalityLookup;
        private ComponentLookup<IndividualBehaviorTuning> _behaviorTuningLookup;
        private ComponentLookup<IndividualMightMagicAlignment> _mightMagicAlignmentLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _outlookLookup = state.GetComponentLookup<EntityOutlook>(true);
            _identityPersonalityLookup = state.GetComponentLookup<PersonalityAxes>(true);
            _affinityLookup = state.GetComponentLookup<MightMagicAffinity>(true);
            _aggregateOutlookLookup = state.GetComponentLookup<AggregateOutlook>(true);
            _aggregatePersonaLookup = state.GetComponentLookup<GroupPersona>(true);
            _aggregatePowerLookup = state.GetComponentLookup<AggregatePowerProfile>(true);
            _individualPersonalityLookup = state.GetComponentLookup<IndividualPersonalityAxes>(true);
            _behaviorTuningLookup = state.GetComponentLookup<IndividualBehaviorTuning>(true);
            _mightMagicAlignmentLookup = state.GetComponentLookup<IndividualMightMagicAlignment>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _outlookLookup.Update(ref state);
            _identityPersonalityLookup.Update(ref state);
            _affinityLookup.Update(ref state);
            _aggregateOutlookLookup.Update(ref state);
            _aggregatePersonaLookup.Update(ref state);
            _aggregatePowerLookup.Update(ref state);
            _individualPersonalityLookup.Update(ref state);
            _behaviorTuningLookup.Update(ref state);
            _mightMagicAlignmentLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var entityManager = state.EntityManager;

            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<AggregateAlignment>>().WithEntityAccess())
            {
                var flavor = BuildAggregateFlavor(entity, alignment.ValueRO);
                UpsertFlavor(entityManager, ref ecb, entity, flavor);
            }

            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<EntityAlignment>>().WithEntityAccess())
            {
                if (entityManager.HasComponent<AggregateAlignment>(entity))
                {
                    continue;
                }

                var flavor = BuildIdentityFlavor(entity, alignment.ValueRO);
                UpsertFlavor(entityManager, ref ecb, entity, flavor);
            }

            foreach (var (alignment, entity) in SystemAPI.Query<RefRO<IndividualAlignmentTriplet>>()
                         .WithNone<EntityAlignment, AggregateAlignment>()
                         .WithEntityAccess())
            {
                var flavor = BuildIndividualFlavor(entity, alignment.ValueRO);
                UpsertFlavor(entityManager, ref ecb, entity, flavor);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private static void UpsertFlavor(EntityManager entityManager, ref EntityCommandBuffer ecb, Entity entity, ArchetypeFlavor flavor)
        {
            if (entityManager.HasComponent<ArchetypeFlavor>(entity))
            {
                entityManager.SetComponentData(entity, flavor);
            }
            else
            {
                ecb.AddComponent(entity, flavor);
            }
        }

        private ArchetypeFlavor BuildAggregateFlavor(Entity entity, in AggregateAlignment alignment)
        {
            var flavor = new ArchetypeFlavor
            {
                Order = NormalizeSigned(alignment.Order, 100f),
                Moral = NormalizeSigned(alignment.Moral, 100f),
                Purity = NormalizeSigned(alignment.Purity, 100f)
            };

            if (_aggregateOutlookLookup.HasComponent(entity))
            {
                var outlook = _aggregateOutlookLookup[entity];
                ApplyOutlookTags(outlook.DominantPrimary, outlook.DominantSecondary, outlook.DominantTertiary, ref flavor);
            }

            if (_aggregatePersonaLookup.HasComponent(entity))
            {
                var persona = _aggregatePersonaLookup[entity];
                flavor.Vengeful = NormalizeSigned(persona.AvgVengefulForgiving, 100f);
                flavor.Bold = NormalizeSigned(persona.AvgCravenBold, 100f);
            }

            if (_aggregatePowerLookup.HasComponent(entity))
            {
                var power = _aggregatePowerLookup[entity];
                flavor.MightMagic = NormalizeSigned(power.AvgMightMagicAxis, 100f);
            }

            return ClampFlavor(flavor);
        }

        private ArchetypeFlavor BuildIdentityFlavor(Entity entity, in EntityAlignment alignment)
        {
            var flavor = new ArchetypeFlavor
            {
                Order = NormalizeSigned(alignment.Order, 100f),
                Moral = NormalizeSigned(alignment.Moral, 100f),
                Purity = NormalizeSigned(alignment.Purity, 100f)
            };

            if (_outlookLookup.HasComponent(entity))
            {
                var outlook = _outlookLookup[entity];
                ApplyOutlookTags(outlook.Primary, outlook.Secondary, outlook.Tertiary, ref flavor);
            }

            if (_identityPersonalityLookup.HasComponent(entity))
            {
                var persona = _identityPersonalityLookup[entity];
                flavor.Vengeful = NormalizeSigned(persona.VengefulForgiving, 100f);
                flavor.Bold = NormalizeSigned(persona.CravenBold, 100f);
            }

            if (_affinityLookup.HasComponent(entity))
            {
                var affinity = _affinityLookup[entity];
                flavor.MightMagic = NormalizeSigned(affinity.Axis, 100f);
            }

            ApplyCooperationFromIndividual(entity, ref flavor);
            return ClampFlavor(flavor);
        }

        private ArchetypeFlavor BuildIndividualFlavor(Entity entity, in IndividualAlignmentTriplet alignment)
        {
            var flavor = new ArchetypeFlavor
            {
                Order = math.clamp(alignment.Order, -1f, 1f),
                Moral = math.clamp(alignment.Moral, -1f, 1f),
                Purity = math.clamp(alignment.Purity, -1f, 1f)
            };

            if (_outlookLookup.HasComponent(entity))
            {
                var outlook = _outlookLookup[entity];
                ApplyOutlookTags(outlook.Primary, outlook.Secondary, outlook.Tertiary, ref flavor);
            }

            if (_mightMagicAlignmentLookup.HasComponent(entity))
            {
                var alignmentAxis = _mightMagicAlignmentLookup[entity];
                flavor.MightMagic = math.clamp(alignmentAxis.Axis, -1f, 1f);
            }
            else if (_affinityLookup.HasComponent(entity))
            {
                var affinity = _affinityLookup[entity];
                flavor.MightMagic = NormalizeSigned(affinity.Axis, 100f);
            }

            ApplyIndividualPersonalityAxes(entity, ref flavor);
            return ClampFlavor(flavor);
        }

        private void ApplyIndividualPersonalityAxes(Entity entity, ref ArchetypeFlavor flavor)
        {
            if (_individualPersonalityLookup.HasComponent(entity))
            {
                var persona = _individualPersonalityLookup[entity];
                flavor.Bold = math.clamp(persona.Boldness, -1f, 1f);
                flavor.Vengeful = math.clamp(persona.Vengefulness, -1f, 1f);
                flavor.Cooperation = math.clamp(persona.Selflessness, -1f, 1f);
            }

            if (_behaviorTuningLookup.HasComponent(entity))
            {
                var tuning = _behaviorTuningLookup[entity];
                flavor.Cooperation = math.clamp(tuning.SocialBias - 1f, -1f, 1f);
            }
        }

        private void ApplyCooperationFromIndividual(Entity entity, ref ArchetypeFlavor flavor)
        {
            if (_individualPersonalityLookup.HasComponent(entity))
            {
                var persona = _individualPersonalityLookup[entity];
                flavor.Cooperation = math.clamp(persona.Selflessness, -1f, 1f);
                return;
            }

            if (_behaviorTuningLookup.HasComponent(entity))
            {
                var tuning = _behaviorTuningLookup[entity];
                flavor.Cooperation = math.clamp(tuning.SocialBias - 1f, -1f, 1f);
            }
        }

        private static void ApplyOutlookTags(OutlookType primary, OutlookType secondary, OutlookType tertiary, ref ArchetypeFlavor flavor)
        {
            float warlike = 0f;
            float authority = 0f;
            float materialism = 0f;
            float xenophobia = 0f;

            ApplyOutlookTag(primary, 1f, ref warlike, ref authority, ref materialism, ref xenophobia);
            ApplyOutlookTag(secondary, 0.6f, ref warlike, ref authority, ref materialism, ref xenophobia);
            ApplyOutlookTag(tertiary, 0.35f, ref warlike, ref authority, ref materialism, ref xenophobia);

            flavor.Warlike = math.clamp(warlike, -1f, 1f);
            flavor.Authority = math.clamp(authority, -1f, 1f);
            flavor.Materialism = math.clamp(materialism, -1f, 1f);
            flavor.Xenophobia = math.clamp(xenophobia, -1f, 1f);
        }

        private static void ApplyOutlookTag(OutlookType outlook, float weight, ref float warlike, ref float authority, ref float materialism, ref float xenophobia)
        {
            switch (outlook)
            {
                case OutlookType.Warlike:
                    warlike += 1f * weight;
                    break;
                case OutlookType.Peaceful:
                    warlike -= 1f * weight;
                    break;
                case OutlookType.Authoritarian:
                    authority += 1f * weight;
                    break;
                case OutlookType.Egalitarian:
                    authority -= 1f * weight;
                    break;
                case OutlookType.Materialistic:
                    materialism += 1f * weight;
                    break;
                case OutlookType.Spiritual:
                    materialism -= 1f * weight;
                    break;
                case OutlookType.Xenophobic:
                    xenophobia += 1f * weight;
                    break;
            }
        }

        private static float NormalizeSigned(float value, float scale)
        {
            if (scale <= math.EPSILON)
            {
                return 0f;
            }

            return math.clamp(value / scale, -1f, 1f);
        }

        private static ArchetypeFlavor ClampFlavor(ArchetypeFlavor flavor)
        {
            flavor.Order = math.clamp(flavor.Order, -1f, 1f);
            flavor.Moral = math.clamp(flavor.Moral, -1f, 1f);
            flavor.Purity = math.clamp(flavor.Purity, -1f, 1f);
            flavor.Warlike = math.clamp(flavor.Warlike, -1f, 1f);
            flavor.Authority = math.clamp(flavor.Authority, -1f, 1f);
            flavor.Materialism = math.clamp(flavor.Materialism, -1f, 1f);
            flavor.Xenophobia = math.clamp(flavor.Xenophobia, -1f, 1f);
            flavor.MightMagic = math.clamp(flavor.MightMagic, -1f, 1f);
            flavor.Cooperation = math.clamp(flavor.Cooperation, -1f, 1f);
            flavor.Vengeful = math.clamp(flavor.Vengeful, -1f, 1f);
            flavor.Bold = math.clamp(flavor.Bold, -1f, 1f);
            return flavor;
        }
    }
}
