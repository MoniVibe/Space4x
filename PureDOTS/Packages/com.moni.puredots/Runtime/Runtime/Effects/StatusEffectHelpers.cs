using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;

namespace PureDOTS.Runtime.Effects
{
    /// <summary>
    /// Static helpers for querying and manipulating status effects.
    /// </summary>
    [BurstCompile]
    public static class StatusEffectHelpers
    {
        /// <summary>
        /// Checks if entity has a specific status effect.
        /// </summary>
        public static bool HasEffect(in DynamicBuffer<ActiveStatusEffect> buffer, StatusEffectType type)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == type)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if entity has any effect from a category.
        /// </summary>
        public static bool HasEffectInCategory(in DynamicBuffer<ActiveStatusEffect> buffer, StatusEffectCategory category)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Category == category)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the total slow percentage from all slow effects.
        /// </summary>
        public static float GetTotalSlowPercent(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float totalSlow = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == StatusEffectType.Slow)
                {
                    // Multiplicative stacking: (1 - slow1) * (1 - slow2) * ...
                    totalSlow = 1f - (1f - totalSlow) * (1f - buffer[i].Value * buffer[i].Stacks);
                }
            }
            return math.clamp(totalSlow, 0f, 0.9f); // Cap at 90% slow
        }

        /// <summary>
        /// Gets the total haste percentage from all haste effects.
        /// </summary>
        public static float GetTotalHastePercent(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float totalHaste = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == StatusEffectType.Haste)
                {
                    totalHaste += buffer[i].Value * buffer[i].Stacks;
                }
            }
            return math.clamp(totalHaste, 0f, 2f); // Cap at 200% haste
        }

        /// <summary>
        /// Gets the net movement speed modifier (accounting for slow and haste).
        /// </summary>
        public static float GetMovementSpeedModifier(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float slow = GetTotalSlowPercent(buffer);
            float haste = GetTotalHastePercent(buffer);
            return (1f + haste) * (1f - slow);
        }

        /// <summary>
        /// Gets the total damage per second from all DoT effects.
        /// </summary>
        public static float GetTotalDamagePerSecond(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float totalDps = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                var effect = buffer[i];
                if (IsDamageOverTime(effect.Type) && effect.TickInterval > 0)
                {
                    float dps = (effect.Value * effect.Stacks) / effect.TickInterval;
                    totalDps += dps;
                }
            }
            return totalDps;
        }

        /// <summary>
        /// Gets the total healing per second from all HoT effects.
        /// </summary>
        public static float GetTotalHealingPerSecond(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float totalHps = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                var effect = buffer[i];
                if (effect.Type == StatusEffectType.Regen && effect.TickInterval > 0)
                {
                    float hps = (effect.Value * effect.Stacks) / effect.TickInterval;
                    totalHps += hps;
                }
            }
            return totalHps;
        }

        /// <summary>
        /// Counts total stacks of a specific effect type.
        /// </summary>
        public static int CountStacks(in DynamicBuffer<ActiveStatusEffect> buffer, StatusEffectType type)
        {
            int totalStacks = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == type)
                {
                    totalStacks += buffer[i].Stacks;
                }
            }
            return totalStacks;
        }

        /// <summary>
        /// Gets the remaining duration of a specific effect type.
        /// </summary>
        public static float GetRemainingDuration(in DynamicBuffer<ActiveStatusEffect> buffer, StatusEffectType type)
        {
            float maxDuration = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == type)
                {
                    maxDuration = math.max(maxDuration, buffer[i].Duration);
                }
            }
            return maxDuration;
        }

        /// <summary>
        /// Checks if entity is crowd controlled (stunned, rooted, etc.).
        /// </summary>
        public static bool IsCrowdControlled(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var type = buffer[i].Type;
                if (type == StatusEffectType.Stun ||
                    type == StatusEffectType.Root ||
                    type == StatusEffectType.Fear ||
                    type == StatusEffectType.Charm ||
                    type == StatusEffectType.Sleep ||
                    type == StatusEffectType.Petrified)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if entity can act (not stunned/sleeping/etc.).
        /// </summary>
        public static bool CanAct(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var type = buffer[i].Type;
                if (type == StatusEffectType.Stun ||
                    type == StatusEffectType.Sleep ||
                    type == StatusEffectType.Petrified ||
                    type == StatusEffectType.Coma)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if entity can move (not rooted/stunned/etc.).
        /// </summary>
        public static bool CanMove(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var type = buffer[i].Type;
                if (type == StatusEffectType.Stun ||
                    type == StatusEffectType.Root ||
                    type == StatusEffectType.Sleep ||
                    type == StatusEffectType.Petrified ||
                    type == StatusEffectType.Coma)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks if entity can use abilities (not silenced).
        /// </summary>
        public static bool CanUseAbilities(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                var type = buffer[i].Type;
                if (type == StatusEffectType.Silence ||
                    type == StatusEffectType.Stun ||
                    type == StatusEffectType.Sleep ||
                    type == StatusEffectType.Petrified ||
                    type == StatusEffectType.Coma)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Gets the total damage reduction from shield effects.
        /// </summary>
        public static float GetShieldAmount(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float totalShield = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == StatusEffectType.Shield)
                {
                    totalShield += buffer[i].Value * buffer[i].Stacks;
                }
            }
            return totalShield;
        }

        /// <summary>
        /// Gets vulnerability multiplier (increased damage taken).
        /// </summary>
        public static float GetVulnerabilityMultiplier(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float totalVuln = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == StatusEffectType.Vulnerability ||
                    buffer[i].Type == StatusEffectType.Exposed)
                {
                    totalVuln += buffer[i].Value * buffer[i].Stacks;
                }
            }
            return 1f + totalVuln;
        }

        /// <summary>
        /// Gets damage reduction from fortify/defensive effects.
        /// </summary>
        public static float GetDamageReduction(in DynamicBuffer<ActiveStatusEffect> buffer)
        {
            float totalReduction = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == StatusEffectType.Fortified)
                {
                    totalReduction += buffer[i].Value * buffer[i].Stacks;
                }
            }
            return math.clamp(totalReduction, 0f, 0.75f); // Cap at 75% reduction
        }

        /// <summary>
        /// Checks if an entity is immune to a specific effect type.
        /// </summary>
        public static bool IsImmune(
            in DynamicBuffer<StatusEffectImmunity> immunities,
            StatusEffectType type,
            StatusEffectCategory category)
        {
            for (int i = 0; i < immunities.Length; i++)
            {
                var immunity = immunities[i];
                // Check specific type immunity
                if (immunity.Type != StatusEffectType.None && immunity.Type == type)
                    return true;
                // Check category immunity
                if (immunity.Type == StatusEffectType.None && immunity.Category == category)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if an effect type deals damage over time.
        /// </summary>
        public static bool IsDamageOverTime(StatusEffectType type)
        {
            return type == StatusEffectType.Poison ||
                   type == StatusEffectType.Bleed ||
                   type == StatusEffectType.Burn ||
                   type == StatusEffectType.Freeze ||
                   type == StatusEffectType.Irradiated ||
                   type == StatusEffectType.Corrode ||
                   type == StatusEffectType.Suffocate;
        }

        /// <summary>
        /// Checks if an effect type is a buff.
        /// </summary>
        public static bool IsBuff(StatusEffectType type)
        {
            return (byte)type >= 20 && (byte)type < 30;
        }

        /// <summary>
        /// Checks if an effect type is a debuff.
        /// </summary>
        public static bool IsDebuff(StatusEffectType type)
        {
            return (byte)type >= 30 && (byte)type < 40 ||
                   (byte)type >= 1 && (byte)type < 20;
        }

        /// <summary>
        /// Finds the index of an effect type in the buffer.
        /// </summary>
        public static int FindEffectIndex(in DynamicBuffer<ActiveStatusEffect> buffer, StatusEffectType type)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Type == type)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Applies a new status effect with proper stacking behavior.
        /// </summary>
        public static bool TryApplyEffect(
            ref DynamicBuffer<ActiveStatusEffect> buffer,
            in DynamicBuffer<StatusEffectImmunity> immunities,
            StatusEffectType type,
            StatusEffectCategory category,
            StackBehavior behavior,
            float duration,
            float value,
            float tickInterval,
            byte maxStacks,
            Entity sourceEntity,
            uint currentTick,
            byte maxEffectsPerEntity)
        {
            // Check immunity
            if (IsImmune(immunities, type, category))
                return false;

            // Check max effects
            if (buffer.Length >= maxEffectsPerEntity)
            {
                // Try to remove expired effects first
                for (int i = buffer.Length - 1; i >= 0; i--)
                {
                    if (buffer[i].Duration <= 0 && buffer[i].Duration >= -0.5f) // Not permanent
                    {
                        buffer.RemoveAt(i);
                        break;
                    }
                }
                if (buffer.Length >= maxEffectsPerEntity)
                    return false;
            }

            int existingIndex = FindEffectIndex(buffer, type);

            if (existingIndex >= 0)
            {
                var existing = buffer[existingIndex];
                
                switch (behavior)
                {
                    case StackBehavior.Replace:
                        existing.Duration = duration;
                        existing.Value = value;
                        existing.Stacks = 1;
                        existing.SourceEntity = sourceEntity;
                        existing.AppliedTick = currentTick;
                        existing.TickTimer = tickInterval;
                        buffer[existingIndex] = existing;
                        return true;

                    case StackBehavior.Refresh:
                        existing.Duration = duration;
                        existing.AppliedTick = currentTick;
                        buffer[existingIndex] = existing;
                        return true;

                    case StackBehavior.Stack:
                        if (existing.Stacks < existing.MaxStacks)
                        {
                            existing.Stacks++;
                            existing.Duration = duration;
                            existing.AppliedTick = currentTick;
                            buffer[existingIndex] = existing;
                            return true;
                        }
                        // At max stacks, just refresh duration
                        existing.Duration = duration;
                        buffer[existingIndex] = existing;
                        return true;

                    case StackBehavior.StackDuration:
                        existing.Duration += duration;
                        buffer[existingIndex] = existing;
                        return true;

                    case StackBehavior.Ignore:
                        return false;
                }
            }

            // Add new effect
            buffer.Add(new ActiveStatusEffect
            {
                Type = type,
                Category = category,
                Behavior = behavior,
                Duration = duration,
                Value = value,
                TickTimer = tickInterval,
                TickInterval = tickInterval,
                Stacks = 1,
                MaxStacks = maxStacks,
                SourceEntity = sourceEntity,
                AppliedTick = currentTick
            });

            return true;
        }

        /// <summary>
        /// Removes all instances of a status effect type.
        /// </summary>
        public static int RemoveEffect(ref DynamicBuffer<ActiveStatusEffect> buffer, StatusEffectType type, bool allStacks)
        {
            int removed = 0;
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (buffer[i].Type == type)
                {
                    if (allStacks)
                    {
                        buffer.RemoveAt(i);
                        removed++;
                    }
                    else
                    {
                        var effect = buffer[i];
                        if (effect.Stacks > 1)
                        {
                            effect.Stacks--;
                            buffer[i] = effect;
                        }
                        else
                        {
                            buffer.RemoveAt(i);
                        }
                        return 1;
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// Removes all debuffs from an entity (cleanse).
        /// </summary>
        public static int CleanseDebuffs(ref DynamicBuffer<ActiveStatusEffect> buffer)
        {
            int removed = 0;
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (IsDebuff(buffer[i].Type))
                {
                    buffer.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }

        /// <summary>
        /// Removes all buffs from an entity (dispel).
        /// </summary>
        public static int DispelBuffs(ref DynamicBuffer<ActiveStatusEffect> buffer)
        {
            int removed = 0;
            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                if (IsBuff(buffer[i].Type))
                {
                    buffer.RemoveAt(i);
                    removed++;
                }
            }
            return removed;
        }
    }
}

