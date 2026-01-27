using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Environment
{
    /// <summary>
    /// Static helpers for light source calculations.
    /// </summary>
    [BurstCompile]
    public static class LightSourceHelpers
    {
        /// <summary>
        /// Calculates light intensity at a position from a light source.
        /// </summary>
        public static float CalculateLightAtPosition(
            in LightSource light,
            float3 lightPosition,
            float3 targetPosition)
        {
            if (light.IsActive == 0)
                return 0;

            if (light.IsDirectional != 0)
            {
                // Directional light - no distance falloff
                return light.Intensity;
            }

            float3 toTarget = targetPosition - lightPosition;
            float distance = math.length(toTarget);

            if (distance > light.Range)
                return 0;

            // Check cone angle for spotlights
            if (light.ConeAngle > 0 && light.ConeAngle < math.PI)
            {
                float3 dir = math.normalizesafe(toTarget);
                float dot = math.dot(dir, light.Direction);
                float angle = math.acos(math.clamp(dot, -1f, 1f));

                if (angle > light.ConeAngle)
                    return 0;

                // Smooth falloff within cone
                float innerAngle = light.InnerConeAngle;
                if (angle > innerAngle)
                {
                    float t = (angle - innerAngle) / (light.ConeAngle - innerAngle);
                    return CalculateAttenuation(light, distance) * (1f - t);
                }
            }

            return CalculateAttenuation(light, distance);
        }

        /// <summary>
        /// Calculates attenuation based on distance and model.
        /// </summary>
        private static float CalculateAttenuation(in LightSource light, float distance)
        {
            if (distance <= 0)
                return light.Intensity;

            float normalizedDist = distance / light.Range;

            return light.Attenuation switch
            {
                LightAttenuation.None => light.Intensity,
                LightAttenuation.Linear => light.Intensity * (1f - normalizedDist),
                LightAttenuation.Quadratic => light.Intensity / (1f + distance * distance * 0.01f),
                LightAttenuation.Exponential => light.Intensity * math.exp(-distance * 0.1f),
                _ => light.Intensity * (1f - normalizedDist)
            };
        }

        /// <summary>
        /// Accumulates light from multiple sources.
        /// </summary>
        public static void AccumulateLightSources(
            ref LightExposure exposure,
            in DynamicBuffer<LightSourceInfluence> influences,
            float ambientLight)
        {
            exposure.TotalIntensity = ambientLight;
            exposure.AmbientIntensity = ambientLight;
            exposure.DirectIntensity = 0;
            exposure.AccumulatedColor = float3.zero;
            exposure.DominantDirection = float3.zero;

            float maxIntensity = 0;
            float totalWeight = 0;

            for (int i = 0; i < influences.Length; i++)
            {
                var inf = influences[i];
                float effectiveIntensity = inf.Intensity * inf.ShadowFactor;

                exposure.TotalIntensity += effectiveIntensity;
                exposure.DirectIntensity += effectiveIntensity;

                // Track dominant direction
                if (effectiveIntensity > maxIntensity)
                {
                    maxIntensity = effectiveIntensity;
                    exposure.DominantDirection = inf.Direction;
                }

                totalWeight += effectiveIntensity;
            }

            exposure.InShadow = (byte)(exposure.DirectIntensity < 10f ? 1 : 0);
            exposure.InFullDarkness = (byte)(exposure.TotalIntensity < 1f ? 1 : 0);
        }

        /// <summary>
        /// Calculates shadow factor from occluder.
        /// </summary>
        public static float CalculateShadowFactor(
            float3 targetPosition,
            float3 lightPosition,
            float3 occluderPosition,
            float occluderRadius,
            float shadowSoftness)
        {
            float3 toLight = lightPosition - targetPosition;
            float lightDist = math.length(toLight);

            if (lightDist < 0.001f)
                return 1f;

            float3 lightDir = toLight / lightDist;
            float3 toOccluder = occluderPosition - targetPosition;

            // Project occluder onto light ray
            float projection = math.dot(toOccluder, lightDir);

            // Occluder must be between target and light
            if (projection <= 0 || projection >= lightDist)
                return 1f;

            // Distance from ray to occluder center
            float3 closestPoint = targetPosition + lightDir * projection;
            float distFromRay = math.length(occluderPosition - closestPoint);

            // Check if in shadow
            if (distFromRay >= occluderRadius)
                return 1f;

            // Calculate shadow with softness
            if (shadowSoftness > 0)
            {
                float penumbra = occluderRadius * shadowSoftness;
                float innerRadius = occluderRadius - penumbra;

                if (distFromRay < innerRadius)
                    return 0;

                return (distFromRay - innerRadius) / penumbra;
            }

            return 0;
        }

        /// <summary>
        /// Gets dominant light direction from influences.
        /// </summary>
        public static float3 GetDominantLightDirection(
            in DynamicBuffer<LightSourceInfluence> influences)
        {
            if (influences.Length == 0)
                return new float3(0, -1, 0); // Default down

            float maxIntensity = 0;
            float3 dominant = float3.zero;

            for (int i = 0; i < influences.Length; i++)
            {
                if (influences[i].Intensity > maxIntensity)
                {
                    maxIntensity = influences[i].Intensity;
                    dominant = influences[i].Direction;
                }
            }

            return math.lengthsq(dominant) > 0 ? math.normalize(dominant) : new float3(0, -1, 0);
        }

        /// <summary>
        /// Calculates weighted average light color.
        /// </summary>
        public static float3 CalculateAverageColor(
            in DynamicBuffer<LightSourceInfluence> influences,
            float3 defaultColor)
        {
            if (influences.Length == 0)
                return defaultColor;

            float3 weightedColor = float3.zero;
            float totalWeight = 0;

            for (int i = 0; i < influences.Length; i++)
            {
                float weight = influences[i].Intensity;
                // Note: Would need color in influence for proper calculation
                totalWeight += weight;
            }

            return totalWeight > 0 ? weightedColor / totalWeight : defaultColor;
        }

        /// <summary>
        /// Updates dynamic light intensity with flickering/pulsing.
        /// </summary>
        public static float UpdateDynamicLight(
            ref LightDynamics dynamics,
            float deltaTime,
            uint currentTick)
        {
            float intensity = dynamics.BaseIntensity;

            // Apply pulse (sine wave)
            if (dynamics.PulseAmount > 0)
            {
                float pulsePhase = currentTick * dynamics.PulseSpeed * 0.001f;
                float pulse = math.sin(pulsePhase * 2f * math.PI) * 0.5f + 0.5f;
                intensity *= 1f - dynamics.PulseAmount + pulse * dynamics.PulseAmount;
            }

            // Apply flicker (random)
            if (dynamics.FlickerAmount > 0)
            {
                var rng = new Unity.Mathematics.Random(dynamics.RandomSeed + currentTick);
                float flicker = rng.NextFloat() * dynamics.FlickerAmount;
                intensity *= 1f - flicker;
            }

            dynamics.CurrentIntensity = intensity;
            return intensity;
        }

        /// <summary>
        /// Blends time-of-day lighting colors.
        /// </summary>
        public static float3 BlendTimeOfDayColor(
            in TimeOfDayLighting tod,
            float timeProgress)
        {
            // 0-0.25: night to dawn, 0.25-0.5: dawn to day
            // 0.5-0.75: day to dusk, 0.75-1: dusk to night

            if (timeProgress < 0.2f)
            {
                // Night
                return tod.SkyColorNight;
            }
            else if (timeProgress < 0.3f)
            {
                // Night to dawn
                float t = (timeProgress - 0.2f) / 0.1f;
                return math.lerp(tod.SkyColorNight, tod.SkyColorDawn, t);
            }
            else if (timeProgress < 0.4f)
            {
                // Dawn to day
                float t = (timeProgress - 0.3f) / 0.1f;
                return math.lerp(tod.SkyColorDawn, tod.SkyColorDay, t);
            }
            else if (timeProgress < 0.7f)
            {
                // Day
                return tod.SkyColorDay;
            }
            else if (timeProgress < 0.8f)
            {
                // Day to dusk
                float t = (timeProgress - 0.7f) / 0.1f;
                return math.lerp(tod.SkyColorDay, tod.SkyColorDusk, t);
            }
            else if (timeProgress < 0.9f)
            {
                // Dusk to night
                float t = (timeProgress - 0.8f) / 0.1f;
                return math.lerp(tod.SkyColorDusk, tod.SkyColorNight, t);
            }
            else
            {
                // Night
                return tod.SkyColorNight;
            }
        }

        /// <summary>
        /// Samples light grid at position.
        /// </summary>
        public static float SampleLightGrid(
            in LightGrid grid,
            in DynamicBuffer<LightGridCell> cells,
            float3 position)
        {
            if (cells.Length == 0)
                return 0;

            // Calculate cell coordinates
            float3 local = position - grid.WorldMin;
            int2 cell = new int2(
                (int)(local.x / grid.CellSize),
                (int)(local.z / grid.CellSize));

            cell = math.clamp(cell, int2.zero, grid.Resolution - 1);
            int index = cell.y * grid.Resolution.x + cell.x;

            if (index < 0 || index >= cells.Length)
                return 0;

            var sample = cells[index];
            return sample.DirectLight + sample.AmbientLight;
        }

        /// <summary>
        /// Creates default ambient light state.
        /// </summary>
        public static AmbientLightState CreateDefaultAmbient()
        {
            return new AmbientLightState
            {
                SkyIntensity = 30f,
                GroundIntensity = 10f,
                SkyColor = new float3(0.5f, 0.7f, 1.0f),
                GroundColor = new float3(0.3f, 0.25f, 0.2f),
                HorizonColor = new float3(0.8f, 0.7f, 0.6f),
                FogDensity = 0.01f,
                FogDistance = 1000f,
                LastUpdateTick = 0
            };
        }

        /// <summary>
        /// Creates default time-of-day lighting.
        /// </summary>
        public static TimeOfDayLighting CreateDefaultTimeOfDay()
        {
            return new TimeOfDayLighting
            {
                SunIntensity = 100f,
                MoonIntensity = 10f,
                SunColor = new float3(1.0f, 0.95f, 0.85f),
                MoonColor = new float3(0.7f, 0.8f, 1.0f),
                SkyColorDay = new float3(0.5f, 0.7f, 1.0f),
                SkyColorNight = new float3(0.05f, 0.05f, 0.15f),
                SkyColorDawn = new float3(1.0f, 0.6f, 0.4f),
                SkyColorDusk = new float3(1.0f, 0.5f, 0.3f),
                TransitionProgress = 0
            };
        }
    }
}

