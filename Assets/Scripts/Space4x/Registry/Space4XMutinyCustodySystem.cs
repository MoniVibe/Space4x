using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// On successful mutiny of a ship-scale aggregate, transition the current command occupants into custody.
    /// This is a first-pass hook; later logic should consult crew doctrine/outlooks and leadership profiles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XMutinySystem))]
    public partial struct Space4XMutinyCustodySystem : ISystem
    {
        private ComponentLookup<CustodyState> _custodyLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _custodyLookup = state.GetComponentLookup<CustodyState>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _custodyLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (mutiny, body, seats, shipEntity) in SystemAPI.Query<
                         RefRO<MutinyState>,
                         RefRO<AuthorityBody>,
                         DynamicBuffer<AuthoritySeatRef>>()
                         .WithAll<CaptainOrder>()
                         .WithNone<MutinyCustodyProcessedTag>()
                         .WithEntityAccess())
            {
                if (mutiny.ValueRO.State != MutinyStateType.Mutiny)
                {
                    continue;
                }

                var severity = mutiny.ValueRO.Severity;
                var captureAll = severity >= 0.8f;
                var captureInnerCircle = severity >= 0.5f;

                var executiveSeat = body.ValueRO.ExecutiveSeat;
                if (executiveSeat != Entity.Null)
                {
                    if (CaptureSeatOccupant(ref state, ref ecb, shipEntity, executiveSeat, CustodyFlags.HighValue, timeState.Tick))
                    {
                        VacateSeat(ref ecb, executiveSeat, timeState.Tick);
                    }
                }

                if (captureAll || captureInnerCircle)
                {
                    for (int i = 0; i < seats.Length; i++)
                    {
                        var seatEntity = seats[i].SeatEntity;
                        if (seatEntity == Entity.Null || seatEntity == executiveSeat)
                        {
                            continue;
                        }

                        if (!_seatLookup.HasComponent(seatEntity))
                        {
                            continue;
                        }

                        var role = _seatLookup[seatEntity].RoleId;
                        var isInnerCircle = role.Equals(new FixedString64Bytes("ship.xo")) ||
                                            role.Equals(new FixedString64Bytes("ship.shipmaster"));
                        if (captureInnerCircle && !captureAll && !isInnerCircle)
                        {
                            continue;
                        }

                        if (CaptureSeatOccupant(ref state, ref ecb, shipEntity, seatEntity, default, timeState.Tick))
                        {
                            VacateSeat(ref ecb, seatEntity, timeState.Tick);
                        }
                    }
                }

                ecb.AddComponent(shipEntity, new MutinyCustodyProcessedTag { Tick = timeState.Tick });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private bool CaptureSeatOccupant(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity captorScope,
            Entity seatEntity,
            CustodyFlags flags,
            uint tick)
        {
            if (!_seatOccupantLookup.HasComponent(seatEntity))
            {
                return false;
            }

            var occupant = _seatOccupantLookup[seatEntity].OccupantEntity;
            if (occupant == Entity.Null || !state.EntityManager.Exists(occupant))
            {
                return false;
            }

            if (_custodyLookup.HasComponent(occupant) || state.EntityManager.HasComponent<CustodyState>(occupant))
            {
                return true;
            }

            ecb.AddComponent(occupant, new CustodyState
            {
                Kind = CustodyKind.PoliticalPrisoner,
                Status = CustodyStatus.Detained,
                Flags = flags,
                CaptorScope = captorScope,
                HoldingEntity = captorScope,
                OriginalAffiliation = Entity.Null,
                CapturedTick = tick,
                LastStatusTick = tick,
                IssuedByAuthority = new IssuedByAuthority
                {
                    IssuingSeat = Entity.Null,
                    IssuingOccupant = Entity.Null,
                    ActingSeat = Entity.Null,
                    ActingOccupant = Entity.Null,
                    IssuedTick = tick
                }
            });
            return true;
        }

        private void VacateSeat(ref EntityCommandBuffer ecb, Entity seatEntity, uint tick)
        {
            ecb.SetComponent(seatEntity, new AuthoritySeatOccupant
            {
                OccupantEntity = Entity.Null,
                AssignedTick = tick,
                LastChangedTick = tick,
                IsActing = 0,
                Reserved0 = 0,
                Reserved1 = 0
            });
        }
    }

    /// <summary>
    /// Tag to ensure mutiny custody processing happens only once per ship mutiny event.
    /// </summary>
    public struct MutinyCustodyProcessedTag : IComponentData
    {
        public uint Tick;
    }
}
