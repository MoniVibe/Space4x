using PureDOTS.Runtime;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Production;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XFacilityBusinessBootstrapSystem))]
    public partial struct Space4XFacilityProductionRuntimeBootstrapSystem : ISystem
    {
        private ComponentLookup<ProductionQueueCapacity> _queueCapacityLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FacilityBusinessClassComponent>();
            _queueCapacityLookup = state.GetComponentLookup<ProductionQueueCapacity>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _queueCapacityLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (facilityClass, production, entity) in SystemAPI
                         .Query<RefRO<FacilityBusinessClassComponent>, RefRO<BusinessProduction>>()
                         .WithEntityAccess())
            {
                var queueCapacity = 4;
                if (_queueCapacityLookup.HasComponent(entity))
                {
                    queueCapacity = math.max(1, _queueCapacityLookup[entity].MaxQueuedJobs);
                }

                var runtimeKind = ResolveFacilityKind(facilityClass.ValueRO.Value);
                var requiredCrew = math.max(1f, production.ValueRO.Capacity * 0.004f);
                var requiredPower = math.max(1f, production.ValueRO.Capacity * 0.012f);

                if (!state.EntityManager.HasComponent<Space4XProductionRuntime>(entity))
                {
                    ecb.AddComponent(entity, new Space4XProductionRuntime
                    {
                        FacilityKind = runtimeKind,
                        QueueCapacity = (byte)math.clamp(queueCapacity, 1, 32),
                        IsEnabled = 1,
                        LastUpdatedTick = 0u
                    });
                }
                else
                {
                    var runtime = state.EntityManager.GetComponentData<Space4XProductionRuntime>(entity);
                    runtime.FacilityKind = runtimeKind;
                    runtime.QueueCapacity = (byte)math.clamp(queueCapacity, 1, 32);
                    ecb.SetComponent(entity, runtime);
                }

                if (!state.EntityManager.HasComponent<Space4XProductionPowerCrewConstraint>(entity))
                {
                    ecb.AddComponent(entity, new Space4XProductionPowerCrewConstraint
                    {
                        RequiredPowerMw = requiredPower,
                        RequiredCrew = requiredCrew,
                        AssignedPowerMw = requiredPower,
                        AssignedCrew = requiredCrew
                    });
                }
                else
                {
                    var constraint = state.EntityManager.GetComponentData<Space4XProductionPowerCrewConstraint>(entity);
                    constraint.RequiredPowerMw = math.max(1f, constraint.RequiredPowerMw);
                    constraint.RequiredCrew = math.max(1f, constraint.RequiredCrew);
                    if (constraint.AssignedPowerMw <= 0f)
                    {
                        constraint.AssignedPowerMw = constraint.RequiredPowerMw;
                    }

                    if (constraint.AssignedCrew <= 0f)
                    {
                        constraint.AssignedCrew = constraint.RequiredCrew;
                    }

                    ecb.SetComponent(entity, constraint);
                }

                if (!state.EntityManager.HasComponent<Space4XProductionStatus>(entity))
                {
                    ecb.AddComponent(entity, new Space4XProductionStatus
                    {
                        ActiveQueueIndex = -1,
                        ActiveEtaSeconds = 0f,
                        IsBlocked = 0,
                        SeatFill01 = 1f,
                        SkillFactor01 = 0.5f,
                        EffectiveThroughput = math.max(1f, production.ValueRO.Capacity * 0.02f)
                    });
                }

                if (!state.EntityManager.HasBuffer<Space4XProductionQueueEntry>(entity))
                {
                    ecb.AddBuffer<Space4XProductionQueueEntry>(entity);
                }

                if (!state.EntityManager.HasBuffer<Space4XProductionInputLine>(entity))
                {
                    ecb.AddBuffer<Space4XProductionInputLine>(entity);
                }

                if (!state.EntityManager.HasBuffer<Space4XProductionOutputLine>(entity))
                {
                    ecb.AddBuffer<Space4XProductionOutputLine>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static Space4XProductionFacilityKind ResolveFacilityKind(FacilityBusinessClass businessClass)
        {
            return businessClass switch
            {
                FacilityBusinessClass.Refinery => Space4XProductionFacilityKind.Refinery,
                FacilityBusinessClass.Production => Space4XProductionFacilityKind.Fabricator,
                FacilityBusinessClass.ModuleFacility => Space4XProductionFacilityKind.ModuleWorks,
                FacilityBusinessClass.ShipFabrication => Space4XProductionFacilityKind.Shipyard,
                FacilityBusinessClass.Shipyard => Space4XProductionFacilityKind.Shipyard,
                FacilityBusinessClass.Research => Space4XProductionFacilityKind.Fabricator,
                FacilityBusinessClass.Construction => Space4XProductionFacilityKind.Fabricator,
                _ => Space4XProductionFacilityKind.Unknown
            };
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFacilityAutoProductionSystem))]
    [UpdateBefore(typeof(PureDOTS.Runtime.Economy.Production.ProductionJobProgressSystem))]
    public partial struct Space4XFacilityProductionRuntimeSystem : ISystem
    {
        private static readonly FixedString64Bytes RoleCaptain = new FixedString64Bytes("ship.captain");
        private static readonly FixedString64Bytes RoleShipmaster = new FixedString64Bytes("ship.shipmaster");
        private static readonly FixedString64Bytes RoleSensorsOfficer = new FixedString64Bytes("ship.sensors_officer");
        private static readonly FixedString64Bytes RoleLogisticsOfficer = new FixedString64Bytes("ship.logistics_officer");
        private static readonly FixedString64Bytes RoleChiefEngineer = new FixedString64Bytes("ship.chief_engineer");
        private static readonly FixedString64Bytes RoleFlightCommander = new FixedString64Bytes("ship.flight_commander");
        private static readonly FixedString64Bytes RoleFlightDirector = new FixedString64Bytes("ship.flight_director");
        private static readonly FixedString64Bytes RoleHangarDeckOfficer = new FixedString64Bytes("ship.hangar_deck_officer");

        private ComponentLookup<BusinessProduction> _productionLookup;
        private BufferLookup<Space4XProductionQueueEntry> _queueLookup;
        private BufferLookup<Space4XProductionOutputLine> _outputLookup;
        private BufferLookup<AuthoritySeatRef> _seatRefLookup;
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _seatOccupantLookup;
        private ComponentLookup<IndividualStats> _individualStatsLookup;
        private ComponentLookup<CrewSkills> _crewSkillsLookup;
        private EntityStorageInfoLookup _entityLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<TickTimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XProductionRuntime>();

            _productionLookup = state.GetComponentLookup<BusinessProduction>(false);
            _queueLookup = state.GetBufferLookup<Space4XProductionQueueEntry>(false);
            _outputLookup = state.GetBufferLookup<Space4XProductionOutputLine>(false);
            _seatRefLookup = state.GetBufferLookup<AuthoritySeatRef>(true);
            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _seatOccupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
            _individualStatsLookup = state.GetComponentLookup<IndividualStats>(true);
            _crewSkillsLookup = state.GetComponentLookup<CrewSkills>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy)
            {
                return;
            }

            var tickTime = SystemAPI.GetSingleton<TickTimeState>();
            if (tickTime.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var deltaTime = tickTime.FixedDeltaTime * math.max(0f, tickTime.CurrentSpeedMultiplier);
            if (deltaTime <= 0f)
            {
                return;
            }

            var hasRecipeCatalog = SystemAPI.TryGetSingleton(out ProductionRecipeCatalog recipeCatalog);

            _productionLookup.Update(ref state);
            _queueLookup.Update(ref state);
            _outputLookup.Update(ref state);
            _seatRefLookup.Update(ref state);
            _seatLookup.Update(ref state);
            _seatOccupantLookup.Update(ref state);
            _individualStatsLookup.Update(ref state);
            _crewSkillsLookup.Update(ref state);
            _entityLookup.Update(ref state);

            foreach (var (runtime, constraint, status, entity) in SystemAPI
                         .Query<RefRW<Space4XProductionRuntime>, RefRW<Space4XProductionPowerCrewConstraint>, RefRW<Space4XProductionStatus>>()
                         .WithEntityAccess())
            {
                if (!IsValidEntity(entity))
                {
                    continue;
                }

                if (!_queueLookup.HasBuffer(entity))
                {
                    continue;
                }

                var queue = _queueLookup[entity];

                var workforce = ResolveWorkforceSnapshot(entity, runtime.ValueRO.FacilityKind);

                var constraintValue = constraint.ValueRO;
                constraintValue.RequiredCrew = math.max(1f, constraintValue.RequiredCrew);
                constraintValue.RequiredPowerMw = math.max(1f, constraintValue.RequiredPowerMw);
                if (constraintValue.AssignedPowerMw <= 0f)
                {
                    constraintValue.AssignedPowerMw = constraintValue.RequiredPowerMw;
                }

                if (workforce.HasRelevantSeats != 0)
                {
                    constraintValue.AssignedCrew = workforce.FilledSeats;
                    if (constraintValue.RequiredCrew < workforce.RelevantSeats)
                    {
                        constraintValue.RequiredCrew = workforce.RelevantSeats;
                    }
                }
                else if (constraintValue.AssignedCrew <= 0f)
                {
                    constraintValue.AssignedCrew = constraintValue.RequiredCrew;
                }

                var crewRatio = math.saturate(constraintValue.AssignedCrew / math.max(1f, constraintValue.RequiredCrew));
                if (workforce.HasRelevantSeats != 0)
                {
                    crewRatio = math.min(crewRatio, workforce.Fill01);
                }

                var powerRatio = math.saturate(constraintValue.AssignedPowerMw / math.max(1f, constraintValue.RequiredPowerMw));
                var blocked = runtime.ValueRO.IsEnabled == 0 || crewRatio < 0.05f || powerRatio < 0.05f;

                var baseThroughput = 1f;
                if (_productionLookup.HasComponent(entity))
                {
                    baseThroughput = math.max(1f, _productionLookup[entity].Capacity * 0.02f);
                }

                var seatScalar = workforce.HasRelevantSeats != 0
                    ? math.lerp(0.35f, 1.25f, workforce.Fill01)
                    : 1f;
                var skillScalar = math.lerp(0.85f, 1.25f, workforce.Skill01);
                var constraintScalar = math.max(0.2f, crewRatio * powerRatio);
                var effectiveThroughput = runtime.ValueRO.IsEnabled == 0
                    ? 0f
                    : math.max(0f, baseThroughput * seatScalar * skillScalar * constraintScalar);

                if (_productionLookup.HasComponent(entity))
                {
                    var production = _productionLookup[entity];
                    production.Throughput = effectiveThroughput;
                    production.LastUpdateTick = tickTime.Tick;
                    _productionLookup[entity] = production;
                }

                var statusValue = status.ValueRO;
                statusValue.SeatFill01 = workforce.HasRelevantSeats != 0 ? workforce.Fill01 : 1f;
                statusValue.SkillFactor01 = workforce.Skill01;
                statusValue.EffectiveThroughput = effectiveThroughput;

                var activeIndex = ResolveActiveQueueIndex(queue);
                if (activeIndex < 0)
                {
                    statusValue.ActiveQueueIndex = -1;
                    statusValue.ActiveEtaSeconds = 0f;
                    statusValue.IsBlocked = 0;
                    status.ValueRW = statusValue;
                    constraint.ValueRW = constraintValue;
                    runtime.ValueRW.LastUpdatedTick = tickTime.Tick;
                    continue;
                }

                var active = queue[activeIndex];
                statusValue.ActiveQueueIndex = activeIndex;

                if (blocked || effectiveThroughput <= 0.001f)
                {
                    if (active.State == Space4XProductionEntryState.Running)
                    {
                        active.State = Space4XProductionEntryState.Blocked;
                    }

                    statusValue.ActiveEtaSeconds = math.max(0f, active.EtaSeconds);
                    statusValue.IsBlocked = 1;
                    queue[activeIndex] = active;
                    status.ValueRW = statusValue;
                    constraint.ValueRW = constraintValue;
                    runtime.ValueRW.LastUpdatedTick = tickTime.Tick;
                    continue;
                }

                if (active.State == Space4XProductionEntryState.Queued || active.State == Space4XProductionEntryState.Blocked)
                {
                    active.State = Space4XProductionEntryState.Running;
                }

                if (active.StartedTick == 0u)
                {
                    active.StartedTick = tickTime.Tick;
                }

                if (active.EtaSeconds <= 0.001f)
                {
                    active.EtaSeconds = ResolveInitialEtaSeconds(active, effectiveThroughput, hasRecipeCatalog, in recipeCatalog);
                }

                active.EtaSeconds = math.max(0f, active.EtaSeconds - deltaTime * math.max(0.1f, effectiveThroughput));
                if (active.EtaSeconds <= 0.001f)
                {
                    active.EtaSeconds = 0f;
                    active.State = Space4XProductionEntryState.Completed;
                    MarkOutputCompleted(entity, active.EntryId);
                }

                statusValue.ActiveEtaSeconds = active.EtaSeconds;
                statusValue.IsBlocked = 0;

                queue[activeIndex] = active;
                status.ValueRW = statusValue;
                constraint.ValueRW = constraintValue;
                runtime.ValueRW.LastUpdatedTick = tickTime.Tick;
            }
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }

        private static int ResolveActiveQueueIndex(DynamicBuffer<Space4XProductionQueueEntry> queue)
        {
            var runningIndex = -1;
            var queuedIndex = -1;
            byte bestPriority = byte.MaxValue;
            uint oldestTick = uint.MaxValue;

            for (int i = 0; i < queue.Length; i++)
            {
                var entry = queue[i];
                if (entry.State == Space4XProductionEntryState.Completed || entry.State == Space4XProductionEntryState.Failed)
                {
                    continue;
                }

                if (entry.State == Space4XProductionEntryState.Running)
                {
                    runningIndex = i;
                    break;
                }

                if (entry.State == Space4XProductionEntryState.Queued || entry.State == Space4XProductionEntryState.Blocked)
                {
                    var isPreferred = queuedIndex < 0 ||
                                      entry.Priority < bestPriority ||
                                      (entry.Priority == bestPriority && entry.QueuedTick < oldestTick);

                    if (isPreferred)
                    {
                        queuedIndex = i;
                        bestPriority = entry.Priority;
                        oldestTick = entry.QueuedTick;
                    }
                }
            }

            if (runningIndex >= 0)
            {
                return runningIndex;
            }

            return queuedIndex;
        }

        private float ResolveInitialEtaSeconds(
            in Space4XProductionQueueEntry entry,
            float effectiveThroughput,
            bool hasRecipeCatalog,
            in ProductionRecipeCatalog recipeCatalog)
        {
            var batchCount = math.max(1, entry.BatchCount);
            var workUnits = 30f * batchCount;

            if (hasRecipeCatalog && !entry.RecipeId.IsEmpty)
            {
                ref var catalog = ref recipeCatalog.Catalog.Value;
                for (int i = 0; i < catalog.Recipes.Length; i++)
                {
                    ref var recipe = ref catalog.Recipes[i];
                    if (recipe.RecipeId.Equals(entry.RecipeId))
                    {
                        workUnits = math.max(5f, recipe.BaseTimeCost * batchCount);
                        break;
                    }
                }
            }

            return math.max(1f, workUnits / math.max(0.1f, effectiveThroughput));
        }

        private void MarkOutputCompleted(Entity facility, in FixedString64Bytes entryId)
        {
            if (!_outputLookup.HasBuffer(facility))
            {
                return;
            }

            var outputs = _outputLookup[facility];
            for (int i = 0; i < outputs.Length; i++)
            {
                var output = outputs[i];
                if (!output.EntryId.Equals(entryId))
                {
                    continue;
                }

                output.ProducedAmount = math.max(output.ProducedAmount, output.PlannedAmount);
                outputs[i] = output;
            }
        }

        private WorkforceSnapshot ResolveWorkforceSnapshot(Entity facility, Space4XProductionFacilityKind facilityKind)
        {
            var snapshot = new WorkforceSnapshot
            {
                HasRelevantSeats = 0,
                RelevantSeats = 0f,
                FilledSeats = 0f,
                Fill01 = 1f,
                Skill01 = ResolveCrewSkillModifier(facility, facilityKind)
            };

            if (!_seatRefLookup.HasBuffer(facility))
            {
                return snapshot;
            }

            var seats = _seatRefLookup[facility];
            if (seats.Length == 0)
            {
                return snapshot;
            }

            var relevant = 0;
            var filled = 0;
            var weightedSkill = 0f;
            var weightTotal = 0f;

            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null || !_seatLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var roleId = _seatLookup[seatEntity].RoleId;
                var seatWeight = ResolveSeatRoleWeight(facilityKind, roleId);
                if (seatWeight <= 0f)
                {
                    continue;
                }

                relevant++;
                if (!_seatOccupantLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                var occupant = _seatOccupantLookup[seatEntity].OccupantEntity;
                if (occupant == Entity.Null || !_entityLookup.Exists(occupant))
                {
                    continue;
                }

                filled++;
                var occupantSkill = 0.45f;
                if (_individualStatsLookup.HasComponent(occupant))
                {
                    occupantSkill = ResolveOccupantSkill(facilityKind, _individualStatsLookup[occupant]);
                }

                weightedSkill += occupantSkill * seatWeight;
                weightTotal += seatWeight;
            }

            if (relevant <= 0)
            {
                return snapshot;
            }

            var seatSkill = weightTotal > 0f ? math.saturate(weightedSkill / weightTotal) : 0.45f;
            snapshot.HasRelevantSeats = 1;
            snapshot.RelevantSeats = relevant;
            snapshot.FilledSeats = filled;
            snapshot.Fill01 = math.saturate((float)filled / relevant);
            snapshot.Skill01 = math.saturate(seatSkill * 0.8f + snapshot.Skill01 * 0.2f);
            return snapshot;
        }

        private float ResolveCrewSkillModifier(Entity facility, Space4XProductionFacilityKind facilityKind)
        {
            if (!_crewSkillsLookup.HasComponent(facility))
            {
                return 0.5f;
            }

            var skills = _crewSkillsLookup[facility];
            return facilityKind switch
            {
                Space4XProductionFacilityKind.Refinery =>
                    math.saturate(skills.MiningSkill * 0.65f + skills.HaulingSkill * 0.35f),
                Space4XProductionFacilityKind.Shipyard =>
                    math.saturate(skills.RepairSkill * 0.5f + skills.HaulingSkill * 0.3f + skills.CombatSkill * 0.2f),
                Space4XProductionFacilityKind.HangarWorks =>
                    math.saturate(skills.RepairSkill * 0.45f + skills.ExplorationSkill * 0.35f + skills.CombatSkill * 0.2f),
                Space4XProductionFacilityKind.ModuleWorks =>
                    math.saturate(skills.RepairSkill * 0.5f + skills.HaulingSkill * 0.25f + skills.MiningSkill * 0.25f),
                _ =>
                    math.saturate(skills.RepairSkill * 0.4f + skills.HaulingSkill * 0.3f + skills.ExplorationSkill * 0.3f)
            };
        }

        private static float ResolveSeatRoleWeight(Space4XProductionFacilityKind facilityKind, in FixedString64Bytes roleId)
        {
            if (roleId.Equals(RoleChiefEngineer))
            {
                return facilityKind switch
                {
                    Space4XProductionFacilityKind.Refinery => 1f,
                    Space4XProductionFacilityKind.ModuleWorks => 1f,
                    Space4XProductionFacilityKind.Shipyard => 0.95f,
                    Space4XProductionFacilityKind.HangarWorks => 0.85f,
                    _ => 0.9f
                };
            }

            if (roleId.Equals(RoleLogisticsOfficer))
            {
                return facilityKind switch
                {
                    Space4XProductionFacilityKind.Refinery => 0.9f,
                    Space4XProductionFacilityKind.Shipyard => 0.85f,
                    Space4XProductionFacilityKind.HangarWorks => 0.75f,
                    _ => 0.8f
                };
            }

            if (roleId.Equals(RoleShipmaster))
            {
                return facilityKind switch
                {
                    Space4XProductionFacilityKind.Refinery => 0.8f,
                    Space4XProductionFacilityKind.Shipyard => 0.75f,
                    _ => 0.65f
                };
            }

            if (roleId.Equals(RoleCaptain))
            {
                return facilityKind == Space4XProductionFacilityKind.Shipyard ? 0.6f : 0.45f;
            }

            if (roleId.Equals(RoleSensorsOfficer))
            {
                return facilityKind == Space4XProductionFacilityKind.Fabricator ? 0.55f : 0.3f;
            }

            if (roleId.Equals(RoleFlightCommander))
            {
                return facilityKind == Space4XProductionFacilityKind.Shipyard || facilityKind == Space4XProductionFacilityKind.HangarWorks ? 0.85f : 0.2f;
            }

            if (roleId.Equals(RoleFlightDirector))
            {
                return facilityKind == Space4XProductionFacilityKind.Shipyard || facilityKind == Space4XProductionFacilityKind.HangarWorks ? 0.95f : 0.2f;
            }

            if (roleId.Equals(RoleHangarDeckOfficer))
            {
                return facilityKind == Space4XProductionFacilityKind.HangarWorks || facilityKind == Space4XProductionFacilityKind.Shipyard ? 1f : 0.25f;
            }

            return 0f;
        }

        private static float ResolveOccupantSkill(Space4XProductionFacilityKind facilityKind, in IndividualStats stats)
        {
            var command = math.saturate((float)stats.Command / 100f);
            var tactics = math.saturate((float)stats.Tactics / 100f);
            var logistics = math.saturate((float)stats.Logistics / 100f);
            var diplomacy = math.saturate((float)stats.Diplomacy / 100f);
            var engineering = math.saturate((float)stats.Engineering / 100f);
            var resolve = math.saturate((float)stats.Resolve / 100f);

            return facilityKind switch
            {
                Space4XProductionFacilityKind.Refinery =>
                    math.saturate(engineering * 0.55f + logistics * 0.35f + command * 0.1f),
                Space4XProductionFacilityKind.ModuleWorks =>
                    math.saturate(engineering * 0.5f + logistics * 0.25f + tactics * 0.15f + resolve * 0.1f),
                Space4XProductionFacilityKind.Shipyard =>
                    math.saturate(engineering * 0.45f + logistics * 0.25f + command * 0.15f + tactics * 0.15f),
                Space4XProductionFacilityKind.HangarWorks =>
                    math.saturate(logistics * 0.35f + tactics * 0.35f + engineering * 0.2f + command * 0.1f),
                _ =>
                    math.saturate(engineering * 0.4f + logistics * 0.25f + command * 0.15f + diplomacy * 0.1f + resolve * 0.1f)
            };
        }

        private struct WorkforceSnapshot
        {
            public byte HasRelevantSeats;
            public float RelevantSeats;
            public float FilledSeats;
            public float Fill01;
            public float Skill01;
        }
    }
}
