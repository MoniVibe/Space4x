#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Buffs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Buffs
{
    /// <summary>
    /// Authoring ScriptableObject for buff catalog.
    /// </summary>
    public class BuffCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class BuffDefinition
        {
            [Header("Identity")]
            public string buffId;
            public string displayName;

            [Header("Configuration")]
            public BuffCategory category = BuffCategory.Buff;
            public StackBehavior stacking = StackBehavior.Additive;
            [Range(1, 255)]
            public int maxStacks = 1;
            [Min(0)]
            public float baseDuration = 10f;
            [Min(0)]
            public float tickInterval = 0f;

            [Header("Stat Modifiers")]
            public List<StatModifierDefinition> statModifiers = new();

            [Header("Periodic Effects")]
            public List<PeriodicEffectDefinition> periodicEffects = new();
        }

        [Serializable]
        public class StatModifierDefinition
        {
            public StatTarget stat = StatTarget.Damage;
            public ModifierType type = ModifierType.Flat;
            public float value = 10f;
        }

        [Serializable]
        public class PeriodicEffectDefinition
        {
            public PeriodicEffectType type = PeriodicEffectType.Damage;
            public float value = 5f;
        }

        public List<BuffDefinition> buffs = new();
    }

    /// <summary>
    /// Baker for BuffCatalogAuthoring.
    /// </summary>
    public sealed class BuffCatalogBaker : Baker<BuffCatalogAuthoring>
    {
        public override void Bake(BuffCatalogAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<BuffDefinitionBlob>();

            var buffArray = bb.Allocate(ref root.Buffs, authoring.buffs.Count);
            for (int i = 0; i < authoring.buffs.Count; i++)
            {
                var src = authoring.buffs[i];
                ref var entry = ref buffArray[i];
                entry.BuffId = new FixedString64Bytes(src.buffId);
                entry.DisplayName = new FixedString64Bytes(src.displayName);
                entry.Category = src.category;
                entry.Stacking = src.stacking;
                entry.MaxStacks = (byte)math.clamp(src.maxStacks, 1, 255);
                entry.BaseDuration = math.max(0f, src.baseDuration);
                entry.TickInterval = math.max(0f, src.tickInterval);

                // Bake stat modifiers
                var modifiers = bb.Allocate(ref entry.StatModifiers, src.statModifiers.Count);
                for (int m = 0; m < src.statModifiers.Count; m++)
                {
                    modifiers[m] = new BuffStatModifier
                    {
                        Stat = src.statModifiers[m].stat,
                        Type = src.statModifiers[m].type,
                        Value = src.statModifiers[m].value
                    };
                }

                // Bake periodic effects
                var periodic = bb.Allocate(ref entry.PeriodicEffects, src.periodicEffects.Count);
                for (int p = 0; p < src.periodicEffects.Count; p++)
                {
                    periodic[p] = new BuffPeriodicEffect
                    {
                        Type = src.periodicEffects[p].type,
                        Value = src.periodicEffects[p].value
                    };
                }

            }

            var blob = bb.CreateBlobAssetReference<BuffDefinitionBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new BuffCatalogRef { Blob = blob });
        }
    }
}
#endif

