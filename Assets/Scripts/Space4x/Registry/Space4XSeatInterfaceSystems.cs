using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Modules;
using PureDOTS.Runtime.Perception;
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
    [UpdateAfter(typeof(Space4X.Systems.Modules.Space4XBridgeCockpitAggregationSystem))]
    [UpdateAfter(typeof(Space4X.Systems.Modules.Space4XSensorModuleAggregationSystem))]
    [UpdateAfter(typeof(Space4X.Systems.Modules.Space4XWeaponModuleAggregationSystem))]
    [UpdateAfter(typeof(Space4X.Systems.Modules.Space4XAmmoCapacityAggregationSystem))]
    public partial struct Space4XShipSystemsSnapshotSystem : ISystem
    {
        private ComponentLookup<BridgeTechLevel> _bridgeLookup;
        private ComponentLookup<NavigationCohesion> _navigationLookup;
        private ComponentLookup<Space4XShield> _shieldLookup;
        private ComponentLookup<Space4XArmor> _armorLookup;
        private ComponentLookup<SupplyStatus> _supplyLookup;
        private ComponentLookup<VesselResourceLevels> _resourceLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private ComponentLookup<SenseCapability> _senseLookup;
        private BufferLookup<PerceivedEntity> _perceivedLookup;
        private BufferLookup<WeaponMount> _weaponLookup;
        private ComponentLookup<ModuleCapabilityOutput> _capabilityLookup;
        private ComponentLookup<EnginePerformanceOutput> _engineOutputLookup;
        private ComponentLookup<ShipSystemsSnapshot> _snapshotLookup;
        private ComponentLookup<CaptainAggregateBrief> _briefLookup;
        private ComponentLookup<CaptainOrder> _orderLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();

            _bridgeLookup = state.GetComponentLookup<BridgeTechLevel>(true);
            _navigationLookup = state.GetComponentLookup<NavigationCohesion>(true);
            _shieldLookup = state.GetComponentLookup<Space4XShield>(true);
            _armorLookup = state.GetComponentLookup<Space4XArmor>(true);
            _supplyLookup = state.GetComponentLookup<SupplyStatus>(true);
            _resourceLookup = state.GetComponentLookup<VesselResourceLevels>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(true);
            _senseLookup = state.GetComponentLookup<SenseCapability>(true);
            _perceivedLookup = state.GetBufferLookup<PerceivedEntity>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMount>(true);
            _capabilityLookup = state.GetComponentLookup<ModuleCapabilityOutput>(true);
            _engineOutputLookup = state.GetComponentLookup<EnginePerformanceOutput>(true);
            _snapshotLookup = state.GetComponentLookup<ShipSystemsSnapshot>(false);
            _briefLookup = state.GetComponentLookup<CaptainAggregateBrief>(false);
            _orderLookup = state.GetComponentLookup<CaptainOrder>(true);
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

            _bridgeLookup.Update(ref state);
            _navigationLookup.Update(ref state);
            _shieldLookup.Update(ref state);
            _armorLookup.Update(ref state);
            _supplyLookup.Update(ref state);
            _resourceLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _senseLookup.Update(ref state);
            _perceivedLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _capabilityLookup.Update(ref state);
            _engineOutputLookup.Update(ref state);
            _snapshotLookup.Update(ref state);
            _briefLookup.Update(ref state);
            _orderLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<CaptainOrder>>().WithNone<Prefab>().WithEntityAccess())
            {
                ProcessEntity(entity, timeState.Tick, ref ecb);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>>().WithNone<Prefab>().WithEntityAccess())
            {
                if (_orderLookup.HasComponent(entity))
                {
                    continue;
                }

                ProcessEntity(entity, timeState.Tick, ref ecb);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<MiningVessel>>().WithNone<Prefab>().WithEntityAccess())
            {
                if (_orderLookup.HasComponent(entity))
                {
                    continue;
                }

                ProcessEntity(entity, timeState.Tick, ref ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ProcessEntity(Entity entity, uint tick, ref EntityCommandBuffer ecb)
        {
            var bridgeTech = _bridgeLookup.HasComponent(entity)
                    ? math.saturate(_bridgeLookup[entity].Value)
                    : 0.5f;
            var navigation = _navigationLookup.HasComponent(entity)
                    ? math.saturate(_navigationLookup[entity].Value)
                    : 0.5f;

                float hullRatio = 1f;
                if (_hullLookup.HasComponent(entity))
                {
                    var hull = _hullLookup[entity];
                    hullRatio = hull.Max > 0f ? hull.Current / hull.Max : 1f;
                }
                else if (_resourceLookup.HasComponent(entity))
                {
                    hullRatio = _resourceLookup[entity].HullRatio;
                }

                float fuelRatio = 1f;
                float ammoRatio = 1f;
                if (_supplyLookup.HasComponent(entity))
                {
                    var supply = _supplyLookup[entity];
                    fuelRatio = supply.FuelRatio;
                    ammoRatio = supply.AmmoRatio;
                }
                else if (_resourceLookup.HasComponent(entity))
                {
                    var levels = _resourceLookup[entity];
                    fuelRatio = levels.FuelRatio;
                    ammoRatio = levels.AmmoRatio;
                }

                float shieldRatio = 0f;
                if (_shieldLookup.HasComponent(entity))
                {
                    var shield = _shieldLookup[entity];
                    shieldRatio = shield.Maximum > 0f ? shield.Current / shield.Maximum : 0f;
                }

                float armorRating = 0f;
                if (_armorLookup.HasComponent(entity))
                {
                    armorRating = math.max(0f, _armorLookup[entity].Thickness);
                }

                float sensorRange = 0f;
                float sensorAcuity = 0f;
                if (_senseLookup.HasComponent(entity))
                {
                    var sense = _senseLookup[entity];
                    sensorRange = math.max(0f, sense.Range);
                    sensorAcuity = math.saturate(sense.Acuity);
                }

                byte contacts = 0;
                if (_perceivedLookup.HasBuffer(entity))
                {
                    contacts = (byte)math.min(255, _perceivedLookup[entity].Length);
                }

                byte weaponMounts = 0;
                byte weaponsOnline = 0;
                if (_weaponLookup.HasBuffer(entity))
                {
                    var mounts = _weaponLookup[entity];
                    var mountCount = mounts.Length;
                    weaponMounts = (byte)math.min(255, mountCount);
                    var enabled = 0;
                    for (var i = 0; i < mountCount; i++)
                    {
                        if (mounts[i].IsEnabled != 0)
                        {
                            enabled++;
                        }
                    }

                    weaponsOnline = (byte)math.min(255, enabled);
                }

                float thrustAuthority = 0f;
                float turnAuthority = 0f;
                if (_capabilityLookup.HasComponent(entity))
                {
                    var capability = _capabilityLookup[entity];
                    thrustAuthority = math.max(0f, capability.ThrustAuthority);
                    turnAuthority = math.max(0f, capability.TurnAuthority);
                }
                else if (_engineOutputLookup.HasComponent(entity))
                {
                    var engine = _engineOutputLookup[entity];
                    thrustAuthority = math.max(0f, engine.ThrustAuthority);
                    turnAuthority = math.max(0f, engine.TurnAuthority);
                }

                var snapshot = new ShipSystemsSnapshot
                {
                    HullRatio = math.saturate(hullRatio),
                    ShieldRatio = math.saturate(shieldRatio),
                    ArmorRating = armorRating,
                    FuelRatio = math.saturate(fuelRatio),
                    AmmoRatio = math.saturate(ammoRatio),
                    SensorRange = sensorRange,
                    SensorAcuity = math.saturate(sensorAcuity),
                    ThrustAuthority = thrustAuthority,
                    TurnAuthority = turnAuthority,
                    BridgeTechLevel = bridgeTech,
                    NavigationCohesion = navigation,
                    WeaponMounts = weaponMounts,
                    WeaponsOnline = weaponsOnline,
                    ContactsTracked = contacts,
                    Reserved = 0,
                    UpdatedTick = tick
                };

                if (_snapshotLookup.HasComponent(entity))
                {
                    _snapshotLookup[entity] = snapshot;
                }
                else
                {
                    ecb.AddComponent(entity, snapshot);
                }

                if (!_orderLookup.HasComponent(entity))
                {
                    return;
                }

                var minResource = math.min(snapshot.FuelRatio, snapshot.AmmoRatio);
                var alert = ShipAlertLevel.Normal;
                if (snapshot.HullRatio <= 0.2f || snapshot.ShieldRatio <= 0.15f || minResource <= 0.15f)
                {
                    alert = ShipAlertLevel.Critical;
                }
                else if (snapshot.HullRatio <= 0.4f || snapshot.ShieldRatio <= 0.35f || minResource <= 0.3f)
                {
                    alert = ShipAlertLevel.Caution;
                }

                var brief = new CaptainAggregateBrief
                {
                    AlertLevel = alert,
                    Reserved0 = 0,
                    Reserved1 = 0,
                    HullRatio = snapshot.HullRatio,
                    ShieldRatio = snapshot.ShieldRatio,
                    FuelRatio = snapshot.FuelRatio,
                    AmmoRatio = snapshot.AmmoRatio,
                    ContactsTracked = snapshot.ContactsTracked,
                    WeaponsOnline = snapshot.WeaponsOnline,
                    Reserved2 = 0,
                    UpdatedTick = tick
                };

                if (_briefLookup.HasComponent(entity))
                {
                    _briefLookup[entity] = brief;
                }
                else
                {
                    ecb.AddComponent(entity, brief);
                }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XShipSystemsSnapshotSystem))]
    [UpdateBefore(typeof(Space4X.Systems.Modules.Space4XCaptainPolicySystem))]
    public partial struct Space4XSeatConsoleInterfaceSystem : ISystem
    {
        private ComponentLookup<ShipSystemsSnapshot> _snapshotLookup;
        private ComponentLookup<SeatConsoleState> _consoleLookup;
        private ComponentLookup<SeatInstrumentFeed> _feedLookup;
        private ComponentLookup<IndividualStats> _statsLookup;
        private ComponentLookup<BehaviorDisposition> _behaviorLookup;
        private ComponentLookup<OfficerProfile> _officerProfileLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AuthoritySeat>();
            state.RequireForUpdate<TimeState>();

            _snapshotLookup = state.GetComponentLookup<ShipSystemsSnapshot>(true);
            _consoleLookup = state.GetComponentLookup<SeatConsoleState>(false);
            _feedLookup = state.GetComponentLookup<SeatInstrumentFeed>(false);
            _statsLookup = state.GetComponentLookup<IndividualStats>(true);
            _behaviorLookup = state.GetComponentLookup<BehaviorDisposition>(true);
            _officerProfileLookup = state.GetComponentLookup<OfficerProfile>(false);
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
            _consoleLookup.Update(ref state);
            _feedLookup.Update(ref state);
            _statsLookup.Update(ref state);
            _behaviorLookup.Update(ref state);
            _officerProfileLookup.Update(ref state);

            var tuning = CraftOperatorTuning.Default;
            if (SystemAPI.TryGetSingleton<CraftOperatorTuning>(out var tuningSingleton))
            {
                tuning = tuningSingleton;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (seat, occupant, seatEntity) in SystemAPI.Query<RefRO<AuthoritySeat>, RefRO<AuthoritySeatOccupant>>().WithEntityAccess())
            {
                var body = seat.ValueRO.BodyEntity;
                if (body == Entity.Null || !_snapshotLookup.HasComponent(body))
                {
                    continue;
                }

                var snapshot = _snapshotLookup[body];
                var domains = seat.ValueRO.Domains;
                var executive = seat.ValueRO.IsExecutive != 0;
                var hasGovernance = (domains & AgencyDomain.Governance) != 0;
                var fullAccess = executive || hasGovernance;

                var feed = BuildSeatFeed(snapshot, domains, fullAccess, timeState.Tick);
                if (_feedLookup.HasComponent(seatEntity))
                {
                    _feedLookup[seatEntity] = feed;
                }
                else
                {
                    ecb.AddComponent(seatEntity, feed);
                }

                var occupantEntity = occupant.ValueRO.OccupantEntity;
                var hasStats = occupantEntity != Entity.Null && _statsLookup.HasComponent(occupantEntity);
                var operatorSkill = hasStats
                    ? Space4XOperatorInterfaceUtility.ResolveOperatorSkill(domains, _statsLookup[occupantEntity], tuning)
                    : 0.5f;
                var consoleQuality = Space4XOperatorInterfaceUtility.ResolveConsoleQuality(domains, snapshot, operatorSkill, tuning);
                var console = new SeatConsoleState
                {
                    ConsoleQuality = consoleQuality,
                    DataLatencySeconds = math.lerp(0.35f, 0.05f, consoleQuality),
                    DataFidelity = consoleQuality,
                    UpdatedTick = timeState.Tick
                };

                if (_consoleLookup.HasComponent(seatEntity))
                {
                    _consoleLookup[seatEntity] = console;
                }
                else
                {
                    ecb.AddComponent(seatEntity, console);
                }

                if ((domains & AgencyDomain.Movement) != 0 && hasStats)
                {
                    var stats = _statsLookup[occupantEntity];
                    var behavior = _behaviorLookup.HasComponent(occupantEntity)
                        ? _behaviorLookup[occupantEntity]
                        : BehaviorDisposition.Default;
                    var profile = Space4XOperatorInterfaceUtility.BuildOfficerProfile(stats, behavior);

                    if (_officerProfileLookup.HasComponent(body))
                    {
                        _officerProfileLookup[body] = profile;
                    }
                    else
                    {
                        ecb.AddComponent(body, profile);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static SeatInstrumentFeed BuildSeatFeed(
            in ShipSystemsSnapshot snapshot,
            AgencyDomain domains,
            bool fullAccess,
            uint tick)
        {
            var feed = new SeatInstrumentFeed
            {
                HullRatio = snapshot.HullRatio,
                ShieldRatio = snapshot.ShieldRatio,
                ArmorRating = snapshot.ArmorRating,
                FuelRatio = snapshot.FuelRatio,
                AmmoRatio = snapshot.AmmoRatio,
                SensorRange = snapshot.SensorRange,
                SensorAcuity = snapshot.SensorAcuity,
                ThrustAuthority = snapshot.ThrustAuthority,
                TurnAuthority = snapshot.TurnAuthority,
                BridgeTechLevel = snapshot.BridgeTechLevel,
                NavigationCohesion = snapshot.NavigationCohesion,
                WeaponMounts = snapshot.WeaponMounts,
                WeaponsOnline = snapshot.WeaponsOnline,
                ContactsTracked = snapshot.ContactsTracked,
                Reserved = 0,
                UpdatedTick = tick
            };

            if (fullAccess)
            {
                return feed;
            }

            if ((domains & AgencyDomain.Logistics) == 0)
            {
                feed.FuelRatio = 0f;
                feed.AmmoRatio = 0f;
            }

            if ((domains & AgencyDomain.Sensors) == 0)
            {
                feed.SensorRange = 0f;
                feed.SensorAcuity = 0f;
                feed.ContactsTracked = 0;
            }

            if ((domains & AgencyDomain.Combat) == 0)
            {
                feed.WeaponMounts = 0;
                feed.WeaponsOnline = 0;
            }

            if ((domains & AgencyDomain.Movement) == 0)
            {
                feed.ThrustAuthority = 0f;
                feed.TurnAuthority = 0f;
                feed.NavigationCohesion = 0f;
                feed.BridgeTechLevel = 0f;
            }

            return feed;
        }

    }

    internal static class Space4XOperatorInterfaceUtility
    {
        public static float ResolveConsoleQuality(AgencyDomain domains, in ShipSystemsSnapshot snapshot, float operatorSkill)
        {
            return ResolveConsoleQuality(domains, snapshot, operatorSkill, CraftOperatorTuning.Default);
        }

        public static float ResolveConsoleQuality(
            AgencyDomain domains,
            in ShipSystemsSnapshot snapshot,
            float operatorSkill,
            in CraftOperatorTuning tuning)
        {
            var quality = math.saturate(snapshot.BridgeTechLevel);

            if ((domains & AgencyDomain.Movement) != 0)
            {
                quality = math.max(quality, snapshot.NavigationCohesion);
            }

            if ((domains & AgencyDomain.Sensors) != 0)
            {
                quality = math.max(quality, snapshot.SensorAcuity);
            }

            if ((domains & AgencyDomain.Combat) != 0)
            {
                var mountCount = snapshot.WeaponMounts > 0 ? snapshot.WeaponMounts : (byte)1;
                var readyRatio = math.saturate(snapshot.WeaponsOnline / (float)mountCount);
                quality = math.max(quality, math.lerp(0.35f, 1f, readyRatio));
            }

            if ((domains & AgencyDomain.Logistics) != 0)
            {
                var logisticsRatio = math.min(snapshot.FuelRatio, snapshot.AmmoRatio);
                quality = math.max(quality, math.lerp(0.35f, 1f, logisticsRatio));
            }

            quality = math.saturate(quality * math.lerp(0.8f, 1.15f, operatorSkill));
            ApplyDomainConsoleTuning(domains, tuning, ref quality);
            return quality;
        }

        public static float ResolveOperatorSkill(AgencyDomain domains, in IndividualStats stats)
        {
            return ResolveOperatorSkill(domains, stats, CraftOperatorTuning.Default);
        }

        public static float ResolveOperatorSkill(AgencyDomain domains, in IndividualStats stats, in CraftOperatorTuning tuning)
        {
            float command = math.saturate(stats.Command / 100f);
            float tactics = math.saturate(stats.Tactics / 100f);
            float logistics = math.saturate(stats.Logistics / 100f);
            float diplomacy = math.saturate(stats.Diplomacy / 100f);
            float engineering = math.saturate(stats.Engineering / 100f);
            float resolve = math.saturate(stats.Resolve / 100f);

            var best = 0f;
            var any = false;
            if ((domains & AgencyDomain.Movement) != 0)
            {
                best = math.max(best, ComputeWeightedSkill(command, tactics, logistics, diplomacy, engineering, resolve, tuning.Movement));
                any = true;
            }

            if ((domains & AgencyDomain.Combat) != 0)
            {
                best = math.max(best, ComputeWeightedSkill(command, tactics, logistics, diplomacy, engineering, resolve, tuning.Combat));
                any = true;
            }

            if ((domains & AgencyDomain.Sensors) != 0)
            {
                best = math.max(best, ComputeWeightedSkill(command, tactics, logistics, diplomacy, engineering, resolve, tuning.Sensors));
                any = true;
            }

            if ((domains & AgencyDomain.Logistics) != 0)
            {
                best = math.max(best, ComputeWeightedSkill(command, tactics, logistics, diplomacy, engineering, resolve, tuning.Logistics));
                any = true;
            }

            if ((domains & AgencyDomain.Communications) != 0)
            {
                best = math.max(best, ComputeWeightedSkill(command, tactics, logistics, diplomacy, engineering, resolve, tuning.Communications));
                any = true;
            }

            if ((domains & AgencyDomain.FlightOps) != 0)
            {
                best = math.max(best, ComputeWeightedSkill(command, tactics, logistics, diplomacy, engineering, resolve, tuning.FlightOps));
                any = true;
            }

            if (!any || best <= 0f)
            {
                best = (command + tactics + logistics + diplomacy + engineering + resolve) / 6f;
            }

            return math.saturate(best);
        }

        private static float ComputeWeightedSkill(
            float command,
            float tactics,
            float logistics,
            float diplomacy,
            float engineering,
            float resolve,
            in OperatorDomainTuning tuning)
        {
            var total = tuning.CommandWeight + tuning.TacticsWeight + tuning.LogisticsWeight +
                        tuning.DiplomacyWeight + tuning.EngineeringWeight + tuning.ResolveWeight;
            if (total <= 0f)
            {
                return 0f;
            }

            var weighted = command * tuning.CommandWeight +
                           tactics * tuning.TacticsWeight +
                           logistics * tuning.LogisticsWeight +
                           diplomacy * tuning.DiplomacyWeight +
                           engineering * tuning.EngineeringWeight +
                           resolve * tuning.ResolveWeight;

            return math.saturate(weighted / total);
        }

        private static void ApplyDomainConsoleTuning(AgencyDomain domains, in CraftOperatorTuning tuning, ref float quality)
        {
            var scale = 1f;
            var bias = 0f;
            var any = false;

            if ((domains & AgencyDomain.Movement) != 0)
            {
                scale = math.max(scale, tuning.Movement.ConsoleQualityScale);
                bias = math.max(bias, tuning.Movement.ConsoleQualityBias);
                any = true;
            }

            if ((domains & AgencyDomain.Combat) != 0)
            {
                scale = math.max(scale, tuning.Combat.ConsoleQualityScale);
                bias = math.max(bias, tuning.Combat.ConsoleQualityBias);
                any = true;
            }

            if ((domains & AgencyDomain.Sensors) != 0)
            {
                scale = math.max(scale, tuning.Sensors.ConsoleQualityScale);
                bias = math.max(bias, tuning.Sensors.ConsoleQualityBias);
                any = true;
            }

            if ((domains & AgencyDomain.Logistics) != 0)
            {
                scale = math.max(scale, tuning.Logistics.ConsoleQualityScale);
                bias = math.max(bias, tuning.Logistics.ConsoleQualityBias);
                any = true;
            }

            if ((domains & AgencyDomain.Communications) != 0)
            {
                scale = math.max(scale, tuning.Communications.ConsoleQualityScale);
                bias = math.max(bias, tuning.Communications.ConsoleQualityBias);
                any = true;
            }

            if ((domains & AgencyDomain.FlightOps) != 0)
            {
                scale = math.max(scale, tuning.FlightOps.ConsoleQualityScale);
                bias = math.max(bias, tuning.FlightOps.ConsoleQualityBias);
                any = true;
            }

            if (!any)
            {
                return;
            }

            quality = math.saturate(quality * scale + bias);
        }

        public static OfficerProfile BuildOfficerProfile(in IndividualStats stats, in BehaviorDisposition behavior)
        {
            float tactics = math.saturate(stats.Tactics / 100f);
            float engineering = math.saturate(stats.Engineering / 100f);
            float command = math.saturate(stats.Command / 100f);
            float caution = math.saturate(behavior.Caution);
            float patience = math.saturate(behavior.Patience);

            float skill = math.saturate(tactics * 0.5f + engineering * 0.3f + command * 0.2f);
            float temperament = math.saturate((caution + patience) * 0.5f);
            float horizon = math.lerp(2.5f, 10f, math.saturate((skill + temperament) * 0.5f));

            return new OfficerProfile
            {
                ExpectedManeuverHorizonSeconds = horizon,
                RiskTolerance = math.saturate(behavior.RiskTolerance)
            };
        }
    }
}
