using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Celestial
{
    /// <summary>
    /// Static helpers for celestial body and orbital calculations.
    /// </summary>
    [BurstCompile]
    public static class CelestialHelpers
    {
        /// <summary>
        /// Calculates orbital position from orbital elements at given tick.
        /// Uses Kepler's laws for elliptical orbits.
        /// </summary>
        public static float3 CalculateOrbitalPosition(
            in OrbitalState orbit,
            float3 parentPosition,
            uint currentTick)
        {
            // Calculate mean anomaly at current tick
            float ticksSinceEpoch = currentTick - orbit.EpochTick;
            float meanMotion = 2f * math.PI / math.max(1f, orbit.OrbitalPeriod);
            float meanAnomaly = orbit.MeanAnomaly + meanMotion * ticksSinceEpoch;
            meanAnomaly = NormalizeAngle(meanAnomaly);
            
            // Solve Kepler's equation for eccentric anomaly (Newton-Raphson)
            float eccentricAnomaly = SolveKeplerEquation(meanAnomaly, orbit.Eccentricity);
            
            // Calculate true anomaly
            float cosE = math.cos(eccentricAnomaly);
            float sinE = math.sin(eccentricAnomaly);
            float trueAnomaly = math.atan2(
                math.sqrt(1f - orbit.Eccentricity * orbit.Eccentricity) * sinE,
                cosE - orbit.Eccentricity);
            
            // Calculate distance from parent
            float distance = orbit.SemiMajorAxis * (1f - orbit.Eccentricity * cosE);
            
            // Position in orbital plane
            float xOrbit = distance * math.cos(trueAnomaly);
            float yOrbit = distance * math.sin(trueAnomaly);
            
            // Rotate to 3D space using orbital elements
            float3 position = RotateToWorldSpace(
                xOrbit, yOrbit,
                orbit.ArgumentOfPeriapsis,
                orbit.Inclination,
                orbit.LongitudeOfAscendingNode);
            
            return parentPosition + position;
        }

        /// <summary>
        /// Solves Kepler's equation M = E - e*sin(E) using Newton-Raphson.
        /// </summary>
        private static float SolveKeplerEquation(float meanAnomaly, float eccentricity, int maxIterations = 10)
        {
            float E = meanAnomaly; // Initial guess
            
            for (int i = 0; i < maxIterations; i++)
            {
                float f = E - eccentricity * math.sin(E) - meanAnomaly;
                float fPrime = 1f - eccentricity * math.cos(E);
                float delta = f / fPrime;
                E -= delta;
                
                if (math.abs(delta) < 1e-6f)
                    break;
            }
            
            return E;
        }

        /// <summary>
        /// Rotates orbital plane coordinates to world space.
        /// </summary>
        private static float3 RotateToWorldSpace(
            float x, float y,
            float argPeriapsis,
            float inclination,
            float longAscNode)
        {
            float cosW = math.cos(argPeriapsis);
            float sinW = math.sin(argPeriapsis);
            float cosI = math.cos(inclination);
            float sinI = math.sin(inclination);
            float cosO = math.cos(longAscNode);
            float sinO = math.sin(longAscNode);
            
            // Rotation matrix elements
            float3 result;
            result.x = (cosW * cosO - sinW * sinO * cosI) * x + 
                      (-sinW * cosO - cosW * sinO * cosI) * y;
            result.y = (cosW * sinO + sinW * cosO * cosI) * x + 
                      (-sinW * sinO + cosW * cosO * cosI) * y;
            result.z = (sinW * sinI) * x + (cosW * sinI) * y;
            
            return result;
        }

        /// <summary>
        /// Gets current orbital phase (0-1).
        /// </summary>
        public static float GetOrbitalPhase(in OrbitalState orbit, uint currentTick)
        {
            float ticksSinceEpoch = currentTick - orbit.EpochTick;
            float phase = ticksSinceEpoch / math.max(1f, orbit.OrbitalPeriod);
            return phase - math.floor(phase);
        }

        /// <summary>
        /// Calculates light intensity at distance from source.
        /// Uses inverse square law.
        /// </summary>
        public static float CalculateLightIntensity(
            in CelestialLightSource source,
            float distance)
        {
            if (source.IsActive == 0 || distance <= 0)
                return 0;
            
            // Inverse square law with minimum threshold
            float intensity = source.Luminosity / (distance * distance);
            
            // Clamp to light radius
            if (distance > source.LightRadius)
                intensity = 0;
            
            return math.saturate(intensity);
        }

        /// <summary>
        /// Checks if a body is eclipsed by another body from a light source.
        /// </summary>
        public static bool IsEclipsed(
            float3 targetPosition,
            float3 lightSourcePosition,
            float3 occluderPosition,
            float occluderRadius,
            out float eclipseFactor)
        {
            eclipseFactor = 1f;
            
            float3 toTarget = targetPosition - lightSourcePosition;
            float3 toOccluder = occluderPosition - lightSourcePosition;
            
            float distToTarget = math.length(toTarget);
            float distToOccluder = math.length(toOccluder);
            
            // Occluder must be between source and target
            if (distToOccluder >= distToTarget)
                return false;
            
            float3 dirToTarget = toTarget / distToTarget;
            
            // Project occluder center onto line to target
            float projection = math.dot(toOccluder, dirToTarget);
            if (projection <= 0)
                return false;
            
            // Closest point on line to occluder center
            float3 closestPoint = lightSourcePosition + dirToTarget * projection;
            float distFromLine = math.length(occluderPosition - closestPoint);
            
            // Calculate apparent radius at target's distance
            float apparentRadius = occluderRadius * (distToTarget / distToOccluder);
            
            if (distFromLine < apparentRadius)
            {
                // Full or partial eclipse
                float overlap = 1f - (distFromLine / apparentRadius);
                eclipseFactor = 1f - math.saturate(overlap);
                return true;
            }
            
            return false;
        }

        /// <summary>
        /// Calculates day/night state from sun position.
        /// </summary>
        public static void UpdateDayNightState(
            ref DayNightState state,
            in DayNightConfig config,
            uint currentTick)
        {
            // Calculate time of day from ticks
            float dayProgress = (currentTick % (uint)config.DayLengthTicks) / config.DayLengthTicks;
            state.DayProgress = dayProgress;
            state.TimeOfDay = dayProgress * 24f;
            
            // Calculate sun altitude (-1 to 1)
            float sunAngle = dayProgress * 2f * math.PI;
            state.SunAltitude = math.sin(sunAngle - math.PI / 2f);
            state.SunAzimuth = sunAngle;
            
            // Determine time phase
            float hour = state.TimeOfDay;
            state.IsDaytime = (byte)(hour >= config.DawnEndHour && hour < config.DuskStartHour ? 1 : 0);
            state.IsTwilight = (byte)((hour >= config.DawnStartHour && hour < config.DawnEndHour) ||
                                       (hour >= config.DuskStartHour && hour < config.DuskEndHour) ? 1 : 0);
            state.IsNight = (byte)(hour < config.DawnStartHour || hour >= config.DuskEndHour ? 1 : 0);
        }

        /// <summary>
        /// Gets ambient light level based on day/night state.
        /// </summary>
        public static float GetAmbientLightLevel(
            in DayNightState state,
            in DayNightConfig config)
        {
            if (state.IsDaytime != 0)
                return config.DayAmbientLight;
            
            if (state.IsNight != 0)
                return config.NightAmbientLight;
            
            // Twilight - interpolate
            float t = state.SunAltitude * 0.5f + 0.5f; // -1 to 1 -> 0 to 1
            return math.lerp(config.NightAmbientLight, config.DayAmbientLight, t);
        }

        /// <summary>
        /// Calculates tidal force from a massive body.
        /// </summary>
        public static float CalculateTidalForce(
            float influencerMass,
            float targetRadius,
            float distance)
        {
            if (distance <= 0)
                return 0;
            
            // Tidal force proportional to mass / distance³
            float force = influencerMass * targetRadius / (distance * distance * distance);
            return force;
        }

        /// <summary>
        /// Gets tidal phase for a given orbital phase.
        /// </summary>
        public static float GetTidalPhase(float orbitalPhase)
        {
            // Two high tides per orbit
            return (orbitalPhase * 2f) % 1f;
        }

        /// <summary>
        /// Calculates orbital velocity at current position.
        /// </summary>
        public static float3 CalculateOrbitalVelocity(
            in OrbitalState orbit,
            float3 currentPosition,
            float3 parentPosition,
            float parentMass)
        {
            float3 relPos = currentPosition - parentPosition;
            float distance = math.length(relPos);
            
            if (distance < 0.001f)
                return float3.zero;
            
            // Vis-viva equation for orbital speed
            float speed = math.sqrt(parentMass * (2f / distance - 1f / orbit.SemiMajorAxis));
            
            // Velocity perpendicular to position
            float3 radial = relPos / distance;
            float3 tangent = math.cross(new float3(0, 1, 0), radial);
            if (math.lengthsq(tangent) < 0.001f)
                tangent = math.cross(new float3(1, 0, 0), radial);
            tangent = math.normalize(tangent);
            
            return tangent * speed;
        }

        /// <summary>
        /// Creates a circular orbit state.
        /// </summary>
        public static OrbitalState CreateCircularOrbit(
            Entity parent,
            float radius,
            float periodTicks,
            float initialPhase = 0)
        {
            return new OrbitalState
            {
                ParentBody = parent,
                SemiMajorAxis = radius,
                Eccentricity = 0,
                Inclination = 0,
                ArgumentOfPeriapsis = 0,
                LongitudeOfAscendingNode = 0,
                OrbitalPeriod = periodTicks,
                CurrentPhase = initialPhase,
                MeanAnomaly = initialPhase * 2f * math.PI,
                EpochTick = 0
            };
        }

        /// <summary>
        /// Creates default day/night config.
        /// </summary>
        public static DayNightConfig CreateDefaultDayNightConfig(float ticksPerDay = 86400)
        {
            return new DayNightConfig
            {
                DayLengthTicks = ticksPerDay,
                DawnStartHour = 5f,
                DawnEndHour = 7f,
                DuskStartHour = 18f,
                DuskEndHour = 20f,
                NightAmbientLight = 0.05f,
                DayAmbientLight = 1f
            };
        }

        /// <summary>
        /// Normalizes angle to 0-2π range.
        /// </summary>
        private static float NormalizeAngle(float angle)
        {
            const float TwoPi = 2f * math.PI;
            angle = angle % TwoPi;
            if (angle < 0)
                angle += TwoPi;
            return angle;
        }

        /// <summary>
        /// Gets star color from spectral class.
        /// </summary>
        public static float3 GetSpectralColor(SpectralClass spectral)
        {
            return spectral switch
            {
                SpectralClass.O => new float3(0.6f, 0.7f, 1.0f),    // Blue
                SpectralClass.B => new float3(0.7f, 0.8f, 1.0f),    // Blue-white
                SpectralClass.A => new float3(0.9f, 0.9f, 1.0f),    // White
                SpectralClass.F => new float3(1.0f, 1.0f, 0.9f),    // Yellow-white
                SpectralClass.G => new float3(1.0f, 1.0f, 0.8f),    // Yellow
                SpectralClass.K => new float3(1.0f, 0.9f, 0.7f),    // Orange
                SpectralClass.M => new float3(1.0f, 0.7f, 0.5f),    // Red
                SpectralClass.L => new float3(0.8f, 0.4f, 0.3f),    // Brown dwarf
                SpectralClass.T => new float3(0.6f, 0.3f, 0.4f),    // Methane dwarf
                SpectralClass.Y => new float3(0.4f, 0.2f, 0.3f),    // Ultra-cool
                _ => new float3(1.0f, 1.0f, 1.0f)
            };
        }

        /// <summary>
        /// Gets approximate temperature from spectral class.
        /// </summary>
        public static float GetSpectralTemperature(SpectralClass spectral)
        {
            return spectral switch
            {
                SpectralClass.O => 40000f,
                SpectralClass.B => 20000f,
                SpectralClass.A => 8750f,
                SpectralClass.F => 6750f,
                SpectralClass.G => 5600f,
                SpectralClass.K => 4450f,
                SpectralClass.M => 3200f,
                SpectralClass.L => 1800f,
                SpectralClass.T => 1000f,
                SpectralClass.Y => 500f,
                _ => 5600f
            };
        }
    }
}

