using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Ships;
using CombatHitEvent = PureDOTS.Runtime.Combat.HitEvent;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// System that routes HitEvent damage to modules or hull fallback.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DamageResolutionSystem))]
    public partial struct ModuleDamageRouterSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<ShipModule> _moduleLookup;
        private ComponentLookup<ModuleHealth> _healthLookup;
        private BufferLookup<CarrierModuleSlot> _slotBufferLookup;
        private ComponentLookup<Unity.Transforms.LocalTransform> _transformLookup;
        private ComponentLookup<ModulePosition> _positionLookup;
        private ComponentLookup<Health> _shipHealthLookup;
        private BufferLookup<DamageEvent> _damageEventLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityLookup = state.GetEntityStorageInfoLookup();
            _moduleLookup = state.GetComponentLookup<ShipModule>(false);
            _healthLookup = state.GetComponentLookup<ModuleHealth>(false);
            _slotBufferLookup = state.GetBufferLookup<CarrierModuleSlot>(true);
            _transformLookup = state.GetComponentLookup<Unity.Transforms.LocalTransform>(true);
            _positionLookup = state.GetComponentLookup<ModulePosition>(true);
            _shipHealthLookup = state.GetComponentLookup<Health>(false);
            _damageEventLookup = state.GetBufferLookup<DamageEvent>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityLookup.Update(ref state);
            _moduleLookup.Update(ref state);
            _healthLookup.Update(ref state);
            _slotBufferLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _positionLookup.Update(ref state);
            _shipHealthLookup.Update(ref state);
            _damageEventLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            uint currentTick = timeState.Tick;

            // Process HitEvent buffers on ships
            foreach (var (hitEvents, shipEntity) in SystemAPI.Query<DynamicBuffer<CombatHitEvent>>()
                .WithEntityAccess()
                .WithAll<CarrierModuleSlot>())
            {
                var hitEventsRW = hitEvents;
                // Check if this ship has module slots (is a carrier/ship)
                if (!_slotBufferLookup.HasBuffer(shipEntity))
                {
                    continue;
                }

                for (int i = 0; i < hitEventsRW.Length; i++)
                {
                    var hitEvent = hitEventsRW[i];

                    // Skip if already processed (HitTick == 0)
                    if (hitEvent.HitTick == 0)
                    {
                        continue;
                    }

                    // Try to find module at hit position
                    Entity hitModule = ModuleDamageRouterService.FindModuleAtPosition(
                        _entityLookup,
                        _slotBufferLookup,
                        _transformLookup,
                        _positionLookup,
                        shipEntity,
                        hitEvent.HitPosition);

                    if (hitModule != Entity.Null && _entityLookup.Exists(hitModule))
                    {
                        // Route damage to module
                        ModuleDamageRouterService.RouteDamageToModule(
                            _entityLookup,
                            _healthLookup,
                            _moduleLookup,
                            shipEntity,
                            hitModule,
                            hitEvent.DamageAmount);

                        // Mark hit event as processed
                        hitEvent.HitTick = 0;
                        hitEventsRW[i] = hitEvent;
                    }
                    else
                    {
                        // No module hit - fallback to hull damage
                        // Apply to ship's health if it has a Health component
                        if (_shipHealthLookup.HasComponent(shipEntity))
                        {
                            var health = _shipHealthLookup[shipEntity];
                            health.Current = math.max(0f, health.Current - hitEvent.DamageAmount);
                            _shipHealthLookup[shipEntity] = health;
                        }

                        // Also create DamageEvent for existing damage system compatibility
                        if (_damageEventLookup.HasBuffer(shipEntity))
                        {
                            var damageBuffer = _damageEventLookup[shipEntity];
                            damageBuffer.Add(new DamageEvent
                            {
                                SourceEntity = hitEvent.AttackerEntity,
                                TargetEntity = shipEntity,
                                RawDamage = hitEvent.DamageAmount,
                                Type = hitEvent.DamageType,
                                Tick = hitEvent.HitTick,
                                Flags = DamageFlags.None
                            });
                        }

                        // Mark hit event as processed
                        hitEvent.HitTick = 0;
                        hitEventsRW[i] = hitEvent;
                    }
                }

                // Clean up processed hit events
                for (int i = hitEventsRW.Length - 1; i >= 0; i--)
                {
                    if (hitEventsRW[i].HitTick == 0)
                    {
                        hitEventsRW.RemoveAt(i);
                    }
                }
            }
        }
    }
}

