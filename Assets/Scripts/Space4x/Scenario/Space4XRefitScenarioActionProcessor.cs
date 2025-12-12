using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4x.Scenario
{
    /// <summary>
    /// Processes timed scenario actions (degrade, repair, refit, move) based on current simulation time.
    /// Note: Not Burst-compiled due to EntityManager operations and string parsing requirements.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(FacilityProximitySystem))]
    public partial struct Space4XRefitScenarioActionProcessor : ISystem
    {
        private BufferLookup<CarrierModuleSlot> _slotLookup;
        private ComponentLookup<ModuleHealth> _healthLookup;
        private ComponentLookup<ModuleTypeId> _moduleTypeLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<MovementCommand> _movementLookup;
        private BufferLookup<ModuleRefitRequest> _refitRequestBufferLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<ScenarioActionScheduler>();
            _slotLookup = state.GetBufferLookup<CarrierModuleSlot>(false);
            _healthLookup = state.GetComponentLookup<ModuleHealth>(false);
            _moduleTypeLookup = state.GetComponentLookup<ModuleTypeId>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _movementLookup = state.GetComponentLookup<MovementCommand>(false);
            _refitRequestBufferLookup = state.GetBufferLookup<ModuleRefitRequest>(false);
        }

        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var currentTimeSeconds = time.Tick * time.FixedDeltaTime;

            _slotLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _moduleTypeLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _refitRequestBufferLookup.Update(ref state);

            foreach (var (scheduler, actions, entity) in SystemAPI.Query<RefRW<ScenarioActionScheduler>, DynamicBuffer<ScenarioActionEntry>>().WithEntityAccess())
            {
                var lastProcessed = scheduler.ValueRO.LastProcessedTime;
                
                for (int i = 0; i < actions.Length; i++)
                {
                    var action = actions[i];
                    if (action.TimeSeconds > lastProcessed && action.TimeSeconds <= currentTimeSeconds)
                    {
                        ProcessAction(ref state, action);
                    }
                }

                scheduler.ValueRW.LastProcessedTime = currentTimeSeconds;
            }
        }

        private void ProcessAction(ref SystemState state, ScenarioActionEntry action)
        {
            var degradeAction = new FixedString64Bytes("degradeEfficiency");
            var repairAction = new FixedString64Bytes("issueRepair");
            var moveAction = new FixedString64Bytes("moveTo");
            var refitAction = new FixedString64Bytes("issueRefit");

            if (action.ActionType == degradeAction)
            {
                ProcessDegradeAction(ref state, action);
            }
            else if (action.ActionType == repairAction)
            {
                ProcessRepairAction(ref state, action);
            }
            else if (action.ActionType == moveAction)
            {
                ProcessMoveAction(ref state, action);
            }
            else if (action.ActionType == refitAction)
            {
                ProcessRefitAction(ref state, action);
            }
        }

        private void ProcessDegradeAction(ref SystemState state, ScenarioActionEntry action)
        {
            if (!TryResolveModule(ref state, action.Target, out var moduleEntity))
            {
                return;
            }

            if (!_healthLookup.HasComponent(moduleEntity))
            {
                return;
            }

            var health = _healthLookup[moduleEntity];
            var targetEfficiency = math.clamp(action.FloatValue, 0f, 1f);
            health.CurrentHealth = health.MaxHealth * targetEfficiency;
            health.Failed = (byte)(health.CurrentHealth <= 0f ? 1 : 0);
            _healthLookup[moduleEntity] = health;
        }

        private void ProcessRepairAction(ref SystemState state, ScenarioActionEntry action)
        {
            var fieldMode = new FixedString64Bytes("Field");
            if (action.Mode != fieldMode)
            {
                return;
            }

            if (action.Target.IsEmpty)
            {
                return;
            }

            var startIdx = 0;
            var target = action.Target;
            
            while (startIdx < target.Length)
            {
                var commaIdx = FindByte(target, (byte)',', startIdx);
                var endIdx = commaIdx >= 0 ? commaIdx : target.Length;
                var length = endIdx - startIdx;
                
                if (length > 0)
                {
                    var targetSlice = target.Substring(startIdx, length);
                    var trimmed = TrimFixedString(targetSlice);
                    if (TryResolveModule(ref state, trimmed, out var moduleEntity))
                    {
                        if (_healthLookup.HasComponent(moduleEntity))
                        {
                            var health = _healthLookup[moduleEntity];
                            if (health.CurrentHealth < health.MaxFieldRepairHealth)
                            {
                                health.CurrentHealth = math.min(health.MaxFieldRepairHealth, health.CurrentHealth + health.MaxHealth * 0.1f);
                                health.Failed = 0;
                                _healthLookup[moduleEntity] = health;
                            }
                        }
                    }
                }
                
                if (commaIdx < 0)
                {
                    break;
                }
                startIdx = commaIdx + 1;
            }
        }

        private FixedString128Bytes TrimFixedString(FixedString128Bytes str)
        {
            var start = 0;
            var end = str.Length;
            
            while (start < end && (str[start] == ' ' || str[start] == '\t'))
            {
                start++;
            }
            
            while (end > start && (str[end - 1] == ' ' || str[end - 1] == '\t'))
            {
                end--;
            }
            
            if (start == 0 && end == str.Length)
            {
                return str;
            }
            
            return str.Substring(start, end - start);
        }

        private void ProcessMoveAction(ref SystemState state, ScenarioActionEntry action)
        {
            if (action.TargetEntityId.IsEmpty)
            {
                return;
            }

            Entity? targetStation = null;
            
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<RefitFacilityTag>().WithEntityAccess())
            {
                targetStation = entity;
                break;
            }

            if (!targetStation.HasValue)
            {
                return;
            }

            var targetPosition = _transformLookup[targetStation.Value].Position;

            foreach (var (movement, carrierEntity) in SystemAPI.Query<RefRW<MovementCommand>>().WithAll<Carrier>().WithEntityAccess())
            {
                movement.ValueRW = new MovementCommand
                {
                    TargetPosition = targetPosition,
                    ArrivalThreshold = 1f
                };
                break;
            }
        }

        private void ProcessRefitAction(ref SystemState state, ScenarioActionEntry action)
        {
            if (action.SlotIndex < 0 || action.NewModuleId.IsEmpty)
            {
                return;
            }

            Entity? carrierEntity = null;
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithAll<Carrier>().WithEntityAccess())
            {
                carrierEntity = entity;
                break;
            }

            if (!carrierEntity.HasValue)
            {
                return;
            }

            if (!_slotLookup.HasBuffer(carrierEntity.Value))
            {
                return;
            }

            var slots = _slotLookup[carrierEntity.Value];
            if (action.SlotIndex >= slots.Length)
            {
                return;
            }

            var newModuleEntity = CreateModuleEntity(ref state, action.NewModuleId);
            if (newModuleEntity == Entity.Null)
            {
                return;
            }

            if (!_refitRequestBufferLookup.HasBuffer(carrierEntity.Value))
            {
                state.EntityManager.AddBuffer<ModuleRefitRequest>(carrierEntity.Value);
                _refitRequestBufferLookup.Update(ref state);
            }

            var requests = _refitRequestBufferLookup[carrierEntity.Value];
            var time = SystemAPI.GetSingleton<TimeState>();
            
            requests.Add(new ModuleRefitRequest
            {
                SlotIndex = action.SlotIndex,
                TargetModule = newModuleEntity,
                Priority = 0,
                RequestTick = time.Tick,
                RequiredWork = 0f
            });
        }

        private bool TryResolveModule(ref SystemState state, FixedString128Bytes target, out Entity moduleEntity)
        {
            moduleEntity = Entity.Null;
            
            var shipPrefix = new FixedString128Bytes("ship[");
            if (!target.StartsWith(shipPrefix))
            {
                return false;
            }

            var slotPrefix = new FixedString128Bytes(".slot[");
            var slotStartIdx = target.IndexOf(slotPrefix);
            if (slotStartIdx < 0)
            {
                return false;
            }

            var shipEndIdx = FindByte(target, (byte)']', 5);
            if (shipEndIdx < 0 || shipEndIdx >= slotStartIdx)
            {
                return false;
            }

            var shipIndex = ParseIntFromFixedString(target, 5, shipEndIdx - 5);
            var slotEndIdx = FindByte(target, (byte)']', slotStartIdx + slotPrefix.Length);
            if (slotEndIdx < 0)
            {
                return false;
            }

            var slotIndex = ParseIntFromFixedString(target, slotStartIdx + slotPrefix.Length, slotEndIdx - (slotStartIdx + slotPrefix.Length));
            
            if (shipIndex < 0 || slotIndex < 0)
            {
                return false;
            }

            Entity? carrierEntity = null;
            int carrierCount = 0;
            foreach (var (_, entity) in SystemAPI.Query<DynamicBuffer<CarrierModuleSlot>>().WithAll<Carrier>().WithEntityAccess())
            {
                if (carrierCount == shipIndex)
                {
                    carrierEntity = entity;
                    break;
                }
                carrierCount++;
            }

            if (!carrierEntity.HasValue)
            {
                return false;
            }

            if (!_slotLookup.HasBuffer(carrierEntity.Value))
            {
                return false;
            }

            var slots = _slotLookup[carrierEntity.Value];
            if (slotIndex < 0 || slotIndex >= slots.Length)
            {
                return false;
            }

            moduleEntity = slots[slotIndex].CurrentModule;
            return moduleEntity != Entity.Null;
        }

        private static int FindByte(FixedString128Bytes str, byte value, int startIndex)
        {
            for (var i = math.max(0, startIndex); i < str.Length; i++)
            {
                if (str[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private Entity CreateModuleEntity(ref SystemState state, FixedString64Bytes moduleId)
        {
            if (!ModuleCatalogUtility.TryGetModuleSpec(ref state, moduleId, out var spec))
            {
                return Entity.Null;
            }

            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(entity, new ModuleTypeId { Value = moduleId });
            state.EntityManager.AddComponentData(entity, new ModuleSlotRequirement
            {
                SlotSize = ConvertMountSize(spec.RequiredSize)
            });

            state.EntityManager.AddComponentData(entity, new ModuleStatModifier
            {
                SpeedMultiplier = 1f,
                CargoMultiplier = 1f,
                EnergyMultiplier = 1f,
                RefitRateMultiplier = 1f,
                RepairRateMultiplier = 1f
            });

            var maxHealth = 100f;
            state.EntityManager.AddComponentData(entity, new ModuleHealth
            {
                MaxHealth = maxHealth,
                CurrentHealth = maxHealth * spec.DefaultEfficiency,
                MaxFieldRepairHealth = maxHealth * 0.8f,
                DegradationPerSecond = 0f,
                RepairPriority = 128,
                Failed = 0
            });

            return entity;
        }

        private int ParseIntFromFixedString(FixedString128Bytes str, int start, int length)
        {
            var value = 0;
            var sign = 1;
            var end = start + length;
            if (start >= end || start < 0 || end > str.Length)
            {
                return -1;
            }

            if (str[start] == '-')
            {
                sign = -1;
                start++;
            }

            for (int i = start; i < end; i++)
            {
                var c = str[i];
                if (c >= '0' && c <= '9')
                {
                    value = value * 10 + (c - '0');
                }
                else
                {
                    return -1;
                }
            }

            return value * sign;
        }

        private ModuleSlotSize ConvertMountSize(MountSize size)
        {
            return size switch
            {
                MountSize.S => ModuleSlotSize.Small,
                MountSize.M => ModuleSlotSize.Medium,
                MountSize.L => ModuleSlotSize.Large,
                _ => ModuleSlotSize.Medium
            };
        }
    }
}

