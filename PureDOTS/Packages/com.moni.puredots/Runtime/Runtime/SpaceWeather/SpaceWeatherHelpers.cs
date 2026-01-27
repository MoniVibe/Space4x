using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.SpaceWeather
{
    /// <summary>
    /// Static helpers for space weather calculations.
    /// </summary>
    [BurstCompile]
    public static class SpaceWeatherHelpers
    {
        /// <summary>
        /// Predicts solar activity based on cycle.
        /// </summary>
        public static float PredictSolarActivity(
            in SolarActivityCycle cycle,
            uint ticksAhead)
        {
            float futureProgress = cycle.CycleProgress + ticksAhead / cycle.CyclePeriod;
            futureProgress = futureProgress - math.floor(futureProgress);
            
            // Solar activity follows roughly sinusoidal pattern
            float sineValue = math.sin(futureProgress * 2f * math.PI);
            float normalized = sineValue * 0.5f + 0.5f;
            
            return math.lerp(cycle.BaseActivityLevel, cycle.PeakActivityLevel, normalized);
        }

        /// <summary>
        /// Calculates probability of solar flare.
        /// </summary>
        public static float CalculateFlareChance(
            in SolarActivityCycle cycle,
            float baseChancePerTick)
        {
            return baseChancePerTick * cycle.CurrentActivity * cycle.FlareChanceMultiplier;
        }

        /// <summary>
        /// Calculates radiation intensity at distance from zone center.
        /// </summary>
        public static float CalculateRadiationIntensity(
            in RadiationZone zone,
            float3 position)
        {
            float distance = math.length(position - zone.Center);
            
            if (distance <= zone.InnerRadius)
                return zone.Intensity;
            
            if (distance >= zone.OuterRadius)
                return 0;
            
            // Linear falloff between inner and outer
            float t = (distance - zone.InnerRadius) / (zone.OuterRadius - zone.InnerRadius);
            return zone.Intensity * (1f - t);
        }

        /// <summary>
        /// Updates radiation zone expansion/contraction.
        /// </summary>
        public static void UpdateRadiationZone(
            ref RadiationZone zone,
            float deltaTime,
            uint currentTick)
        {
            if (zone.IsExpanding != 0)
            {
                zone.OuterRadius += zone.ExpansionRate * deltaTime;
                zone.InnerRadius += zone.ExpansionRate * 0.5f * deltaTime;
                
                if (zone.OuterRadius >= zone.MaxRadius)
                {
                    zone.OuterRadius = zone.MaxRadius;
                    zone.IsExpanding = 0;
                }
            }
            else if (zone.IsPersistent == 0)
            {
                // Decay intensity
                zone.Intensity = math.max(0, zone.Intensity - zone.DecayRate * deltaTime);
                
                // Contract zone as intensity drops
                if (zone.Intensity < 10f)
                {
                    zone.OuterRadius = math.max(zone.InnerRadius, zone.OuterRadius - zone.ExpansionRate * 0.5f * deltaTime);
                }
            }
        }

        /// <summary>
        /// Applies radiation damage based on dose.
        /// </summary>
        public static float ApplyRadiationDamage(
            in RadiationExposure exposure,
            in RadiationDamageConfig config)
        {
            if (exposure.CurrentDose <= config.SafeThreshold)
                return 0;
            
            float effectiveDose = exposure.CurrentDose * (1f - exposure.ShieldingFactor);
            effectiveDose *= 1f - exposure.Resistance;
            
            if (effectiveDose <= config.SafeThreshold)
                return 0;
            
            if (effectiveDose >= config.LethalThreshold)
                return config.DamagePerDose * 5f; // Extreme damage
            
            if (effectiveDose >= config.DangerThreshold)
                return config.DamagePerDose * 2f; // Heavy damage
            
            if (effectiveDose >= config.WarningThreshold)
                return config.DamagePerDose; // Normal damage
            
            return 0;
        }

        /// <summary>
        /// Accumulates radiation exposure.
        /// </summary>
        public static void AccumulateExposure(
            ref RadiationExposure exposure,
            float radiationLevel,
            float deltaTime,
            uint currentTick)
        {
            float effectiveRadiation = radiationLevel * (1f - exposure.ShieldingFactor);
            effectiveRadiation *= 1f - exposure.Resistance;
            
            exposure.CurrentDose = effectiveRadiation;
            exposure.DoseRate = effectiveRadiation;
            exposure.AccumulatedDose += effectiveRadiation * deltaTime;
            exposure.LastExposureTick = currentTick;
            
            if (effectiveRadiation > 0)
            {
                exposure.ExposureDuration++;
            }
            else
            {
                exposure.ExposureDuration = 0;
            }
        }

        /// <summary>
        /// Calculates shielding effectiveness against weather type.
        /// </summary>
        public static float CalculateShieldingEffectiveness(
            in SpaceWeatherShielding shielding,
            SpaceWeatherType weatherType)
        {
            float integrityFactor = shielding.ShieldIntegrity / math.max(1f, shielding.MaxIntegrity);
            
            float baseEffectiveness = weatherType switch
            {
                SpaceWeatherType.SolarFlare => shielding.RadiationShielding,
                SpaceWeatherType.RadiationStorm => shielding.RadiationShielding,
                SpaceWeatherType.CosmicRayBurst => shielding.ParticleShielding * 0.7f,
                SpaceWeatherType.MagneticStorm => shielding.MagneticShielding,
                SpaceWeatherType.CoranalMassEjection => (shielding.ParticleShielding + shielding.MagneticShielding) * 0.5f,
                SpaceWeatherType.SolarWind => shielding.ParticleShielding,
                SpaceWeatherType.GammaRayBurst => shielding.RadiationShielding * 0.3f, // Penetrating
                SpaceWeatherType.NeutronStorm => shielding.RadiationShielding * 0.2f, // Very penetrating
                _ => 0
            };
            
            return baseEffectiveness * integrityFactor;
        }

        /// <summary>
        /// Updates shielding degradation during space weather.
        /// </summary>
        public static void UpdateShieldDegradation(
            ref SpaceWeatherShielding shielding,
            in SpaceWeatherState weather,
            float deltaTime)
        {
            if (weather.CurrentWeather == SpaceWeatherType.Clear)
            {
                // Recharge shields
                shielding.ShieldIntegrity = math.min(
                    shielding.MaxIntegrity,
                    shielding.ShieldIntegrity + shielding.RechargeRate * deltaTime);
            }
            else
            {
                // Degrade shields
                float degradation = shielding.DegradeRate * weather.Intensity * 0.01f * deltaTime;
                shielding.ShieldIntegrity = math.max(0, shielding.ShieldIntegrity - degradation);
            }
        }

        /// <summary>
        /// Calculates system disruption level.
        /// </summary>
        public static void CalculateSystemDisruption(
            ref SystemDisruption disruption,
            in SpaceWeatherState weather,
            in MagneticField magneticField,
            uint currentTick)
        {
            if (weather.CurrentWeather == SpaceWeatherType.Clear)
            {
                // Recover from disruption
                disruption.SensorDisruption = math.max(0, disruption.SensorDisruption - 0.01f);
                disruption.CommunicationDisruption = math.max(0, disruption.CommunicationDisruption - 0.01f);
                disruption.NavigationDisruption = math.max(0, disruption.NavigationDisruption - 0.01f);
                disruption.PowerDisruption = math.max(0, disruption.PowerDisruption - 0.01f);
                return;
            }
            
            float protectionFactor = magneticField.HasMagnetosphere != 0 ? magneticField.ProtectionFactor : 0;
            float weatherIntensity = weather.Intensity * 0.01f * (1f - protectionFactor);
            
            switch (weather.CurrentWeather)
            {
                case SpaceWeatherType.MagneticStorm:
                    disruption.SensorDisruption = math.saturate(weatherIntensity * 0.8f);
                    disruption.NavigationDisruption = math.saturate(weatherIntensity * 0.9f);
                    disruption.CommunicationDisruption = math.saturate(weatherIntensity * 0.7f);
                    break;
                    
                case SpaceWeatherType.SolarFlare:
                    disruption.CommunicationDisruption = math.saturate(weatherIntensity);
                    disruption.SensorDisruption = math.saturate(weatherIntensity * 0.5f);
                    break;
                    
                case SpaceWeatherType.CoranalMassEjection:
                    disruption.PowerDisruption = math.saturate(weatherIntensity * 0.6f);
                    disruption.NavigationDisruption = math.saturate(weatherIntensity * 0.8f);
                    disruption.CommunicationDisruption = math.saturate(weatherIntensity * 0.5f);
                    break;
            }
            
            disruption.DisruptionStartTick = currentTick;
            disruption.IsCritical = (byte)(
                (disruption.SensorDisruption > 0.8f || 
                 disruption.NavigationDisruption > 0.8f ||
                 disruption.PowerDisruption > 0.8f) ? 1 : 0);
        }

        /// <summary>
        /// Checks if CME has reached a position.
        /// </summary>
        public static bool HasCMEArrived(
            in CoronalMassEjection cme,
            float3 targetPosition,
            float targetRadius)
        {
            float3 cmeToTarget = targetPosition - cme.CurrentPosition;
            float dot = math.dot(math.normalize(cmeToTarget), cme.Direction);
            
            if (dot < math.cos(cme.Width * 0.5f))
                return false; // Outside cone
            
            float distance = math.length(cmeToTarget);
            return distance <= targetRadius;
        }

        /// <summary>
        /// Updates CME position.
        /// </summary>
        public static void UpdateCMEPosition(
            ref CoronalMassEjection cme,
            float3 sourcePosition,
            float deltaTime)
        {
            float travelTime = deltaTime * cme.Speed;
            cme.CurrentPosition = sourcePosition + cme.Direction * travelTime;
        }

        /// <summary>
        /// Gets severity from intensity value.
        /// </summary>
        public static WeatherSeverity GetSeverityFromIntensity(float intensity)
        {
            if (intensity >= 80) return WeatherSeverity.Extreme;
            if (intensity >= 60) return WeatherSeverity.Severe;
            if (intensity >= 40) return WeatherSeverity.Strong;
            if (intensity >= 20) return WeatherSeverity.Moderate;
            return WeatherSeverity.Minor;
        }

        /// <summary>
        /// Creates default radiation damage config.
        /// </summary>
        public static RadiationDamageConfig CreateDefaultRadiationConfig()
        {
            return new RadiationDamageConfig
            {
                SafeThreshold = 5f,
                WarningThreshold = 20f,
                DangerThreshold = 50f,
                LethalThreshold = 100f,
                DamagePerDose = 0.1f,
                RecoveryRate = 0.5f,
                AccumulationDecay = 0.01f
            };
        }

        /// <summary>
        /// Creates default solar activity cycle.
        /// </summary>
        public static SolarActivityCycle CreateDefaultSolarCycle(Entity starEntity, float cyclePeriodicTicks = 1000000)
        {
            return new SolarActivityCycle
            {
                StarEntity = starEntity,
                CycleProgress = 0,
                CyclePeriod = cyclePeriodicTicks,
                BaseActivityLevel = 0.2f,
                PeakActivityLevel = 1f,
                CurrentActivity = 0.2f,
                FlareChanceMultiplier = 1f
            };
        }

        /// <summary>
        /// Gets intensity multiplier for weather severity.
        /// </summary>
        public static float GetSeverityMultiplier(WeatherSeverity severity)
        {
            return severity switch
            {
                WeatherSeverity.Minor => 0.2f,
                WeatherSeverity.Moderate => 0.4f,
                WeatherSeverity.Strong => 0.6f,
                WeatherSeverity.Severe => 0.8f,
                WeatherSeverity.Extreme => 1.0f,
                _ => 0.2f
            };
        }
    }
}

