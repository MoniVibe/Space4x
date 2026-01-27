#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Spells;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Spells
{
    /// <summary>
    /// Authoring ScriptableObject for spell catalog.
    /// </summary>
    public class SpellCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class SpellDefinition
        {
            [Header("Identity")]
            public string spellId;
            public string displayName;

            [Header("Classification")]
            public SpellSchool school = SpellSchool.Arcane;
            public SpellCastType castType = SpellCastType.Instant;
            public SpellTargetType targetType = SpellTargetType.SingleEnemy;

            [Header("Costs")]
            [Min(0)] public float manaCost = 10f;
            [Min(0)] public float cooldown = 5f;
            [Min(0)] public float castTime = 0f;

            [Header("Targeting")]
            [Min(0)] public float range = 10f;
            [Min(0)] public float areaRadius = 0f;

            [Header("Requirements")]
            [Range(0, 10)] public int requiredEnlightenment = 0;
            [Range(0, 100)] public int requiredSkillLevel = 0;

            [Header("Prerequisites")]
            public List<PrerequisiteDefinition> prerequisites = new();

            [Header("Effects")]
            public List<SpellEffectDefinition> effects = new();
        }

        [Serializable]
        public class PrerequisiteDefinition
        {
            public PrerequisiteType type = PrerequisiteType.Lesson;
            public string targetId;
            [Range(0, 255)] public int requiredLevel = 1;
        }

        [Serializable]
        public class SpellEffectDefinition
        {
            public SpellEffectType type = SpellEffectType.Damage;
            public float baseValue = 10f;
            public float scalingFactor = 1f;
            public float duration = 0f;
            public string buffId;
        }

        public List<SpellDefinition> spells = new();
    }

    /// <summary>
    /// Baker for SpellCatalogAuthoring.
    /// </summary>
    public sealed class SpellCatalogBaker : Baker<SpellCatalogAuthoring>
    {
        public override void Bake(SpellCatalogAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<SpellDefinitionBlob>();

            var spellArray = bb.Allocate(ref root.Spells, authoring.spells.Count);
            for (int i = 0; i < authoring.spells.Count; i++)
            {
                var src = authoring.spells[i];
                ref var entry = ref spellArray[i];
                entry.SpellId = new FixedString64Bytes(src.spellId);
                entry.DisplayName = new FixedString64Bytes(src.displayName);
                entry.School = src.school;
                entry.CastType = src.castType;
                entry.TargetType = src.targetType;
                entry.ManaCost = src.manaCost;
                entry.Cooldown = src.cooldown;
                entry.CastTime = src.castTime;
                entry.Range = src.range;
                entry.AreaRadius = src.areaRadius;
                entry.RequiredEnlightenment = (byte)src.requiredEnlightenment;
                entry.RequiredSkillLevel = (byte)src.requiredSkillLevel;

                // Bake prerequisites
                var prereqs = bb.Allocate(ref entry.Prerequisites, src.prerequisites.Count);
                for (int p = 0; p < src.prerequisites.Count; p++)
                {
                    prereqs[p] = new SpellPrerequisite
                    {
                        Type = src.prerequisites[p].type,
                        TargetId = new FixedString64Bytes(src.prerequisites[p].targetId),
                        RequiredLevel = (byte)src.prerequisites[p].requiredLevel
                    };
                }

                // Bake effects
                var effects = bb.Allocate(ref entry.Effects, src.effects.Count);
                for (int e = 0; e < src.effects.Count; e++)
                {
                    effects[e] = new SpellEffect
                    {
                        Type = src.effects[e].type,
                        BaseValue = src.effects[e].baseValue,
                        ScalingFactor = src.effects[e].scalingFactor,
                        Duration = src.effects[e].duration,
                        BuffId = new FixedString64Bytes(src.effects[e].buffId ?? "")
                    };
                }

            }

            var blob = bb.CreateBlobAssetReference<SpellDefinitionBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SpellCatalogRef { Blob = blob });
        }
    }
}
#endif

