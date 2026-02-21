using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;

namespace Space4X.Registry
{
    using PDCarrierModuleSlot = PureDOTS.Runtime.Ships.CarrierModuleSlot;
    using PDShipModule = PureDOTS.Runtime.Ships.ShipModule;

    /// <summary>
    /// Resolves area effects with distance falloff and spherical occlusion.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(Space4XHazardMitigationSystem))]
    public partial struct Space4XAreaEffectSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<HullIntegrity> _hullLookup;
        private ComponentLookup<IndividualStats> _individualLookup;
        private BufferLookup<SubsystemHealth> _subsystemHealthLookup;
        private BufferLookup<SubsystemDisabled> _subsystemDisabledLookup;
        private BufferLookup<HazardDamageEvent> _hazardLookup;
        private BufferLookup<Space4XStatusEffectEvent> _statusEffectLookup;
        private BufferLookup<PDCarrierModuleSlot> _moduleSlotLookup;
        private ComponentLookup<PDShipModule> _moduleLookup;
        private BufferLookup<ModuleLimbState> _moduleLimbStateLookup;
        private BufferLookup<ModuleLimbDamageEvent> _moduleLimbDamageLookup;

        private struct TargetSnapshot
        {
            public Entity Entity;
            public float3 Position;
            public byte IsShip;
            public byte IsIndividual;
        }

        private struct OccluderSnapshot
        {
            public Entity Entity;
            public float3 Position;
            public float Radius;
            public float Strength01;
            public Space4XAreaOcclusionChannel Channels;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<Space4XAreaEffectEmitter>();

            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _hullLookup = state.GetComponentLookup<HullIntegrity>(false);
            _individualLookup = state.GetComponentLookup<IndividualStats>(true);
            _subsystemHealthLookup = state.GetBufferLookup<SubsystemHealth>(true);
            _subsystemDisabledLookup = state.GetBufferLookup<SubsystemDisabled>(false);
            _hazardLookup = state.GetBufferLookup<HazardDamageEvent>(false);
            _statusEffectLookup = state.GetBufferLookup<Space4XStatusEffectEvent>(false);
            _moduleSlotLookup = state.GetBufferLookup<PDCarrierModuleSlot>(true);
            _moduleLookup = state.GetComponentLookup<PDShipModule>(true);
            _moduleLimbStateLookup = state.GetBufferLookup<ModuleLimbState>(true);
            _moduleLimbDamageLookup = state.GetBufferLookup<ModuleLimbDamageEvent>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            _transformLookup.Update(ref state);
            _hullLookup.Update(ref state);
            _individualLookup.Update(ref state);
            _subsystemHealthLookup.Update(ref state);
            _subsystemDisabledLookup.Update(ref state);
            _hazardLookup.Update(ref state);
            _statusEffectLookup.Update(ref state);
            _moduleSlotLookup.Update(ref state);
            _moduleLookup.Update(ref state);
            _moduleLimbStateLookup.Update(ref state);
            _moduleLimbDamageLookup.Update(ref state);

            var currentTick = timeState.Tick;
            var hasPhysicsWorld = SystemAPI.TryGetSingleton<PhysicsWorldSingleton>(out var physicsWorld);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var modulesWithPendingLimbBuffer = new NativeParallelHashSet<Entity>(64, Allocator.Temp);

            var targets = new NativeList<TargetSnapshot>(Allocator.Temp);
            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>().WithEntityAccess())
            {
                var isShip = _hullLookup.HasComponent(entity) || _subsystemHealthLookup.HasBuffer(entity);
                var isIndividual = _individualLookup.HasComponent(entity);
                if (!isShip && !isIndividual)
                {
                    continue;
                }

                targets.Add(new TargetSnapshot
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    IsShip = (byte)(isShip ? 1 : 0),
                    IsIndividual = (byte)(isIndividual ? 1 : 0)
                });
            }

            var occluders = new NativeList<OccluderSnapshot>(Allocator.Temp);
            foreach (var (transform, occluder, entity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<Space4XAreaOccluder>>().WithEntityAccess())
            {
                var radius = math.max(0f, occluder.ValueRO.Radius);
                if (radius <= 0f || occluder.ValueRO.BlocksChannels == Space4XAreaOcclusionChannel.None)
                {
                    continue;
                }

                occluders.Add(new OccluderSnapshot
                {
                    Entity = entity,
                    Position = transform.ValueRO.Position,
                    Radius = radius,
                    Strength01 = math.saturate(occluder.ValueRO.Strength01),
                    Channels = occluder.ValueRO.BlocksChannels
                });
            }

            foreach (var (emitterRef, emitterEntity) in SystemAPI.Query<RefRW<Space4XAreaEffectEmitter>>().WithEntityAccess())
            {
                var emitter = emitterRef.ValueRO;
                if (emitter.Active == 0)
                {
                    continue;
                }

                var radius = math.max(0f, emitter.Radius);
                if (radius <= 1e-5f || emitter.Magnitude <= 1e-5f)
                {
                    continue;
                }

                if (currentTick < emitter.NextPulseTick)
                {
                    continue;
                }

                var center = emitter.CenterWorld;
                if (_transformLookup.HasComponent(emitterEntity))
                {
                    center = _transformLookup[emitterEntity].Position + emitter.LocalOffset;
                }

                for (var i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    if (target.Entity == emitterEntity && emitter.AffectsSource == 0)
                    {
                        continue;
                    }

                    if (target.Entity == emitter.ExcludedEntity)
                    {
                        continue;
                    }

                    if (!MatchesTargetMask(emitter.TargetMask, target.IsShip != 0, target.IsIndividual != 0))
                    {
                        continue;
                    }

                    var toTarget = target.Position - center;
                    var distance = math.length(toTarget);
                    if (distance > radius)
                    {
                        continue;
                    }

                    var falloff = ComputeFalloff(distance, radius, emitter.InnerRadius, emitter.FalloffExponent);
                    if (falloff <= 1e-5f)
                    {
                        continue;
                    }

                    var occlusion = ComputeOcclusionFactor(
                        center,
                        target.Position,
                        emitterEntity,
                        target.Entity,
                        emitter.OcclusionChannel,
                        emitter.OcclusionMode,
                        emitter.OcclusionRadiusBias,
                        occluders,
                        hasPhysicsWorld,
                        physicsWorld);

                    if (occlusion <= 1e-5f)
                    {
                        continue;
                    }

                    var appliedMagnitude = emitter.Magnitude * falloff * occlusion;
                    if (appliedMagnitude <= 1e-5f)
                    {
                        continue;
                    }

                    ApplyToTarget(
                        target.Entity,
                        emitterEntity,
                        emitter,
                        appliedMagnitude,
                        currentTick,
                        ref ecb,
                        ref modulesWithPendingLimbBuffer);
                }

                var updated = emitterRef.ValueRO;
                var interval = math.max(1u, updated.PulseIntervalTicks);
                updated.NextPulseTick = currentTick + interval;
                if (updated.RemainingPulses > 0u)
                {
                    updated.RemainingPulses--;
                    if (updated.RemainingPulses == 0u)
                    {
                        updated.Active = 0;
                    }
                }
                emitterRef.ValueRW = updated;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            modulesWithPendingLimbBuffer.Dispose();
            occluders.Dispose();
            targets.Dispose();
        }

        private void ApplyToTarget(
            Entity target,
            Entity source,
            in Space4XAreaEffectEmitter emitter,
            float magnitude,
            uint currentTick,
            ref EntityCommandBuffer ecb,
            ref NativeParallelHashSet<Entity> modulesWithPendingLimbBuffer)
        {
            if ((emitter.ImpactMask & Space4XAreaEffectImpactMask.HullDamage) != 0 &&
                _hullLookup.HasComponent(target))
            {
                var hull = _hullLookup[target];
                hull.Current = math.max(0f, hull.Current - magnitude);
                hull.LastDamageTick = currentTick;
                _hullLookup[target] = hull;
            }

            if ((emitter.ImpactMask & Space4XAreaEffectImpactMask.Hazard) != 0 &&
                _hazardLookup.HasBuffer(target))
            {
                var events = _hazardLookup[target];
                events.Add(new HazardDamageEvent
                {
                    HazardType = emitter.HazardType,
                    Amount = magnitude
                });
            }

            if (_subsystemDisabledLookup.HasBuffer(target))
            {
                var disableUntil = emitter.DisableDurationTicks == 0u
                    ? uint.MaxValue
                    : currentTick + emitter.DisableDurationTicks;

                var disabled = _subsystemDisabledLookup[target];
                if ((emitter.ImpactMask & Space4XAreaEffectImpactMask.DisableWeapons) != 0)
                {
                    UpsertSubsystemDisable(ref disabled, SubsystemType.Weapons, disableUntil);
                }

                if ((emitter.ImpactMask & Space4XAreaEffectImpactMask.DisableEngines) != 0)
                {
                    UpsertSubsystemDisable(ref disabled, SubsystemType.Engines, disableUntil);
                }
            }

            if ((emitter.ImpactMask & Space4XAreaEffectImpactMask.ModuleLimbDamage) != 0 &&
                _moduleSlotLookup.HasBuffer(target))
            {
                var scaled = magnitude * math.max(0f, emitter.ModuleDamageScale) * ResolveLimbDamageScale(emitter.DamageType);
                if (scaled > 1e-5f)
                {
                    var slots = _moduleSlotLookup[target];
                    var moduleCount = 0;
                    for (var i = 0; i < slots.Length; i++)
                    {
                        var module = slots[i].InstalledModule;
                        if (module == Entity.Null || !_moduleLookup.HasComponent(module) || !_moduleLimbStateLookup.HasBuffer(module))
                        {
                            continue;
                        }

                        moduleCount++;
                    }

                    if (moduleCount > 0)
                    {
                        var perModuleDamage = scaled / moduleCount;
                        var family = ResolveLimbFamily(emitter.DamageType);
                        for (var i = 0; i < slots.Length; i++)
                        {
                            var module = slots[i].InstalledModule;
                            if (module == Entity.Null || !_moduleLookup.HasComponent(module) || !_moduleLimbStateLookup.HasBuffer(module))
                            {
                                continue;
                            }

                            if (!_moduleLimbDamageLookup.HasBuffer(module) &&
                                !modulesWithPendingLimbBuffer.Contains(module))
                            {
                                ecb.AddBuffer<ModuleLimbDamageEvent>(module);
                                modulesWithPendingLimbBuffer.Add(module);
                            }

                            ecb.AppendToBuffer(module, new ModuleLimbDamageEvent
                            {
                                Family = family,
                                LimbId = ModuleLimbId.Unknown,
                                Damage = perModuleDamage,
                                Tick = currentTick
                            });
                        }
                    }
                }
            }

            if (_statusEffectLookup.HasBuffer(target))
            {
                var effects = _statusEffectLookup[target];
                effects.Add(new Space4XStatusEffectEvent
                {
                    SourceEntity = source,
                    Scope = emitter.Scope,
                    ImpactMask = emitter.ImpactMask,
                    HazardType = emitter.HazardType,
                    DamageType = emitter.DamageType,
                    Magnitude = magnitude,
                    Tick = currentTick
                });
            }
        }

        private static void UpsertSubsystemDisable(
            ref DynamicBuffer<SubsystemDisabled> disabled,
            SubsystemType type,
            uint untilTick)
        {
            for (var i = 0; i < disabled.Length; i++)
            {
                var entry = disabled[i];
                if (entry.Type != type)
                {
                    continue;
                }

                if (entry.UntilTick == uint.MaxValue || untilTick == uint.MaxValue)
                {
                    entry.UntilTick = uint.MaxValue;
                }
                else
                {
                    entry.UntilTick = math.max(entry.UntilTick, untilTick);
                }
                disabled[i] = entry;
                return;
            }

            disabled.Add(new SubsystemDisabled
            {
                Type = type,
                UntilTick = untilTick
            });
        }

        private static bool MatchesTargetMask(
            Space4XAreaEffectTargetMask mask,
            bool isShip,
            bool isIndividual)
        {
            if (mask == Space4XAreaEffectTargetMask.None)
            {
                return false;
            }

            var allowShip = (mask & Space4XAreaEffectTargetMask.Ships) != 0;
            var allowIndividual = (mask & Space4XAreaEffectTargetMask.Individuals) != 0;
            return (allowShip && isShip) || (allowIndividual && isIndividual);
        }

        private static float ComputeFalloff(float distance, float radius, float innerRadius, float exponent)
        {
            var inner = math.clamp(innerRadius, 0f, radius);
            if (distance <= inner)
            {
                return 1f;
            }

            var denom = math.max(1e-5f, radius - inner);
            var t = math.saturate((distance - inner) / denom);
            var exp = math.max(0.1f, exponent);
            return math.pow(1f - t, exp);
        }

        private static float ComputeOcclusionFactor(
            float3 origin,
            float3 target,
            Entity sourceEntity,
            Entity targetEntity,
            Space4XAreaOcclusionChannel channel,
            Space4XAreaOcclusionMode mode,
            float radiusBias,
            NativeList<OccluderSnapshot> occluders,
            bool hasPhysicsWorld,
            in PhysicsWorldSingleton physicsWorld)
        {
            if (mode == Space4XAreaOcclusionMode.None ||
                channel == Space4XAreaOcclusionChannel.None)
            {
                return 1f;
            }

            if (mode == Space4XAreaOcclusionMode.PhysicsRaycast && hasPhysicsWorld)
            {
                var rayInput = new RaycastInput
                {
                    Start = origin,
                    End = target,
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    var hitEntity = hit.Entity;
                    if (hitEntity != sourceEntity && hitEntity != targetEntity)
                    {
                        return 0f;
                    }
                }

                return 1f;
            }

            if (occluders.Length == 0)
            {
                return 1f;
            }

            var factor = 1f;
            for (var i = 0; i < occluders.Length; i++)
            {
                var occluder = occluders[i];
                if (occluder.Entity == sourceEntity || occluder.Entity == targetEntity)
                {
                    continue;
                }

                if ((occluder.Channels & channel) == 0)
                {
                    continue;
                }

                var effectiveRadius = math.max(0f, occluder.Radius + radiusBias);
                if (!IntersectsSegmentSphere(origin, target, occluder.Position, effectiveRadius))
                {
                    continue;
                }

                if (mode == Space4XAreaOcclusionMode.BinaryBlock)
                {
                    return 0f;
                }

                factor *= 1f - math.saturate(occluder.Strength01);
                if (factor <= 1e-5f)
                {
                    return 0f;
                }
            }

            return math.saturate(factor);
        }

        private static ModuleLimbFamily ResolveLimbFamily(Space4XDamageType damageType)
        {
            return damageType switch
            {
                Space4XDamageType.Thermal => ModuleLimbFamily.Cooling,
                Space4XDamageType.EM => ModuleLimbFamily.Power,
                Space4XDamageType.Radiation => ModuleLimbFamily.Sensors,
                Space4XDamageType.Caustic => ModuleLimbFamily.Structural,
                Space4XDamageType.Energy => ModuleLimbFamily.Lensing,
                Space4XDamageType.Kinetic => ModuleLimbFamily.Structural,
                Space4XDamageType.Explosive => ModuleLimbFamily.Structural,
                _ => ModuleLimbFamily.Structural
            };
        }

        private static float ResolveLimbDamageScale(Space4XDamageType damageType)
        {
            return damageType switch
            {
                Space4XDamageType.Thermal => 1.15f,
                Space4XDamageType.EM => 1.1f,
                Space4XDamageType.Caustic => 1.2f,
                _ => 1f
            };
        }

        private static bool IntersectsSegmentSphere(float3 a, float3 b, float3 center, float radius)
        {
            var ab = b - a;
            var length = math.length(ab);
            if (length <= 1e-5f || radius <= 1e-5f)
            {
                return false;
            }

            var dir = ab / length;
            var toCenter = center - a;
            var projection = math.dot(toCenter, dir);
            if (projection <= 0f || projection >= length)
            {
                return false;
            }

            var closest = a + dir * projection;
            var distanceSq = math.lengthsq(center - closest);
            return distanceSq <= radius * radius;
        }
    }
}
