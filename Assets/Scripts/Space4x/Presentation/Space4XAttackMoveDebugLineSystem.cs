using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    public partial struct Space4XAttackMoveDebugLineSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<VesselAimDirective> _aimLookup;
        private BufferLookup<WeaponMount> _weaponLookup;
        private BufferLookup<SubsystemHealth> _subsystemLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _aimLookup = state.GetComponentLookup<VesselAimDirective>(true);
            _weaponLookup = state.GetBufferLookup<WeaponMount>(true);
            _subsystemLookup = state.GetBufferLookup<SubsystemHealth>(true);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XPresentationDebugConfig>(out var debugConfig) ||
                debugConfig.EnableAttackMoveDebugLines == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _aimLookup.Update(ref state);
            _weaponLookup.Update(ref state);
            _subsystemLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);

            foreach (var (intent, transform, entity) in SystemAPI.Query<RefRO<AttackMoveIntent>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                var origin = transform.ValueRO.Position;

                var target = intent.ValueRO.EngageTarget;
                if (_aimLookup.HasComponent(entity))
                {
                    var aim = _aimLookup[entity];
                    if (aim.AimTarget != Entity.Null)
                    {
                        target = aim.AimTarget;
                    }
                }

                var moveColor = new Color(0.45f, 0.5f, 0.6f, 1f);
                if (target != Entity.Null && _transformLookup.HasComponent(target))
                {
                    var targetPos = _transformLookup[target].Position;
                    var maxRange = ResolveMaxWeaponRange(entity);
                    var destinationDistance = math.distance(intent.ValueRO.Destination, targetPos);
                    moveColor = maxRange > 0f && destinationDistance <= maxRange
                        ? new Color(0.12f, 0.85f, 0.3f, 1f)
                        : new Color(0.95f, 0.7f, 0.2f, 1f);

                    UnityDebug.DrawLine(origin, targetPos, new Color(1f, 0.35f, 0.2f, 1f));
                }

                UnityDebug.DrawLine(origin, intent.ValueRO.Destination, moveColor);
            }
        }

        private float ResolveMaxWeaponRange(Entity entity)
        {
            if (!_weaponLookup.HasBuffer(entity))
            {
                return 0f;
            }

            var maxRange = 0f;
            var weapons = _weaponLookup[entity];
            var hasSubsystems = _subsystemLookup.HasBuffer(entity);
            var hasDisabled = _subsystemDisabledLookup.HasBuffer(entity);
            DynamicBuffer<SubsystemHealth> subsystems = default;
            DynamicBuffer<SubsystemDisabled> disabled = default;
            var weaponsDisabled = false;
            if (hasSubsystems)
            {
                subsystems = _subsystemLookup[entity];
                if (hasDisabled)
                {
                    disabled = _subsystemDisabledLookup[entity];
                    weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, disabled, SubsystemType.Weapons);
                }
                else
                {
                    weaponsDisabled = Space4XSubsystemUtility.IsSubsystemDisabled(subsystems, SubsystemType.Weapons);
                }
            }
            for (int i = 0; i < weapons.Length; i++)
            {
                var mount = weapons[i];
                if (mount.IsEnabled == 0)
                {
                    continue;
                }
                if (weaponsDisabled)
                {
                    if (hasDisabled)
                    {
                        if (Space4XSubsystemUtility.IsWeaponMountDisabled(entity, i, subsystems, disabled))
                        {
                            continue;
                        }
                    }
                    else if (Space4XSubsystemUtility.ShouldDisableMount(entity, i))
                    {
                        continue;
                    }
                }

                maxRange = math.max(maxRange, mount.Weapon.MaxRange);
            }

            return maxRange;
        }
    }
}
