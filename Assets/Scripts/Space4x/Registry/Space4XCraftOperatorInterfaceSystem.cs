using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Agency.AgencyControlResolutionSystem))]
    [UpdateAfter(typeof(Space4XShipSystemsSnapshotSystem))]
    public partial struct Space4XCraftOperatorInterfaceSystem : ISystem
    {
        private ComponentLookup<ShipSystemsSnapshot> _snapshotLookup;
        private BufferLookup<ResolvedControl> _resolvedLookup;
        private BufferLookup<ControlClaim> _claimLookup;
        private ComponentLookup<StrikeCraftPilotLink> _strikePilotLookup;
        private ComponentLookup<VesselPilotLink> _vesselPilotLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorLookup;
        private ComponentLookup<OfficerProfile> _officerProfileLookup;
        private BufferLookup<CraftOperatorAssignment> _assignmentLookup;
        private BufferLookup<CraftOperatorConsole> _consoleLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _snapshotLookup = state.GetComponentLookup<ShipSystemsSnapshot>(true);
            _resolvedLookup = state.GetBufferLookup<ResolvedControl>(true);
            _claimLookup = state.GetBufferLookup<ControlClaim>(true);
            _strikePilotLookup = state.GetComponentLookup<StrikeCraftPilotLink>(true);
            _vesselPilotLookup = state.GetComponentLookup<VesselPilotLink>(true);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _behaviorLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _officerProfileLookup = state.GetComponentLookup<OfficerProfile>(false);
            _assignmentLookup = state.GetBufferLookup<CraftOperatorAssignment>(false);
            _consoleLookup = state.GetBufferLookup<CraftOperatorConsole>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewindState) &&
                rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _snapshotLookup.Update(ref state);
            _resolvedLookup.Update(ref state);
            _claimLookup.Update(ref state);
            _strikePilotLookup.Update(ref state);
            _vesselPilotLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _behaviorLookup.Update(ref state);
            _officerProfileLookup.Update(ref state);
            _assignmentLookup.Update(ref state);
            _consoleLookup.Update(ref state);

            var tuning = CraftOperatorTuning.Default;
            if (SystemAPI.TryGetSingleton<CraftOperatorTuning>(out var tuningSingleton))
            {
                tuning = tuningSingleton;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var query = SystemAPI.QueryBuilder()
                .WithAny<StrikeCraftProfile, MiningVessel>()
                .WithNone<Prefab>()
                .Build();

            if (!query.IsEmptyIgnoreFilter)
            {
                using var entities = query.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    var craft = entities[i];
                    if (!_snapshotLookup.HasComponent(craft))
                    {
                        continue;
                    }

                    var snapshot = _snapshotLookup[craft];
                    var pilot = ResolvePilot(craft);

                    var assignments = _assignmentLookup.HasBuffer(craft)
                        ? _assignmentLookup[craft]
                        : ecb.AddBuffer<CraftOperatorAssignment>(craft);
                    assignments.Clear();

                    var consoles = _consoleLookup.HasBuffer(craft)
                        ? _consoleLookup[craft]
                        : ecb.AddBuffer<CraftOperatorConsole>(craft);
                    consoles.Clear();

                    WriteDomain(craft, pilot, AgencyDomain.Movement, snapshot, tuning, ref assignments, ref consoles, timeState.Tick, ref ecb);
                    WriteDomain(craft, pilot, AgencyDomain.Combat, snapshot, tuning, ref assignments, ref consoles, timeState.Tick, ref ecb);
                    WriteDomain(craft, pilot, AgencyDomain.Sensors, snapshot, tuning, ref assignments, ref consoles, timeState.Tick, ref ecb);
                    WriteDomain(craft, pilot, AgencyDomain.Logistics, snapshot, tuning, ref assignments, ref consoles, timeState.Tick, ref ecb);
                    WriteDomain(craft, pilot, AgencyDomain.Communications, snapshot, tuning, ref assignments, ref consoles, timeState.Tick, ref ecb);
                    WriteDomain(craft, pilot, AgencyDomain.FlightOps, snapshot, tuning, ref assignments, ref consoles, timeState.Tick, ref ecb);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void WriteDomain(
            Entity craft,
            Entity pilot,
            AgencyDomain domain,
            in ShipSystemsSnapshot snapshot,
            in CraftOperatorTuning tuning,
            ref DynamicBuffer<CraftOperatorAssignment> assignments,
            ref DynamicBuffer<CraftOperatorConsole> consoles,
            uint tick,
            ref EntityCommandBuffer ecb)
        {
            var controller = ResolveController(craft, pilot, domain, out var score);
            if (controller == Entity.Null)
            {
                controller = craft;
            }

            var sourceKind = ResolveSourceKind(craft, controller, domain, pilot);
            var operatorSkill = 0.5f;
            if (_statsLookup.HasComponent(controller))
            {
                operatorSkill = Space4XOperatorInterfaceUtility.ResolveOperatorSkill(domain, _statsLookup[controller], tuning);
            }

            var consoleQuality = Space4XOperatorInterfaceUtility.ResolveConsoleQuality(domain, snapshot, operatorSkill, tuning);
            var console = new CraftOperatorConsole
            {
                Domain = domain,
                Controller = controller,
                SourceKind = sourceKind,
                Reserved0 = 0,
                ConsoleQuality = consoleQuality,
                DataLatencySeconds = math.lerp(0.4f, 0.06f, consoleQuality),
                DataFidelity = consoleQuality,
                UpdatedTick = tick
            };

            consoles.Add(console);
            assignments.Add(new CraftOperatorAssignment
            {
                Domain = domain,
                Controller = controller,
                SourceKind = sourceKind,
                Reserved0 = 0,
                Score = score,
                UpdatedTick = tick
            });

            if (domain == AgencyDomain.Movement && _statsLookup.HasComponent(controller))
            {
                var behavior = _behaviorLookup.HasComponent(controller)
                    ? _behaviorLookup[controller]
                    : BehaviorDisposition.Default;
                var profile = Space4XOperatorInterfaceUtility.BuildOfficerProfile(_statsLookup[controller], behavior);

                if (_officerProfileLookup.HasComponent(craft))
                {
                    _officerProfileLookup[craft] = profile;
                }
                else
                {
                    ecb.AddComponent(craft, profile);
                }
            }
        }

        private Entity ResolvePilot(Entity craft)
        {
            if (_strikePilotLookup.HasComponent(craft))
            {
                var link = _strikePilotLookup[craft];
                if (link.Pilot != Entity.Null)
                {
                    return link.Pilot;
                }
            }

            if (_vesselPilotLookup.HasComponent(craft))
            {
                var link = _vesselPilotLookup[craft];
                if (link.Pilot != Entity.Null)
                {
                    return link.Pilot;
                }
            }

            return Entity.Null;
        }

        private Entity ResolveController(Entity craft, Entity pilot, AgencyDomain domain, out float score)
        {
            score = 0f;
            if (_resolvedLookup.HasBuffer(craft))
            {
                var resolved = _resolvedLookup[craft];
                for (int i = 0; i < resolved.Length; i++)
                {
                    if (resolved[i].Domain == domain)
                    {
                        score = resolved[i].Score;
                        if (resolved[i].Controller != Entity.Null)
                        {
                            return resolved[i].Controller;
                        }
                        break;
                    }
                }
            }

            if (pilot != Entity.Null)
            {
                return pilot;
            }

            return Entity.Null;
        }

        private ControlClaimSourceKind ResolveSourceKind(Entity craft, Entity controller, AgencyDomain domain, Entity pilot)
        {
            if (controller == Entity.Null)
            {
                return ControlClaimSourceKind.None;
            }

            if (pilot != Entity.Null && controller == pilot)
            {
                return ControlClaimSourceKind.Operator;
            }

            if (!_claimLookup.HasBuffer(craft))
            {
                return ControlClaimSourceKind.None;
            }

            var claims = _claimLookup[craft];
            for (int i = 0; i < claims.Length; i++)
            {
                var claim = claims[i];
                if (claim.Controller == controller && (claim.Domains & domain) != 0)
                {
                    return claim.SourceKind;
                }
            }

            return ControlClaimSourceKind.None;
        }
    }
}
