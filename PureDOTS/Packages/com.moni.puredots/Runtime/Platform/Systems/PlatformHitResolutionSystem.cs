using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Platform.Blobs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Platform
{
    /// <summary>
    /// Resolves platform hit events through the damage pipeline:
    /// Shield → Armor → Segment → Modules → Crew
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlatformHitResolutionSystem : ISystem
    {
        private BufferLookup<PlatformHitEvent> _hitEventsLookup;
        private BufferLookup<PlatformModuleSlot> _moduleSlotsLookup;
        private BufferLookup<PlatformSegmentState> _segmentStatesLookup;
        private BufferLookup<PlatformArcInstance> _arcInstancesLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HullDefRegistry>();
            state.RequireForUpdate<ModuleDefRegistry>();
            _hitEventsLookup = state.GetBufferLookup<PlatformHitEvent>(false);
            _moduleSlotsLookup = state.GetBufferLookup<PlatformModuleSlot>(false);
            _segmentStatesLookup = state.GetBufferLookup<PlatformSegmentState>(false);
            _arcInstancesLookup = state.GetBufferLookup<PlatformArcInstance>(isReadOnly: true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hullRegistry = SystemAPI.GetSingleton<HullDefRegistry>();
            var moduleRegistry = SystemAPI.GetSingleton<ModuleDefRegistry>();

            if (!hullRegistry.Registry.IsCreated || !moduleRegistry.Registry.IsCreated)
            {
                return;
            }

            ref var hullRegistryBlob = ref hullRegistry.Registry.Value;
            ref var moduleRegistryBlob = ref moduleRegistry.Registry.Value;

            _hitEventsLookup.Update(ref state);
            _moduleSlotsLookup.Update(ref state);
            _segmentStatesLookup.Update(ref state);
            _arcInstancesLookup.Update(ref state);

            foreach (var (transform, hullRef, entity) in SystemAPI.Query<
                RefRO<LocalTransform>,
                RefRO<PlatformHullRef>>().WithEntityAccess())
            {
                if (!_hitEventsLookup.HasBuffer(entity) ||
                    !_moduleSlotsLookup.HasBuffer(entity) ||
                    !_segmentStatesLookup.HasBuffer(entity) ||
                    !_arcInstancesLookup.HasBuffer(entity))
                {
                    continue;
                }

                var hitEvents = _hitEventsLookup[entity];
                if (hitEvents.Length == 0)
                {
                    continue;
                }

                var moduleSlots = _moduleSlotsLookup[entity];
                var segmentStates = _segmentStatesLookup[entity];
                var arcInstances = _arcInstancesLookup[entity];

                var hullId = hullRef.ValueRO.HullId;
                if (hullId < 0 || hullId >= hullRegistryBlob.Hulls.Length)
                {
                    hitEvents.Clear();
                    continue;
                }

                ref var hullDef = ref hullRegistryBlob.Hulls[hullId];

                for (int i = hitEvents.Length - 1; i >= 0; i--)
                {
                    var hit = hitEvents[i];
                    var platformEntityRef = entity;
                    ResolveHit(
                        ref state,
                        ref platformEntityRef,
                        in hit,
                        in transform.ValueRO,
                        in hullDef,
                        ref moduleSlots,
                        ref segmentStates,
                        in arcInstances,
                        ref moduleRegistryBlob,
                        ref hullRegistryBlob);

                    hitEvents.RemoveAt(i);
                }
            }
        }

        [BurstCompile]
        private static void ResolveHit(
            ref SystemState state,
            ref Entity platformEntity,
            in PlatformHitEvent hit,
            in LocalTransform transform,
            in HullDef hullDef,
            ref DynamicBuffer<PlatformModuleSlot> moduleSlots,
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            in DynamicBuffer<PlatformArcInstance> arcInstances,
            ref ModuleDefRegistryBlob moduleRegistry,
            ref HullDefRegistryBlob hullRegistry)
        {
            if (hullDef.SegmentCount == 0)
            {
                return;
            }

            var localHitPos = math.transform(math.inverse(transform.ToMatrix()), hit.WorldHitPosition);
            var segmentIndex = SelectSegment(in localHitPos, in hullDef, ref hullRegistry);

            if (segmentIndex < 0)
            {
                return;
            }

            var segmentOffset = hullDef.SegmentOffset + segmentIndex;
            if (segmentOffset < 0 || segmentOffset >= hullRegistry.Segments.Length)
            {
                return;
            }

            ref var segmentDef = ref hullRegistry.Segments[segmentOffset];

            var damageRemaining = hit.DamageAmount;
            GetWeaponProfile(hit.WeaponId, ref moduleRegistry, out var weaponProfile);

            damageRemaining = ApplyShieldDamage(
                damageRemaining,
                in hit.WorldHitPosition,
                in weaponProfile,
                in arcInstances,
                ref moduleSlots,
                ref moduleRegistry);

            if (damageRemaining <= 0f)
            {
                return;
            }

            damageRemaining = ApplyArmorReduction(
                damageRemaining,
                in weaponProfile,
                in segmentDef,
                ref hullRegistry);

            if (damageRemaining <= 0f)
            {
                return;
            }

            ApplySegmentDamage(
                ref segmentStates,
                segmentIndex,
                damageRemaining,
                in weaponProfile);

            ApplyModuleDamage(
                ref moduleSlots,
                segmentIndex,
                damageRemaining,
                in weaponProfile,
                ref moduleRegistry);

            ApplyEnvironmentEffects(
                ref segmentStates,
                segmentIndex,
                in weaponProfile);
        }

        [BurstCompile]
        private static int SelectSegment(
            in float3 localHitPos,
            in HullDef hullDef,
            ref HullDefRegistryBlob hullRegistry)
        {
            int bestSegment = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < hullDef.SegmentCount; i++)
            {
                var segmentOffset = hullDef.SegmentOffset + i;
                if (segmentOffset < 0 || segmentOffset >= hullRegistry.Segments.Length)
                {
                    continue;
                }

                ref var segmentDef = ref hullRegistry.Segments[segmentOffset];
                var min = segmentDef.LocalPosition - segmentDef.Extents;
                var max = segmentDef.LocalPosition + segmentDef.Extents;

                if (localHitPos.x >= min.x && localHitPos.x <= max.x &&
                    localHitPos.y >= min.y && localHitPos.y <= max.y &&
                    localHitPos.z >= min.z && localHitPos.z <= max.z)
                {
                    var distance = math.distance(localHitPos, segmentDef.LocalPosition);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestSegment = i;
                    }
                }
            }

            if (bestSegment >= 0)
            {
                return bestSegment;
            }

            if (hullDef.SegmentCount > 0)
            {
                var coreSegment = FindCoreSegment(in hullDef, ref hullRegistry);
                if (coreSegment >= 0)
                {
                    return coreSegment;
                }
                return 0;
            }

            return -1;
        }

        [BurstCompile]
        private static int FindCoreSegment(in HullDef hullDef, ref HullDefRegistryBlob hullRegistry)
        {
            for (int i = 0; i < hullDef.SegmentCount; i++)
            {
                var segmentOffset = hullDef.SegmentOffset + i;
                if (segmentOffset >= 0 && segmentOffset < hullRegistry.Segments.Length)
                {
                    ref var segmentDef = ref hullRegistry.Segments[segmentOffset];
                    if (segmentDef.IsCore != 0)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        [BurstCompile]
        private static void GetWeaponProfile(
            int weaponId,
            ref ModuleDefRegistryBlob moduleRegistry,
            out WeaponDamageProfile profile)
        {
            profile = new WeaponDamageProfile
            {
                WeaponId = weaponId,
                BaseDamage = 100f,
                TypeFlags = DamageTypeFlags.Kinetic,
                BehaviorFlags = 0,
                ArmorPenetration = 0.5f,
                ShieldEfficiency = 1f,
                HullEfficiency = 1f,
                ModuleEfficiency = 0.5f,
                CrewEfficiency = 0.3f,
                Radius = 0f
            };

            if (weaponId >= 0 && weaponId < moduleRegistry.Modules.Length)
            {
                ref var moduleDef = ref moduleRegistry.Modules[weaponId];
                profile.BaseDamage = moduleDef.PowerDraw * 10f;
            }
        }

        [BurstCompile]
        private static float ApplyShieldDamage(
            float damage,
            in float3 worldHitPos,
            in WeaponDamageProfile weaponProfile,
            in DynamicBuffer<PlatformArcInstance> arcInstances,
            ref DynamicBuffer<PlatformModuleSlot> moduleSlots,
            ref ModuleDefRegistryBlob moduleRegistry)
        {
            if ((weaponProfile.BehaviorFlags & DamageBehaviorFlags.BypassesShields) != 0)
            {
                return damage;
            }

            float shieldAbsorbed = 0f;

            for (int i = 0; i < arcInstances.Length; i++)
            {
                var arc = arcInstances[i];
                if (arc.ModuleId < 0 || arc.ModuleId >= moduleRegistry.Modules.Length)
                {
                    continue;
                }

                ref var moduleDef = ref moduleRegistry.Modules[arc.ModuleId];
                if (moduleDef.Category != ModuleCategory.Shield)
                {
                    continue;
                }

                var distance = math.distance(worldHitPos, arc.WorldPosition);
                if (distance <= arc.ArcAngle * 10f)
                {
                    shieldAbsorbed += damage * weaponProfile.ShieldEfficiency * 0.5f;
                }
            }

            return math.max(0f, damage - shieldAbsorbed);
        }

        [BurstCompile]
        private static float ApplyArmorReduction(
            float damage,
            in WeaponDamageProfile weaponProfile,
            in PlatformSegmentDef segmentDef,
            ref HullDefRegistryBlob hullRegistry)
        {
            if ((weaponProfile.BehaviorFlags & DamageBehaviorFlags.PenetratesArmor) != 0)
            {
                return damage * 0.5f;
            }

            if (segmentDef.ArmorProfileId < 0)
            {
                return damage;
            }

            var armorFactor = 1f;
            for (int i = 0; i < hullRegistry.ArmorProfiles.Length; i++)
            {
                ref var profile = ref hullRegistry.ArmorProfiles[i];
                if (profile.ProfileId == segmentDef.ArmorProfileId)
                {
                    if ((weaponProfile.TypeFlags & DamageTypeFlags.Kinetic) != 0)
                    {
                        armorFactor = 1f - profile.KineticResist;
                    }
                    else if ((weaponProfile.TypeFlags & DamageTypeFlags.Energy) != 0)
                    {
                        armorFactor = 1f - profile.EnergyResist;
                    }
                    else if ((weaponProfile.TypeFlags & DamageTypeFlags.EMP) != 0)
                    {
                        armorFactor = 1f - profile.EMPResist;
                    }
                    else if ((weaponProfile.TypeFlags & DamageTypeFlags.Radiation) != 0)
                    {
                        armorFactor = 1f - profile.RadiationResist;
                    }
                    break;
                }
            }

            var armorThickness = segmentDef.ArmorThickness;
            var penetration = weaponProfile.ArmorPenetration;
            var effectiveFactor = math.lerp(armorFactor, 1f, penetration);

            return damage * effectiveFactor * weaponProfile.HullEfficiency;
        }

        [BurstCompile]
        private static void ApplySegmentDamage(
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            int segmentIndex,
            float damage,
            in WeaponDamageProfile weaponProfile)
        {
            for (int i = 0; i < segmentStates.Length; i++)
            {
                if (segmentStates[i].SegmentIndex == segmentIndex)
                {
                    var state = segmentStates[i];
                    state.HP = math.max(0f, state.HP - damage);
                    if (state.HP <= 0f && (state.Status & SegmentStatusFlags.Destroyed) == 0)
                    {
                        state.Status |= SegmentStatusFlags.Destroyed;
                    }
                    segmentStates[i] = state;
                    break;
                }
            }
        }

        [BurstCompile]
        private static void ApplyModuleDamage(
            ref DynamicBuffer<PlatformModuleSlot> moduleSlots,
            int segmentIndex,
            float damage,
            in WeaponDamageProfile weaponProfile,
            ref ModuleDefRegistryBlob moduleRegistry)
        {
            if ((weaponProfile.BehaviorFlags & DamageBehaviorFlags.ModulePreferred) == 0)
            {
                return;
            }

            var moduleDamage = damage * weaponProfile.ModuleEfficiency;
            var modulesDamaged = 0;
            var maxModulesToDamage = 3;

            for (int i = 0; i < moduleSlots.Length && modulesDamaged < maxModulesToDamage; i++)
            {
                if (moduleSlots[i].SegmentIndex == segmentIndex &&
                    moduleSlots[i].State == ModuleSlotState.Installed)
                {
                    var slot = moduleSlots[i];
                    slot.State = ModuleSlotState.Damaged;
                    moduleSlots[i] = slot;
                    modulesDamaged++;
                }
            }
        }

        [BurstCompile]
        private static void ApplyEnvironmentEffects(
            ref DynamicBuffer<PlatformSegmentState> segmentStates,
            int segmentIndex,
            in WeaponDamageProfile weaponProfile)
        {
            for (int i = 0; i < segmentStates.Length; i++)
            {
                if (segmentStates[i].SegmentIndex == segmentIndex)
                {
                    var state = segmentStates[i];
                    if ((weaponProfile.TypeFlags & DamageTypeFlags.Thermal) != 0)
                    {
                        state.Status |= SegmentStatusFlags.OnFire;
                    }
                    if ((weaponProfile.BehaviorFlags & DamageBehaviorFlags.InternalOrigin) != 0)
                    {
                        state.Status |= SegmentStatusFlags.Breached;
                        state.Status |= SegmentStatusFlags.Depressurized;
                    }
                    segmentStates[i] = state;
                    break;
                }
            }
        }
    }
}

