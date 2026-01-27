using Unity.Mathematics;
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;

namespace PureDOTS.Runtime.Progression
{
    /// <summary>
    /// Static helpers for progression calculations.
    /// </summary>
    [BurstCompile]
    public static class ProgressionHelpers
    {
        // XP thresholds for mastery tiers
        private const uint XP_NOVICE = 20;
        private const uint XP_APPRENTICE = 50;
        private const uint XP_JOURNEYMAN = 100;
        private const uint XP_ADEPT = 200;
        private const uint XP_MASTER = 500;
        private const uint XP_GRANDMASTER = 1000;

        /// <summary>
        /// Gets the mastery tier for a given XP amount.
        /// </summary>
        public static SkillMastery GetMasteryForXP(uint xp)
        {
            if (xp >= XP_GRANDMASTER) return SkillMastery.Grandmaster;
            if (xp >= XP_MASTER) return SkillMastery.Master;
            if (xp >= XP_ADEPT) return SkillMastery.Adept;
            if (xp >= XP_JOURNEYMAN) return SkillMastery.Journeyman;
            if (xp >= XP_APPRENTICE) return SkillMastery.Apprentice;
            if (xp >= XP_NOVICE) return SkillMastery.Novice;
            return SkillMastery.Untrained;
        }

        /// <summary>
        /// Gets the XP threshold for a mastery tier.
        /// </summary>
        public static uint GetXPThreshold(SkillMastery mastery)
        {
            return mastery switch
            {
                SkillMastery.Novice => XP_NOVICE,
                SkillMastery.Apprentice => XP_APPRENTICE,
                SkillMastery.Journeyman => XP_JOURNEYMAN,
                SkillMastery.Adept => XP_ADEPT,
                SkillMastery.Master => XP_MASTER,
                SkillMastery.Grandmaster => XP_GRANDMASTER,
                _ => 0
            };
        }

        /// <summary>
        /// Gets the XP required to reach the next mastery tier.
        /// </summary>
        public static uint GetXPToNextMastery(uint currentXP)
        {
            var currentMastery = GetMasteryForXP(currentXP);
            if (currentMastery == SkillMastery.Grandmaster)
                return 0; // Already at max

            var nextMastery = (SkillMastery)((byte)currentMastery + 1);
            var threshold = GetXPThreshold(nextMastery);
            return threshold > currentXP ? threshold - currentXP : 0;
        }

        /// <summary>
        /// Gets the bonus multiplier for a mastery tier.
        /// </summary>
        public static float GetMasteryBonus(SkillMastery mastery)
        {
            return mastery switch
            {
                SkillMastery.Untrained => 0f,
                SkillMastery.Novice => 0.05f,      // 5%
                SkillMastery.Apprentice => 0.10f, // 10%
                SkillMastery.Journeyman => 0.20f, // 20%
                SkillMastery.Adept => 0.30f,      // 30%
                SkillMastery.Master => 0.40f,     // 40%
                SkillMastery.Grandmaster => 0.50f,// 50%
                _ => 0f
            };
        }

        /// <summary>
        /// Gets the speed bonus for a mastery tier (affects action speed).
        /// </summary>
        public static float GetMasterySpeedBonus(SkillMastery mastery)
        {
            return mastery switch
            {
                SkillMastery.Untrained => 0f,
                SkillMastery.Novice => 0.02f,
                SkillMastery.Apprentice => 0.05f,
                SkillMastery.Journeyman => 0.10f,
                SkillMastery.Adept => 0.15f,
                SkillMastery.Master => 0.20f,
                SkillMastery.Grandmaster => 0.25f,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets the quality bonus for a mastery tier (affects crafting quality).
        /// </summary>
        public static float GetMasteryQualityBonus(SkillMastery mastery)
        {
            return mastery switch
            {
                SkillMastery.Untrained => 0f,
                SkillMastery.Novice => 0.05f,
                SkillMastery.Apprentice => 0.10f,
                SkillMastery.Journeyman => 0.15f,
                SkillMastery.Adept => 0.25f,
                SkillMastery.Master => 0.35f,
                SkillMastery.Grandmaster => 0.50f,
                _ => 0f
            };
        }

        /// <summary>
        /// Gets the critical chance bonus for a mastery tier.
        /// </summary>
        public static float GetMasteryCritBonus(SkillMastery mastery)
        {
            return mastery switch
            {
                SkillMastery.Untrained => 0f,
                SkillMastery.Novice => 0.01f,
                SkillMastery.Apprentice => 0.02f,
                SkillMastery.Journeyman => 0.04f,
                SkillMastery.Adept => 0.06f,
                SkillMastery.Master => 0.08f,
                SkillMastery.Grandmaster => 0.10f,
                _ => 0f
            };
        }

        /// <summary>
        /// Calculates XP required for a given level.
        /// </summary>
        public static uint CalculateXPForLevel(byte level, uint baseXP, float scaling)
        {
            if (level <= 1) return 0;
            // XP = baseXP * (level ^ scaling)
            return (uint)(baseXP * math.pow(level, scaling));
        }

        /// <summary>
        /// Calculates total XP earned across all levels.
        /// </summary>
        public static uint CalculateTotalXPForLevel(byte level, uint baseXP, float scaling)
        {
            uint total = 0;
            for (byte i = 2; i <= level; i++)
            {
                total += CalculateXPForLevel(i, baseXP, scaling);
            }
            return total;
        }

        /// <summary>
        /// Gets skill XP for a specific domain from buffer.
        /// </summary>
        public static SkillXP GetSkillXP(in DynamicBuffer<SkillXP> buffer, SkillDomain domain)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Domain == domain)
                    return buffer[i];
            }
            return new SkillXP { Domain = domain, CurrentXP = 0, Mastery = SkillMastery.Untrained };
        }

        /// <summary>
        /// Finds the index of a skill domain in the buffer.
        /// </summary>
        public static int FindSkillIndex(in DynamicBuffer<SkillXP> buffer, SkillDomain domain)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Domain == domain)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Awards XP to a skill domain.
        /// </summary>
        public static bool AwardXP(
            ref DynamicBuffer<SkillXP> buffer,
            SkillDomain domain,
            uint amount,
            uint currentTick,
            out SkillMastery oldMastery,
            out SkillMastery newMastery)
        {
            int index = FindSkillIndex(buffer, domain);
            
            if (index < 0)
            {
                // Add new skill entry
                oldMastery = SkillMastery.Untrained;
                newMastery = GetMasteryForXP(amount);
                buffer.Add(new SkillXP
                {
                    Domain = domain,
                    CurrentXP = amount,
                    Mastery = newMastery,
                    LastGainTick = currentTick
                });
                return newMastery != oldMastery;
            }

            var skill = buffer[index];
            oldMastery = skill.Mastery;
            skill.CurrentXP += amount;
            skill.Mastery = GetMasteryForXP(skill.CurrentXP);
            skill.LastGainTick = currentTick;
            newMastery = skill.Mastery;
            buffer[index] = skill;

            return newMastery != oldMastery;
        }

        /// <summary>
        /// Checks if an entity has the required mastery for a skill.
        /// </summary>
        public static bool HasRequiredMastery(
            in DynamicBuffer<SkillXP> buffer,
            SkillDomain domain,
            SkillMastery requiredMastery)
        {
            var skill = GetSkillXP(buffer, domain);
            return skill.Mastery >= requiredMastery;
        }

        /// <summary>
        /// Checks if an entity has unlocked a specific skill.
        /// </summary>
        public static bool HasSkill(
            in DynamicBuffer<UnlockedSkill> buffer,
            FixedString32Bytes skillId)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].SkillId.Equals(skillId))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the highest mastery tier across all skills.
        /// </summary>
        public static SkillMastery GetHighestMastery(in DynamicBuffer<SkillXP> buffer)
        {
            SkillMastery highest = SkillMastery.Untrained;
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].Mastery > highest)
                    highest = buffer[i].Mastery;
            }
            return highest;
        }

        /// <summary>
        /// Gets the primary skill domain (highest XP).
        /// </summary>
        public static SkillDomain GetPrimaryDomain(in DynamicBuffer<SkillXP> buffer)
        {
            SkillDomain primary = SkillDomain.None;
            uint highestXP = 0;
            
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].CurrentXP > highestXP)
                {
                    highestXP = buffer[i].CurrentXP;
                    primary = buffer[i].Domain;
                }
            }
            return primary;
        }

        /// <summary>
        /// Calculates skill points gained per level.
        /// </summary>
        public static byte CalculateSkillPointsForLevel(byte level, byte basePoints)
        {
            // Extra point every 10 levels
            return (byte)(basePoints + (level / 10));
        }

        /// <summary>
        /// Checks if level qualifies for talent point.
        /// </summary>
        public static bool QualifiesForTalentPoint(byte level, byte interval)
        {
            return level > 0 && (level % interval) == 0;
        }

        /// <summary>
        /// Gets the skill tier requirement for a mastery level.
        /// </summary>
        public static byte GetMaxSkillTierForMastery(SkillMastery mastery)
        {
            return mastery switch
            {
                SkillMastery.Untrained => 0,
                SkillMastery.Novice => 1,
                SkillMastery.Apprentice => 2,
                SkillMastery.Journeyman => 3,
                SkillMastery.Adept => 4,
                SkillMastery.Master => 5,
                SkillMastery.Grandmaster => 5,
                _ => 0
            };
        }
    }
}

