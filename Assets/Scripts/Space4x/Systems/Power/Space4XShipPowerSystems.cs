using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using PureDOTS.Systems;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Power
{
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    public partial struct Space4XShipPowerFocusCommandSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipPowerFocusCommand>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (command, entity) in SystemAPI.Query<RefRO<ShipPowerFocusCommand>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<ShipPowerFocus>(entity))
                {
                    var focus = SystemAPI.GetComponent<ShipPowerFocus>(entity);
                    focus.Mode = command.ValueRO.Mode;
                    SystemAPI.SetComponent(entity, focus);
                }
                else
                {
                    ecb.AddComponent(entity, new ShipPowerFocus { Mode = command.ValueRO.Mode });
                }

                ecb.RemoveComponent<ShipPowerFocusCommand>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4XShipPowerFocusCommandSystem))]
    public partial struct Space4XShipPowerFocusPresetSystem : ISystem
    {
        private ComponentLookup<PowerAllocationTarget> _targetLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipPowerFocus>();
            state.RequireForUpdate<ShipPowerConsumer>();
            _targetLookup = state.GetComponentLookup<PowerAllocationTarget>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _targetLookup.Update(ref state);

            foreach (var (focus, consumers) in SystemAPI.Query<RefRO<ShipPowerFocus>, DynamicBuffer<ShipPowerConsumer>>())
            {
                var mode = focus.ValueRO.Mode;
                for (var i = 0; i < consumers.Length; i++)
                {
                    var consumerEntity = consumers[i].Consumer;
                    if (consumerEntity == Entity.Null || !_targetLookup.HasComponent(consumerEntity))
                    {
                        continue;
                    }

                    var allocation = ResolveAllocation(mode, consumers[i].Type);
                    _targetLookup[consumerEntity] = new PowerAllocationTarget
                    {
                        Value = math.clamp(allocation, 0f, 250f)
                    };
                }
            }
        }

        private static float ResolveAllocation(ShipPowerFocusMode mode, ShipPowerConsumerType type)
        {
            return mode switch
            {
                ShipPowerFocusMode.Balanced => type switch
                {
                    _ => 100f
                },
                ShipPowerFocusMode.Attack => type switch
                {
                    ShipPowerConsumerType.Weapons => 175f,
                    ShipPowerConsumerType.Shields => 50f,
                    ShipPowerConsumerType.Sensors => 75f,
                    ShipPowerConsumerType.Stealth => 75f,
                    ShipPowerConsumerType.Mobility => 100f,
                    ShipPowerConsumerType.LifeSupport => 100f,
                    _ => 100f
                },
                ShipPowerFocusMode.Defense => type switch
                {
                    ShipPowerConsumerType.Weapons => 75f,
                    ShipPowerConsumerType.Shields => 175f,
                    ShipPowerConsumerType.Sensors => 100f,
                    ShipPowerConsumerType.Stealth => 50f,
                    ShipPowerConsumerType.Mobility => 50f,
                    ShipPowerConsumerType.LifeSupport => 100f,
                    _ => 100f
                },
                ShipPowerFocusMode.Mobility => type switch
                {
                    ShipPowerConsumerType.Weapons => 100f,
                    ShipPowerConsumerType.Shields => 0f,
                    ShipPowerConsumerType.Sensors => 125f,
                    ShipPowerConsumerType.Stealth => 75f,
                    ShipPowerConsumerType.Mobility => 175f,
                    ShipPowerConsumerType.LifeSupport => 100f,
                    _ => 100f
                },
                ShipPowerFocusMode.Stealth => type switch
                {
                    ShipPowerConsumerType.Weapons => 50f,
                    ShipPowerConsumerType.Shields => 0f,
                    ShipPowerConsumerType.Sensors => 200f,
                    ShipPowerConsumerType.Stealth => 200f,
                    ShipPowerConsumerType.Mobility => 75f,
                    ShipPowerConsumerType.LifeSupport => 100f,
                    _ => 100f
                },
                ShipPowerFocusMode.Emergency => type switch
                {
                    ShipPowerConsumerType.Weapons => 250f,
                    ShipPowerConsumerType.LifeSupport => 100f,
                    _ => 0f
                },
                _ => 100f
            };
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Power.PowerAllocationSystem))]
    public partial struct Space4XShipPowerAllocationSyncSystem : ISystem
    {
        private ComponentLookup<ShipReactorSpec> _reactorLookup;
        private ComponentLookup<RestartState> _restartLookup;
        private ComponentLookup<PowerAllocationTarget> _targetLookup;
        private ComponentLookup<PowerAllocationPercent> _percentLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ShipPowerConsumer>();
            state.RequireForUpdate<TimeState>();
            _reactorLookup = state.GetComponentLookup<ShipReactorSpec>(true);
            _restartLookup = state.GetComponentLookup<RestartState>(false);
            _targetLookup = state.GetComponentLookup<PowerAllocationTarget>(true);
            _percentLookup = state.GetComponentLookup<PowerAllocationPercent>(false);
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
            _reactorLookup.Update(ref state);
            _restartLookup.Update(ref state);
            _targetLookup.Update(ref state);
            _percentLookup.Update(ref state);

            foreach (var (consumers, shipEntity) in SystemAPI.Query<DynamicBuffer<ShipPowerConsumer>>().WithEntityAccess())
            {
                if (!_restartLookup.HasComponent(shipEntity))
                {
                    continue;
                }

                var restart = _restartLookup[shipEntity];
                var reactorSpec = _reactorLookup.HasComponent(shipEntity)
                    ? _reactorLookup[shipEntity]
                    : default;

                var hotRestart = math.max(0.1f, reactorSpec.HotRestartSeconds);
                var coldRestart = math.max(hotRestart, reactorSpec.ColdRestartSeconds);

                Entity mobilityConsumer = Entity.Null;
                float mobilityTarget = 0f;

                for (var i = 0; i < consumers.Length; i++)
                {
                    var entry = consumers[i];
                    var consumerEntity = entry.Consumer;
                    if (consumerEntity == Entity.Null || !_targetLookup.HasComponent(consumerEntity) ||
                        !_percentLookup.HasComponent(consumerEntity))
                    {
                        continue;
                    }

                    var target = math.clamp(_targetLookup[consumerEntity].Value, 0f, 250f);
                    if (entry.Type == ShipPowerConsumerType.Mobility)
                    {
                        mobilityConsumer = consumerEntity;
                        mobilityTarget = target;
                        continue;
                    }

                    _percentLookup[consumerEntity] = new PowerAllocationPercent { Value = target };
                }

                if (mobilityConsumer != Entity.Null && _percentLookup.HasComponent(mobilityConsumer))
                {
                    var currentPercent = _percentLookup[mobilityConsumer].Value;
                    var wasPowered = currentPercent > 0.01f;
                    var nextPercent = mobilityTarget;

                    if (mobilityTarget <= 0.01f)
                    {
                        nextPercent = 0f;
                        restart.RestartTimer = 0f;
                        restart.Warmth = math.max(0f, restart.Warmth - deltaTime / coldRestart);
                    }
                    else if (restart.RestartTimer > 0f)
                    {
                        restart.RestartTimer = math.max(0f, restart.RestartTimer - deltaTime);
                        if (restart.RestartTimer > 0.01f)
                        {
                            nextPercent = 0f;
                        }
                    }
                    else if (!wasPowered)
                    {
                        var useHot = restart.Warmth >= 0.5f;
                        restart.Mode = useHot ? RestartMode.Hot : RestartMode.Cold;
                        restart.RestartTimer = useHot ? hotRestart : coldRestart;
                        if (restart.RestartTimer > 0.01f)
                        {
                            nextPercent = 0f;
                        }
                    }

                    if (nextPercent > 0.01f)
                    {
                        restart.Warmth = math.min(1f, restart.Warmth + deltaTime / hotRestart);
                    }

                    _percentLookup[mobilityConsumer] = new PowerAllocationPercent
                    {
                        Value = math.clamp(nextPercent, 0f, 250f)
                    };
                }

                _restartLookup[shipEntity] = restart;
            }
        }
    }
}
