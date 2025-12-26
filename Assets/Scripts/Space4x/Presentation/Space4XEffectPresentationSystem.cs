using PureDOTS.Runtime.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.VFX;

namespace Space4X.Presentation
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XEffectRequestConsumeSystem))]
    public partial class Space4XEffectPresentationSystem : SystemBase
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        protected override void OnCreate()
        {
            RequireForUpdate<Space4XEffectInstance>();
            _transformLookup = GetComponentLookup<LocalTransform>(true);
        }

        protected override void OnUpdate()
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            if (!TryGetCatalog(out var catalog) || catalog?.Entries == null)
            {
                return;
            }

            _transformLookup.Update(this);

            foreach (var (instance, entity) in SystemAPI.Query<RefRW<Space4XEffectInstance>>().WithEntityAccess())
            {
                var data = instance.ValueRO;
                var entry = FindEntry(catalog.Entries, data.EffectId);
                if (entry == null)
                {
                    continue;
                }

                var position = ResolveEffectPosition(data, entity, entry);
                var direction = math.lengthsq(data.Direction) > 1e-4f ? data.Direction : new float3(0f, 0f, 1f);
                var rotation = Quaternion.LookRotation(new Vector3(direction.x, direction.y, direction.z), Vector3.up);

                if (!EntityManager.HasComponent<Space4XEffectPresenter>(entity))
                {
                    var presenter = CreatePresenter(entry, position, rotation, data.Intensity, data.Direction);
                    if (presenter == null)
                    {
                        continue;
                    }

                    if (entry.LifetimeOverride > 0f)
                    {
                        data.Lifetime = entry.LifetimeOverride;
                        instance.ValueRW = data;
                    }

                    EntityManager.AddComponentObject(entity, presenter);
                }
                else
                {
                    var presenter = EntityManager.GetComponentObject<Space4XEffectPresenter>(entity);
                    if (presenter?.Instance == null)
                    {
                        EntityManager.RemoveComponent<Space4XEffectPresenter>(entity);
                        continue;
                    }

                    presenter.Instance.transform.SetPositionAndRotation(position, rotation);
                    ApplyIntensity(presenter, data.Intensity, data.Direction);
                }
            }
        }

        private Space4XEffectPresenter CreatePresenter(
            Space4XEffectCatalogEntry entry,
            float3 position,
            Quaternion rotation,
            float intensity,
            float3 direction)
        {
            if (entry.Prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(entry.Prefab);
            instance.transform.SetPositionAndRotation(new Vector3(position.x, position.y, position.z), rotation);

            var presenter = new Space4XEffectPresenter
            {
                Instance = instance,
                BindingMode = entry.BindingMode,
                BaseScale = instance.transform.localScale.x,
                IntensityProperty = string.IsNullOrWhiteSpace(entry.IntensityProperty) ? "_Intensity" : entry.IntensityProperty,
                DirectionProperty = string.IsNullOrWhiteSpace(entry.DirectionProperty) ? "_Direction" : entry.DirectionProperty
            };

            if (entry.BindingMode == Space4XEffectBindingMode.VfxGraph)
            {
                presenter.Vfx = instance.GetComponentInChildren<VisualEffect>();
            }
            else if (entry.BindingMode == Space4XEffectBindingMode.Particle)
            {
                presenter.Particle = instance.GetComponentInChildren<ParticleSystem>();
                if (presenter.Particle != null)
                {
                    var emission = presenter.Particle.emission;
                    presenter.BaseEmissionRate = emission.rateOverTimeMultiplier;
                    var main = presenter.Particle.main;
                    presenter.BaseStartSize = main.startSizeMultiplier;
                    presenter.BaseStartSpeed = main.startSpeedMultiplier;
                }
            }

            ApplyIntensity(presenter, intensity, direction);
            return presenter;
        }

        private void ApplyIntensity(Space4XEffectPresenter presenter, float intensity, float3 direction)
        {
            var safeIntensity = math.max(0f, intensity);
            if (presenter.BindingMode == Space4XEffectBindingMode.VfxGraph && presenter.Vfx != null)
            {
                presenter.Vfx.SetFloat(presenter.IntensityProperty, safeIntensity);
                presenter.Vfx.SetVector3(presenter.DirectionProperty, new Vector3(direction.x, direction.y, direction.z));
                return;
            }

            if (presenter.BindingMode == Space4XEffectBindingMode.Particle && presenter.Particle != null)
            {
                var emission = presenter.Particle.emission;
                emission.rateOverTimeMultiplier = presenter.BaseEmissionRate * math.max(0.1f, safeIntensity);

                var main = presenter.Particle.main;
                main.startSizeMultiplier = presenter.BaseStartSize * math.max(0.2f, safeIntensity);
                main.startSpeedMultiplier = presenter.BaseStartSpeed * math.max(0.2f, safeIntensity);
                return;
            }

            if (presenter.Instance != null)
            {
                var scale = presenter.BaseScale * math.max(0.25f, safeIntensity);
                presenter.Instance.transform.localScale = new Vector3(scale, scale, scale);
            }
        }

        private float3 ResolveEffectPosition(in Space4XEffectInstance data, Entity entity, Space4XEffectCatalogEntry entry)
        {
            if (entry != null && entry.FollowMode != Space4XEffectFollowMode.World && data.Source != Entity.Null && _transformLookup.HasComponent(data.Source))
            {
                var sourcePos = _transformLookup[data.Source].Position;
                if (entry.FollowMode == Space4XEffectFollowMode.FollowSourceWithOffset)
                {
                    return sourcePos + entry.FollowOffset;
                }

                return sourcePos;
            }

            if (entry != null && entry.FollowMode == Space4XEffectFollowMode.FollowSourceWithOffset && data.Source == Entity.Null)
            {
                return data.Position + entry.FollowOffset;
            }

            if (entry == null || entry.FollowMode == Space4XEffectFollowMode.World)
            {
                return data.Position;
            }

            if (_transformLookup.HasComponent(entity))
            {
                return _transformLookup[entity].Position;
            }

            return data.Position;
        }

        private static Space4XEffectCatalogEntry FindEntry(Space4XEffectCatalogEntry[] entries, FixedString64Bytes effectId)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].EffectId.Equals(effectId))
                {
                    return entries[i];
                }
            }

            return null;
        }

        private bool TryGetCatalog(out Space4XEffectCatalog catalog)
        {
            catalog = null;
            using var query = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Space4XEffectCatalog>());
            if (query.IsEmptyIgnoreFilter)
            {
                return false;
            }

            var entity = query.GetSingletonEntity();
            catalog = EntityManager.GetComponentObject<Space4XEffectCatalog>(entity);
            return catalog != null;
        }
    }

    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(Space4XEffectLifetimeSystem))]
    public partial class Space4XEffectCleanupSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            if (RuntimeMode.IsHeadless)
            {
                return;
            }

            foreach (var presenter in SystemAPI.Query<Space4XEffectPresenter>().WithNone<Space4XEffectInstance>())
            {
                if (presenter?.Instance != null)
                {
                    Object.Destroy(presenter.Instance);
                }
            }

            EntityManager.RemoveComponent<Space4XEffectPresenter>(SystemAPI.QueryBuilder().WithAll<Space4XEffectPresenter>().WithNone<Space4XEffectInstance>().Build());
        }
    }
}
