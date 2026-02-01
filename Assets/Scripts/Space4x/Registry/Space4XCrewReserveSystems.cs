using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Individual;
using PureDOTS.Runtime.Platform;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;


namespace Space4X.Registry
{
    /// <summary>
    /// Seeds reserve/transfer/training policies for colonies, stations, and ships.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XShipLoopBootstrapSystem))]
    public partial struct Space4XCrewReserveBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (colony, entity) in SystemAPI.Query<RefRO<Space4XColony>>().WithEntityAccess())
            {
                if (!em.HasComponent<CrewReservePolicy>(entity))
                {
                    ecb.AddComponent(entity, new CrewReservePolicy
                    {
                        ReserveFraction = 0.01f,
                        ReserveCap = 0f,
                        RecruitmentRate = 0.05f,
                        TrainingRatePerTick = 0.0005f,
                        MinTraining = 0.1f,
                        MaxTraining = 0.8f
                    });
                }

                if (!em.HasComponent<CrewReservePool>(entity))
                {
                    var baseReserve = math.max(0f, colony.ValueRO.Population * 0.01f);
                    ecb.AddComponent(entity, new CrewReservePool
                    {
                        Available = math.min(5f, baseReserve),
                        MaxReserve = baseReserve,
                        TrainingLevel = 0.1f
                    });
                }
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<StationId>>().WithEntityAccess())
            {
                if (!em.HasComponent<CrewReservePolicy>(entity))
                {
                    ecb.AddComponent(entity, new CrewReservePolicy
                    {
                        ReserveFraction = 0f,
                        ReserveCap = 50f,
                        RecruitmentRate = 0.06f,
                        TrainingRatePerTick = 0.0007f,
                        MinTraining = 0.2f,
                        MaxTraining = 0.85f
                    });
                }

                if (!em.HasComponent<CrewReservePool>(entity))
                {
                    ecb.AddComponent(entity, new CrewReservePool
                    {
                        Available = 5f,
                        MaxReserve = 50f,
                        TrainingLevel = 0.2f
                    });
                }
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<CrewCapacity>>().WithEntityAccess())
            {
                var capacity = em.GetComponentData<CrewCapacity>(entity);
                var hasReservePolicy = em.HasComponent<CrewReservePolicy>(entity);
                var hasReservePool = em.HasComponent<CrewReservePool>(entity);

                if (!hasReservePolicy)
                {
                    var reserveCap = math.max(2f, capacity.MaxCrew * 0.1f);
                    ecb.AddComponent(entity, new CrewReservePolicy
                    {
                        ReserveFraction = 0f,
                        ReserveCap = reserveCap,
                        RecruitmentRate = 0.02f,
                        TrainingRatePerTick = 0.0004f,
                        MinTraining = 0.1f,
                        MaxTraining = 0.8f
                    });
                }

                if (!hasReservePool)
                {
                    var reserveCap = math.max(2f, capacity.MaxCrew * 0.1f);
                    ecb.AddComponent(entity, new CrewReservePool
                    {
                        Available = math.min(2f, reserveCap),
                        MaxReserve = reserveCap,
                        TrainingLevel = 0.15f
                    });
                }

                if (!em.HasComponent<CrewTrainingState>(entity))
                {
                    ecb.AddComponent(entity, new CrewTrainingState
                    {
                        TrainingLevel = 0.2f,
                        TrainingRatePerTick = 0.0003f,
                        MaxTraining = 0.85f,
                        LastUpdateTick = 0u
                    });
                }

                if (!em.HasComponent<CrewTransferPolicy>(entity))
                {
                    ecb.AddComponent(entity, new CrewTransferPolicy
                    {
                        DesiredCrewRatio = 1f,
                        MaxTransferPerTick = 5,
                        MinProviderTraining = 0.1f
                    });
                }

                if (!em.HasComponent<CrewPromotionPolicy>(entity))
                {
                    ecb.AddComponent(entity, new CrewPromotionPolicy
                    {
                        MinTrainingForOfficer = 0.2f,
                        MinCrewReserve = 1,
                        MaxPromotionsPerTick = 1,
                        PromotionCooldownTicks = 30u,
                        LastPromotionTick = 0u
                    });
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }
    }

    /// <summary>
    /// Updates reserve pools by recruiting and training crew over time.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct Space4XCrewReserveUpdateSystem : ISystem
    {
        private ComponentLookup<Space4XColony> _colonyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _colonyLookup = state.GetComponentLookup<Space4XColony>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _colonyLookup.Update(ref state);

            foreach (var (policy, pool, entity) in SystemAPI.Query<RefRO<CrewReservePolicy>, RefRW<CrewReservePool>>().WithEntityAccess())
            {
                float target = math.max(0f, policy.ValueRO.ReserveCap);
                if (_colonyLookup.HasComponent(entity))
                {
                    var population = _colonyLookup[entity].Population;
                    target = math.max(target, population * math.max(0f, policy.ValueRO.ReserveFraction));
                }

                pool.ValueRW.MaxReserve = target;

                var gap = target - pool.ValueRO.Available;
                var rate = math.clamp(policy.ValueRO.RecruitmentRate, 0f, 1f);
                pool.ValueRW.Available = math.clamp(pool.ValueRO.Available + (gap * rate), 0f, target);

                if (pool.ValueRW.Available > 0f)
                {
                    var training = pool.ValueRO.TrainingLevel + policy.ValueRO.TrainingRatePerTick;
                    pool.ValueRW.TrainingLevel = math.clamp(training, policy.ValueRO.MinTraining, policy.ValueRO.MaxTraining);
                }
            }
        }
    }

    /// <summary>
    /// Transfers reserve crew into ships that are under target staffing.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCrewReserveUpdateSystem))]
    public partial struct Space4XCrewTransferSystem : ISystem
    {
        private ComponentLookup<CrewReservePool> _reserveLookup;
        private ComponentLookup<CrewTrainingState> _trainingLookup;
        private ComponentLookup<CrewGrowthState> _growthLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<VesselAIState> _aiLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<CrewTransferMission> _missionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _reserveLookup = state.GetComponentLookup<CrewReservePool>(false);
            _trainingLookup = state.GetComponentLookup<CrewTrainingState>(false);
            _growthLookup = state.GetComponentLookup<CrewGrowthState>(false);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _aiLookup = state.GetComponentLookup<VesselAIState>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _missionLookup = state.GetComponentLookup<CrewTransferMission>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _reserveLookup.Update(ref state);
            _trainingLookup.Update(ref state);
            _growthLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _aiLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _missionLookup.Update(ref state);

            var staticProviders = new NativeList<Entity>(Allocator.Temp);
            var mobileProviders = new NativeList<Entity>(Allocator.Temp);
            foreach (var (pool, entity) in SystemAPI.Query<RefRO<CrewReservePool>>().WithEntityAccess())
            {
                if (pool.ValueRO.Available >= 1f)
                {
                    if (_aiLookup.HasComponent(entity) && _transformLookup.HasComponent(entity))
                    {
                        mobileProviders.Add(entity);
                    }
                    else
                    {
                        staticProviders.Add(entity);
                    }
                }
            }

            if (staticProviders.Length == 0 && mobileProviders.Length == 0)
            {
                staticProviders.Dispose();
                mobileProviders.Dispose();
                return;
            }

            var assignedProviders = new NativeList<Entity>(Allocator.Temp);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (capacity, policy, entity) in SystemAPI.Query<RefRW<CrewCapacity>, RefRO<CrewTransferPolicy>>().WithEntityAccess())
            {
                var maxCrew = capacity.ValueRO.MaxCrew;
                if (maxCrew <= 0)
                {
                    continue;
                }

                var ratio = math.max(0f, policy.ValueRO.DesiredCrewRatio);
                var desired = (int)math.ceil(maxCrew * ratio);
                if (capacity.ValueRO.CriticalMax > 0)
                {
                    desired = math.min(desired, capacity.ValueRO.CriticalMax);
                }

                var currentCrew = capacity.ValueRO.CurrentCrew;
                var needed = desired - currentCrew;
                if (needed <= 0)
                {
                    continue;
                }

                var maxPerTick = policy.ValueRO.MaxTransferPerTick > 0 ? policy.ValueRO.MaxTransferPerTick : needed;
                var remaining = math.min(needed, maxPerTick);

                float weightedTraining = 0f;
                if (_trainingLookup.HasComponent(entity))
                {
                    weightedTraining = _trainingLookup[entity].TrainingLevel * currentCrew;
                }

                var totalTaken = 0;
                for (int i = 0; i < staticProviders.Length && remaining > 0; i++)
                {
                    var provider = staticProviders[i];
                    if (provider == entity || !_reserveLookup.HasComponent(provider))
                    {
                        continue;
                    }

                    if (!HasSharedAffiliation(entity, provider, _affiliationLookup))
                    {
                        continue;
                    }

                    var pool = _reserveLookup[provider];
                    if (pool.Available < 1f || pool.TrainingLevel < policy.ValueRO.MinProviderTraining)
                    {
                        continue;
                    }

                    var available = (int)math.floor(pool.Available);
                    if (available <= 0)
                    {
                        continue;
                    }

                    var take = math.min(remaining, available);
                    if (take <= 0)
                    {
                        continue;
                    }

                    pool.Available -= take;
                    _reserveLookup[provider] = pool;

                    remaining -= take;
                    totalTaken += take;
                    weightedTraining += pool.TrainingLevel * take;
                }

                if (totalTaken > 0)
                {
                    var newCrew = currentCrew + totalTaken;
                    capacity.ValueRW.CurrentCrew = math.min(newCrew, desired);
                    currentCrew = capacity.ValueRW.CurrentCrew;

                    if (_trainingLookup.HasComponent(entity))
                    {
                        var training = _trainingLookup[entity];
                        var blended = currentCrew > 0 ? weightedTraining / currentCrew : training.TrainingLevel;
                        training.TrainingLevel = math.clamp(blended, 0f, training.MaxTraining);
                        _trainingLookup[entity] = training;
                    }

                    if (_growthLookup.HasComponent(entity))
                    {
                        var growth = _growthLookup[entity];
                        growth.CurrentCrew = capacity.ValueRW.CurrentCrew;
                        _growthLookup[entity] = growth;
                    }
                }

                if (remaining <= 0)
                {
                    continue;
                }

                for (int i = 0; i < mobileProviders.Length && remaining > 0; i++)
                {
                    var provider = mobileProviders[i];
                    if (provider == entity || !_reserveLookup.HasComponent(provider))
                    {
                        continue;
                    }

                    if (_missionLookup.HasComponent(provider))
                    {
                        continue;
                    }

                    if (IsProviderAssigned(provider, assignedProviders))
                    {
                        continue;
                    }

                    if (!HasSharedAffiliation(entity, provider, _affiliationLookup))
                    {
                        continue;
                    }

                    if (_aiLookup.HasComponent(provider))
                    {
                        var aiState = _aiLookup[provider];
                        if (aiState.CurrentState != VesselAIState.State.Idle)
                        {
                            continue;
                        }
                    }

                    var pool = _reserveLookup[provider];
                    if (pool.Available < 1f || pool.TrainingLevel < policy.ValueRO.MinProviderTraining)
                    {
                        continue;
                    }

                    var available = (int)math.floor(pool.Available);
                    if (available <= 0)
                    {
                        continue;
                    }

                    var take = math.min(remaining, available);
                    if (take <= 0)
                    {
                        continue;
                    }

                    pool.Available -= take;
                    _reserveLookup[provider] = pool;

                    ecb.AddComponent(provider, new CrewTransferMission
                    {
                        Target = entity,
                        ReservedCrew = take,
                        ReservedTraining = pool.TrainingLevel * take,
                        TransferRadius = 5f,
                        RequestedTick = time.Tick,
                        LastUpdateTick = time.Tick,
                        Status = CrewTransferMissionStatus.Pending,
                        AddedInterceptCapability = 0
                    });

                    assignedProviders.Add(provider);
                    remaining -= take;
                    break;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            assignedProviders.Dispose();
            staticProviders.Dispose();
            mobileProviders.Dispose();
        }

        private static bool IsProviderAssigned(Entity provider, NativeList<Entity> assignedProviders)
        {
            for (int i = 0; i < assignedProviders.Length; i++)
            {
                if (assignedProviders[i] == provider)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSharedAffiliation(Entity a, Entity b, BufferLookup<AffiliationTag> lookup)
        {
            if (!lookup.HasBuffer(a) || !lookup.HasBuffer(b))
            {
                return true;
            }

            var aBuffer = lookup[a];
            var bBuffer = lookup[b];

            if (aBuffer.Length == 0 || bBuffer.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < aBuffer.Length; i++)
            {
                var aTag = aBuffer[i];
                for (int j = 0; j < bBuffer.Length; j++)
                {
                    var bTag = bBuffer[j];
                    if (aTag.Type == bTag.Type && aTag.Target == bTag.Target)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Advances onboard crew training and keeps CrewSkills in sync with baseline training.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XCrewTransferSystem))]
    public partial struct Space4XCrewTrainingSystem : ISystem
    {
        private ComponentLookup<CrewSkills> _skillsLookup;
        private ComponentLookup<SkillExperienceGain> _xpLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _skillsLookup = state.GetComponentLookup<CrewSkills>(false);
            _xpLookup = state.GetComponentLookup<SkillExperienceGain>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<CrewTrainingState>>().WithEntityAccess())
            {
                if (!em.HasComponent<CrewSkills>(entity))
                {
                    ecb.AddComponent(entity, new CrewSkills());
                }

                if (!em.HasComponent<SkillExperienceGain>(entity))
                {
                    ecb.AddComponent(entity, new SkillExperienceGain
                    {
                        MiningXp = 0f,
                        HaulingXp = 0f,
                        CombatXp = 0f,
                        RepairXp = 0f,
                        ExplorationXp = 0f,
                        LastProcessedTick = time.Tick
                    });
                }
            }

            ecb.Playback(em);
            ecb.Dispose();

            _skillsLookup.Update(ref state);
            _xpLookup.Update(ref state);

            foreach (var (training, capacity, entity) in SystemAPI.Query<RefRW<CrewTrainingState>, RefRO<CrewCapacity>>().WithEntityAccess())
            {
                if (capacity.ValueRO.CurrentCrew <= 0)
                {
                    continue;
                }

                var crewRatio = capacity.ValueRO.MaxCrew > 0
                    ? math.saturate((float)capacity.ValueRO.CurrentCrew / capacity.ValueRO.MaxCrew)
                    : 0f;

                if (crewRatio <= 0f)
                {
                    continue;
                }

                var nextTraining = training.ValueRO.TrainingLevel + (training.ValueRO.TrainingRatePerTick * crewRatio);
                nextTraining = math.min(training.ValueRO.MaxTraining, nextTraining);

                training.ValueRW.TrainingLevel = nextTraining;
                training.ValueRW.LastUpdateTick = time.Tick;

                if (!_skillsLookup.HasComponent(entity) || !_xpLookup.HasComponent(entity))
                {
                    continue;
                }

                var targetSkill = math.saturate(nextTraining);
                var targetXp = Space4XSkillUtility.SkillToXp(targetSkill);

                var xp = _xpLookup[entity];
                xp.MiningXp = math.max(xp.MiningXp, targetXp);
                xp.HaulingXp = math.max(xp.HaulingXp, targetXp);
                xp.CombatXp = math.max(xp.CombatXp, targetXp);
                xp.RepairXp = math.max(xp.RepairXp, targetXp);
                xp.ExplorationXp = math.max(xp.ExplorationXp, targetXp);
                xp.LastProcessedTick = time.Tick;
                _xpLookup[entity] = xp;

                var skills = _skillsLookup[entity];
                skills.MiningSkill = math.max(skills.MiningSkill, targetSkill);
                skills.HaulingSkill = math.max(skills.HaulingSkill, targetSkill);
                skills.CombatSkill = math.max(skills.CombatSkill, targetSkill);
                skills.RepairSkill = math.max(skills.RepairSkill, targetSkill);
                skills.ExplorationSkill = math.max(skills.ExplorationSkill, targetSkill);
                _skillsLookup[entity] = skills;
            }
        }
    }

    /// <summary>
    /// Promotes generic crew into officer entities when seats are vacant.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XCrewReserveBootstrapSystem))]
    [UpdateBefore(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XCrewPromotionSystem : ISystem
    {
        private ComponentLookup<AuthoritySeat> _seatLookup;
        private ComponentLookup<AuthoritySeatOccupant> _occupantLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<AuthorityBody>();

            _seatLookup = state.GetComponentLookup<AuthoritySeat>(true);
            _occupantLookup = state.GetComponentLookup<AuthoritySeatOccupant>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            _seatLookup.Update(ref state);
            _occupantLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (body, seats, crew, capacity, training, policy, shipEntity) in SystemAPI.Query<
                         RefRO<AuthorityBody>,
                         DynamicBuffer<AuthoritySeatRef>,
                         DynamicBuffer<PlatformCrewMember>,
                         RefRO<CrewCapacity>,
                         RefRO<CrewTrainingState>,
                         RefRW<CrewPromotionPolicy>>().WithEntityAccess())
            {
                if (seats.Length == 0)
                {
                    continue;
                }

                if (policy.ValueRO.MaxPromotionsPerTick <= 0)
                {
                    continue;
                }

                if (training.ValueRO.TrainingLevel < policy.ValueRO.MinTrainingForOfficer)
                {
                    continue;
                }

                var cooldown = policy.ValueRO.PromotionCooldownTicks;
                if (cooldown > 0u && time.Tick < policy.ValueRO.LastPromotionTick + cooldown)
                {
                    continue;
                }

                var vacant = CountVacantSeats(seats, _seatLookup, _occupantLookup);
                if (vacant <= 0)
                {
                    continue;
                }

                var reserve = capacity.ValueRO.CurrentCrew - crew.Length;
                if (reserve <= 0 || reserve < policy.ValueRO.MinCrewReserve)
                {
                    continue;
                }

                var promotions = math.min(vacant, math.min(reserve, policy.ValueRO.MaxPromotionsPerTick));
                if (promotions <= 0)
                {
                    continue;
                }

                for (int i = 0; i < promotions; i++)
                {
                    var crewEntity = ecb.CreateEntity();
                    ecb.AddComponent<SimIndividualTag>(crewEntity);
                    ecb.AddComponent(crewEntity, new IndividualProfileId
                    {
                        Id = new FixedString64Bytes("baseline")
                    });
                    ecb.AppendToBuffer(shipEntity, new PlatformCrewMember
                    {
                        CrewEntity = crewEntity,
                        RoleId = 0
                    });
                }

                policy.ValueRW.LastPromotionTick = time.Tick;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int CountVacantSeats(
            DynamicBuffer<AuthoritySeatRef> seats,
            ComponentLookup<AuthoritySeat> seatLookup,
            ComponentLookup<AuthoritySeatOccupant> occupantLookup)
        {
            var vacant = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                var seatEntity = seats[i].SeatEntity;
                if (seatEntity == Entity.Null)
                {
                    continue;
                }

                if (!seatLookup.HasComponent(seatEntity) || !occupantLookup.HasComponent(seatEntity))
                {
                    continue;
                }

                if (occupantLookup[seatEntity].OccupantEntity == Entity.Null)
                {
                    vacant++;
                }
            }

            return vacant;
        }
    }
}
