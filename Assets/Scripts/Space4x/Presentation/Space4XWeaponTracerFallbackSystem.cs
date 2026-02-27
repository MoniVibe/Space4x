using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using Space4X.Registry;
using Space4x.Scenario;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Presentation
{
    /// <summary>
    /// Draws lightweight fallback shot tracers for player-fired weapons when projectile visuals are unavailable.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XPresentationLifecycleSystem))]
    public partial struct Space4XWeaponTracerFallbackSystem : ISystem
    {
        private const float TracerDurationSeconds = 0.08f;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Space4XRunPlayerTag> _runPlayerLookup;
        private ComponentLookup<PlayerFlagshipTag> _flagshipLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<WeaponMount>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _runPlayerLookup = state.GetComponentLookup<Space4XRunPlayerTag>(true);
            _flagshipLookup = state.GetComponentLookup<PlayerFlagshipTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!RuntimeMode.IsRenderingEnabled)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _runPlayerLookup.Update(ref state);
            _flagshipLookup.Update(ref state);

            foreach (var (weapons, transform, entity) in
                     SystemAPI.Query<DynamicBuffer<WeaponMount>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (!_runPlayerLookup.HasComponent(entity) && !_flagshipLookup.HasComponent(entity))
                {
                    continue;
                }

                var origin = transform.ValueRO.Position;
                for (int i = 0; i < weapons.Length; i++)
                {
                    var mount = weapons[i];
                    if (mount.IsEnabled == 0 || mount.Weapon.CooldownTicks == 0)
                    {
                        continue;
                    }

                    // Mount is considered "just fired" in the same way combat hit resolution detects it.
                    if (mount.Weapon.CurrentCooldown != mount.Weapon.CooldownTicks)
                    {
                        continue;
                    }

                    var target = mount.CurrentTarget;
                    if (target == Entity.Null || !_transformLookup.HasComponent(target))
                    {
                        continue;
                    }

                    var targetPos = _transformLookup[target].Position;
                    UnityDebug.DrawLine(origin, targetPos, ResolveTracerColor(mount.Weapon.Type), TracerDurationSeconds, false);
                }
            }
        }

        private static Color ResolveTracerColor(WeaponType weaponType)
        {
            return weaponType switch
            {
                WeaponType.Laser => new Color(0.4f, 0.95f, 1f, 1f),
                WeaponType.Kinetic => new Color(1f, 0.95f, 0.5f, 1f),
                WeaponType.Missile => new Color(1f, 0.55f, 0.2f, 1f),
                WeaponType.Torpedo => new Color(1f, 0.35f, 0.1f, 1f),
                WeaponType.Ion => new Color(0.6f, 0.7f, 1f, 1f),
                WeaponType.Plasma => new Color(1f, 0.45f, 0.75f, 1f),
                WeaponType.PointDefense => new Color(0.9f, 0.9f, 0.9f, 1f),
                WeaponType.Flak => new Color(1f, 0.7f, 0.25f, 1f),
                _ => new Color(0.8f, 0.9f, 1f, 1f)
            };
        }
    }
}
