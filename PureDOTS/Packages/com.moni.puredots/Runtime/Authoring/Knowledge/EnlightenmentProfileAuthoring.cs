#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Knowledge;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PureDOTS.Authoring.Knowledge
{
    /// <summary>
    /// Authoring ScriptableObject for enlightenment progression profiles.
    /// </summary>
    public class EnlightenmentProfileAuthoring : MonoBehaviour
    {
        [Header("Level Thresholds")]
        [Tooltip("XP required to reach each level (index 0 = level 1)")]
        public List<float> levelThresholds = new()
        {
            100f,    // Level 1 (Aware)
            300f,    // Level 2 (Initiated)
            600f,    // Level 3 (Apprentice)
            1000f,   // Level 4 (Adept)
            1500f,   // Level 5 (Proficient)
            2200f,   // Level 6 (Expert)
            3000f,   // Level 7 (Master)
            4000f,   // Level 8 (Grandmaster)
            5500f,   // Level 9 (Sage)
            7500f    // Level 10 (Transcendent)
        };

        [Header("Path Bonuses")]
        public List<PathBonusDefinition> pathBonuses = new();

        [Serializable]
        public class PathBonusDefinition
        {
            public EnlightenmentPath path = EnlightenmentPath.Arcane;
            [Range(1, 10)] public int level = 1;
            public EnlightenmentBonusType bonusType = EnlightenmentBonusType.SpellPowerBonus;
            public float bonusValue = 0.1f;
            [Tooltip("Spell/ability/recipe ID unlocked (if applicable)")]
            public string unlockId;
        }
    }

    /// <summary>
    /// Baker for EnlightenmentProfileAuthoring.
    /// </summary>
    public sealed class EnlightenmentProfileBaker : Baker<EnlightenmentProfileAuthoring>
    {
        public override void Bake(EnlightenmentProfileAuthoring authoring)
        {
            using var bb = new BlobBuilder(Allocator.Temp);
            ref var root = ref bb.ConstructRoot<EnlightenmentProfileBlob>();

            // Bake level thresholds
            var thresholds = bb.Allocate(ref root.LevelThresholds, authoring.levelThresholds.Count);
            for (int i = 0; i < authoring.levelThresholds.Count; i++)
            {
                thresholds[i] = authoring.levelThresholds[i];
            }

            // Bake path bonuses
            var bonuses = bb.Allocate(ref root.PathBonuses, authoring.pathBonuses.Count);
            for (int i = 0; i < authoring.pathBonuses.Count; i++)
            {
                var src = authoring.pathBonuses[i];
                bonuses[i] = new EnlightenmentPathBonus
                {
                    Path = src.path,
                    Level = (byte)src.level,
                    BonusType = src.bonusType,
                    BonusValue = src.bonusValue,
                    UnlockId = new FixedString64Bytes(src.unlockId ?? "")
                };
            }

            var blob = bb.CreateBlobAssetReference<EnlightenmentProfileBlob>(Allocator.Persistent);
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new EnlightenmentProfileRef { Blob = blob });
        }
    }
}
#endif

