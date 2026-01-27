using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Exposure
{
    /// <summary>
    /// Static helpers for exposure effect calculations.
    /// </summary>
    [BurstCompile]
    public static class ExposureHelpers
    {
        /// <summary>
        /// Calculates comfort level based on current vs preferred light.
        /// </summary>
        public static float CalculateComfort(
            in LightPreference preference,
            float currentLightLevel)
        {
            float deviation = math.abs(currentLightLevel - preference.PreferredLevel);

            if (deviation <= preference.ToleranceRange)
                return 1f;

            // Beyond tolerance, comfort drops
            float excessDeviation = deviation - preference.ToleranceRange;
            float maxDeviation = math.max(
                preference.PreferredLevel - preference.MinTolerable,
                preference.MaxTolerable - preference.PreferredLevel) - preference.ToleranceRange;

            if (maxDeviation <= 0)
                return 0;

            float discomfort = excessDeviation / maxDeviation;
            return math.saturate(1f - discomfort);
        }

        /// <summary>
        /// Calculates comfort for cave mushrooms (scotophilic) - thrives in darkness.
        /// </summary>
        public static float CalculateScotophilicComfort(
            float currentLightLevel,
            float preferredLevel,
            float maxTolerable)
        {
            if (currentLightLevel <= preferredLevel)
                return 1f;

            if (currentLightLevel >= maxTolerable)
                return 0;

            float excess = currentLightLevel - preferredLevel;
            float range = maxTolerable - preferredLevel;
            return 1f - (excess / range);
        }

        /// <summary>
        /// Accumulates exposure over time.
        /// </summary>
        public static void AccumulateExposure(
            ref ExposureAccumulator accumulator,
            float exposureLevel,
            float deltaTime,
            uint currentTick)
        {
            accumulator.CurrentExposure = exposureLevel;

            if (exposureLevel > 0)
            {
                accumulator.AccumulatedDose += exposureLevel * deltaTime;
                accumulator.ExposureDuration += deltaTime;

                if (exposureLevel > accumulator.PeakExposure)
                    accumulator.PeakExposure = exposureLevel;
            }
            else
            {
                // Decay when not exposed
                accumulator.AccumulatedDose = math.max(0,
                    accumulator.AccumulatedDose - accumulator.DecayRate * deltaTime);
                accumulator.ExposureDuration = 0;
            }

            accumulator.LastExposureTick = currentTick;
        }

        /// <summary>
        /// Applies exposure effects based on thresholds.
        /// </summary>
        public static void ApplyExposureEffects(
            ref DynamicBuffer<ExposureEffect> effects,
            in ExposureAccumulator accumulator,
            in ExposureConfig config,
            uint currentTick)
        {
            float exposure = accumulator.CurrentExposure;

            // Clear expired effects
            for (int i = effects.Length - 1; i >= 0; i--)
            {
                var effect = effects[i];
                if (effect.Duration <= 0 || effect.IsActive == 0)
                {
                    effects.RemoveAt(i);
                    continue;
                }

                effect.Duration -= 1;
                effects[i] = effect;
            }

            // Add new effects if thresholds crossed
            if (exposure >= config.DamageThreshold)
            {
                bool hasEffect = false;
                for (int i = 0; i < effects.Length; i++)
                {
                    if (effects[i].Type == accumulator.TrackedType)
                    {
                        hasEffect = true;
                        var effect = effects[i];
                        effect.Intensity = math.max(effect.Intensity, exposure * 0.01f);
                        effect.Duration = 100;
                        effects[i] = effect;
                        break;
                    }
                }

                if (!hasEffect)
                {
                    effects.Add(new ExposureEffect
                    {
                        Type = accumulator.TrackedType,
                        EffectId = "exposure_damage",
                        Intensity = exposure * 0.01f,
                        Duration = 100,
                        AppliedTick = currentTick,
                        IsPositive = 0,
                        IsActive = 1
                    });
                }
            }
        }

        /// <summary>
        /// Calculates growth modifier from light.
        /// </summary>
        public static float CalculateGrowthModifier(
            in LightPreference preference,
            in PhotosynthesisRate photosynthesis,
            float currentLightLevel)
        {
            if (preference.CanPhotosynthesize == 0)
            {
                // Non-photosynthetic - just comfort based
                return CalculateComfort(preference, currentLightLevel);
            }

            // Photosynthetic growth curve
            if (currentLightLevel < photosynthesis.CompensationPoint)
                return 0; // Below compensation point

            if (currentLightLevel >= photosynthesis.SaturationPoint)
                return 1f; // At or above saturation

            if (currentLightLevel <= photosynthesis.OptimalLight)
            {
                // Linear increase to optimal
                float t = (currentLightLevel - photosynthesis.CompensationPoint) /
                    (photosynthesis.OptimalLight - photosynthesis.CompensationPoint);
                return t;
            }
            else
            {
                // Plateau to saturation
                return 1f;
            }
        }

        /// <summary>
        /// Updates photosynthesis rate.
        /// </summary>
        public static void UpdatePhotosynthesis(
            ref PhotosynthesisRate rate,
            float lightLevel)
        {
            if (lightLevel < rate.CompensationPoint)
            {
                rate.CurrentRate = 0;
                rate.EnergyProduced = 0;
                return;
            }

            float efficiency;
            if (lightLevel <= rate.OptimalLight)
            {
                efficiency = lightLevel / rate.OptimalLight;
            }
            else if (lightLevel <= rate.SaturationPoint)
            {
                efficiency = 1f;
            }
            else
            {
                // Photoinhibition above saturation
                float excess = lightLevel - rate.SaturationPoint;
                efficiency = 1f - (excess * 0.01f);
                efficiency = math.max(0.5f, efficiency);
            }

            rate.CurrentRate = rate.BaseRate * efficiency * rate.LightEfficiency;
            rate.EnergyProduced = rate.CurrentRate;
        }

        /// <summary>
        /// Updates environmental comfort.
        /// </summary>
        public static void UpdateEnvironmentalComfort(
            ref EnvironmentalComfort comfort,
            in LightPreference lightPref,
            in TemperatureExposure tempExposure,
            float lightLevel,
            float humidity,
            uint currentTick)
        {
            // Light comfort
            comfort.LightComfort = CalculateComfort(lightPref, lightLevel);

            // Temperature comfort
            float tempDeviation = math.abs(tempExposure.BodyTemperature - tempExposure.OptimalTemperature);
            float tempRange = (tempExposure.HeatThreshold - tempExposure.ColdThreshold) * 0.5f;
            comfort.TemperatureComfort = math.saturate(1f - (tempDeviation / tempRange));

            // Humidity comfort (assume 40-60% is optimal)
            float humidityDeviation = math.abs(humidity - 50f);
            comfort.HumidityComfort = math.saturate(1f - (humidityDeviation / 50f));

            // Combined comfort
            comfort.OverallComfort = comfort.LightComfort * 0.3f +
                                     comfort.TemperatureComfort * 0.5f +
                                     comfort.HumidityComfort * 0.2f;

            comfort.ComfortModifier = comfort.OverallComfort;
            comfort.LastUpdateTick = currentTick;
        }

        /// <summary>
        /// Updates circadian rhythm.
        /// </summary>
        public static void UpdateCircadianRhythm(
            ref CircadianRhythm rhythm,
            float timeOfDay,
            float lightLevel)
        {
            rhythm.CycleProgress = timeOfDay / 24f;

            // Determine if should be active
            bool shouldBeActive;
            if (rhythm.IsDiurnal != 0)
            {
                shouldBeActive = timeOfDay >= rhythm.WakeTime && timeOfDay < rhythm.SleepTime;
            }
            else if (rhythm.IsNocturnal != 0)
            {
                shouldBeActive = timeOfDay < rhythm.WakeTime || timeOfDay >= rhythm.SleepTime;
            }
            else // Crepuscular
            {
                float dawnStart = rhythm.WakeTime - 1f;
                float dawnEnd = rhythm.WakeTime + 1f;
                float duskStart = rhythm.SleepTime - 1f;
                float duskEnd = rhythm.SleepTime + 1f;
                shouldBeActive = (timeOfDay >= dawnStart && timeOfDay <= dawnEnd) ||
                                 (timeOfDay >= duskStart && timeOfDay <= duskEnd);
            }

            // Update energy based on activity phase
            if (shouldBeActive)
            {
                // Active period - energy gradually decreases
                rhythm.EnergyLevel = math.max(0.1f, rhythm.EnergyLevel - 0.001f);
            }
            else
            {
                // Rest period - energy recovers
                float recovery = 0.002f * rhythm.RestQuality;
                rhythm.EnergyLevel = math.min(1f, rhythm.EnergyLevel + recovery);
            }

            // Jet lag from light at wrong times
            bool lightMismatch = (rhythm.IsNocturnal != 0 && lightLevel > 50f) ||
                                 (rhythm.IsDiurnal != 0 && shouldBeActive && lightLevel < 10f);
            if (lightMismatch)
            {
                rhythm.JetLag = math.min(1f, rhythm.JetLag + 0.001f);
            }
            else
            {
                rhythm.JetLag = math.max(0, rhythm.JetLag - 0.0005f);
            }
        }

        /// <summary>
        /// Updates light damage/benefit.
        /// </summary>
        public static void UpdateLightDamageAndBenefit(
            ref LightDamage damage,
            ref LightBenefit benefit,
            float lightLevel,
            float deltaTime)
        {
            float protectedLight = lightLevel * (1f - damage.ProtectionLevel);

            // Damage accumulation
            if (protectedLight > damage.DamageThreshold)
            {
                float excess = protectedLight - damage.DamageThreshold;
                damage.DamageAccumulated += excess * deltaTime * 0.01f;
                damage.HasSunburn = (byte)(damage.DamageAccumulated > 0.5f ? 1 : 0);
            }
            else
            {
                // Healing
                damage.DamageAccumulated = math.max(0,
                    damage.DamageAccumulated - damage.HealingRate * deltaTime);
                if (damage.DamageAccumulated < 0.1f)
                    damage.HasSunburn = 0;
            }

            // Benefit accumulation
            if (lightLevel > 0 && lightLevel < benefit.SaturationPoint)
            {
                benefit.CurrentExposure += lightLevel * deltaTime * 0.001f;
                benefit.BenefitLevel = math.min(1f,
                    benefit.CurrentExposure / benefit.OptimalExposure);
            }

            benefit.IsDeficient = (byte)(benefit.BenefitLevel < benefit.DeficiencyThreshold ? 1 : 0);
            benefit.IsOptimal = (byte)(benefit.BenefitLevel >= 0.8f && benefit.BenefitLevel <= 1f ? 1 : 0);
        }

        /// <summary>
        /// Creates default light preference for photophilic entity (loves light).
        /// </summary>
        public static LightPreference CreatePhotophilicPreference()
        {
            return new LightPreference
            {
                Category = LightPreferenceCategory.Photophilic,
                PreferredLevel = 70f,
                ToleranceRange = 20f,
                MinTolerable = 20f,
                MaxTolerable = 100f,
                AdaptationRate = 0.01f,
                CanPhotosynthesize = 1,
                TakesLightDamage = 0
            };
        }

        /// <summary>
        /// Creates default light preference for scotophilic entity (cave mushroom).
        /// </summary>
        public static LightPreference CreateScotophilicPreference()
        {
            return new LightPreference
            {
                Category = LightPreferenceCategory.Scotophilic,
                PreferredLevel = 5f,
                ToleranceRange = 5f,
                MinTolerable = 0f,
                MaxTolerable = 30f,
                AdaptationRate = 0.005f,
                CanPhotosynthesize = 0,
                TakesLightDamage = 1
            };
        }

        /// <summary>
        /// Creates default exposure config.
        /// </summary>
        public static ExposureConfig CreateDefaultExposureConfig()
        {
            return new ExposureConfig
            {
                SafeLightLevel = 80f,
                DangerousLightLevel = 150f,
                SafeRadiationLevel = 5f,
                DangerousRadiationLevel = 50f,
                ComfortTemperatureMin = 15f,
                ComfortTemperatureMax = 30f,
                EffectTriggerThreshold = 0.5f,
                DamageThreshold = 0.8f
            };
        }

        /// <summary>
        /// Gets stress level from light exposure.
        /// </summary>
        public static float GetLightStress(
            in LightPreference preference,
            float currentLightLevel)
        {
            if (currentLightLevel < preference.MinTolerable)
            {
                // Too dark
                return (preference.MinTolerable - currentLightLevel) / preference.MinTolerable;
            }

            if (currentLightLevel > preference.MaxTolerable)
            {
                // Too bright
                return (currentLightLevel - preference.MaxTolerable) /
                    (100f - preference.MaxTolerable + 0.01f);
            }

            return 0;
        }
    }
}

