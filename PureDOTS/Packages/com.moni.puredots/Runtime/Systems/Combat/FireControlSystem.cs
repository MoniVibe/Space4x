using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Combat.Targeting;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Controls weapon firing based on targets, arcs, range, and cooldowns.
    /// Emits FireEvent which is consumed by projectile spawn systems.
    /// Integrates with existing TargetSelectionSystem and WeaponMount systems.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct FireControlSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _transformLookup.Update(ref state);
            var weaponBufferLookup = SystemAPI.GetBufferLookup<WeaponComponent>(false);
            weaponBufferLookup.Update(ref state);

            // Process entities with weapons and targets
            foreach (var (targetPriority, transform, fireEvents, entity) in
                SystemAPI.Query<RefRO<TargetPriority>, RefRO<LocalTransform>, DynamicBuffer<FireEvent>>()
                .WithEntityAccess())
            {
                if (!weaponBufferLookup.HasBuffer(entity))
                {
                    continue;
                }

                var weapons = weaponBufferLookup[entity];
                
                // Skip if no target
                if (targetPriority.ValueRO.CurrentTarget == Entity.Null)
                {
                    continue;
                }

                // Get target position
                if (!_transformLookup.HasComponent(targetPriority.ValueRO.CurrentTarget))
                {
                    continue;
                }

                var targetTransform = _transformLookup[targetPriority.ValueRO.CurrentTarget];
                var targetPos = targetTransform.Position;
                var ourPos = transform.ValueRO.Position;
                var toTarget = targetPos - ourPos;
                var distance = math.length(toTarget);
                var direction = distance > 0.001f ? toTarget / distance : float3.zero;

                // Check each weapon
                for (int i = 0; i < weapons.Length; i++)
                {
                    var weapon = weapons[i];

                    // Check cooldown
                    var currentTime = (float)timeState.Tick * timeState.FixedDeltaTime;
                    if (currentTime - weapon.LastFireTime < (1f / weapon.FireRate))
                    {
                        continue; // Still on cooldown
                    }

                    // Check range
                    if (distance > weapon.Range)
                    {
                        continue; // Out of range
                    }

                    // Check fire arc (if constrained)
                    if (weapon.FireArcDegrees > 0f && weapon.FireArcDegrees < 360f)
                    {
                        var forward = math.forward(transform.ValueRO.Rotation);
                        var dot = math.dot(forward, direction);
                        var angleRad = math.acos(math.clamp(dot, -1f, 1f));
                        var angleDeg = math.degrees(angleRad);
                        var halfArc = weapon.FireArcDegrees * 0.5f;

                        if (angleDeg > halfArc)
                        {
                            continue; // Outside fire arc
                        }
                    }

                    // Fire weapon
                    fireEvents.Add(new FireEvent
                    {
                        EmitterEntity = entity,
                        TargetEntity = targetPriority.ValueRO.CurrentTarget,
                        TargetPosition = targetPos,
                        WeaponIndex = weapon.WeaponIndex,
                        FireDirection = direction,
                        FireTick = timeState.Tick,
                        DamageAmount = weapon.BaseDamage,
                        DamageType = weapon.DamageType
                    });

                    // Update weapon cooldown
                    weapon.LastFireTime = currentTime;
                    weapon.CooldownRemaining = 1f / weapon.FireRate;
                    weapons[i] = weapon;
                }
            }
        }
    }
}

