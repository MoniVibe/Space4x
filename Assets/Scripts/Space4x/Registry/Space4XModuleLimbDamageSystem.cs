using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Applies limb damage events to module limb states and refreshes limb profiles.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct Space4XModuleLimbDamageSystem : ISystem
    {
        private ComponentLookup<ModuleLimbProfile> _profileLookup;
        private BufferLookup<HazardDamageEvent> _hazardLookup;
        private ComponentLookup<ModuleHealth> _moduleHealthLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleLimbDamageEvent>();
            _profileLookup = state.GetComponentLookup<ModuleLimbProfile>(false);
            _hazardLookup = state.GetBufferLookup<HazardDamageEvent>(false);
            _moduleHealthLookup = state.GetComponentLookup<ModuleHealth>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _profileLookup.Update(ref state);
            _hazardLookup.Update(ref state);
            _moduleHealthLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (events, limbs, entity) in SystemAPI.Query<DynamicBuffer<ModuleLimbDamageEvent>, DynamicBuffer<ModuleLimbState>>().WithEntityAccess())
            {
                if (events.Length == 0 || limbs.Length == 0)
                {
                    events.Clear();
                    continue;
                }

                var limbsBuffer = limbs;
                for (var i = 0; i < events.Length; i++)
                {
                    var evt = events[i];
                    var damage = math.max(0f, evt.Damage);
                    if (damage <= 0f)
                    {
                        continue;
                    }

                    var limbIndex = ResolveTargetLimb(limbsBuffer, evt);
                    if (limbIndex < 0)
                    {
                        continue;
                    }

                    var limb = limbsBuffer[limbIndex];
                    limb.Integrity = math.max(0f, limb.Integrity - damage);
                    limbsBuffer[limbIndex] = limb;

                    if (limb.Family == ModuleLimbFamily.Cooling && limb.Integrity <= 0.1f)
                    {
                        TryApplySecondaryDamage(limbsBuffer, ModuleLimbFamily.Power, damage * 0.5f);
                        TryApplySecondaryDamage(limbsBuffer, ModuleLimbFamily.Structural, damage * 0.35f);
                    }

                    if (IsCriticalLimb(limb.LimbId) && limb.Integrity <= 0.05f && _moduleHealthLookup.HasComponent(entity))
                    {
                        if (!_hazardLookup.HasBuffer(entity))
                        {
                            ecb.AddBuffer<HazardDamageEvent>(entity);
                        }

                        ecb.AppendToBuffer(entity, new HazardDamageEvent
                        {
                            HazardType = HazardTypeId.Thermal,
                            Amount = damage * 0.4f
                        });
                    }
                }

                events.Clear();

                if (_profileLookup.HasComponent(entity))
                {
                    var profile = _profileLookup[entity];
                    RebuildProfileFromLimbs(limbsBuffer, ref profile);
                    _profileLookup[entity] = profile;
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private static int ResolveTargetLimb(DynamicBuffer<ModuleLimbState> limbs, in ModuleLimbDamageEvent evt)
        {
            if (evt.LimbId != ModuleLimbId.Unknown)
            {
                for (var i = 0; i < limbs.Length; i++)
                {
                    if (limbs[i].LimbId == evt.LimbId)
                    {
                        return i;
                    }
                }
            }

            var bestIndex = -1;
            var bestExposure = -1f;
            for (var i = 0; i < limbs.Length; i++)
            {
                var limb = limbs[i];
                if (limb.Family != evt.Family)
                {
                    continue;
                }

                if (limb.Exposure > bestExposure)
                {
                    bestExposure = limb.Exposure;
                    bestIndex = i;
                }
            }

            if (bestIndex >= 0)
            {
                return bestIndex;
            }

            bestExposure = -1f;
            for (var i = 0; i < limbs.Length; i++)
            {
                var limb = limbs[i];
                if (limb.Exposure > bestExposure)
                {
                    bestExposure = limb.Exposure;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static void TryApplySecondaryDamage(DynamicBuffer<ModuleLimbState> limbs, ModuleLimbFamily family, float damage)
        {
            if (damage <= 0f)
            {
                return;
            }

            var bestIndex = -1;
            var bestExposure = -1f;
            for (var i = 0; i < limbs.Length; i++)
            {
                var limb = limbs[i];
                if (limb.Family != family)
                {
                    continue;
                }

                if (limb.Exposure > bestExposure)
                {
                    bestExposure = limb.Exposure;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return;
            }

            var target = limbs[bestIndex];
            target.Integrity = math.max(0f, target.Integrity - damage);
            limbs[bestIndex] = target;
        }

        private static bool IsCriticalLimb(ModuleLimbId limbId)
        {
            return limbId == ModuleLimbId.StructuralFrame ||
                   limbId == ModuleLimbId.Capacitor ||
                   limbId == ModuleLimbId.PowerCoupler;
        }

        private static void RebuildProfileFromLimbs(DynamicBuffer<ModuleLimbState> limbs, ref ModuleLimbProfile profile)
        {
            Accumulate(limbs, ModuleLimbFamily.Cooling, out profile.Cooling);
            Accumulate(limbs, ModuleLimbFamily.Sensors, out profile.Sensors);
            Accumulate(limbs, ModuleLimbFamily.Lensing, out profile.Lensing);
            Accumulate(limbs, ModuleLimbFamily.Projector, out profile.Projector);
            Accumulate(limbs, ModuleLimbFamily.Guidance, out profile.Guidance);
            Accumulate(limbs, ModuleLimbFamily.Actuator, out profile.Actuator);
            Accumulate(limbs, ModuleLimbFamily.Structural, out profile.Structural);
            Accumulate(limbs, ModuleLimbFamily.Power, out profile.Power);
        }

        private static void Accumulate(DynamicBuffer<ModuleLimbState> limbs, ModuleLimbFamily family, out float value)
        {
            var sum = 0f;
            var count = 0;
            for (var i = 0; i < limbs.Length; i++)
            {
                if (limbs[i].Family != family)
                {
                    continue;
                }

                sum += math.saturate(limbs[i].Integrity);
                count++;
            }

            value = count > 0 ? math.saturate(sum / count) : 0f;
        }
    }
}
