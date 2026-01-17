using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    public enum MiningOrderSource : byte
    {
        None = 0,
        Scripted = 1,
        Input = 2
    }

    public enum MiningOrderStatus : byte
    {
        None = 0,
        Pending = 1,
        Active = 2,
        Completed = 3
    }

    /// <summary>
    /// Logical mining order assigned to a vessel. Uses registry-friendly resource identifiers.
    /// </summary>
    public struct MiningOrder : IComponentData
    {
        public FixedString64Bytes ResourceId;
        public MiningOrderSource Source;
        public MiningOrderStatus Status;
        public Entity PreferredTarget;
        public Entity TargetEntity;
        public uint IssuedTick;
    }

    public enum MiningPhase : byte
    {
        Idle = 0,
        Undocking = 1,
        ApproachTarget = 2,
        Latching = 3,
        Mining = 4,
        Detaching = 5,
        ReturnApproach = 6,
        Docking = 7
    }

    public enum MiningDecisionReason : byte
    {
        None = 0,
        NoTarget = 1,
        UndockWait = 2,
        NotInRange = 3,
        LatchNotReady = 4,
        Digging = 5,
        DigZero = 6,
        ReturnFull = 7,
        DockingWait = 8,
        NotAligned = 9
    }

    /// <summary>
    /// Tracks the active mining state and cadence for a miner vessel.
    /// </summary>
    public struct MiningState : IComponentData
    {
        public MiningPhase Phase;
        public Entity ActiveTarget;
        public float MiningTimer;
        public float TickInterval;
        public float PhaseTimer;
        public byte InDigRange;
        public float3 DigHeadLocal;
        public float3 DigDirectionLocal;
        public Entity DigVolumeEntity;
        public byte HasDigHead;
        public Entity LatchTarget;
        public int LatchRegionId;
        public float3 LatchSurfacePoint;
        public byte HasLatchPoint;
        public uint LatchSettleUntilTick;
        public uint LastLatchTelemetryTick;
    }

    public struct MiningDecisionTrace : IComponentData
    {
        public MiningDecisionReason Reason;
        public Entity Target;
        public float DistanceToTarget;
        public float RangeThreshold;
        public float Standoff;
        public float ArrivalDistance;
        public float ApproachDistance;
        public byte Aligned;
        public uint Tick;
    }

    public struct Space4XMiningLatchConfig : IComponentData
    {
        public int RegionCount;
        public float SurfaceEpsilon;
        public float AlignDotThreshold;
        public uint SettleTicks;
        public byte ReserveRegionWhileApproaching;
        public uint TelemetrySampleEveryTicks;
        public int TelemetryMaxSamples;

        public static Space4XMiningLatchConfig Default => new Space4XMiningLatchConfig
        {
            RegionCount = Space4XMiningLatchUtility.DefaultLatchRegionCount,
            SurfaceEpsilon = 3.2f, // Match latch readiness to mining standoff + arrival distance.
            AlignDotThreshold = 0.25f,
            SettleTicks = 6u,
            ReserveRegionWhileApproaching = 1,
            TelemetrySampleEveryTicks = 30u,
            TelemetryMaxSamples = 2048
        };
    }

    [InternalBufferCapacity(4)]
    public struct Space4XMiningLatchReservation : IBufferElementData
    {
        public Entity Miner;
        public int RegionId;
        public uint ReservedTick;
    }

    /// <summary>
    /// Accumulates mined output and exposes a spawn trigger for downstream systems.
    /// </summary>
    public struct MiningYield : IComponentData
    {
        public FixedString64Bytes ResourceId;
        public float PendingAmount;
        public float SpawnThreshold;
        public byte SpawnReady;
    }

    /// <summary>
    /// Tracks the current mining target a carrier is scanning or moving toward.
    /// </summary>
    public struct CarrierMiningTarget : IComponentData
    {
        public Entity TargetEntity;
        public float3 TargetPosition;
        public uint AssignedTick;
        public uint NextScanTick;
    }

    public struct MiningApproachTelemetrySample : IBufferElementData
    {
        public uint Tick;
        public Entity Miner;
        public Entity Target;
        public MiningPhase Phase;
        public int LatchRegionId;
        public float DistanceToSurface;
    }

    internal static class Space4XMiningLatchUtility
    {
        public const int DefaultLatchRegionCount = 12;
        public const float MinerAsteroidStandoff = 1.1f;
        public const float DefaultAsteroidStandoff = 5.5f;
        public const float CarrierAsteroidStandoff = 6.5f;
        public const float StandoffPadding = 0.25f;
        public const float MiningRangePadding = 1.4f;
        public const float MiningEnterSlack = 0.50f;
        public const float MiningExitSlack = 0.90f;
        public const float MiningInnerTargetSlack = 0.10f;

        public static float ResolveAsteroidStandoff(bool isMiner, bool isCarrier, float vesselRadius)
        {
            var standoff = isMiner ? MinerAsteroidStandoff : DefaultAsteroidStandoff;
            if (isCarrier)
            {
                standoff = CarrierAsteroidStandoff;
            }

            return math.max(standoff, vesselRadius + StandoffPadding);
        }

        public static float ResolveMiningRangeThreshold(float surfaceEpsilon)
        {
            return math.max(0.05f, surfaceEpsilon) + MiningRangePadding;
        }

        public static float ResolveMiningApproachStandoff(float surfaceEpsilon)
        {
            var rangeThreshold = ResolveMiningRangeThreshold(surfaceEpsilon);
            return math.max(0.05f, rangeThreshold - MiningInnerTargetSlack);
        }

        public static int ComputeLatchRegion(Entity miner, Entity target, uint targetSeed, int regionCount = DefaultLatchRegionCount)
        {
            var count = regionCount > 0 ? regionCount : DefaultLatchRegionCount;
            var seed = math.hash(new uint3((uint)(miner.Index + 1), (uint)(target.Index + 1), targetSeed));
            return (int)(seed % (uint)count);
        }

        public static float3 ComputeSurfaceLatchPoint(float3 center, float radius, int regionId, uint targetSeed)
        {
            radius = math.max(0.5f, radius);
            var seed = math.hash(new uint3((uint)(regionId + 1), targetSeed, math.asuint(radius)));
            seed = seed == 0u ? 1u : seed;
            var random = Unity.Mathematics.Random.CreateFromIndex(seed);
            var direction = random.NextFloat3Direction();
            return center + direction * radius;
        }

        public static float ResolveDistanceToSurface(float3 center, float radius, float3 position)
        {
            radius = math.max(0f, radius);
            var distance = math.distance(position, center);
            return math.abs(distance - radius);
        }

        public static int ResolveReservedLatchRegion(
            Entity miner,
            Entity target,
            uint targetSeed,
            int regionCount,
            uint currentTick,
            ref DynamicBuffer<Space4XMiningLatchReservation> reservations,
            in ComponentLookup<MiningState> miningStateLookup)
        {
            var count = regionCount > 0 ? regionCount : DefaultLatchRegionCount;
            var desiredRegion = ComputeLatchRegion(miner, target, targetSeed, count);
            var existingRegion = -1;

            for (var i = reservations.Length - 1; i >= 0; i--)
            {
                var reservation = reservations[i];
                if (!IsReservationValid(reservation, target, miningStateLookup))
                {
                    reservations.RemoveAt(i);
                    continue;
                }

                if (reservation.Miner == miner)
                {
                    existingRegion = reservation.RegionId;
                    reservation.ReservedTick = currentTick;
                    reservations[i] = reservation;
                }
            }

            if (existingRegion >= 0)
            {
                return existingRegion;
            }

            for (var offset = 0; offset < count; offset++)
            {
                var regionId = (desiredRegion + offset) % count;
                if (!IsRegionReserved(reservations, regionId))
                {
                    reservations.Add(new Space4XMiningLatchReservation
                    {
                        Miner = miner,
                        RegionId = regionId,
                        ReservedTick = currentTick
                    });
                    return regionId;
                }
            }

            reservations.Add(new Space4XMiningLatchReservation
            {
                Miner = miner,
                RegionId = desiredRegion,
                ReservedTick = currentTick
            });
            return desiredRegion;
        }

        public static void ReleaseReservation(Entity miner, ref DynamicBuffer<Space4XMiningLatchReservation> reservations)
        {
            for (var i = reservations.Length - 1; i >= 0; i--)
            {
                if (reservations[i].Miner == miner)
                {
                    reservations.RemoveAt(i);
                }
            }
        }

        public static void UpsertReservation(
            Entity miner,
            int regionId,
            uint currentTick,
            ref DynamicBuffer<Space4XMiningLatchReservation> reservations)
        {
            for (var i = 0; i < reservations.Length; i++)
            {
                if (reservations[i].Miner == miner)
                {
                    var reservation = reservations[i];
                    reservation.RegionId = regionId;
                    reservation.ReservedTick = currentTick;
                    reservations[i] = reservation;
                    return;
                }
            }

            reservations.Add(new Space4XMiningLatchReservation
            {
                Miner = miner,
                RegionId = regionId,
                ReservedTick = currentTick
            });
        }

        private static bool IsReservationValid(
            in Space4XMiningLatchReservation reservation,
            Entity target,
            in ComponentLookup<MiningState> miningStateLookup)
        {
            if (!miningStateLookup.HasComponent(reservation.Miner))
            {
                return false;
            }

            var miningState = miningStateLookup[reservation.Miner];
            if (miningState.Phase == MiningPhase.Idle)
            {
                return false;
            }

            return miningState.ActiveTarget == target;
        }

        private static bool IsRegionReserved(DynamicBuffer<Space4XMiningLatchReservation> reservations, int regionId)
        {
            for (var i = 0; i < reservations.Length; i++)
            {
                if (reservations[i].RegionId == regionId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
