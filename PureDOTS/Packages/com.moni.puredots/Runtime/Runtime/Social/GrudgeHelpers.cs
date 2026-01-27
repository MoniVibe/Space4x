using Unity.Mathematics;
using Unity.Entities;
using Unity.Burst;

namespace PureDOTS.Runtime.Social
{
    /// <summary>
    /// Static helpers for grudge calculations.
    /// </summary>
    [BurstCompile]
    public static class GrudgeHelpers
    {
        /// <summary>
        /// Default grudge configuration.
        /// </summary>
        public static GrudgeConfig DefaultConfig => new GrudgeConfig
        {
            DecayRatePerDay = 0.5f,
            MinIntensityForAction = 50,
            VendettaThreshold = 75,
            InheritanceDecay = 0.5f,
            AllowForgiveness = true,
            TicksPerDay = 86400 // Assuming 1 tick per second
        };

        /// <summary>
        /// Gets severity from intensity.
        /// </summary>
        public static GrudgeSeverity GetSeverity(byte intensity)
        {
            if (intensity == 0) return GrudgeSeverity.Forgotten;
            if (intensity <= 25) return GrudgeSeverity.Minor;
            if (intensity <= 50) return GrudgeSeverity.Moderate;
            if (intensity <= 75) return GrudgeSeverity.Serious;
            return GrudgeSeverity.Vendetta;
        }

        /// <summary>
        /// Gets base intensity for a grudge type.
        /// </summary>
        public static byte GetBaseIntensity(GrudgeType type)
        {
            return type switch
            {
                GrudgeType.Insult => 15,
                GrudgeType.Theft => 35,
                GrudgeType.Assault => 50,
                GrudgeType.Betrayal => 65,
                GrudgeType.Murder => 100,
                GrudgeType.Humiliation => 25,
                GrudgeType.Abandonment => 40,
                GrudgeType.Demotion => 30,
                GrudgeType.Sabotage => 45,
                GrudgeType.CreditStolen => 35,
                GrudgeType.Exploitation => 40,
                GrudgeType.Blackmail => 55,
                GrudgeType.WarCrime => 90,
                GrudgeType.TerritoryLoss => 60,
                GrudgeType.EconomicHarm => 45,
                GrudgeType.CulturalOffense => 35,
                GrudgeType.Genocide => 100,
                _ => 20
            };
        }

        /// <summary>
        /// Adds or intensifies a grudge.
        /// </summary>
        public static void AddGrudge(
            ref DynamicBuffer<EntityGrudge> buffer,
            Entity offender,
            GrudgeType type,
            byte baseIntensity,
            uint currentTick,
            bool isPublic = false,
            bool isInherited = false)
        {
            // Check if grudge against this entity already exists
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].OffenderEntity == offender)
                {
                    // Intensify existing grudge
                    var existing = buffer[i];
                    existing.Intensity = (byte)math.min(100, existing.Intensity + baseIntensity / 2);
                    existing.LastRenewedTick = currentTick;
                    existing.Severity = GetSeverity(existing.Intensity);
                    
                    // Upgrade type if new offense is worse
                    if (GetBaseIntensity(type) > GetBaseIntensity(existing.Type))
                    {
                        existing.Type = type;
                    }
                    
                    buffer[i] = existing;
                    return;
                }
            }

            // Add new grudge
            buffer.Add(new EntityGrudge
            {
                OffenderEntity = offender,
                Type = type,
                Intensity = baseIntensity,
                Severity = GetSeverity(baseIntensity),
                OriginTick = currentTick,
                LastRenewedTick = currentTick,
                IsInherited = isInherited,
                IsPublic = isPublic
            });
        }

        /// <summary>
        /// Renews/intensifies an existing grudge.
        /// </summary>
        public static void RenewGrudge(ref EntityGrudge grudge, byte additionalIntensity, uint currentTick)
        {
            grudge.Intensity = (byte)math.min(100, grudge.Intensity + additionalIntensity);
            grudge.LastRenewedTick = currentTick;
            grudge.Severity = GetSeverity(grudge.Intensity);
        }

        /// <summary>
        /// Checks if entity should seek revenge.
        /// </summary>
        public static bool ShouldSeekRevenge(in EntityGrudge grudge, in GrudgeBehavior behavior)
        {
            if (!behavior.SeeksRevenge) return false;
            return grudge.Intensity >= behavior.RevengeThreshold;
        }

        /// <summary>
        /// Gets cooperation penalty from grudge intensity.
        /// </summary>
        public static float GetCooperationPenalty(byte intensity)
        {
            // 0 intensity = 0% penalty, 100 intensity = -100% cooperation
            return -(intensity / 100f);
        }

        /// <summary>
        /// Gets combat target priority bonus from grudge.
        /// </summary>
        public static float GetCombatTargetPriority(byte intensity)
        {
            // Higher intensity = more likely to target
            return intensity * 0.5f; // Max +50 priority
        }

        /// <summary>
        /// Calculates grudge decay for a time period.
        /// </summary>
        public static byte ApplyDecay(
            byte currentIntensity,
            uint ticksSinceRenewal,
            in GrudgeConfig config,
            in GrudgeBehavior behavior)
        {
            if (currentIntensity == 0) return 0;
            
            // Calculate days elapsed
            float daysElapsed = ticksSinceRenewal / (float)config.TicksPerDay;
            
            // Apply forgiveness modifier (higher forgiveness = faster decay)
            float decayRate = config.DecayRatePerDay * (behavior.Forgiveness / 50f);
            
            // Apply memory strength (higher memory = slower decay)
            decayRate *= (1f - behavior.MemoryStrength / 200f);
            
            float decay = daysElapsed * decayRate;
            return (byte)math.max(0, currentIntensity - (int)decay);
        }

        /// <summary>
        /// Finds grudge against a specific entity.
        /// </summary>
        public static bool TryFindGrudge(in DynamicBuffer<EntityGrudge> buffer, Entity offender, out EntityGrudge grudge)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].OffenderEntity == offender)
                {
                    grudge = buffer[i];
                    return true;
                }
            }
            grudge = default;
            return false;
        }

        /// <summary>
        /// Gets the strongest grudge.
        /// </summary>
        public static bool TryGetStrongestGrudge(in DynamicBuffer<EntityGrudge> buffer, out EntityGrudge strongest)
        {
            strongest = default;
            byte maxIntensity = 0;
            
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Intensity > maxIntensity)
                {
                    maxIntensity = buffer[i].Intensity;
                    strongest = buffer[i];
                }
            }
            
            return maxIntensity > 0;
        }

        /// <summary>
        /// Removes a grudge (forgiveness).
        /// </summary>
        public static bool RemoveGrudge(ref DynamicBuffer<EntityGrudge> buffer, Entity offender)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].OffenderEntity == offender)
                {
                    buffer.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Creates inherited grudge for offspring/faction member.
        /// </summary>
        public static EntityGrudge CreateInheritedGrudge(in EntityGrudge original, uint currentTick, in GrudgeConfig config)
        {
            byte inheritedIntensity = (byte)(original.Intensity * config.InheritanceDecay);
            
            return new EntityGrudge
            {
                OffenderEntity = original.OffenderEntity,
                Type = original.Type,
                Intensity = inheritedIntensity,
                Severity = GetSeverity(inheritedIntensity),
                OriginTick = currentTick,
                LastRenewedTick = currentTick,
                IsInherited = true,
                IsPublic = original.IsPublic
            };
        }

        /// <summary>
        /// Creates default grudge behavior.
        /// </summary>
        public static GrudgeBehavior CreateDefaultBehavior()
        {
            return new GrudgeBehavior
            {
                Vengefulness = 50,
                Forgiveness = 50,
                SeeksRevenge = false,
                RevengeThreshold = 75,
                MemoryStrength = 50
            };
        }
    }
}

