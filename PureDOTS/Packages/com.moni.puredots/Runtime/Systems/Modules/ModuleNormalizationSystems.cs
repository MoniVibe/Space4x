using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Identity;
using PureDOTS.Runtime.Modules;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Modules
{
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(AISystemGroup))]
    [UpdateBefore(typeof(ResourceSystemGroup))]
    public partial struct ModuleStateMachineSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ModuleRuntimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var deltaTime = time.FixedDeltaTime;

            foreach (var (runtime, specRef, request, commands) in SystemAPI
                         .Query<RefRW<ModuleRuntimeState>, RefRO<ModuleSpec>, RefRW<ModulePowerRequest>, DynamicBuffer<ModuleCommand>>())
            {
                var stateChanged = false;
                for (var i = 0; i < commands.Length; i++)
                {
                    var command = commands[i];
                    if ((command.Flags & ModuleCommandFlags.Posture) != 0)
                    {
                        if (runtime.ValueRW.Posture != command.Posture)
                        {
                            runtime.ValueRW.Posture = command.Posture;
                            stateChanged = true;
                        }
                    }

                    if ((command.Flags & ModuleCommandFlags.TargetOutput) != 0)
                    {
                        runtime.ValueRW.TargetOutput = math.saturate(command.TargetOutput);
                    }
                }

                commands.Clear();

                if (stateChanged)
                {
                    runtime.ValueRW.TimeInState = 0f;
                }
                else
                {
                    runtime.ValueRW.TimeInState += deltaTime;
                }

                request.ValueRW.RequestedPower = ResolvePowerDraw(runtime.ValueRO.Posture, specRef.ValueRO.Spec);
            }
        }

        private static float ResolvePowerDraw(ModulePosture posture, BlobAssetReference<ModuleSpecBlob> specRef)
        {
            if (!specRef.IsCreated)
            {
                return 0f;
            }

            ref var spec = ref specRef.Value;
            return posture switch
            {
                ModulePosture.Off => spec.PowerDrawOff,
                ModulePosture.Standby => spec.PowerDrawStandby,
                ModulePosture.Online => spec.PowerDrawOnline,
                ModulePosture.Emergency => spec.PowerDrawEmergency,
                _ => spec.PowerDrawOff
            };
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ModuleStateMachineSystem))]
    [UpdateBefore(typeof(ResourceSystemGroup))]
    public partial struct ModulePowerBudgetSystem : ISystem
    {
        private ComponentLookup<ModulePowerRequest> _requestLookup;
        private ComponentLookup<ModulePowerAllocation> _allocationLookup;
        private ComponentLookup<ModulePowerSupply> _supplyLookup;
        private ComponentLookup<EnergyPool> _energyLookup;
        private ComponentLookup<ModulePowerBudget> _budgetLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();
            state.RequireForUpdate<TimeState>();

            _requestLookup = state.GetComponentLookup<ModulePowerRequest>(true);
            _allocationLookup = state.GetComponentLookup<ModulePowerAllocation>(false);
            _supplyLookup = state.GetComponentLookup<ModulePowerSupply>(true);
            _energyLookup = state.GetComponentLookup<EnergyPool>(true);
            _budgetLookup = state.GetComponentLookup<ModulePowerBudget>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var deltaTime = time.FixedDeltaTime;

            _requestLookup.Update(ref state);
            _allocationLookup.Update(ref state);
            _supplyLookup.Update(ref state);
            _energyLookup.Update(ref state);
            _budgetLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                var totalRequested = 0f;
                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_requestLookup.HasComponent(module))
                    {
                        continue;
                    }

                    totalRequested += math.max(0f, _requestLookup[module].RequestedPower);
                }

                var availablePower = ResolveAvailablePower(owner, deltaTime);
                var supplyRatio = totalRequested > 0f
                    ? math.saturate(availablePower / totalRequested)
                    : 1f;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_allocationLookup.HasComponent(module))
                    {
                        continue;
                    }

                    var requested = _requestLookup.HasComponent(module)
                        ? math.max(0f, _requestLookup[module].RequestedPower)
                        : 0f;

                    _allocationLookup[module] = new ModulePowerAllocation
                    {
                        AllocatedPower = requested * supplyRatio,
                        SupplyRatio = supplyRatio
                    };
                }

                var budget = new ModulePowerBudget
                {
                    TotalRequested = totalRequested,
                    TotalAllocated = totalRequested * supplyRatio,
                    SupplyRatio = supplyRatio
                };

                if (_budgetLookup.HasComponent(owner))
                {
                    _budgetLookup[owner] = budget;
                }
                else
                {
                    ecb.AddComponent(owner, budget);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private float ResolveAvailablePower(Entity owner, float deltaTime)
        {
            if (_supplyLookup.HasComponent(owner))
            {
                return math.max(0f, _supplyLookup[owner].AvailablePower);
            }

            if (_energyLookup.HasComponent(owner))
            {
                var pool = _energyLookup[owner];
                return math.max(0f, pool.Current + pool.RegenPerSecond * deltaTime);
            }

            return float.PositiveInfinity;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ModulePowerBudgetSystem))]
    [UpdateBefore(typeof(ResourceSystemGroup))]
    public partial struct ModuleNormalizationSystem : ISystem
    {
        private ComponentLookup<ModuleOwner> _ownerLookup;
        private ComponentLookup<EngineeringCohesion> _engineeringLookup;
        private ComponentLookup<NavigationCohesion> _navigationLookup;
        private ComponentLookup<BridgeTechLevel> _bridgeTechLookup;
        private ComponentLookup<ModulePowerAllocation> _allocationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ModuleRuntimeState>();

            _ownerLookup = state.GetComponentLookup<ModuleOwner>(true);
            _engineeringLookup = state.GetComponentLookup<EngineeringCohesion>(true);
            _navigationLookup = state.GetComponentLookup<NavigationCohesion>(true);
            _bridgeTechLookup = state.GetComponentLookup<BridgeTechLevel>(true);
            _allocationLookup = state.GetComponentLookup<ModulePowerAllocation>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            var deltaTime = time.FixedDeltaTime;

            _ownerLookup.Update(ref state);
            _engineeringLookup.Update(ref state);
            _navigationLookup.Update(ref state);
            _bridgeTechLookup.Update(ref state);
            _allocationLookup.Update(ref state);

            foreach (var (runtime, specRef, entity) in SystemAPI
                         .Query<RefRW<ModuleRuntimeState>, RefRO<ModuleSpec>>()
                         .WithEntityAccess())
            {
                if (!specRef.ValueRO.Spec.IsCreated)
                {
                    continue;
                }

                var powerRatio = 1f;
                if (_allocationLookup.HasComponent(entity))
                {
                    powerRatio = math.saturate(_allocationLookup[entity].SupplyRatio);
                }

                var targetOutput = math.saturate(runtime.ValueRO.TargetOutput) * powerRatio;
                var tau = ResolveTau(runtime.ValueRO.Posture, runtime.ValueRO.NormalizedOutput, targetOutput, specRef.ValueRO.Spec);
                tau = ApplyCohesionAndTech(tau, entity);

                var maxStep = tau > 0f ? deltaTime / tau : 1f;
                if (specRef.ValueRO.Spec.Value.RampRateLimit > 0f)
                {
                    maxStep = math.min(maxStep, specRef.ValueRO.Spec.Value.RampRateLimit * deltaTime);
                }

                var delta = targetOutput - runtime.ValueRO.NormalizedOutput;
                var step = math.clamp(delta, -maxStep, maxStep);
                runtime.ValueRW.NormalizedOutput = math.saturate(runtime.ValueRO.NormalizedOutput + step);
            }
        }

        private static float ResolveTau(ModulePosture posture, float currentOutput, float targetOutput, BlobAssetReference<ModuleSpecBlob> specRef)
        {
            ref var spec = ref specRef.Value;
            var increasing = targetOutput > currentOutput + 0.0001f;

            if (increasing)
            {
                return posture == ModulePosture.Off ? spec.TauColdToOnline : spec.TauWarmToOnline;
            }

            return posture == ModulePosture.Off ? spec.TauStandbyToOff : spec.TauOnlineToStandby;
        }

        private float ApplyCohesionAndTech(float tau, Entity moduleEntity)
        {
            if (!_ownerLookup.HasComponent(moduleEntity))
            {
                return math.max(0.01f, tau);
            }

            var owner = _ownerLookup[moduleEntity].Owner;
            var engineering = _engineeringLookup.HasComponent(owner) ? _engineeringLookup[owner].Value : 0f;
            var navigation = _navigationLookup.HasComponent(owner) ? _navigationLookup[owner].Value : 0f;
            var tech = _bridgeTechLookup.HasComponent(owner) ? _bridgeTechLookup[owner].Value : 0f;

            var cohesionBoost = 1f + math.saturate(engineering) * 0.25f + math.saturate(navigation) * 0.2f;
            var techBoost = 1f + math.saturate(tech) * 0.3f;
            return math.max(0.01f, tau / (cohesionBoost * techBoost));
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(ModuleNormalizationSystem))]
    [UpdateBefore(typeof(ResourceSystemGroup))]
    public partial struct ModuleEffectsSystem : ISystem
    {
        private ComponentLookup<ModuleRuntimeState> _runtimeLookup;
        private ComponentLookup<ModuleSpec> _specLookup;
        private ComponentLookup<ModuleCapabilityOutput> _capabilityLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleAttachment>();

            _runtimeLookup = state.GetComponentLookup<ModuleRuntimeState>(true);
            _specLookup = state.GetComponentLookup<ModuleSpec>(true);
            _capabilityLookup = state.GetComponentLookup<ModuleCapabilityOutput>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _runtimeLookup.Update(ref state);
            _specLookup.Update(ref state);
            _capabilityLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (modules, owner) in SystemAPI.Query<DynamicBuffer<ModuleAttachment>>().WithEntityAccess())
            {
                var thrust = 0f;
                var turn = 0f;

                for (var i = 0; i < modules.Length; i++)
                {
                    var module = modules[i].Module;
                    if (module == Entity.Null || !_runtimeLookup.HasComponent(module) || !_specLookup.HasComponent(module))
                    {
                        continue;
                    }

                    var specRef = _specLookup[module].Spec;
                    if (!specRef.IsCreated)
                    {
                        continue;
                    }

                    var output = math.saturate(_runtimeLookup[module].NormalizedOutput) * math.max(0f, specRef.Value.MaxOutput);
                    switch (specRef.Value.Capability)
                    {
                        case ModuleCapabilityKind.ThrustAuthority:
                            thrust += output;
                            break;
                        case ModuleCapabilityKind.TurnAuthority:
                            turn += output;
                            break;
                    }
                }

                if (_capabilityLookup.HasComponent(owner))
                {
                    _capabilityLookup[owner] = new ModuleCapabilityOutput
                    {
                        ThrustAuthority = thrust,
                        TurnAuthority = turn
                    };
                }
                else
                {
                    ecb.AddComponent(owner, new ModuleCapabilityOutput
                    {
                        ThrustAuthority = thrust,
                        TurnAuthority = turn
                    });
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
