using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Ensures a reusable AuthorityBody + seat hierarchy exists for ship-scale aggregates.
    /// Seats are separate entities; captain orders can reference the captain seat for stable attribution.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XAuthoritySeatBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainOrder>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (order, entity) in SystemAPI.Query<RefRO<CaptainOrder>>().WithEntityAccess())
            {
                EnsureShipAuthority(entityManager, entity, order.ValueRO, ref ecb);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private static void EnsureShipAuthority(
            EntityManager entityManager,
            Entity shipEntity,
            in CaptainOrder order,
            ref EntityCommandBuffer ecb)
        {
            var seatCount = 0;
            if (entityManager.HasBuffer<AuthoritySeatRef>(shipEntity))
            {
                seatCount = entityManager.GetBuffer<AuthoritySeatRef>(shipEntity).Length;
            }
            else
            {
                ecb.AddBuffer<AuthoritySeatRef>(shipEntity);
            }

            var hasBody = entityManager.HasComponent<AuthorityBody>(shipEntity);
            if (seatCount > 0)
            {
                if (!hasBody)
                {
                    var seats = entityManager.GetBuffer<AuthoritySeatRef>(shipEntity);
                    var executiveSeat = seats[0].SeatEntity;
                    for (int i = 0; i < seats.Length; i++)
                    {
                        var candidate = seats[i].SeatEntity;
                        if (candidate != Entity.Null &&
                            entityManager.HasComponent<AuthoritySeat>(candidate) &&
                            entityManager.GetComponentData<AuthoritySeat>(candidate).IsExecutive != 0)
                        {
                            executiveSeat = candidate;
                            break;
                        }
                    }

                    ecb.AddComponent(shipEntity, new AuthorityBody
                    {
                        Mode = AuthorityBodyMode.SingleExecutive,
                        ExecutiveSeat = executiveSeat,
                        CreatedTick = 0u
                    });
                }

                if (!entityManager.HasComponent<IssuedByAuthority>(shipEntity))
                {
                    ecb.AddComponent(shipEntity, new IssuedByAuthority
                    {
                        IssuingSeat = Entity.Null,
                        IssuingOccupant = Entity.Null,
                        ActingSeat = Entity.Null,
                        ActingOccupant = Entity.Null,
                        IssuedTick = 0u
                    });
                }

                return;
            }

            var captainSeat = ecb.CreateEntity();
            ecb.AddComponent(captainSeat, AuthoritySeatDefaults.CreateExecutive(
                shipEntity,
                new FixedString64Bytes("ship.captain"),
                AgencyDomain.Governance | AgencyDomain.Sensors | AgencyDomain.Logistics | AgencyDomain.FlightOps | AgencyDomain.Combat));
            ecb.AddComponent(captainSeat, AuthoritySeatDefaults.Vacant(0u));
            var delegations = ecb.AddBuffer<AuthorityDelegation>(captainSeat);

            var xoSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.xo", AgencyDomain.Governance);
            var shipmasterSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.shipmaster", AgencyDomain.Governance);
            var fleetAdmiralSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.fleet_admiral",
                AgencyDomain.Governance | AgencyDomain.Combat | AgencyDomain.Logistics | AgencyDomain.Sensors |
                AgencyDomain.FlightOps | AgencyDomain.Communications);
            var navigationSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.navigation_officer",
                AgencyDomain.Movement | AgencyDomain.Sensors);
            var weaponsSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.weapons_officer", AgencyDomain.Combat);
            var sensorsSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.sensors_officer", AgencyDomain.Sensors);
            var commsSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.communications_officer",
                AgencyDomain.Communications | AgencyDomain.Sensors);
            var logisticsSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.logistics_officer", AgencyDomain.Logistics);
            var engineeringSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.chief_engineer",
                AgencyDomain.Construction | AgencyDomain.Logistics | AgencyDomain.Work);
            var securitySeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.security_officer",
                AgencyDomain.Security | AgencyDomain.Combat);
            var marineCommanderSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.marine_commander",
                AgencyDomain.Security | AgencyDomain.Combat);
            var marineSergeantSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.marine_sergeant",
                AgencyDomain.Security | AgencyDomain.Combat);
            var flightOpsSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.flight_commander", AgencyDomain.FlightOps);
            var flightDirectorSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.flight_director",
                AgencyDomain.FlightOps | AgencyDomain.Communications);
            var hangarSeat = CreateDelegateSeat(ref ecb, shipEntity, "ship.hangar_deck_officer", AgencyDomain.FlightOps);

            delegations.Add(BuildDelegation(shipmasterSeat, AgencyDomain.Governance, AuthorityAttributionMode.AsPrincipalSeat));
            delegations.Add(BuildDelegation(xoSeat, AgencyDomain.Governance));
            delegations.Add(BuildDelegation(fleetAdmiralSeat,
                AgencyDomain.Governance | AgencyDomain.Combat | AgencyDomain.Logistics | AgencyDomain.Sensors |
                AgencyDomain.FlightOps | AgencyDomain.Communications));
            delegations.Add(BuildDelegation(navigationSeat, AgencyDomain.Movement | AgencyDomain.Sensors));
            delegations.Add(BuildDelegation(weaponsSeat, AgencyDomain.Combat));
            delegations.Add(BuildDelegation(sensorsSeat, AgencyDomain.Sensors));
            delegations.Add(BuildDelegation(commsSeat, AgencyDomain.Communications | AgencyDomain.Sensors));
            delegations.Add(BuildDelegation(logisticsSeat, AgencyDomain.Logistics));
            delegations.Add(BuildDelegation(engineeringSeat, AgencyDomain.Construction | AgencyDomain.Logistics | AgencyDomain.Work));
            delegations.Add(BuildDelegation(securitySeat, AgencyDomain.Security | AgencyDomain.Combat));
            delegations.Add(BuildDelegation(marineCommanderSeat, AgencyDomain.Security | AgencyDomain.Combat));
            delegations.Add(BuildDelegation(marineSergeantSeat, AgencyDomain.Security | AgencyDomain.Combat));
            delegations.Add(BuildDelegation(flightOpsSeat, AgencyDomain.FlightOps));
            delegations.Add(BuildDelegation(flightDirectorSeat, AgencyDomain.FlightOps | AgencyDomain.Communications));
            delegations.Add(BuildDelegation(hangarSeat, AgencyDomain.FlightOps));

            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = captainSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = xoSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = shipmasterSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = fleetAdmiralSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = navigationSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = weaponsSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = sensorsSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = commsSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = logisticsSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = engineeringSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = securitySeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = marineCommanderSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = marineSergeantSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = flightOpsSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = flightDirectorSeat });
            ecb.AppendToBuffer(shipEntity, new AuthoritySeatRef { SeatEntity = hangarSeat });

            var body = new AuthorityBody
            {
                Mode = AuthorityBodyMode.SingleExecutive,
                ExecutiveSeat = captainSeat,
                CreatedTick = 0u
            };

            if (hasBody)
            {
                ecb.SetComponent(shipEntity, body);
            }
            else
            {
                ecb.AddComponent(shipEntity, body);
            }

            if (order.IssuingAuthority == Entity.Null)
            {
                var updated = order;
                updated.IssuingAuthority = captainSeat;
                ecb.SetComponent(shipEntity, updated);
            }

            if (!entityManager.HasComponent<IssuedByAuthority>(shipEntity))
            {
                ecb.AddComponent(shipEntity, new IssuedByAuthority
                {
                    IssuingSeat = Entity.Null,
                    IssuingOccupant = Entity.Null,
                    ActingSeat = Entity.Null,
                    ActingOccupant = Entity.Null,
                    IssuedTick = 0u
                });
            }
        }

        private static Entity CreateDelegateSeat(ref EntityCommandBuffer ecb, Entity body, string roleId, AgencyDomain domains)
        {
            var seat = ecb.CreateEntity();
            ecb.AddComponent(seat, AuthoritySeatDefaults.CreateDelegate(
                body,
                new FixedString64Bytes(roleId),
                domains,
                AuthoritySeatRights.Recommend | AuthoritySeatRights.Issue));
            ecb.AddComponent(seat, AuthoritySeatDefaults.Vacant(0u));
            return seat;
        }

        private static AuthorityDelegation BuildDelegation(
            Entity delegateSeat,
            AgencyDomain domains,
            AuthorityAttributionMode attribution = AuthorityAttributionMode.AsDelegateSeat)
        {
            return new AuthorityDelegation
            {
                DelegateSeat = delegateSeat,
                Domains = domains,
                GrantedRights = AuthoritySeatRights.Recommend | AuthoritySeatRights.Issue,
                Condition = AuthorityDelegationCondition.Always,
                Attribution = attribution
            };
        }
    }
}
