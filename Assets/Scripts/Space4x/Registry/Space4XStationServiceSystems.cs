using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Systems.AI;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Seeds service profiles and access policies for station entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XDockingPolicyBootstrapSystem))]
    public partial struct Space4XStationServiceBootstrapSystem : ISystem
    {
        private ComponentLookup<Space4XMarket> _marketLookup;
        private ComponentLookup<ModuleRefitFacility> _refitLookup;
        private ComponentLookup<RefitFacilityTag> _refitTagLookup;
        private ComponentLookup<StationOverhaulFacility> _overhaulLookup;
        private ComponentLookup<Space4XMissionBoardState> _missionBoardLookup;
        private ComponentLookup<DockingCapacity> _dockingLookup;
        private ComponentLookup<Space4XStationServiceProfileOverride> _serviceOverrideLookup;
        private ComponentLookup<Space4XStationAccessPolicyOverride> _accessOverrideLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StationId>();
            _marketLookup = state.GetComponentLookup<Space4XMarket>(true);
            _refitLookup = state.GetComponentLookup<ModuleRefitFacility>(true);
            _refitTagLookup = state.GetComponentLookup<RefitFacilityTag>(true);
            _overhaulLookup = state.GetComponentLookup<StationOverhaulFacility>(true);
            _missionBoardLookup = state.GetComponentLookup<Space4XMissionBoardState>(true);
            _dockingLookup = state.GetComponentLookup<DockingCapacity>(true);
            _serviceOverrideLookup = state.GetComponentLookup<Space4XStationServiceProfileOverride>(true);
            _accessOverrideLookup = state.GetComponentLookup<Space4XStationAccessPolicyOverride>(true);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _marketLookup.Update(ref state);
            _refitLookup.Update(ref state);
            _refitTagLookup.Update(ref state);
            _overhaulLookup.Update(ref state);
            _missionBoardLookup.Update(ref state);
            _dockingLookup.Update(ref state);
            _serviceOverrideLookup.Update(ref state);
            _accessOverrideLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (stationId, entity) in SystemAPI.Query<RefRO<StationId>>().WithEntityAccess())
            {
                var hasStationSpec = ModuleCatalogUtility.TryGetStationSpec(em, stationId.ValueRO.Id, out var stationSpec);

                if (!em.HasComponent<Space4XStationServiceProfile>(entity))
                {
                    var profile = BuildServiceProfile(entity, hasStationSpec, in stationSpec);
                    ecb.AddComponent(entity, profile);
                }

                if (!em.HasComponent<Space4XStationAccessPolicy>(entity))
                {
                    var profile = em.HasComponent<Space4XStationServiceProfile>(entity)
                        ? em.GetComponentData<Space4XStationServiceProfile>(entity)
                        : BuildServiceProfile(entity, hasStationSpec, in stationSpec);
                    ecb.AddComponent(entity, BuildAccessPolicy(entity, profile, hasStationSpec, in stationSpec));
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private Space4XStationServiceProfile BuildServiceProfile(Entity entity, bool hasStationSpec, in StationSpec stationSpec)
        {
            if (_serviceOverrideLookup.HasComponent(entity))
            {
                var entityOverride = _serviceOverrideLookup[entity];
                if (entityOverride.Enabled != 0)
                {
                    return new Space4XStationServiceProfile
                    {
                        Specialization = entityOverride.Specialization,
                        Services = entityOverride.Services,
                        Tier = (byte)math.clamp(entityOverride.Tier, (byte)1, (byte)8),
                        ServiceScale = math.max(0.1f, entityOverride.ServiceScale)
                    };
                }
            }

            if (hasStationSpec && stationSpec.HasServiceProfileOverride != 0)
            {
                return new Space4XStationServiceProfile
                {
                    Specialization = stationSpec.Specialization,
                    Services = stationSpec.Services,
                    Tier = (byte)math.clamp(stationSpec.Tier, (byte)1, (byte)8),
                    ServiceScale = math.max(0.1f, stationSpec.ServiceScale)
                };
            }

            var services = Space4XStationServiceFlags.None;
            if (_dockingLookup.HasComponent(entity))
            {
                services |= Space4XStationServiceFlags.Docking;
            }

            if (_marketLookup.HasComponent(entity))
            {
                services |= Space4XStationServiceFlags.TradeMarket | Space4XStationServiceFlags.SupplyDepot;
            }

            if (_refitLookup.HasComponent(entity) || _refitTagLookup.HasComponent(entity))
            {
                services |= Space4XStationServiceFlags.Shipyard | Space4XStationServiceFlags.Refit;
            }

            if (_overhaulLookup.HasComponent(entity))
            {
                services |= Space4XStationServiceFlags.Overhaul | Space4XStationServiceFlags.Shipyard;
            }

            if (_missionBoardLookup.HasComponent(entity))
            {
                services |= Space4XStationServiceFlags.MissionBoard | Space4XStationServiceFlags.MercenaryBoard;
            }

            if (hasStationSpec && stationSpec.HasRefitFacility)
            {
                services |= Space4XStationServiceFlags.Shipyard | Space4XStationServiceFlags.Refit;
            }

            var specialization = ResolveSpecialization(entity, services);
            var tier = ResolveTier(entity);

            return new Space4XStationServiceProfile
            {
                Specialization = specialization,
                Services = services,
                Tier = tier,
                ServiceScale = math.max(1f, tier)
            };
        }

        private Space4XStationSpecialization ResolveSpecialization(Entity entity, Space4XStationServiceFlags services)
        {
            if ((services & Space4XStationServiceFlags.TradeMarket) != 0 && (services & Space4XStationServiceFlags.Shipyard) == 0)
            {
                return Space4XStationSpecialization.TradeHub;
            }

            if ((services & Space4XStationServiceFlags.Shipyard) != 0)
            {
                return Space4XStationSpecialization.Shipyard;
            }

            if ((services & Space4XStationServiceFlags.MercenaryBoard) != 0)
            {
                return Space4XStationSpecialization.Mercenary;
            }

            if (Space4XStationAccessUtility.TryResolveFaction(entity, in _carrierLookup, in _affiliationLookup, in _factionLookup, out var factionEntity, out _)
                && factionEntity != Entity.Null
                && _factionLookup.HasComponent(factionEntity))
            {
                var faction = _factionLookup[factionEntity];
                if ((faction.Outlook & FactionOutlook.Militarist) != 0 || (float)faction.MilitaryFocus >= 0.6f)
                {
                    return Space4XStationSpecialization.Military;
                }
            }

            return Space4XStationSpecialization.General;
        }

        private byte ResolveTier(Entity entity)
        {
            if (!_dockingLookup.HasComponent(entity))
            {
                return 1;
            }

            var docking = _dockingLookup[entity];
            if (docking.TotalCapacity >= 24) return 4;
            if (docking.TotalCapacity >= 14) return 3;
            if (docking.TotalCapacity >= 8) return 2;
            return 1;
        }

        private Space4XStationAccessPolicy BuildAccessPolicy(Entity entity, in Space4XStationServiceProfile profile, bool hasStationSpec, in StationSpec stationSpec)
        {
            if (_accessOverrideLookup.HasComponent(entity))
            {
                var entityOverride = _accessOverrideLookup[entity];
                if (entityOverride.Enabled != 0)
                {
                    return SanitizePolicy(new Space4XStationAccessPolicy
                    {
                        MinStandingForApproach = entityOverride.MinStandingForApproach,
                        MinStandingForDock = entityOverride.MinStandingForDock,
                        WarningRadiusMeters = entityOverride.WarningRadiusMeters,
                        NoFlyRadiusMeters = entityOverride.NoFlyRadiusMeters,
                        EnforceNoFlyZone = entityOverride.EnforceNoFlyZone,
                        DenyDockingWithoutStanding = entityOverride.DenyDockingWithoutStanding
                    });
                }
            }

            if (hasStationSpec && stationSpec.HasAccessPolicyOverride != 0)
            {
                return SanitizePolicy(new Space4XStationAccessPolicy
                {
                    MinStandingForApproach = stationSpec.MinStandingForApproach,
                    MinStandingForDock = stationSpec.MinStandingForDock,
                    WarningRadiusMeters = stationSpec.WarningRadiusMeters,
                    NoFlyRadiusMeters = stationSpec.NoFlyRadiusMeters,
                    EnforceNoFlyZone = stationSpec.EnforceNoFlyZone,
                    DenyDockingWithoutStanding = stationSpec.DenyDockingWithoutStanding
                });
            }

            var policy = new Space4XStationAccessPolicy
            {
                MinStandingForApproach = 0.1f,
                MinStandingForDock = 0.15f,
                WarningRadiusMeters = 120f,
                NoFlyRadiusMeters = 70f,
                EnforceNoFlyZone = 1,
                DenyDockingWithoutStanding = 1
            };

            switch (profile.Specialization)
            {
                case Space4XStationSpecialization.TradeHub:
                    policy.MinStandingForApproach = 0f;
                    policy.MinStandingForDock = 0.05f;
                    policy.WarningRadiusMeters = 90f;
                    policy.NoFlyRadiusMeters = 45f;
                    break;
                case Space4XStationSpecialization.Shipyard:
                    policy.MinStandingForApproach = 0.15f;
                    policy.MinStandingForDock = 0.25f;
                    policy.WarningRadiusMeters = 130f;
                    policy.NoFlyRadiusMeters = 75f;
                    break;
                case Space4XStationSpecialization.Military:
                    policy.MinStandingForApproach = 0.45f;
                    policy.MinStandingForDock = 0.55f;
                    policy.WarningRadiusMeters = 180f;
                    policy.NoFlyRadiusMeters = 110f;
                    break;
            }

            var tierScale = math.max(1f, profile.Tier);
            policy.WarningRadiusMeters *= (1f + (tierScale - 1f) * 0.15f);
            policy.NoFlyRadiusMeters *= (1f + (tierScale - 1f) * 0.15f);
            return SanitizePolicy(policy);
        }

        private static Space4XStationAccessPolicy SanitizePolicy(in Space4XStationAccessPolicy policy)
        {
            var sanitized = policy;
            sanitized.MinStandingForApproach = math.clamp(sanitized.MinStandingForApproach, 0f, 1f);
            sanitized.MinStandingForDock = math.clamp(sanitized.MinStandingForDock, 0f, 1f);
            sanitized.WarningRadiusMeters = math.max(0f, sanitized.WarningRadiusMeters);
            sanitized.NoFlyRadiusMeters = math.max(0f, sanitized.NoFlyRadiusMeters);
            sanitized.NoFlyRadiusMeters = math.min(sanitized.NoFlyRadiusMeters, sanitized.WarningRadiusMeters);
            return sanitized;
        }
    }

    /// <summary>
    /// Evaluates relation-based station fly zones and marks no-fly violations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    public partial struct Space4XStationNoFlyBoundarySystem : ISystem
    {
        private EntityQuery _stationQuery;
        private EntityQuery _vesselQuery;

        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XStationAccessPolicy> _stationAccessLookup;
        private ComponentLookup<Space4XTrespassIntentTag> _intentLookup;
        private ComponentLookup<Space4XStationNoFlyViolation> _violationLookup;
        private ComponentLookup<DockingRequest> _dockingRequestLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<Space4XContactStanding> _contactLookup;
        private BufferLookup<FactionRelationEntry> _relationLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _stationQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StationId>(),
                    ComponentType.ReadOnly<LocalTransform>(),
                    ComponentType.ReadOnly<Space4XStationAccessPolicy>()
                }
            });

            _vesselQuery = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<LocalTransform>() },
                Any = new[] { ComponentType.ReadOnly<Carrier>(), ComponentType.ReadOnly<MiningVessel>() },
                None = new[] { ComponentType.ReadOnly<StationId>() }
            });

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _stationAccessLookup = state.GetComponentLookup<Space4XStationAccessPolicy>(true);
            _intentLookup = state.GetComponentLookup<Space4XTrespassIntentTag>(true);
            _violationLookup = state.GetComponentLookup<Space4XStationNoFlyViolation>(false);
            _dockingRequestLookup = state.GetComponentLookup<DockingRequest>(false);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _contactLookup = state.GetBufferLookup<Space4XContactStanding>(true);
            _relationLookup = state.GetBufferLookup<FactionRelationEntry>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _stationAccessLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _violationLookup.Update(ref state);
            _dockingRequestLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _contactLookup.Update(ref state);
            _relationLookup.Update(ref state);

            using var stationEntities = _stationQuery.ToEntityArray(Allocator.Temp);
            if (stationEntities.Length == 0)
            {
                return;
            }

            using var vesselEntities = _vesselQuery.ToEntityArray(Allocator.Temp);
            if (vesselEntities.Length == 0)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (var i = 0; i < vesselEntities.Length; i++)
            {
                var vessel = vesselEntities[i];
                if (!_transformLookup.HasComponent(vessel))
                {
                    continue;
                }

                if (_intentLookup.HasComponent(vessel))
                {
                    if (_violationLookup.HasComponent(vessel))
                    {
                        ecb.RemoveComponent<Space4XStationNoFlyViolation>(vessel);
                    }
                    continue;
                }

                var vesselPos = _transformLookup[vessel].Position;
                var bestStation = Entity.Null;
                var bestDistance = float.MaxValue;
                var bestPolicy = default(Space4XStationAccessPolicy);

                for (var j = 0; j < stationEntities.Length; j++)
                {
                    var station = stationEntities[j];
                    if (!_stationAccessLookup.HasComponent(station) || !_transformLookup.HasComponent(station))
                    {
                        continue;
                    }

                    var policy = _stationAccessLookup[station];
                    if (policy.EnforceNoFlyZone == 0 || policy.WarningRadiusMeters <= 0f)
                    {
                        continue;
                    }

                    var stationPos = _transformLookup[station].Position;
                    var distance = math.distance(vesselPos, stationPos);
                    if (distance > policy.WarningRadiusMeters)
                    {
                        continue;
                    }

                    var passesApproach = Space4XStationAccessUtility.PassesStandingGate(
                        vessel,
                        station,
                        policy.MinStandingForApproach,
                        in _carrierLookup,
                        in _affiliationLookup,
                        in _factionLookup,
                        in _contactLookup,
                        in _relationLookup);
                    if (passesApproach)
                    {
                        continue;
                    }

                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestStation = station;
                        bestPolicy = policy;
                    }
                }

                if (bestStation == Entity.Null)
                {
                    if (_violationLookup.HasComponent(vessel))
                    {
                        ecb.RemoveComponent<Space4XStationNoFlyViolation>(vessel);
                    }
                    continue;
                }

                var insideNoFly = bestDistance <= bestPolicy.NoFlyRadiusMeters;
                var severity = ResolveSeverity(bestDistance, bestPolicy.WarningRadiusMeters, bestPolicy.NoFlyRadiusMeters);
                var violation = new Space4XStationNoFlyViolation
                {
                    Station = bestStation,
                    DistanceMeters = bestDistance,
                    Severity = severity,
                    LastTick = time.Tick,
                    InsideNoFly = insideNoFly ? (byte)1 : (byte)0
                };

                if (_violationLookup.HasComponent(vessel))
                {
                    ecb.SetComponent(vessel, violation);
                }
                else
                {
                    ecb.AddComponent(vessel, violation);
                }

                if (_dockingRequestLookup.HasComponent(vessel))
                {
                    var request = _dockingRequestLookup[vessel];
                    if (request.TargetCarrier == bestStation)
                    {
                        var passesDocking = Space4XStationAccessUtility.PassesStandingGate(
                            vessel,
                            bestStation,
                            bestPolicy.MinStandingForDock,
                            in _carrierLookup,
                            in _affiliationLookup,
                            in _factionLookup,
                            in _contactLookup,
                            in _relationLookup);
                        if (!passesDocking)
                        {
                            ecb.RemoveComponent<DockingRequest>(vessel);
                        }
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static float ResolveSeverity(float distance, float warningRadius, float noFlyRadius)
        {
            if (distance <= noFlyRadius)
            {
                return 1f;
            }

            if (warningRadius <= noFlyRadius)
            {
                return 0.5f;
            }

            var t = (warningRadius - distance) / (warningRadius - noFlyRadius);
            return math.saturate(t);
        }
    }
}
