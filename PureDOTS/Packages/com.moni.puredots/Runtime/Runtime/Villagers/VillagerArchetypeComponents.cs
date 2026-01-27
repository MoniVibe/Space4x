using PureDOTS.Config;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Villagers
{
    /// <summary>
    /// Singleton component referencing the baked villager archetype catalog blob.
    /// </summary>
    public struct VillagerArchetypeCatalogComponent : IComponentData
    {
        public BlobAssetReference<VillagerArchetypeCatalogBlob> Catalog;
    }

    /// <summary>
    /// Optional per-villager assignment that selects a named archetype from the catalog.
    /// CachedIndex is filled lazily to avoid repeated lookups.
    /// </summary>
    public struct VillagerArchetypeAssignment : IComponentData
    {
        public FixedString64Bytes ArchetypeName;
        public int CachedIndex;

        public bool HasCachedIndex => CachedIndex >= 0 && ArchetypeName.Length > 0;
    }

    /// <summary>
    /// Resolved archetype data after applying catalog selection and layered modifiers.
    /// </summary>
    public struct VillagerArchetypeResolved : IComponentData
    {
        public int ArchetypeIndex;
        public VillagerArchetypeData Data;
    }

    /// <summary>
    /// Identifies the type of aggregate relationship driving belonging/modifier influence.
    /// </summary>
    public enum VillagerAggregateKind : byte
    {
        Family = 0,
        Dynasty = 1,
        Village = 2,
        Band = 3,
        Guild = 4,
        Culture = 5,
        Faction = 6,
        Custom = 255
    }

    /// <summary>
    /// Ranked belonging entry for the top aggregates a villager cares about.
    /// Loyalty is clamped to [-200, 200]; only the top N entries (default 5) are considered when applying modifiers.
    /// </summary>
    public struct VillagerBelonging : IBufferElementData
    {
        public Entity AggregateEntity;
        public VillagerAggregateKind Kind;
        public short Loyalty;
        public ushort InfluenceOrder;
        public uint LastInteractionTick;
    }

    public static class VillagerBelongingLimits
    {
        public const int MaxTrackedBelongings = 5;
        public const short MinLoyalty = -200;
        public const short MaxLoyalty = 200;
    }

    public enum VillagerArchetypeModifierSource : byte
    {
        Village = 0,
        Aggregate = 1,
        Family = 2,
        Ambient = 3,
        Education = 4,
        Outlook = 5,
        Discipline = 6,
        Custom = 7
    }

    /// <summary>
    /// Layered modifier applied on top of the base archetype.
    /// Deltas are additive for job weights; multipliers override decay rates when non-zero.
    /// </summary>
    public struct VillagerArchetypeModifier : IBufferElementData
    {
        public VillagerArchetypeModifierSource Source;
        public sbyte GatherJobDelta;
        public sbyte BuildJobDelta;
        public sbyte CraftJobDelta;
        public sbyte CombatJobDelta;
        public sbyte TradeJobDelta;
        public float HungerDecayMultiplier;
        public float EnergyDecayMultiplier;
        public float MoraleDecayMultiplier;
    }

    /// <summary>
    /// Authoring/runtime data that lives on aggregate entities (villages, bands, dynasties, etc.)
    /// describing the cultural modifiers they broadcast to members.
    /// LoyaltyThreshold defines when the modifier should reach full strength.
    /// </summary>
    public struct VillagerAggregateModifierProfile : IBufferElementData
    {
        public short LoyaltyThreshold;
        public VillagerArchetypeModifier Modifier;
    }

    [BurstCompile]
    public static class VillagerArchetypeDefaults
    {
        [BurstCompile]
        public static void CreateFallback(out VillagerArchetypeData data)
        {
            // Use empty name for fallback (Burst-compatible, no System.String usage)
            data = new VillagerArchetypeData
            {
                ArchetypeName = default,
                BasePhysique = 50,
                BaseFinesse = 50,
                BaseWillpower = 50,
                HungerDecayRate = 0.05f,
                EnergyDecayRate = 0.03f,
                MoraleDecayRate = 0.02f,
                GatherJobWeight = 60,
                BuildJobWeight = 50,
                CraftJobWeight = 40,
                CombatJobWeight = 35,
                TradeJobWeight = 30,
                MoralAxisLean = 0,
                OrderAxisLean = 0,
                PurityAxisLean = 0,
                BaseLoyalty = 50
            };
        }

        public static VillagerArchetypeData ApplyModifier(in VillagerArchetypeData data, in VillagerArchetypeModifier modifier)
        {
            var adjusted = data;
            adjusted.GatherJobWeight = (byte)math.clamp(adjusted.GatherJobWeight + modifier.GatherJobDelta, 0, 100);
            adjusted.BuildJobWeight = (byte)math.clamp(adjusted.BuildJobWeight + modifier.BuildJobDelta, 0, 100);
            adjusted.CraftJobWeight = (byte)math.clamp(adjusted.CraftJobWeight + modifier.CraftJobDelta, 0, 100);
            adjusted.CombatJobWeight = (byte)math.clamp(adjusted.CombatJobWeight + modifier.CombatJobDelta, 0, 100);
            adjusted.TradeJobWeight = (byte)math.clamp(adjusted.TradeJobWeight + modifier.TradeJobDelta, 0, 100);

            var hungerMul = math.select(1f, modifier.HungerDecayMultiplier, math.abs(modifier.HungerDecayMultiplier) > 1e-4f);
            var energyMul = math.select(1f, modifier.EnergyDecayMultiplier, math.abs(modifier.EnergyDecayMultiplier) > 1e-4f);
            var moraleMul = math.select(1f, modifier.MoraleDecayMultiplier, math.abs(modifier.MoraleDecayMultiplier) > 1e-4f);

            adjusted.HungerDecayRate = math.clamp(adjusted.HungerDecayRate * hungerMul, 0f, 1f);
            adjusted.EnergyDecayRate = math.clamp(adjusted.EnergyDecayRate * energyMul, 0f, 1f);
            adjusted.MoraleDecayRate = math.clamp(adjusted.MoraleDecayRate * moraleMul, 0f, 1f);

            return adjusted;
        }

        public static VillagerArchetypeModifier ScaleModifier(in VillagerArchetypeModifier modifier, float scale)
        {
            var clampedScale = math.clamp(scale, -2f, 2f);

            return new VillagerArchetypeModifier
            {
                Source = modifier.Source,
                GatherJobDelta = (sbyte)math.clamp(math.round(modifier.GatherJobDelta * clampedScale), sbyte.MinValue, sbyte.MaxValue),
                BuildJobDelta = (sbyte)math.clamp(math.round(modifier.BuildJobDelta * clampedScale), sbyte.MinValue, sbyte.MaxValue),
                CraftJobDelta = (sbyte)math.clamp(math.round(modifier.CraftJobDelta * clampedScale), sbyte.MinValue, sbyte.MaxValue),
                CombatJobDelta = (sbyte)math.clamp(math.round(modifier.CombatJobDelta * clampedScale), sbyte.MinValue, sbyte.MaxValue),
                TradeJobDelta = (sbyte)math.clamp(math.round(modifier.TradeJobDelta * clampedScale), sbyte.MinValue, sbyte.MaxValue),
                HungerDecayMultiplier = modifier.HungerDecayMultiplier == 0f ? 0f : math.pow(modifier.HungerDecayMultiplier, clampedScale),
                EnergyDecayMultiplier = modifier.EnergyDecayMultiplier == 0f ? 0f : math.pow(modifier.EnergyDecayMultiplier, clampedScale),
                MoraleDecayMultiplier = modifier.MoraleDecayMultiplier == 0f ? 0f : math.pow(modifier.MoraleDecayMultiplier, clampedScale)
            };
        }
    }
}
