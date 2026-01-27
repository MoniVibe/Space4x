using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// WARM path: Advances the shared ClimateState using authored profile data.
    /// Keeps global temperature, wind, moisture, and seasonal progression in sync with simulation time.
    /// Staggered updates, only simulate where something is happening.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup))]
    public partial struct ClimateStateUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<EnvironmentGridConfigData>();
            state.RequireForUpdate<ClimateState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState))
            {
                return;
            }
            var tickTimeState = SystemAPI.GetSingleton<TickTimeState>();
            
            // Use TimeHelpers to check if we should update (handles pause, rewind, stasis)
            // Climate is global, so use default membership (no bubble)
            var defaultMembership = default(TimeBubbleMembership);
            if (!TimeHelpers.ShouldUpdate(timeState, rewindState, defaultMembership))
            {
                return;
            }
            
            // Also check for catch-up mode (climate should update during catch-up)
            if (rewindState.Mode != RewindMode.Record && rewindState.Mode != RewindMode.CatchUp)
            {
                return;
            }

            var config = SystemAPI.GetSingleton<EnvironmentGridConfigData>();

            ClimateProfileData profile;
            var hasProfile = SystemAPI.TryGetSingleton(out profile);
            if (!hasProfile)
            {
                profile = ClimateProfileDefaults.Create(in config);
            }

            var climateEntity = SystemAPI.GetSingletonEntity<ClimateState>();
            var climate = SystemAPI.GetComponent<ClimateState>(climateEntity);

            var currentTick = timeState.Tick;
            if (climate.LastUpdateTick == currentTick)
            {
                return;
            }

            uint tickDelta;
            if (climate.LastUpdateTick == uint.MaxValue)
            {
                tickDelta = 1u;
            }
            else
            {
                tickDelta = EnvironmentEffectUtility.TickDelta(currentTick, climate.LastUpdateTick);
                if (tickDelta == 0u)
                {
                    return;
                }
            }

            // Use TimeHelpers to get effective delta time
            var effectiveDelta = TimeHelpers.GetGlobalDelta(tickTimeState, timeState);
            var deltaSeconds = effectiveDelta * tickDelta;
            if (deltaSeconds <= 0f)
            {
                climate.LastUpdateTick = currentTick;
                SystemAPI.SetComponent(climateEntity, climate);
                return;
            }

            AdvanceTimeOfDay(ref climate, profile, deltaSeconds);
            AdvanceSeason(ref climate, profile, deltaSeconds);
            UpdateTemperature(ref climate, profile);
            UpdateWind(ref climate, profile, currentTick, timeState.FixedDeltaTime * currentTick);
            UpdateMoistureAndClouds(ref climate, profile, deltaSeconds);

            climate.LastUpdateTick = currentTick;
            SystemAPI.SetComponent(climateEntity, climate);
        }

        static void AdvanceTimeOfDay(ref PureDOTS.Environment.ClimateState climate, in ClimateProfileData profile, float deltaSeconds)
        {
            var hoursPerSecond = math.max(0.001f, profile.HoursPerSecond);
            var hoursDelta = deltaSeconds * hoursPerSecond;
            var nextHours = EnvironmentEffectUtility.WrapHours(climate.TimeOfDayHours + hoursDelta);
            climate.TimeOfDayHours = nextHours;
            climate.DayNightProgress = math.saturate(nextHours / 24f);
        }

        static void AdvanceSeason(ref PureDOTS.Environment.ClimateState climate, in ClimateProfileData profile, float deltaSeconds)
        {
            var daysPerSeason = math.max(1f, profile.DaysPerSeason);
            var daysDelta = deltaSeconds * profile.HoursPerSecond / 24f;
            var totalProgress = climate.SeasonProgress + daysDelta / daysPerSeason;

            var seasonAdvance = (int)math.floor(totalProgress);
            var newProgress = totalProgress - seasonAdvance;

            if (seasonAdvance > 0)
            {
                var currentIndex = (int)climate.CurrentSeason;
                currentIndex = (currentIndex + seasonAdvance) % 4;
                climate.CurrentSeason = (Season)currentIndex;
            }

            climate.SeasonProgress = math.clamp(newProgress, 0f, 0.999f);
        }

        static void UpdateTemperature(ref PureDOTS.Environment.ClimateState climate, in ClimateProfileData profile)
        {
            var currentSeasonIndex = (int)climate.CurrentSeason;
            var nextSeasonIndex = (currentSeasonIndex + 1) % 4;

            var currentBase = profile.SeasonalTemperatures[currentSeasonIndex];
            var nextBase = profile.SeasonalTemperatures[nextSeasonIndex];

            var smoothing = math.saturate(profile.SeasonalTransitionSmoothing);
            var blendStart = smoothing > 0f ? math.max(0f, 1f - smoothing) : 1f;
            var blendT = 0f;
            if (smoothing > 0f && climate.SeasonProgress >= blendStart)
            {
                var range = math.max(1e-3f, smoothing);
                blendT = math.saturate((climate.SeasonProgress - blendStart) / range);
            }

            var baseTemperature = math.lerp(currentBase, nextBase, blendT);
            var dayPhase = climate.DayNightProgress - 0.5f;
            var dayNightWave = math.cos(dayPhase * math.PI * 2f);
            var dayNightOffset = profile.DayNightTemperatureSwing * dayNightWave;

            climate.GlobalTemperature = baseTemperature + dayNightOffset;
        }

        static void UpdateWind(ref PureDOTS.Environment.ClimateState climate, in ClimateProfileData profile, uint currentTick, float simulationSeconds)
        {
            var amplitude = math.clamp(profile.WindVariationAmplitude, 0f, 1f);
            var frequency = math.max(0f, profile.WindVariationFrequency);

            var gustPhase = frequency > 0f
                ? simulationSeconds * frequency
                : (float)currentTick * 0.005f;

            var gustWave = math.sin(gustPhase);

            var baseDirection = profile.BaseWindDirection;
            if (math.lengthsq(baseDirection) < 1e-6f)
            {
                baseDirection = new float2(0f, 1f);
            }

            var angleOffset = gustWave * amplitude * 0.35f;
            var sin = math.sin(angleOffset);
            var cos = math.cos(angleOffset);
            var rotated = new float2(
                baseDirection.x * cos - baseDirection.y * sin,
                baseDirection.x * sin + baseDirection.y * cos);

            climate.GlobalWindDirection = math.normalizesafe(rotated, new float2(0f, 1f));

            var strength = profile.BaseWindStrength * (1f + amplitude * gustWave);
            climate.GlobalWindStrength = math.max(0f, strength);
        }

        static void UpdateMoistureAndClouds(ref PureDOTS.Environment.ClimateState climate, in ClimateProfileData profile, float deltaSeconds)
        {
            var seasonIndex = (int)climate.CurrentSeason;
            var seasonProgress = climate.SeasonProgress;
            var seasonalPhase = (seasonIndex + seasonProgress) * (math.PI * 0.5f);
            var wave = math.sin(seasonalPhase);

            var humidityTarget = math.clamp(
                profile.AtmosphericMoistureBase + profile.AtmosphericMoistureVariation * wave,
                0f,
                100f);

            var lerpFactor = math.saturate(deltaSeconds * 0.5f);
            climate.AtmosphericMoisture = math.lerp(climate.AtmosphericMoisture, humidityTarget, lerpFactor);

            var cloudBias = math.saturate(humidityTarget / 100f);
            var cloudTarget = math.clamp(
                profile.CloudCoverBase + profile.CloudCoverVariation * cloudBias,
                0f,
                100f);

            climate.CloudCover = math.lerp(climate.CloudCover, cloudTarget, lerpFactor);
        }
    }
}

