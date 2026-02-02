using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Movement;
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
                _practiceLookup[pilot] = practice;
            }

            foreach (var (practice, proficiency, entity) in SystemAPI.Query<RefRO<Space4XPilotPracticeTime>, RefRW<PilotProficiency>>()
                .WithEntityAccess())
            {
                ResolvePilotStats(entity, out var physique01, out var finesse01, out var will01, out var wisdom01);

                var aptitude = ComputeAptitude(config.NavigationAptitude, physique01, finesse01, will01);
                var wisdomFactor = math.lerp(config.WisdomMultiplierMin, config.WisdomMultiplierMax, wisdom01);
                var aptitudeFactor = math.lerp(config.AptitudeMultiplierMin, config.AptitudeMultiplierMax, aptitude);
                var effectiveSeconds = practice.ValueRO.NavigationSeconds * wisdomFactor * aptitudeFactor;

                var skill01 = config.SecondsToMastery > 0f
                    ? math.saturate(effectiveSeconds / config.SecondsToMastery)
                    : 0f;

                proficiency.ValueRW.ControlMult = math.lerp(config.ControlMin, config.ControlMax, skill01);
                proficiency.ValueRW.TurnRateMult = math.lerp(config.TurnMin, config.TurnMax, skill01);
                proficiency.ValueRW.EnergyMult = math.lerp(config.EnergyMax, config.EnergyMin, skill01);
                proficiency.ValueRW.Jitter = math.lerp(config.JitterMax, config.JitterMin, skill01);
                proficiency.ValueRW.ReactionSec = math.lerp(config.ReactionMax, config.ReactionMin, skill01);
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
