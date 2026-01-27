using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Runtime.Skills;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Ships
{
    /// <summary>
    /// Processes refit requests on carrier module slots, respecting facility/field constraints.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ModuleRepairSystem))]
    public partial struct ModuleRefitSystem : ISystem
    {
        private ComponentLookup<ShipModule> _moduleLookup;
        private ComponentLookup<CarrierRefitSettings> _settingsLookup;
        private ComponentLookup<CarrierRefitState> _stateLookup;
        private ComponentLookup<SkillSet> _skillLookup;
        private ComponentLookup<CarrierOwner> _ownerLookup;
        private ComponentLookup<ModuleHealth> _healthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleRefitRequest>();
            state.RequireForUpdate<TimeState>();
            _moduleLookup = state.GetComponentLookup<ShipModule>(true);
            _settingsLookup = state.GetComponentLookup<CarrierRefitSettings>(true);
            _stateLookup = state.GetComponentLookup<CarrierRefitState>(true);
            _skillLookup = state.GetComponentLookup<SkillSet>(true);
            _ownerLookup = state.GetComponentLookup<CarrierOwner>(false);
            _healthLookup = state.GetComponentLookup<ModuleHealth>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused || (SystemAPI.TryGetSingleton(out RewindState rewindState) && rewindState.Mode != RewindMode.Record))
            {
                return;
            }

            _moduleLookup.Update(ref state);
            _settingsLookup.Update(ref state);
            _stateLookup.Update(ref state);
            _skillLookup.Update(ref state);
            _ownerLookup.Update(ref state);
            _healthLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var refitQuery = SystemAPI.QueryBuilder()
                .WithAll<CarrierModuleSlot, ModuleRefitRequest>()
                .Build();

            var entities = refitQuery.ToEntityArray(state.WorldUpdateAllocator);
            foreach (var entity in entities)
            {
                if (!state.EntityManager.HasBuffer<CarrierModuleSlot>(entity) || !state.EntityManager.HasBuffer<ModuleRefitRequest>(entity))
                {
                    continue;
                }

                var slots = state.EntityManager.GetBuffer<CarrierModuleSlot>(entity);
                var refits = state.EntityManager.GetBuffer<ModuleRefitRequest>(entity);
                if (refits.Length == 0)
                {
                    continue;
                }

                var settings = _settingsLookup.HasComponent(entity)
                    ? _settingsLookup[entity]
                    : CarrierRefitSettings.CreateDefaults();

                var refitState = _stateLookup.HasComponent(entity)
                    ? _stateLookup[entity]
                    : new CarrierRefitState { InRefitFacility = 0, SpeedMultiplier = 1f };

                var skillLevel = _skillLookup.HasComponent(entity)
                    ? _skillLookup[entity].GetLevel(SkillId.ShipRefit)
                    : (byte)0;

                var maxConcurrent = settings.MaxConcurrent == 0 ? (byte)1 : settings.MaxConcurrent;
                var active = 0;

                for (int i = refits.Length - 1; i >= 0; i--)
                {
                    if (active >= maxConcurrent)
                    {
                        continue;
                    }

                    var request = refits[i];
                    if (request.SlotIndex >= slots.Length || request.NewModule == Entity.Null || !_moduleLookup.HasComponent(request.NewModule))
                    {
                        refits.RemoveAt(i);
                        continue;
                    }

                    var module = _moduleLookup[request.NewModule];
                    if (!ModuleMaintenanceUtility.IsRefitAllowed(request.Kind, settings, refitState))
                    {
                        continue;
                    }

                    if (request.Status == ModuleRefitStatus.Pending)
                    {
                        var duration = ModuleMaintenanceUtility.CalculateRefitDuration(module, settings, skillLevel, refitState.SpeedMultiplier <= 0f ? 1f : refitState.SpeedMultiplier);
                        request.Status = ModuleRefitStatus.InProgress;
                        request.StartedTick = timeState.Tick;
                        request.ReadyTick = timeState.Tick + duration;
                        refits[i] = request;
                        active++;
                        continue;
                    }

                    if (timeState.Tick < request.ReadyTick)
                    {
                        active++;
                        continue;
                    }

                    var slot = slots[request.SlotIndex];
                    slot.InstalledModule = request.NewModule;
                    slots[request.SlotIndex] = slot;

                    if (_ownerLookup.HasComponent(request.NewModule))
                    {
                        var owner = _ownerLookup[request.NewModule];
                        owner.Carrier = entity;
                        _ownerLookup[request.NewModule] = owner;
                    }
                    else
                    {
                        ecb.AddComponent(request.NewModule, new CarrierOwner { Carrier = entity });
                    }

                    if (_healthLookup.HasComponent(request.NewModule))
                    {
                        var health = _healthLookup[request.NewModule];
                        health.LastProcessedTick = timeState.Tick;
                        _healthLookup[request.NewModule] = health;
                    }

                    refits.RemoveAt(i);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
