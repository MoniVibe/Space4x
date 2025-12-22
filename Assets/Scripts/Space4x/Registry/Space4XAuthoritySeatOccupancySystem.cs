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
        private FixedString64Bytes _roleCaptain;
        private FixedString64Bytes _roleXo;
        private FixedString64Bytes _roleShipmaster;
        private FixedString64Bytes _roleWeaponsOfficer;
        private FixedString64Bytes _roleSensorsOfficer;
        private FixedString64Bytes _roleLogisticsOfficer;
        private FixedString64Bytes _roleFlightCommander;
        private FixedString64Bytes _roleHangarDeckOfficer;

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

            _roleCaptain = BuildRoleCaptain();
            _roleXo = BuildRoleXo();
            _roleShipmaster = BuildRoleShipmaster();
            _roleWeaponsOfficer = BuildRoleWeaponsOfficer();
            _roleSensorsOfficer = BuildRoleSensorsOfficer();
            _roleLogisticsOfficer = BuildRoleLogisticsOfficer();
            _roleFlightCommander = BuildRoleFlightCommander();
            _roleHangarDeckOfficer = BuildRoleHangarDeckOfficer();
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
        private float ScoreCandidate(in FixedString64Bytes roleId, in IndividualStats stats)
        {
            var command = (float)stats.Command;
            var tactics = (float)stats.Tactics;
            var logistics = (float)stats.Logistics;
            var diplomacy = (float)stats.Diplomacy;
            var engineering = (float)stats.Engineering;
            var resolve = (float)stats.Resolve;

            if (roleId.Equals(_roleCaptain))
            {
                return command * 2.25f + resolve * 1.5f + tactics * 0.5f;
            }

            if (roleId.Equals(_roleXo))
            {
                return command * 1.5f + tactics * 1.25f + resolve;
            }

            if (roleId.Equals(_roleShipmaster))
            {
                return logistics * 1.75f + command + resolve;
            }

            if (roleId.Equals(_roleWeaponsOfficer))
            {
                return tactics * 2.0f + command * 0.5f + resolve * 0.25f;
            }

            if (roleId.Equals(_roleSensorsOfficer))
            {
                return tactics * 1.25f + engineering * 1.25f + diplomacy * 0.5f;
            }

            if (roleId.Equals(_roleLogisticsOfficer))
            {
                return logistics * 2.0f + resolve * 0.5f + command * 0.25f;
            }

            if (roleId.Equals(_roleFlightCommander) || roleId.Equals(_roleHangarDeckOfficer))
            {
                return tactics * 1.25f + command + logistics * 0.75f;
            }

            // Fallback: "most capable generalist".
            return command + tactics + logistics + diplomacy + engineering + resolve;
        }

        private static FixedString64Bytes BuildRoleCaptain()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('c');
            id.Append('a');
            id.Append('p');
            id.Append('t');
            id.Append('a');
            id.Append('i');
            id.Append('n');
            return id;
        }

        private static FixedString64Bytes BuildRoleXo()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('x');
            id.Append('o');
            return id;
        }

        private static FixedString64Bytes BuildRoleShipmaster()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('m');
            id.Append('a');
            id.Append('s');
            id.Append('t');
            id.Append('e');
            id.Append('r');
            return id;
        }

        private static FixedString64Bytes BuildRoleWeaponsOfficer()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('w');
            id.Append('e');
            id.Append('a');
            id.Append('p');
            id.Append('o');
            id.Append('n');
            id.Append('s');
            id.Append('_');
            id.Append('o');
            id.Append('f');
            id.Append('f');
            id.Append('i');
            id.Append('c');
            id.Append('e');
            id.Append('r');
            return id;
        }

        private static FixedString64Bytes BuildRoleSensorsOfficer()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('s');
            id.Append('e');
            id.Append('n');
            id.Append('s');
            id.Append('o');
            id.Append('r');
            id.Append('s');
            id.Append('_');
            id.Append('o');
            id.Append('f');
            id.Append('f');
            id.Append('i');
            id.Append('c');
            id.Append('e');
            id.Append('r');
            return id;
        }

        private static FixedString64Bytes BuildRoleLogisticsOfficer()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('l');
            id.Append('o');
            id.Append('g');
            id.Append('i');
            id.Append('s');
            id.Append('t');
            id.Append('i');
            id.Append('c');
            id.Append('s');
            id.Append('_');
            id.Append('o');
            id.Append('f');
            id.Append('f');
            id.Append('i');
            id.Append('c');
            id.Append('e');
            id.Append('r');
            return id;
        }

        private static FixedString64Bytes BuildRoleFlightCommander()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('f');
            id.Append('l');
            id.Append('i');
            id.Append('g');
            id.Append('h');
            id.Append('t');
            id.Append('_');
            id.Append('c');
            id.Append('o');
            id.Append('m');
            id.Append('m');
            id.Append('a');
            id.Append('n');
            id.Append('d');
            id.Append('e');
            id.Append('r');
            return id;
        }

        private static FixedString64Bytes BuildRoleHangarDeckOfficer()
        {
            var id = new FixedString64Bytes();
            id.Append('s');
            id.Append('h');
            id.Append('i');
            id.Append('p');
            id.Append('.');
            id.Append('h');
            id.Append('a');
            id.Append('n');
            id.Append('g');
            id.Append('a');
            id.Append('r');
            id.Append('_');
            id.Append('d');
            id.Append('e');
            id.Append('c');
            id.Append('k');
            id.Append('_');
            id.Append('o');
            id.Append('f');
            id.Append('f');
            id.Append('i');
            id.Append('c');
            id.Append('e');
            id.Append('r');
            return id;
        }
    }
}
