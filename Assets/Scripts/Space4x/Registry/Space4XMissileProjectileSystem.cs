using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Math;
using PureDOTS.Runtime.Movement;
using PureDOTS.Runtime.Steering;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    /// <summary>
    /// Runtime missile projectile state used by Space4X combat (independent from PureDOTS projectile damage pipeline).
    /// </summary>
    public struct Space4XMissileProjectile : IComponentData
    {
        public Entity Source;
        public Entity Target;
        public float3 Velocity;
        public float Speed;
        public float TurnRateDeg;
        public float LifetimeSeconds;
        public float AgeSeconds;
        public float ImpactRadius;
        public float RawDamage;
        public half ShieldModifier;
        public half ArmorPenetration;
        public WeaponType WeaponType;
        public byte IsCritical;
        public uint SpawnTick;
    }

    /// <summary>
    /// Moves and resolves homing missile impacts for Space4X combat.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XDamageResolutionSystem))]
    public partial struct Space4XMissileProjectileSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselMovement> _movementLookup;
        private ComponentLookup<Space4XShield> _shieldLookup;
        private ComponentLookup<Space4XArmor> _armorLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private BufferLookup<DamageEvent> _damageEventLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<Space4XMissileProjectile>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
            _shieldLookup = state.GetComponentLookup<Space4XShield>(false);
            _armorLookup = state.GetComponentLookup<Space4XArmor>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(false);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(false);
            _damageEventLookup = state.GetBufferLookup<DamageEvent>(false);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var dt = math.max(0f, timeState.DeltaTime);
            if (dt <= 0f)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _movementLookup.Update(ref state);
            _shieldLookup.Update(ref state);
            _armorLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _damageEventLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (missile, transform, entity) in
                     SystemAPI.Query<RefRW<Space4XMissileProjectile>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                var stateValue = missile.ValueRO;
                stateValue.AgeSeconds += dt;
                if (stateValue.AgeSeconds >= stateValue.LifetimeSeconds)
                {
                    ecb.DestroyEntity(entity);
                    continue;
                }

                var position = transform.ValueRO.Position;
                var speed = math.max(1f, stateValue.Speed);
                var velocity = stateValue.Velocity;
                if (math.lengthsq(velocity) <= 1e-6f)
                {
                    velocity = math.forward(transform.ValueRO.Rotation) * speed;
                }

                var currentDir = math.normalizesafe(velocity, math.forward(transform.ValueRO.Rotation));
                var desiredDir = currentDir;
                var hasTarget = stateValue.Target != Entity.Null &&
                                _entityLookup.Exists(stateValue.Target) &&
                                _transformLookup.HasComponent(stateValue.Target);
                var targetPos = float3.zero;

                if (hasTarget)
                {
                    targetPos = _transformLookup[stateValue.Target].Position;
                    var targetVelocity = _movementLookup.HasComponent(stateValue.Target)
                        ? _movementLookup[stateValue.Target].Velocity
                        : float3.zero;

                    var aimPoint = targetPos;
                    if (math.lengthsq(targetVelocity) > 1e-4f &&
                        SteeringPrimitives.LeadInterceptPoint(targetPos, targetVelocity, position, speed, out var interceptPoint, out _))
                    {
                        aimPoint = interceptPoint;
                    }

                    desiredDir = math.normalizesafe(aimPoint - position, currentDir);
                    var dot = math.clamp(math.dot(currentDir, desiredDir), -1f, 1f);
                    var angle = math.acos(dot);
                    var maxTurn = math.radians(math.max(1f, stateValue.TurnRateDeg)) * dt;
                    var t = angle <= 1e-5f ? 1f : math.saturate(maxTurn / angle);
                    currentDir = math.normalizesafe(math.lerp(currentDir, desiredDir, t), desiredDir);
                }

                velocity = currentDir * speed;
                var newPosition = position + velocity * dt;

                if (hasTarget && SegmentDistanceToPoint(position, newPosition, targetPos) <= math.max(0.25f, stateValue.ImpactRadius))
                {
                    ApplyImpactDamage(stateValue, ref ecb, timeState.Tick);
                    ecb.DestroyEntity(entity);
                    continue;
                }

                OrientationHelpers.LookRotationSafe3D(currentDir, OrientationHelpers.WorldUp, out var rotation);
                transform.ValueRW = LocalTransform.FromPositionRotationScale(newPosition, rotation, transform.ValueRO.Scale);
                stateValue.Velocity = velocity;
                missile.ValueRW = stateValue;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void ApplyImpactDamage(in Space4XMissileProjectile missile, ref EntityCommandBuffer ecb, uint tick)
        {
            var target = missile.Target;
            if (target == Entity.Null || !_entityLookup.Exists(target))
            {
                return;
            }

            float remainingDamage = math.max(0f, missile.RawDamage);
            float shieldDamage = 0f;
            float armorDamage = 0f;
            float hullDamage = 0f;
            var damageType = Space4XWeapon.ResolveDamageType(missile.WeaponType, Space4XDamageType.Unknown);

            if (_shieldLookup.HasComponent(target))
            {
                var shield = _shieldLookup[target];
                if (shield.Current > 0f)
                {
                    var resistance = CombatMath.GetWeaponResistance(damageType, shield);
                    var effectiveDamage = CombatMath.CalculateShieldDamage(remainingDamage, (float)missile.ShieldModifier, resistance);
                    shieldDamage = math.min(shield.Current, effectiveDamage);
                    shield.Current -= shieldDamage;
                    shield.CurrentDelay = shield.RechargeDelay;
                    remainingDamage = math.max(0f, effectiveDamage - shieldDamage);
                    _shieldLookup[target] = shield;
                }
            }

            if (remainingDamage > 0f && _armorLookup.HasComponent(target))
            {
                var armor = _armorLookup[target];
                var resistance = CombatMath.GetArmorResistance(damageType, armor);
                var mitigatedDamage = CombatMath.CalculateArmorDamage(
                    remainingDamage,
                    armor.Thickness,
                    (float)missile.ArmorPenetration,
                    resistance);
                armorDamage = remainingDamage - mitigatedDamage;
                remainingDamage = mitigatedDamage;
            }

            if (remainingDamage > 0f && _hullLookup.HasComponent(target))
            {
                var hull = _hullLookup[target];
                hullDamage = remainingDamage;
                hull.Current = (half)math.max(0f, (float)hull.Current - hullDamage);
                _hullLookup[target] = hull;
            }

            if (_damageEventLookup.HasBuffer(target))
            {
                var events = _damageEventLookup[target];
                if (events.Length < events.Capacity)
                {
                    events.Add(new DamageEvent
                    {
                        Source = missile.Source,
                        WeaponType = missile.WeaponType,
                        RawDamage = missile.RawDamage,
                        ShieldDamage = shieldDamage,
                        ArmorDamage = armorDamage,
                        HullDamage = hullDamage,
                        Tick = tick,
                        IsCritical = missile.IsCritical
                    });
                }
            }
            else
            {
                ecb.AddBuffer<DamageEvent>(target);
            }

            if (_engagementLookup.HasComponent(target))
            {
                var targetEngagement = _engagementLookup[target];
                targetEngagement.DamageReceived += missile.RawDamage;
                _engagementLookup[target] = targetEngagement;
            }

            if (missile.Source != Entity.Null && _entityLookup.Exists(missile.Source) && _engagementLookup.HasComponent(missile.Source))
            {
                var sourceEngagement = _engagementLookup[missile.Source];
                sourceEngagement.DamageDealt += missile.RawDamage;
                _engagementLookup[missile.Source] = sourceEngagement;
            }
        }

        private static float SegmentDistanceToPoint(float3 a, float3 b, float3 p)
        {
            var ab = b - a;
            var denom = math.lengthsq(ab);
            if (denom <= 1e-6f)
            {
                return math.distance(p, a);
            }

            var t = math.saturate(math.dot(p - a, ab) / denom);
            var closest = a + ab * t;
            return math.distance(p, closest);
        }
    }
}
