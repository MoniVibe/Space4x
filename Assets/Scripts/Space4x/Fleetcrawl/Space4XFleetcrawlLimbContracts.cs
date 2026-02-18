using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4x.Fleetcrawl
{
    [Flags]
    public enum FleetcrawlComboTag : ushort
    {
        None = 0,
        Agile = 1 << 0,
        Siege = 1 << 1,
        Vanguard = 1 << 2,
        Support = 1 << 3,
        Arc = 1 << 4,
        Kinetic = 1 << 5,
        Drone = 1 << 6,
        Flux = 1 << 7
    }

    public enum FleetcrawlLimbQualityTier : byte
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    public enum FleetcrawlLimbSlot : byte
    {
        Core = 0,
        Barrel = 1,
        Stabilizer = 2,
        Scope = 3,
        Battery = 4,
        Cooling = 5,
        Utility = 6
    }

    public enum FleetcrawlModuleType : byte
    {
        Weapon = 0,
        Reactor = 1,
        Hangar = 2,
        Utility = 3
    }

    public enum FleetcrawlLimbSharingMode : byte
    {
        Shared = 0,
        Unique = 1
    }

    [InternalBufferCapacity(32)]
    public struct FleetcrawlModuleLimbDefinition : IBufferElementData
    {
        public FixedString64Bytes LimbId;
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public FleetcrawlLimbSharingMode SharingMode;
        public FleetcrawlComboTag ComboTags;
        public FleetcrawlLimbQualityTier MinQuality;
        public FleetcrawlLimbQualityTier MaxQuality;
        public int Weight;
        public int MinLevel;
        public float TurnRateMultiplier;
        public float AccelerationMultiplier;
        public float DecelerationMultiplier;
        public float MaxSpeedMultiplier;
        public float CooldownMultiplier;
        public float DamageMultiplier;
    }

    [InternalBufferCapacity(32)]
    public struct FleetcrawlLimbAffixDefinition : IBufferElementData
    {
        public FixedString64Bytes AffixId;
        public FleetcrawlLimbSlot Slot;
        public FleetcrawlComboTag ComboTags;
        public FleetcrawlLimbQualityTier MinQuality;
        public FleetcrawlLimbQualityTier MaxQuality;
        public int Weight;
        public float TurnRateMultiplier;
        public float AccelerationMultiplier;
        public float DecelerationMultiplier;
        public float MaxSpeedMultiplier;
        public float CooldownMultiplier;
        public float DamageMultiplier;
    }

    public struct FleetcrawlRolledLimb
    {
        public FixedString64Bytes LimbId;
        public FixedString64Bytes AffixId;
        public FleetcrawlModuleType ModuleType;
        public FleetcrawlLimbSlot Slot;
        public FleetcrawlLimbSharingMode SharingMode;
        public FleetcrawlComboTag ComboTags;
        public FleetcrawlLimbQualityTier Quality;
        public uint RollHash;
        public int Level;
        public int RoomIndex;
    }

    public static class FleetcrawlModuleLimbCompatibility
    {
        public static bool IsCompatible(in FleetcrawlModuleLimbDefinition limb, FleetcrawlModuleType moduleType, FleetcrawlLimbSlot slot, int level)
        {
            if (limb.ModuleType != moduleType)
            {
                return false;
            }

            if (limb.Slot != slot)
            {
                return false;
            }

            return level >= math.max(1, limb.MinLevel);
        }

        public static bool HasLimbConflict(in FleetcrawlRolledLimb candidate, DynamicBuffer<FleetcrawlRolledLimbBufferElement> equipped)
        {
            for (var i = 0; i < equipped.Length; i++)
            {
                var existing = equipped[i].Value;
                if (!existing.LimbId.Equals(candidate.LimbId))
                {
                    continue;
                }

                if (candidate.SharingMode == FleetcrawlLimbSharingMode.Unique ||
                    existing.SharingMode == FleetcrawlLimbSharingMode.Unique)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [InternalBufferCapacity(8)]
    public struct FleetcrawlRolledLimbBufferElement : IBufferElementData
    {
        public FleetcrawlRolledLimb Value;
    }

    public static class FleetcrawlDeterministicLimbRollService
    {
        public static uint ComputeRollHash(uint seed, int roomIndex, int level, FleetcrawlLimbSlot slot, int stream)
        {
            var input = new uint4(
                seed ^ 0x9E3779B9u,
                (uint)(roomIndex + 1) * 2246822519u,
                ((uint)math.max(1, level) * 3266489917u) ^ ((uint)slot + 1u),
                (uint)(stream + 1) * 668265263u);
            return math.hash(input);
        }

        public static FleetcrawlLimbQualityTier RollQualityTier(uint seed, int roomIndex, int level, FleetcrawlLimbSlot slot, int stream)
        {
            var hash = ComputeRollHash(seed, roomIndex, level, slot, stream);
            var roll = hash % 1000u;
            var levelBonus = math.clamp(level / 3, 0, 120);

            var uncommonThreshold = 430u + (uint)levelBonus;
            var rareThreshold = 170u + (uint)(levelBonus / 2);
            var epicThreshold = 45u + (uint)(levelBonus / 3);
            var legendaryThreshold = 8u + (uint)(levelBonus / 4);

            if (roll < legendaryThreshold)
            {
                return FleetcrawlLimbQualityTier.Legendary;
            }

            if (roll < epicThreshold)
            {
                return FleetcrawlLimbQualityTier.Epic;
            }

            if (roll < rareThreshold)
            {
                return FleetcrawlLimbQualityTier.Rare;
            }

            if (roll < uncommonThreshold)
            {
                return FleetcrawlLimbQualityTier.Uncommon;
            }

            return FleetcrawlLimbQualityTier.Common;
        }

        public static int PickWeightedIndex<T>(DynamicBuffer<T> entries, Func<T, bool> predicate, Func<T, int> weightSelector, uint hash)
            where T : unmanaged, IBufferElementData
        {
            var totalWeight = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                var row = entries[i];
                if (!predicate(row))
                {
                    continue;
                }

                totalWeight += math.max(0, weightSelector(row));
            }

            if (totalWeight <= 0)
            {
                return -1;
            }

            var pick = (int)(hash % (uint)totalWeight);
            var cursor = 0;
            for (var i = 0; i < entries.Length; i++)
            {
                var row = entries[i];
                if (!predicate(row))
                {
                    continue;
                }

                var weight = math.max(0, weightSelector(row));
                if (weight <= 0)
                {
                    continue;
                }

                cursor += weight;
                if (pick < cursor)
                {
                    return i;
                }
            }

            return -1;
        }

        public static FleetcrawlRolledLimb RollLimb(
            uint seed,
            int roomIndex,
            int level,
            FleetcrawlModuleType moduleType,
            FleetcrawlLimbSlot slot,
            int stream,
            DynamicBuffer<FleetcrawlModuleLimbDefinition> limbDefinitions,
            DynamicBuffer<FleetcrawlLimbAffixDefinition> affixDefinitions)
        {
            var hash = ComputeRollHash(seed, roomIndex, level, slot, stream);
            var quality = RollQualityTier(seed, roomIndex, level, slot, stream);

            var limbIndex = PickWeightedIndex(
                limbDefinitions,
                limb =>
                    FleetcrawlModuleLimbCompatibility.IsCompatible(limb, moduleType, slot, level) &&
                    quality >= limb.MinQuality &&
                    quality <= limb.MaxQuality,
                limb => limb.Weight,
                hash ^ 0xA511E9B3u);

            var limb = limbIndex >= 0 ? limbDefinitions[limbIndex] : default;

            var affixIndex = PickWeightedIndex(
                affixDefinitions,
                affix =>
                    affix.Slot == slot &&
                    quality >= affix.MinQuality &&
                    quality <= affix.MaxQuality,
                affix => affix.Weight,
                hash ^ 0x7F4A7C15u);

            var affix = affixIndex >= 0 ? affixDefinitions[affixIndex] : default;

            var combinedTags = limb.ComboTags | affix.ComboTags;
            if (combinedTags == FleetcrawlComboTag.None)
            {
                combinedTags = moduleType switch
                {
                    FleetcrawlModuleType.Weapon => FleetcrawlComboTag.Siege,
                    FleetcrawlModuleType.Hangar => FleetcrawlComboTag.Drone,
                    FleetcrawlModuleType.Reactor => FleetcrawlComboTag.Flux,
                    _ => FleetcrawlComboTag.Support
                };
            }

            return new FleetcrawlRolledLimb
            {
                LimbId = limb.LimbId,
                AffixId = affix.AffixId,
                ModuleType = moduleType,
                Slot = slot,
                SharingMode = limb.SharingMode,
                ComboTags = combinedTags,
                Quality = quality,
                RollHash = hash,
                Level = math.max(1, level),
                RoomIndex = roomIndex
            };
        }
    }
}
