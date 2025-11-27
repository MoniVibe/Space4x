using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Warp-in tactical style based on outlook and doctrine.
    /// </summary>
    public enum ReinforcementTactic : byte
    {
        /// <summary>
        /// Default arrival - standard safe distance.
        /// </summary>
        Standard = 0,

        /// <summary>
        /// Close-range drop-in, attempt to flank or encircle.
        /// Chaotic/Warlike captains prefer this.
        /// </summary>
        Flanking = 1,

        /// <summary>
        /// Arrive at standoff range, form protective screen.
        /// Lawful/Defensive captains prefer this.
        /// </summary>
        DefensiveScreen = 2,

        /// <summary>
        /// Aggressive close drop directly into enemy formation.
        /// High risk, high reward for experienced crews.
        /// </summary>
        AggressiveDrop = 3,

        /// <summary>
        /// Arrive at max range, approach cautiously.
        /// Conservative captains or damaged fleets.
        /// </summary>
        CautiousApproach = 4,

        /// <summary>
        /// Scattered arrival for unpredictability.
        /// Low-tech or chaotic fleets.
        /// </summary>
        Scattered = 5,

        /// <summary>
        /// Precise coordinated arrival in formation.
        /// High-tech lawful fleets.
        /// </summary>
        CoordinatedFormation = 6,

        /// <summary>
        /// Arrive behind enemy lines for rear attacks.
        /// Requires high warp precision.
        /// </summary>
        RearAssault = 7
    }

    /// <summary>
    /// Formation type for arrival.
    /// </summary>
    public enum ArrivalFormation : byte
    {
        None = 0,
        Line = 1,
        Wedge = 2,
        Circle = 3,
        Wall = 4,
        Scattered = 5,
        Custom = 6
    }

    /// <summary>
    /// Tech level affecting warp precision.
    /// </summary>
    public enum WarpTechTier : byte
    {
        /// <summary>
        /// Primitive - significant scatter, poor orientation control.
        /// </summary>
        Primitive = 0,

        /// <summary>
        /// Basic - moderate scatter, basic orientation.
        /// </summary>
        Basic = 1,

        /// <summary>
        /// Standard - minor scatter, good orientation.
        /// </summary>
        Standard = 2,

        /// <summary>
        /// Advanced - minimal scatter, precise orientation.
        /// </summary>
        Advanced = 3,

        /// <summary>
        /// Experimental - pinpoint accuracy, perfect orientation.
        /// </summary>
        Experimental = 4
    }

    /// <summary>
    /// Reinforcement tactics configuration for a fleet/vessel.
    /// </summary>
    public struct ReinforcementTactics : IComponentData
    {
        /// <summary>
        /// Preferred warp-in tactic.
        /// </summary>
        public ReinforcementTactic PreferredTactic;

        /// <summary>
        /// Fallback tactic if preferred is not possible.
        /// </summary>
        public ReinforcementTactic FallbackTactic;

        /// <summary>
        /// Arrival formation.
        /// </summary>
        public ArrivalFormation Formation;

        /// <summary>
        /// Preferred standoff distance after arrival.
        /// </summary>
        public float StandoffDistance;

        /// <summary>
        /// How aggressive to be with close drops [0, 1].
        /// </summary>
        public half Aggression;

        /// <summary>
        /// Whether to coordinate arrival with other reinforcing units.
        /// </summary>
        public byte CoordinateArrival;

        /// <summary>
        /// Delay between coordinated arrivals (ticks).
        /// </summary>
        public ushort CoordinationDelay;

        public static ReinforcementTactics Flanking => new ReinforcementTactics
        {
            PreferredTactic = ReinforcementTactic.Flanking,
            FallbackTactic = ReinforcementTactic.Standard,
            Formation = ArrivalFormation.Wedge,
            StandoffDistance = 100f,
            Aggression = (half)0.7f,
            CoordinateArrival = 0,
            CoordinationDelay = 0
        };

        public static ReinforcementTactics DefensiveScreen => new ReinforcementTactics
        {
            PreferredTactic = ReinforcementTactic.DefensiveScreen,
            FallbackTactic = ReinforcementTactic.CautiousApproach,
            Formation = ArrivalFormation.Wall,
            StandoffDistance = 500f,
            Aggression = (half)0.2f,
            CoordinateArrival = 1,
            CoordinationDelay = 5
        };

        public static ReinforcementTactics Aggressive => new ReinforcementTactics
        {
            PreferredTactic = ReinforcementTactic.AggressiveDrop,
            FallbackTactic = ReinforcementTactic.Flanking,
            Formation = ArrivalFormation.Wedge,
            StandoffDistance = 50f,
            Aggression = (half)0.9f,
            CoordinateArrival = 0,
            CoordinationDelay = 0
        };

        public static ReinforcementTactics Cautious => new ReinforcementTactics
        {
            PreferredTactic = ReinforcementTactic.CautiousApproach,
            FallbackTactic = ReinforcementTactic.DefensiveScreen,
            Formation = ArrivalFormation.Line,
            StandoffDistance = 800f,
            Aggression = (half)0.1f,
            CoordinateArrival = 1,
            CoordinationDelay = 10
        };

        public static ReinforcementTactics Coordinated => new ReinforcementTactics
        {
            PreferredTactic = ReinforcementTactic.CoordinatedFormation,
            FallbackTactic = ReinforcementTactic.Standard,
            Formation = ArrivalFormation.Line,
            StandoffDistance = 300f,
            Aggression = (half)0.5f,
            CoordinateArrival = 1,
            CoordinationDelay = 3
        };
    }

    /// <summary>
    /// Warp precision based on tech level.
    /// </summary>
    public struct WarpPrecision : IComponentData
    {
        /// <summary>
        /// Current warp tech tier.
        /// </summary>
        public WarpTechTier TechTier;

        /// <summary>
        /// Position scatter radius in units.
        /// </summary>
        public float PositionScatter;

        /// <summary>
        /// Orientation scatter in degrees.
        /// </summary>
        public half OrientationScatter;

        /// <summary>
        /// Arrival timing scatter in ticks.
        /// </summary>
        public ushort TimingScatter;

        /// <summary>
        /// Minimum safe distance from objects.
        /// </summary>
        public float MinSafeDistance;

        public static WarpPrecision FromTier(WarpTechTier tier)
        {
            return tier switch
            {
                WarpTechTier.Primitive => new WarpPrecision
                {
                    TechTier = tier,
                    PositionScatter = 200f,
                    OrientationScatter = (half)45f,
                    TimingScatter = 30,
                    MinSafeDistance = 300f
                },
                WarpTechTier.Basic => new WarpPrecision
                {
                    TechTier = tier,
                    PositionScatter = 100f,
                    OrientationScatter = (half)25f,
                    TimingScatter = 15,
                    MinSafeDistance = 200f
                },
                WarpTechTier.Standard => new WarpPrecision
                {
                    TechTier = tier,
                    PositionScatter = 50f,
                    OrientationScatter = (half)10f,
                    TimingScatter = 5,
                    MinSafeDistance = 100f
                },
                WarpTechTier.Advanced => new WarpPrecision
                {
                    TechTier = tier,
                    PositionScatter = 20f,
                    OrientationScatter = (half)3f,
                    TimingScatter = 2,
                    MinSafeDistance = 50f
                },
                WarpTechTier.Experimental => new WarpPrecision
                {
                    TechTier = tier,
                    PositionScatter = 5f,
                    OrientationScatter = (half)1f,
                    TimingScatter = 0,
                    MinSafeDistance = 20f
                },
                _ => new WarpPrecision
                {
                    TechTier = WarpTechTier.Standard,
                    PositionScatter = 50f,
                    OrientationScatter = (half)10f,
                    TimingScatter = 5,
                    MinSafeDistance = 100f
                }
            };
        }
    }

    /// <summary>
    /// Incoming reinforcement request.
    /// </summary>
    public struct ReinforcementRequest : IComponentData
    {
        /// <summary>
        /// Entity requesting reinforcement.
        /// </summary>
        public Entity RequestingEntity;

        /// <summary>
        /// Position to reinforce.
        /// </summary>
        public float3 TargetPosition;

        /// <summary>
        /// Direction to face on arrival.
        /// </summary>
        public float3 FacingDirection;

        /// <summary>
        /// Enemy center of mass (for flanking calculations).
        /// </summary>
        public float3 EnemyCenter;

        /// <summary>
        /// Urgency level [0, 1].
        /// </summary>
        public half Urgency;

        /// <summary>
        /// Tick when request was made.
        /// </summary>
        public uint RequestTick;

        /// <summary>
        /// Whether request has been acknowledged.
        /// </summary>
        public byte Acknowledged;

        /// <summary>
        /// Expected arrival tick.
        /// </summary>
        public uint ExpectedArrivalTick;
    }

    /// <summary>
    /// Calculated arrival position and orientation.
    /// </summary>
    public struct ReinforcementArrival : IComponentData
    {
        /// <summary>
        /// Calculated arrival position.
        /// </summary>
        public float3 ArrivalPosition;

        /// <summary>
        /// Calculated arrival orientation (facing direction).
        /// </summary>
        public quaternion ArrivalRotation;

        /// <summary>
        /// Arrival tick (with timing scatter applied).
        /// </summary>
        public uint ArrivalTick;

        /// <summary>
        /// Tactic being used.
        /// </summary>
        public ReinforcementTactic UsedTactic;

        /// <summary>
        /// Formation slot index (if coordinated).
        /// </summary>
        public byte FormationSlot;

        /// <summary>
        /// Whether arrival has been executed.
        /// </summary>
        public byte HasArrived;
    }

    /// <summary>
    /// Group coordination for synchronized reinforcement arrivals.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ReinforcementGroup : IBufferElementData
    {
        /// <summary>
        /// Entity in this reinforcement group.
        /// </summary>
        public Entity Entity;

        /// <summary>
        /// Formation slot in group.
        /// </summary>
        public byte Slot;

        /// <summary>
        /// Relative arrival delay (ticks).
        /// </summary>
        public ushort ArrivalDelay;

        /// <summary>
        /// Whether this entity is ready.
        /// </summary>
        public byte IsReady;
    }

    /// <summary>
    /// Utility functions for reinforcement calculations.
    /// </summary>
    public static class ReinforcementUtility
    {
        /// <summary>
        /// Calculates reinforcement tactics based on alignment and outlook.
        /// </summary>
        public static ReinforcementTactics TacticsFromAlignment(in AlignmentTriplet alignment, WarpTechTier techTier)
        {
            float law = (float)alignment.Law;
            float good = (float)alignment.Good;

            // Chaotic + aggressive = flanking/aggressive
            if (law < -0.3f)
            {
                if (good < -0.2f) // Chaotic Evil
                {
                    return ReinforcementTactics.Aggressive;
                }
                else // Chaotic Good/Neutral
                {
                    return ReinforcementTactics.Flanking;
                }
            }

            // Lawful = defensive/coordinated
            if (law > 0.3f)
            {
                if (good > 0.2f) // Lawful Good
                {
                    return ReinforcementTactics.DefensiveScreen;
                }
                else if (techTier >= WarpTechTier.Advanced)
                {
                    return ReinforcementTactics.Coordinated;
                }
                else
                {
                    return ReinforcementTactics.DefensiveScreen;
                }
            }

            // Neutral = cautious or standard based on tech
            if (techTier >= WarpTechTier.Standard)
            {
                return ReinforcementTactics.Coordinated;
            }
            else
            {
                return ReinforcementTactics.Cautious;
            }
        }

        /// <summary>
        /// Calculates flanking position relative to enemy center.
        /// </summary>
        public static float3 CalculateFlankingPosition(
            float3 allyCenter,
            float3 enemyCenter,
            float flankAngle,
            float distance)
        {
            float3 toEnemy = math.normalize(enemyCenter - allyCenter);
            float3 up = new float3(0, 1, 0);
            float3 right = math.cross(up, toEnemy);

            // Rotate around enemy position
            float angleRad = math.radians(flankAngle);
            float3 offset = right * math.sin(angleRad) + toEnemy * math.cos(angleRad);

            return enemyCenter + offset * distance;
        }

        /// <summary>
        /// Calculates defensive screen position between allies and enemies.
        /// </summary>
        public static float3 CalculateScreenPosition(
            float3 allyCenter,
            float3 enemyCenter,
            float standoffRatio)
        {
            return math.lerp(allyCenter, enemyCenter, standoffRatio);
        }

        /// <summary>
        /// Applies scatter based on warp precision.
        /// </summary>
        public static float3 ApplyPositionScatter(float3 position, float scatter, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            float3 offset = new float3(
                random.NextFloat(-scatter, scatter),
                random.NextFloat(-scatter * 0.2f, scatter * 0.2f), // Less vertical scatter
                random.NextFloat(-scatter, scatter)
            );
            return position + offset;
        }

        /// <summary>
        /// Applies orientation scatter.
        /// </summary>
        public static quaternion ApplyOrientationScatter(quaternion rotation, float scatterDegrees, uint seed)
        {
            var random = new Unity.Mathematics.Random(seed);
            float3 scatterAxis = random.NextFloat3Direction();
            float scatterAngle = random.NextFloat(-scatterDegrees, scatterDegrees);
            quaternion scatterRotation = quaternion.AxisAngle(scatterAxis, math.radians(scatterAngle));
            return math.mul(scatterRotation, rotation);
        }

        /// <summary>
        /// Calculates formation positions for arrival group.
        /// </summary>
        public static float3 CalculateFormationOffset(ArrivalFormation formation, int slot, int totalSlots, float spacing)
        {
            switch (formation)
            {
                case ArrivalFormation.Line:
                    int halfSlots = totalSlots / 2;
                    return new float3((slot - halfSlots) * spacing, 0, 0);

                case ArrivalFormation.Wedge:
                    float wedgeDepth = slot * 0.5f * spacing;
                    float wedgeWidth = slot * spacing;
                    int side = slot % 2 == 0 ? 1 : -1;
                    return new float3(wedgeWidth * side * 0.5f, 0, -wedgeDepth);

                case ArrivalFormation.Circle:
                    float angle = (2f * math.PI / totalSlots) * slot;
                    float radius = spacing * totalSlots / (2f * math.PI);
                    return new float3(math.cos(angle) * radius, 0, math.sin(angle) * radius);

                case ArrivalFormation.Wall:
                    int row = slot / 4;
                    int col = slot % 4;
                    return new float3((col - 1.5f) * spacing, 0, row * spacing * 0.5f);

                case ArrivalFormation.Scattered:
                    var random = new Unity.Mathematics.Random((uint)(slot + 1) * 12345);
                    return new float3(
                        random.NextFloat(-spacing * 2, spacing * 2),
                        0,
                        random.NextFloat(-spacing * 2, spacing * 2)
                    );

                default:
                    return float3.zero;
            }
        }
    }
}

