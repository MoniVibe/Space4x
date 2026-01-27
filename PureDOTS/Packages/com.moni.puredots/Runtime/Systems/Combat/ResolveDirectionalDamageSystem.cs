using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Ships;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

// Alias to disambiguate HitEvent between Combat and Ships namespaces
using ShipHitEvent = PureDOTS.Runtime.Ships.HitEvent;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Resolves directional damage from ShipHitEvent buffer.
    /// Pipeline: World→Local → Facing select → Shield check → Armor apply → Module OBB pick → Module damage → Hull overflow.
    /// Runs in CombatSystemGroup after ProjectileCollisionSystem.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ProjectileCollisionSystem))]
    [UpdateBefore(typeof(DamageApplicationSystem))]
    public partial struct ResolveDirectionalDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;

            // Get damage rules singleton (if exists)
            DamageRulesBlob damageRules = new DamageRulesBlob
            {
                OverpenToModuleFrac = 0.5f,
                OverkillToHullFrac = 0.3f,
                DestroyedModuleHullLeak = 0.2f,
                LifeBoatThreshold = 0.3f
            };

            if (SystemAPI.TryGetSingleton<DamageRulesSingleton>(out var rulesRef) && rulesRef.Rules.IsCreated)
            {
                ref var rules = ref rulesRef.Rules.Value;
                damageRules = rules;
            }

            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            transformLookup.Update(ref state);

            var job = new ResolveDirectionalDamageJob
            {
                CurrentTick = currentTick,
                DamageRules = damageRules,
                TransformLookup = transformLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ResolveDirectionalDamageJob : IJobEntity
        {
            public uint CurrentTick;
            public DamageRulesBlob DamageRules;
            [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;

            void Execute(
                Entity entity,
                ref DynamicBuffer<ShipHitEvent> hitEvents,
                ref HullState hull,
                ref ShieldArcState shields,
                ref ArmorDegradeState armor,
                ref DynamicBuffer<ModuleRuntimeStateElement> modules,
                ref DynamicBuffer<ModuleDamageEvent> moduleDamageEvents,
                in ShipLayoutRef layoutRef,
                in LocalTransform transform)
            {
                if (!layoutRef.Blob.IsCreated)
                {
                    hitEvents.Clear();
                    return;
                }

                ref var layout = ref layoutRef.Blob.Value;

                // Process each hit event
                for (int i = 0; i < hitEvents.Length; i++)
                {
                    var hit = hitEvents[i];

                    // Transform world position/normal to local space
                    float4x4 worldToLocal = math.inverse(transform.ToMatrix());
                    float3 localPos = math.transform(worldToLocal, hit.WorldPos);
                    float3 localNormal = math.rotate(worldToLocal, hit.WorldNormal);

                    // Select facing (8-way)
                    Facing8 facing = FacingOf(localNormal);

                    float damage = hit.Damage;

                    // Shield check
                    if (layout.Shields.Length > 0)
                    {
                        int shieldIndex = FindShieldArc(ref layout, facing);
                        if (shieldIndex >= 0)
                        {
                            ref var shieldArc = ref layout.Shields[shieldIndex];
                            float shieldHP = shields.GetHP(facing);

                            // Check coverage (simplified - would use actual facing direction)
                            if (shieldHP > 0f)
                            {
                                float shieldTake = math.min(damage, shieldHP);
                                shields.SetHP(facing, shieldHP - shieldTake);
                                damage -= shieldTake;
                            }
                        }
                    }

                    if (damage <= 0f)
                    {
                        continue; // Shield absorbed all damage
                    }

                    // Armor apply
                    int armorIndex = FindArmorArc(ref layout, facing);
                    if (armorIndex >= 0)
                    {
                        ref var armorArc = ref layout.Armor[armorIndex];
                        float resist = GetResistance(in armorArc, hit.Kind);
                        float armorThickness = armorArc.Thickness - armor.GetDegrade(facing);

                        // Reduce damage by thickness, then apply resistance
                        damage = math.max(0f, damage - armorThickness) * (1f - resist);

                        // Apply degradation (ablative armor)
                        armor.SetDegrade(facing, armor.GetDegrade(facing) + damage * 0.1f);
                    }

                    if (damage <= 0f)
                    {
                        // Armor absorbed all damage
                        hull.HP -= damage * DamageRules.OverpenToModuleFrac;
                        continue;
                    }

                    // Module pick via OBB test
                    int moduleIndex = FindHitModule(ref layout, localPos, hit.WorldPos, transform);

                    if (moduleIndex >= 0 && moduleIndex < modules.Length)
                    {
                        ref var module = ref modules.ElementAt(moduleIndex);

                        if (module.HP > 0f)
                        {
                            // Module takes damage
                            float moduleTake = math.min(damage, module.HP);
                            module.HP -= moduleTake;
                            damage -= moduleTake;

                            bool wasDestroyed = module.Destroyed == 0 && module.HP <= 0f;
                            if (wasDestroyed)
                            {
                                module.Destroyed = 1;
                            }

                            // Emit module damage event
                            moduleDamageEvents.Add(new ModuleDamageEvent
                            {
                                ShipEntity = entity,
                                ModuleIndex = (ushort)moduleIndex,
                                Damage = moduleTake,
                                WasDestroyed = wasDestroyed ? (byte)1 : (byte)0,
                                Tick = CurrentTick
                            });

                            // Overflow to hull
                            if (damage > 0f)
                            {
                                hull.HP -= damage * DamageRules.OverkillToHullFrac;
                            }
                        }
                        else if (module.Destroyed != 0)
                        {
                            // Already destroyed module leaks to hull
                            hull.HP -= damage * DamageRules.DestroyedModuleHullLeak;
                        }
                    }
                    else
                    {
                        // No module hit, overflow to hull
                        hull.HP -= damage * DamageRules.OverkillToHullFrac;
                    }

                    // Clamp hull HP
                    hull.HP = math.max(0f, hull.HP);
                }

                // Clear processed hit events
                hitEvents.Clear();
            }

            private static Facing8 FacingOf(float3 localNormal)
            {
                // Choose axis octant by largest component signs
                float3 abs = math.abs(localNormal);
                float3 n = localNormal;

                if (abs.z >= abs.x && abs.z >= abs.y)
                {
                    return n.z >= 0 ? Facing8.Fore : Facing8.Aft;
                }

                if (abs.x >= abs.y)
                {
                    return n.x >= 0 ? Facing8.Starboard : Facing8.Port;
                }

                // Diagonal facings (simplified)
                if (n.y >= 0)
                {
                    return n.x >= 0 ? Facing8.ForeStar : Facing8.ForePort;
                }
                else
                {
                    return n.x >= 0 ? Facing8.AftStar : Facing8.AftPort;
                }
            }

            private static int FindArmorArc(ref ShipLayoutBlob layout, Facing8 facing)
            {
                for (int i = 0; i < layout.Armor.Length; i++)
                {
                    if (layout.Armor[i].Facing == facing)
                    {
                        return i;
                    }
                }
                return -1;
            }

            private static int FindShieldArc(ref ShipLayoutBlob layout, Facing8 facing)
            {
                for (int i = 0; i < layout.Shields.Length; i++)
                {
                    if (layout.Shields[i].Facing == facing)
                    {
                        return i;
                    }
                }
                return -1;
            }

            private static float GetResistance(in ArmorArc armor, DamageKind kind)
            {
                return kind switch
                {
                    DamageKind.Kinetic => armor.KineticResist,
                    DamageKind.Energy => armor.EnergyResist,
                    DamageKind.Explosive => armor.ExplosiveResist,
                    _ => 0f
                };
            }

            private static int FindHitModule(ref ShipLayoutBlob layout, float3 localPos, float3 worldPos, LocalTransform transform)
            {
                // Test OBBs for module hit
                // For now, use simplified point-in-OBB test
                // Full implementation would test segment vs OBB

                for (int slotIdx = 0; slotIdx < layout.Modules.Length; slotIdx++)
                {
                    ref var slot = ref layout.Modules[slotIdx];
                    for (int volIdx = 0; volIdx < slot.Volumes.Length; volIdx++)
                    {
                        ref var obb = ref slot.Volumes[volIdx];
                        if (PointInOBB(localPos, in obb))
                        {
                            return slotIdx;
                        }
                    }
                }

                return -1;
            }

            private static bool PointInOBB(float3 point, in ModuleHitOBB obb)
            {
                // Transform point to OBB local space
                float4x4 obbToLocal = math.inverse(new float4x4(obb.Rot, obb.Center));
                float3 localPoint = math.transform(obbToLocal, point);

                // Test against AABB
                float3 diff = localPoint - obb.Center;
                return math.all(math.abs(diff) <= obb.Extents);
            }
        }
    }

    /// <summary>
    /// Singleton component holding damage rules blob reference.
    /// </summary>
    public struct DamageRulesSingleton : IComponentData
    {
        public BlobAssetReference<DamageRulesBlob> Rules;
    }
}

