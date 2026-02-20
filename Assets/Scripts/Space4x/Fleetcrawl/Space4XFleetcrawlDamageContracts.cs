using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Fleetcrawl
{
    public enum FleetcrawlShieldTopology : byte
    {
        None = 0,
        Bubble = 1,
        Directional = 2
    }

    public enum FleetcrawlShieldArc : byte
    {
        Any = 0,
        Front = 1,
        Left = 2,
        Right = 3,
        Rear = 4
    }

    public enum FleetcrawlHullClass : byte
    {
        LightChassis = 0,
        Balanced = 1,
        HeavyChassis = 2
    }

    public enum FleetcrawlModuleDiscipline : byte
    {
        Reactor = 0,
        Engine = 1,
        ShieldCapacitor = 2,
        ShieldCanopy = 3,
        ArmorPlating = 4
    }

    public enum FleetcrawlDamageOpKind : byte
    {
        None = 0,
        DamageOverTime = 1,
        PowerReduction = 2,
        ShieldRechargeModifier = 3,
        MassModifier = 4,
        ReflectModifier = 5,
        ResistanceModifier = 6,
        SpawnSecondaryPayload = 7
    }

    [System.Flags]
    public enum FleetcrawlDamageResolutionFlags : ushort
    {
        None = 0,
        HitBubbleShield = 1 << 0,
        HitDirectionalShield = 1 << 1,
        ShieldBypassed = 1 << 2,
        HitHull = 1 << 3,
        HullSegmentDestroyed = 1 << 4,
        AppliedDamageOverTime = 1 << 5,
        AppliedPowerReduction = 1 << 6
    }

    public struct FleetcrawlResistanceProfile
    {
        public float Energy;
        public float Thermal;
        public float EM;
        public float Radiation;
        public float Kinetic;
        public float Explosive;
        public float Caustic;

        public static FleetcrawlResistanceProfile Identity => new FleetcrawlResistanceProfile
        {
            Energy = 1f,
            Thermal = 1f,
            EM = 1f,
            Radiation = 1f,
            Kinetic = 1f,
            Explosive = 1f,
            Caustic = 1f
        };
    }

    public struct FleetcrawlDamageDefenderState : IComponentData
    {
        public float3 Forward;
        public float3 Up;
    }

    public struct FleetcrawlDefenseRuntimeState : IComponentData
    {
        public float MassMultiplier;
        public float ShieldRechargeMultiplier;
        public float ReactorOutputMultiplier;
        public float ReflectBonusPct;
        public float IncomingDamageMultiplier;
    }

    [InternalBufferCapacity(4)]
    public struct FleetcrawlShieldLayerState : IBufferElementData
    {
        public FixedString32Bytes LayerId;
        public FleetcrawlShieldTopology Topology;
        public FleetcrawlShieldArc Arc;
        public float Current;
        public float Max;
        public float RechargePerTick;
        public uint RechargeDelayTicks;
        public uint RechargeResumeTick;
        public float ReflectPct;
        public FleetcrawlResistanceProfile Resistances;
    }

    [InternalBufferCapacity(8)]
    public struct FleetcrawlHullSegmentState : IBufferElementData
    {
        public FixedString32Bytes SegmentId;
        public FleetcrawlHullClass HullClass;
        public float Current;
        public float Max;
        public float Armor;
        public float Mass;
        public FleetcrawlResistanceProfile Resistances;
        public byte Active;
    }

    [InternalBufferCapacity(16)]
    public struct FleetcrawlModuleDefenseModifier : IBufferElementData
    {
        public FixedString64Bytes ModifierId;
        public FleetcrawlModuleDiscipline Discipline;
        public float ShieldCapacityMul;
        public float ShieldRechargeMul;
        public float ArmorMul;
        public float MassMul;
        public float ReactorOutputMul;
        public float ReflectAddPct;
        public float EmpResistanceAdd;
        public float CausticResistanceAdd;
    }

    public struct FleetcrawlDamagePacket
    {
        public Entity Source;
        public Entity Target;
        public Space4XDamageType DamageType;
        public WeaponDelivery Delivery;
        public float BaseDamage;
        public float CritMultiplier;
        public float Penetration01;
        public float3 IncomingDirection;
        public int PreferredHullSegmentIndex;
        public FleetcrawlWeaponBehaviorTag BehaviorTags;
    }

    [InternalBufferCapacity(8)]
    public struct FleetcrawlDamagePayloadOp : IBufferElementData
    {
        public FixedString32Bytes EffectId;
        public FleetcrawlDamageOpKind Kind;
        public Space4XDamageType DamageType;
        public float Magnitude;
        public float AuxA;
        public float AuxB;
        public uint DurationTicks;
        public uint TickInterval;
        public byte MaxStacks;
    }

    [InternalBufferCapacity(16)]
    public struct FleetcrawlPendingEffect : IBufferElementData
    {
        public FixedString32Bytes EffectId;
        public FleetcrawlDamageOpKind Kind;
        public Space4XDamageType DamageType;
        public float Magnitude;
        public float AuxA;
        public float AuxB;
        public uint RemainingTicks;
        public uint TickInterval;
        public uint NextTick;
        public byte Stacks;
    }

    public struct FleetcrawlDamageResolution
    {
        public FleetcrawlShieldArc IncomingArc;
        public int ShieldLayerIndex;
        public int HullSegmentIndex;
        public float AppliedShieldDamage;
        public float AppliedHullDamage;
        public float RemainingDamage;
        public float ReflectedDamage;
        public FleetcrawlDamageResolutionFlags Flags;
    }

    public static class FleetcrawlDamageContractResolver
    {
        public static FleetcrawlShieldArc ResolveIncomingArc(float3 defenderForward, float3 incomingDirection)
        {
            var forward = math.normalizesafe(defenderForward, new float3(0f, 0f, 1f));
            var up = new float3(0f, 1f, 0f);
            var right = math.normalizesafe(math.cross(up, forward), new float3(1f, 0f, 0f));
            var approach = math.normalizesafe(-incomingDirection, forward);
            var f = math.dot(forward, approach);
            var r = math.dot(right, approach);
            if (f >= 0.5f) return FleetcrawlShieldArc.Front;
            if (f <= -0.5f) return FleetcrawlShieldArc.Rear;
            return r >= 0f ? FleetcrawlShieldArc.Right : FleetcrawlShieldArc.Left;
        }

        public static FleetcrawlDamageResolution ResolvePacket(
            in FleetcrawlDamagePacket packet,
            in FleetcrawlDamageDefenderState defender,
            DynamicBuffer<FleetcrawlShieldLayerState> shields,
            DynamicBuffer<FleetcrawlHullSegmentState> hullSegments,
            DynamicBuffer<FleetcrawlPendingEffect> pendingEffects,
            DynamicBuffer<FleetcrawlDamagePayloadOp> payloadOps,
            uint tick)
        {
            var result = new FleetcrawlDamageResolution
            {
                IncomingArc = ResolveIncomingArc(defender.Forward, packet.IncomingDirection),
                ShieldLayerIndex = -1,
                HullSegmentIndex = -1,
                RemainingDamage = math.max(0f, packet.BaseDamage * math.max(1f, packet.CritMultiplier)),
                Flags = FleetcrawlDamageResolutionFlags.None
            };

            var penetration = math.saturate(packet.Penetration01);
            var reflected = 0f;
            for (var i = 0; i < shields.Length && result.RemainingDamage > 1e-5f; i++)
            {
                var layer = shields[i];
                if (layer.Current <= 1e-5f || layer.Topology == FleetcrawlShieldTopology.None)
                {
                    continue;
                }
                if (!ArcMatches(layer, result.IncomingArc))
                {
                    continue;
                }

                var resistance = ResolveResistance(layer.Resistances, packet.DamageType);
                var scaledIncoming = result.RemainingDamage * resistance;
                var absorbed = math.min(layer.Current, scaledIncoming);
                if (absorbed <= 1e-5f)
                {
                    continue;
                }

                // Convert post-resistance shield damage back to incoming-damage budget.
                var consumedIncoming = absorbed / math.max(0.05f, resistance);
                layer.Current = math.max(0f, layer.Current - absorbed);
                shields[i] = layer;
                result.AppliedShieldDamage += absorbed;
                result.RemainingDamage = math.max(0f, result.RemainingDamage - consumedIncoming);
                result.ShieldLayerIndex = i;
                reflected += absorbed * math.saturate(layer.ReflectPct);
                result.Flags |= layer.Topology == FleetcrawlShieldTopology.Bubble
                    ? FleetcrawlDamageResolutionFlags.HitBubbleShield
                    : FleetcrawlDamageResolutionFlags.HitDirectionalShield;
            }

            if (result.RemainingDamage > 1e-5f)
            {
                result.Flags |= FleetcrawlDamageResolutionFlags.ShieldBypassed;
                var segmentIndex = ResolveHullSegmentIndex(packet.PreferredHullSegmentIndex, hullSegments);
                result.HullSegmentIndex = segmentIndex;
                if (segmentIndex >= 0)
                {
                    var segment = hullSegments[segmentIndex];
                    var resistance = ResolveResistance(segment.Resistances, packet.DamageType);
                    var resisted = result.RemainingDamage * resistance;
                    var armorBlock = math.max(0f, segment.Armor * (1f - penetration));
                    var finalDamage = math.max(0f, resisted - armorBlock);
                    segment.Current = math.max(0f, segment.Current - finalDamage);
                    if (segment.Current <= 1e-5f)
                    {
                        segment.Active = 0;
                        result.Flags |= FleetcrawlDamageResolutionFlags.HullSegmentDestroyed;
                    }

                    hullSegments[segmentIndex] = segment;
                    result.AppliedHullDamage = finalDamage;
                    result.RemainingDamage = 0f;
                    result.Flags |= FleetcrawlDamageResolutionFlags.HitHull;
                }
            }

            result.ReflectedDamage = reflected;
            ApplyPayloadOps(payloadOps, pendingEffects, packet.DamageType, tick, ref result);
            return result;
        }

        public static void ApplyModuleDefenseModifiers(
            DynamicBuffer<FleetcrawlModuleDefenseModifier> modifiers,
            DynamicBuffer<FleetcrawlShieldLayerState> shields,
            DynamicBuffer<FleetcrawlHullSegmentState> hullSegments,
            ref FleetcrawlDefenseRuntimeState runtimeState)
        {
            for (var i = 0; i < modifiers.Length; i++)
            {
                var modifier = modifiers[i];
                var shieldCapMul = math.max(0.05f, modifier.ShieldCapacityMul == 0f ? 1f : modifier.ShieldCapacityMul);
                var shieldRechargeMul = math.max(0.05f, modifier.ShieldRechargeMul == 0f ? 1f : modifier.ShieldRechargeMul);
                var armorMul = math.max(0.05f, modifier.ArmorMul == 0f ? 1f : modifier.ArmorMul);
                var massMul = math.max(0.05f, modifier.MassMul == 0f ? 1f : modifier.MassMul);
                var reactorMul = math.max(0.05f, modifier.ReactorOutputMul == 0f ? 1f : modifier.ReactorOutputMul);

                runtimeState.MassMultiplier *= massMul;
                runtimeState.ShieldRechargeMultiplier *= shieldRechargeMul;
                runtimeState.ReactorOutputMultiplier *= reactorMul;
                runtimeState.ReflectBonusPct += math.max(0f, modifier.ReflectAddPct);

                for (var s = 0; s < shields.Length; s++)
                {
                    var shield = shields[s];
                    shield.Max *= shieldCapMul;
                    shield.Current = math.min(shield.Max, shield.Current * shieldCapMul);
                    shield.RechargePerTick *= shieldRechargeMul;
                    shield.ReflectPct += math.max(0f, modifier.ReflectAddPct);
                    shield.Resistances.EM = math.max(0.05f, shield.Resistances.EM * (1f - modifier.EmpResistanceAdd));
                    shield.Resistances.Caustic = math.max(0.05f, shield.Resistances.Caustic * (1f - modifier.CausticResistanceAdd));
                    shields[s] = shield;
                }

                for (var h = 0; h < hullSegments.Length; h++)
                {
                    var segment = hullSegments[h];
                    segment.Armor *= armorMul;
                    segment.Mass *= massMul;
                    segment.Resistances.EM = math.max(0.05f, segment.Resistances.EM * (1f - modifier.EmpResistanceAdd));
                    segment.Resistances.Caustic = math.max(0.05f, segment.Resistances.Caustic * (1f - modifier.CausticResistanceAdd));
                    hullSegments[h] = segment;
                }
            }
        }

        public static void TickPendingEffects(
            uint tick,
            DynamicBuffer<FleetcrawlPendingEffect> pendingEffects,
            DynamicBuffer<FleetcrawlHullSegmentState> hullSegments,
            ref FleetcrawlDefenseRuntimeState runtimeState)
        {
            runtimeState.IncomingDamageMultiplier = 1f;
            runtimeState.ShieldRechargeMultiplier = math.max(0.05f, runtimeState.ShieldRechargeMultiplier);
            runtimeState.ReactorOutputMultiplier = math.max(0.05f, runtimeState.ReactorOutputMultiplier);
            runtimeState.MassMultiplier = math.max(0.05f, runtimeState.MassMultiplier);
            runtimeState.ReflectBonusPct = math.max(0f, runtimeState.ReflectBonusPct);

            for (var i = pendingEffects.Length - 1; i >= 0; i--)
            {
                var effect = pendingEffects[i];
                if (effect.RemainingTicks == 0u)
                {
                    pendingEffects.RemoveAt(i);
                    continue;
                }

                var elapsedTick = effect.NextTick <= tick;
                if (elapsedTick)
                {
                    if (effect.Kind == FleetcrawlDamageOpKind.DamageOverTime)
                    {
                        ApplyTickDamage(effect, hullSegments);
                    }
                    else if (effect.Kind == FleetcrawlDamageOpKind.PowerReduction)
                    {
                        runtimeState.ReactorOutputMultiplier *= math.max(0.05f, 1f - effect.Magnitude * effect.Stacks);
                    }
                    else if (effect.Kind == FleetcrawlDamageOpKind.ShieldRechargeModifier)
                    {
                        runtimeState.ShieldRechargeMultiplier *= math.max(0.05f, 1f + effect.Magnitude * effect.Stacks);
                    }
                    else if (effect.Kind == FleetcrawlDamageOpKind.MassModifier)
                    {
                        runtimeState.MassMultiplier *= math.max(0.05f, 1f + effect.Magnitude * effect.Stacks);
                    }
                    else if (effect.Kind == FleetcrawlDamageOpKind.ReflectModifier)
                    {
                        runtimeState.ReflectBonusPct += math.max(0f, effect.Magnitude * effect.Stacks);
                    }

                    var interval = math.max(1u, effect.TickInterval);
                    effect.NextTick = tick + interval;
                }

                effect.RemainingTicks = effect.RemainingTicks > 0u ? effect.RemainingTicks - 1u : 0u;
                if (effect.RemainingTicks == 0u)
                {
                    pendingEffects.RemoveAt(i);
                }
                else
                {
                    pendingEffects[i] = effect;
                }
            }
        }

        private static void ApplyPayloadOps(
            DynamicBuffer<FleetcrawlDamagePayloadOp> payloadOps,
            DynamicBuffer<FleetcrawlPendingEffect> pendingEffects,
            Space4XDamageType fallbackType,
            uint tick,
            ref FleetcrawlDamageResolution result)
        {
            for (var i = 0; i < payloadOps.Length; i++)
            {
                var op = payloadOps[i];
                var kind = op.Kind;
                if (kind != FleetcrawlDamageOpKind.DamageOverTime &&
                    kind != FleetcrawlDamageOpKind.PowerReduction &&
                    kind != FleetcrawlDamageOpKind.ShieldRechargeModifier &&
                    kind != FleetcrawlDamageOpKind.MassModifier &&
                    kind != FleetcrawlDamageOpKind.ReflectModifier &&
                    kind != FleetcrawlDamageOpKind.ResistanceModifier)
                {
                    continue;
                }

                AddPendingEffect(op, pendingEffects, fallbackType, tick);
                if (kind == FleetcrawlDamageOpKind.DamageOverTime)
                {
                    result.Flags |= FleetcrawlDamageResolutionFlags.AppliedDamageOverTime;
                }
                else if (kind == FleetcrawlDamageOpKind.PowerReduction)
                {
                    result.Flags |= FleetcrawlDamageResolutionFlags.AppliedPowerReduction;
                }
            }
        }

        private static void AddPendingEffect(
            in FleetcrawlDamagePayloadOp op,
            DynamicBuffer<FleetcrawlPendingEffect> pendingEffects,
            Space4XDamageType fallbackType,
            uint tick)
        {
            var effectId = op.EffectId;
            var maxStacks = math.max(1, (int)op.MaxStacks);
            var foundIndex = -1;
            for (var i = 0; i < pendingEffects.Length; i++)
            {
                var existing = pendingEffects[i];
                if (existing.Kind == op.Kind &&
                    existing.DamageType == (op.DamageType == Space4XDamageType.Unknown ? fallbackType : op.DamageType) &&
                    existing.EffectId.Equals(effectId))
                {
                    foundIndex = i;
                    break;
                }
            }

            if (foundIndex >= 0)
            {
                var existing = pendingEffects[foundIndex];
                existing.Stacks = (byte)math.min(maxStacks, (int)existing.Stacks + 1);
                existing.Magnitude += op.Magnitude;
                existing.RemainingTicks = math.max(existing.RemainingTicks, math.max(1u, op.DurationTicks));
                pendingEffects[foundIndex] = existing;
                return;
            }

            pendingEffects.Add(new FleetcrawlPendingEffect
            {
                EffectId = effectId,
                Kind = op.Kind,
                DamageType = op.DamageType == Space4XDamageType.Unknown ? fallbackType : op.DamageType,
                Magnitude = op.Magnitude,
                AuxA = op.AuxA,
                AuxB = op.AuxB,
                RemainingTicks = math.max(1u, op.DurationTicks),
                TickInterval = math.max(1u, op.TickInterval),
                NextTick = tick + math.max(1u, op.TickInterval),
                Stacks = 1
            });
        }

        private static void ApplyTickDamage(in FleetcrawlPendingEffect effect, DynamicBuffer<FleetcrawlHullSegmentState> hullSegments)
        {
            var segmentIndex = ResolveHullSegmentIndex(-1, hullSegments);
            if (segmentIndex < 0)
            {
                return;
            }

            var segment = hullSegments[segmentIndex];
            var resistance = ResolveResistance(segment.Resistances, effect.DamageType);
            var damage = math.max(0f, effect.Magnitude * effect.Stacks * resistance);
            segment.Current = math.max(0f, segment.Current - damage);
            if (segment.Current <= 1e-5f)
            {
                segment.Active = 0;
            }
            hullSegments[segmentIndex] = segment;
        }

        private static bool ArcMatches(in FleetcrawlShieldLayerState layer, FleetcrawlShieldArc incomingArc)
        {
            if (layer.Topology == FleetcrawlShieldTopology.Bubble)
            {
                return true;
            }

            return layer.Arc == FleetcrawlShieldArc.Any || layer.Arc == incomingArc;
        }

        private static int ResolveHullSegmentIndex(int preferredIndex, DynamicBuffer<FleetcrawlHullSegmentState> hullSegments)
        {
            if (preferredIndex >= 0 && preferredIndex < hullSegments.Length && hullSegments[preferredIndex].Active != 0)
            {
                return preferredIndex;
            }

            for (var i = 0; i < hullSegments.Length; i++)
            {
                if (hullSegments[i].Active != 0 && hullSegments[i].Current > 1e-5f)
                {
                    return i;
                }
            }

            return -1;
        }

        private static float ResolveResistance(in FleetcrawlResistanceProfile profile, Space4XDamageType damageType)
        {
            var multiplier = damageType switch
            {
                Space4XDamageType.Energy => profile.Energy,
                Space4XDamageType.Thermal => profile.Thermal,
                Space4XDamageType.EM => profile.EM,
                Space4XDamageType.Radiation => profile.Radiation,
                Space4XDamageType.Kinetic => profile.Kinetic,
                Space4XDamageType.Explosive => profile.Explosive,
                Space4XDamageType.Caustic => profile.Caustic,
                _ => 1f
            };
            return math.clamp(multiplier, 0.05f, 4f);
        }
    }
}
