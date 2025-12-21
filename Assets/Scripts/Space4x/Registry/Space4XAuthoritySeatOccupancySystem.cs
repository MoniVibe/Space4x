using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Social;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Fills vacant ship authority seats from the onboard named-crew pool.
    /// This enables mutiny succession (vacated captain seat -> promoted replacement) and stable authority attribution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XMutinyCustodySystem))]
    [UpdateBefore(typeof(Space4XIssuedByAuthorityStampSystem))]
    public partial struct Space4XAuthoritySeatOccupancySystem : ISystem
    {
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private ComponentLookup<CustodyState> _custodyLookup;
        private ComponentLookup<IndividualStats> _statsLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<CaptainOrder>();

            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _custodyLookup = state.GetComponentLookup<CustodyState>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _custodyLookup.Update(ref state);
            _statsLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (body, seats, crew, shipEntity) in SystemAPI.Query<
                         RefRO<AuthorityBody>,
                         DynamicBuffer<AuthoritySeatRef>,
                         DynamicBuffer<PlatformCrewMember>>()
                         .WithAll<CaptainOrder>()
                         .WithEntityAccess())
            {
                if (seats.Length == 0 || crew.Length == 0)
                {
                    continue;
                }

                var used = new NativeHashSet<Entity>(math.max(crew.Length, 8), Allocator.Temp);

                // Preserve existing occupants and avoid double-assignment.
                for (int i = 0; i < seats.Length; i++)
                {
                    var seatEntity = seats[i].SeatEntity;
                    if (seatEntity == Entity.Null || !_seatOccupantLookup.HasComponent(seatEntity))
                    {
                        continue;
                    }

                    var occupant = _seatOccupantLookup[seatEntity].OccupantEntity;
                    if (occupant != Entity.Null)
                    {
                        used.Add(occupant);
                    }
                }

                // Fill seats in buffer order (captain-first by bootstrap).
                for (int i = 0; i < seats.Length; i++)
                {
                    var seatEntity = seats[i].SeatEntity;
                    if (seatEntity == Entity.Null ||
                        !_seatLookup.HasComponent(seatEntity) ||
                        !_seatOccupantLookup.HasComponent(seatEntity))
                    {
                        continue;
                    }

                    var occupant = _seatOccupantLookup[seatEntity].OccupantEntity;
                    if (occupant != Entity.Null)
                    {
                        continue;
                    }

                    var roleId = _seatLookup[seatEntity].RoleId;
                    if (!TrySelectCandidate(state.EntityManager, crew, used, roleId, out var candidate))
                    {
                        continue;
                    }

                    used.Add(candidate);
                    ecb.SetComponent(seatEntity, new AuthoritySeatOccupant
                    {
                        OccupantEntity = candidate,
                        AssignedTick = timeState.Tick,
                        LastChangedTick = timeState.Tick,
                        IsActing = 0,
                        Reserved0 = 0,
                        Reserved1 = 0
                    });

                    if (seatEntity == body.ValueRO.ExecutiveSeat)
                    {
                        // Ensure the captain order authority stays bound to the captain seat (not the occupant entity).
                        var order = SystemAPI.GetComponent<CaptainOrder>(shipEntity);
                        if (order.IssuingAuthority == Entity.Null)
                        {
                            order.IssuingAuthority = seatEntity;
                            ecb.SetComponent(shipEntity, order);
                        }
                    }
                }

                used.Dispose();
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool TrySelectCandidate(
            EntityManager entityManager,
            DynamicBuffer<PlatformCrewMember> crew,
            NativeHashSet<Entity> used,
            in FixedString64Bytes roleId,
            out Entity chosen)
        {
            chosen = Entity.Null;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < crew.Length; i++)
            {
                var candidate = crew[i].CrewEntity;
                if (candidate == Entity.Null || used.Contains(candidate))
                {
                    continue;
                }

                if (!entityManager.Exists(candidate))
                {
                    continue;
                }

                if (_custodyLookup.HasComponent(candidate))
                {
                    continue;
                }

                float score = 0f;
                if (_statsLookup.HasComponent(candidate))
                {
                    score = ScoreCandidate(roleId, _statsLookup[candidate]);
                }

                if (score > bestScore || (math.abs(score - bestScore) < 0.0001f && candidate.Index < chosen.Index))
                {
                    bestScore = score;
                    chosen = candidate;
                }
            }

            return chosen != Entity.Null;
        }

        [BurstCompile]
        private static float ScoreCandidate(in FixedString64Bytes roleId, in IndividualStats stats)
        {
            var command = (float)stats.Command;
            var tactics = (float)stats.Tactics;
            var logistics = (float)stats.Logistics;
            var diplomacy = (float)stats.Diplomacy;
            var engineering = (float)stats.Engineering;
            var resolve = (float)stats.Resolve;

            if (roleId.Equals(new FixedString64Bytes("ship.captain")))
            {
                return command * 2.25f + resolve * 1.5f + tactics * 0.5f;
            }

            if (roleId.Equals(new FixedString64Bytes("ship.xo")))
            {
                return command * 1.5f + tactics * 1.25f + resolve;
            }

            if (roleId.Equals(new FixedString64Bytes("ship.shipmaster")))
            {
                return logistics * 1.75f + command + resolve;
            }

            if (roleId.Equals(new FixedString64Bytes("ship.weapons_officer")))
            {
                return tactics * 2.0f + command * 0.5f + resolve * 0.25f;
            }

            if (roleId.Equals(new FixedString64Bytes("ship.sensors_officer")))
            {
                return tactics * 1.25f + engineering * 1.25f + diplomacy * 0.5f;
            }

            if (roleId.Equals(new FixedString64Bytes("ship.logistics_officer")))
            {
                return logistics * 2.0f + resolve * 0.5f + command * 0.25f;
            }

            if (roleId.Equals(new FixedString64Bytes("ship.flight_commander")) ||
                roleId.Equals(new FixedString64Bytes("ship.hangar_deck_officer")))
            {
                return tactics * 1.25f + command + logistics * 0.75f;
            }

            // Fallback: "most capable generalist".
            return command + tactics + logistics + diplomacy + engineering + resolve;
        }
    }
}
