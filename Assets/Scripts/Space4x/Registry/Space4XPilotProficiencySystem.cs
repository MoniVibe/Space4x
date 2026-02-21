using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Movement;
using PureDOTS.Runtime.Progression;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Synthesizes PilotProficiency from practice time, wisdom, and aptitude.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
[UpdateBefore(typeof(ResourceSystemGroup))]
    public partial struct Space4XPilotProficiencySystem : ISystem
    {
        private ComponentLookup<Space4XPilotPracticeTime> _practiceLookup;
        private ComponentLookup<Space4XNormalizedIndividualStats> _normalizedLookup;
        private ComponentLookup<PhysiqueFinesseWill> _physiqueLookup;
        private ComponentLookup<PureDOTS.Runtime.Stats.WisdomStat> _wisdomLookup;
        private ComponentLookup<StrikeCraftKinematics> _strikeKinematicsLookup;
        private ComponentLookup<StrikeCraftDogfightTag> _dogfightTagLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _practiceLookup = state.GetComponentLookup<Space4XPilotPracticeTime>(false);
            _normalizedLookup = state.GetComponentLookup<Space4XNormalizedIndividualStats>(true);
            _physiqueLookup = state.GetComponentLookup<PhysiqueFinesseWill>(true);
            _wisdomLookup = state.GetComponentLookup<PureDOTS.Runtime.Stats.WisdomStat>(true);
            _strikeKinematicsLookup = state.GetComponentLookup<StrikeCraftKinematics>(true);
            _dogfightTagLookup = state.GetComponentLookup<StrikeCraftDogfightTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var config = Space4XPilotProficiencyConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XPilotProficiencyConfig>(out var configSingleton))
            {
                config = configSingleton;
            }

            _practiceLookup.Update(ref state);
            _normalizedLookup.Update(ref state);
            _physiqueLookup.Update(ref state);
            _wisdomLookup.Update(ref state);
            _strikeKinematicsLookup.Update(ref state);
            _dogfightTagLookup.Update(ref state);

            var deltaSeconds = time.DeltaSeconds;

            foreach (var (pilotLink, movement) in SystemAPI.Query<RefRO<VesselPilotLink>, RefRO<VesselMovement>>())
            {
                if (movement.ValueRO.IsMoving == 0)
                {
                    continue;
                }

                var pilot = pilotLink.ValueRO.Pilot;
                if (pilot == Entity.Null || !_practiceLookup.HasComponent(pilot))
                {
                    continue;
                }

                var practice = _practiceLookup[pilot];
                practice.NavigationSeconds += deltaSeconds;
                _practiceLookup[pilot] = practice;
            }

            foreach (var (pilotLink, engagement) in SystemAPI.Query<RefRO<VesselPilotLink>, RefRO<Space4XEngagement>>())
            {
                var pilot = pilotLink.ValueRO.Pilot;
                if (pilot == Entity.Null || !_practiceLookup.HasComponent(pilot))
                {
                    continue;
                }

                if (engagement.ValueRO.Phase != EngagementPhase.Engaged &&
                    engagement.ValueRO.Phase != EngagementPhase.Approaching)
                {
                    continue;
                }

                var practice = _practiceLookup[pilot];
                if (engagement.ValueRO.Phase == EngagementPhase.Engaged)
                {
                    practice.DogfightSeconds += deltaSeconds;
                    practice.GunnerySeconds += deltaSeconds;
                }
                else
                {
                    practice.DogfightSeconds += deltaSeconds * 0.4f;
                }

                _practiceLookup[pilot] = practice;
            }

            foreach (var (pilotLink, profile, entity) in SystemAPI.Query<RefRO<StrikeCraftPilotLink>, RefRO<StrikeCraftProfile>>()
                .WithEntityAccess())
            {
                if (profile.ValueRO.Phase == AttackRunPhase.Docked)
                {
                    continue;
                }

                var moving = true;
                if (_strikeKinematicsLookup.HasComponent(entity))
                {
                    var velocity = _strikeKinematicsLookup[entity].Velocity;
                    moving = math.lengthsq(velocity) > 0.0001f;
                }

                if (!moving)
                {
                    continue;
                }

                var pilot = pilotLink.ValueRO.Pilot;
                if (pilot == Entity.Null || !_practiceLookup.HasComponent(pilot))
                {
                    continue;
                }

                var practice = _practiceLookup[pilot];
                practice.NavigationSeconds += deltaSeconds;
                if (_dogfightTagLookup.HasComponent(entity))
                {
                    practice.DogfightSeconds += deltaSeconds;
                }

                if (profile.ValueRO.Phase == AttackRunPhase.Execute || _dogfightTagLookup.HasComponent(entity))
                {
                    practice.GunnerySeconds += deltaSeconds;
                }

                _practiceLookup[pilot] = practice;
            }

            foreach (var (practice, proficiency, entity) in SystemAPI.Query<RefRO<Space4XPilotPracticeTime>, RefRW<PilotProficiency>>()
                .WithEntityAccess())
            {
                ResolvePilotStats(entity, out var physique01, out var finesse01, out var will01, out var wisdom01);

                var navigationAptitude = ComputeAptitude(config.NavigationAptitude, physique01, finesse01, will01);
                var dogfightAptitude = ComputeAptitude(config.DogfightAptitude, physique01, finesse01, will01);
                var gunneryAptitude = ComputeAptitude(config.GunneryAptitude, physique01, finesse01, will01);

                var navigationSkill01 = ProgressionMath.ResolveSkill01FromPractice(
                    practice.ValueRO.NavigationSeconds,
                    config.SecondsToMastery,
                    wisdom01,
                    navigationAptitude,
                    config.WisdomMultiplierMin,
                    config.WisdomMultiplierMax,
                    config.AptitudeMultiplierMin,
                    config.AptitudeMultiplierMax);
                var dogfightSkill01 = ProgressionMath.ResolveSkill01FromPractice(
                    practice.ValueRO.DogfightSeconds,
                    config.SecondsToMastery,
                    wisdom01,
                    dogfightAptitude,
                    config.WisdomMultiplierMin,
                    config.WisdomMultiplierMax,
                    config.AptitudeMultiplierMin,
                    config.AptitudeMultiplierMax);
                var gunnerySkill01 = ProgressionMath.ResolveSkill01FromPractice(
                    practice.ValueRO.GunnerySeconds,
                    config.SecondsToMastery,
                    wisdom01,
                    gunneryAptitude,
                    config.WisdomMultiplierMin,
                    config.WisdomMultiplierMax,
                    config.AptitudeMultiplierMin,
                    config.AptitudeMultiplierMax);

                var controlSkill01 = math.saturate(navigationSkill01 * 0.55f + dogfightSkill01 * 0.45f);
                var reactionSkill01 = math.saturate(dogfightSkill01 * 0.65f + gunnerySkill01 * 0.35f);

                proficiency.ValueRW.ControlMult = math.lerp(config.ControlMin, config.ControlMax, controlSkill01);
                proficiency.ValueRW.TurnRateMult = math.lerp(config.TurnMin, config.TurnMax, dogfightSkill01);
                proficiency.ValueRW.EnergyMult = math.lerp(config.EnergyMax, config.EnergyMin, navigationSkill01);
                proficiency.ValueRW.Jitter = math.lerp(config.JitterMax, config.JitterMin, gunnerySkill01);
                proficiency.ValueRW.ReactionSec = math.lerp(config.ReactionMax, config.ReactionMin, reactionSkill01);
            }
        }

        private void ResolvePilotStats(
            Entity entity,
            out float physique01,
            out float finesse01,
            out float will01,
            out float wisdom01)
        {
            physique01 = 0.5f;
            finesse01 = 0.5f;
            will01 = 0.5f;
            wisdom01 = 0.5f;

            if (_normalizedLookup.HasComponent(entity))
            {
                var normalized = _normalizedLookup[entity];
                physique01 = normalized.Physique;
                finesse01 = normalized.Finesse;
                will01 = normalized.Will;
                wisdom01 = normalized.Wisdom;
                return;
            }

            if (_physiqueLookup.HasComponent(entity))
            {
                var physique = _physiqueLookup[entity];
                physique01 = math.saturate((float)physique.Physique / 100f);
                finesse01 = math.saturate((float)physique.Finesse / 100f);
                will01 = math.saturate((float)physique.Will / 100f);
                wisdom01 = will01;
            }

            if (_wisdomLookup.HasComponent(entity))
            {
                wisdom01 = math.saturate(_wisdomLookup[entity].Wisdom / 100f);
            }
        }

        private static float ComputeAptitude(
            in Space4XProficiencyAptitudeWeights weights,
            float physique01,
            float finesse01,
            float will01)
        {
            var sum = weights.Physique + weights.Finesse + weights.Will;
            if (sum <= 0.0001f)
            {
                return math.saturate((physique01 + finesse01 + will01) / 3f);
            }

            return math.saturate(
                (physique01 * weights.Physique + finesse01 * weights.Finesse + will01 * weights.Will) / sum);
        }
    }
}
