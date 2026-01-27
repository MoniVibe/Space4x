#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using PureDOTS.Runtime.Spells;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Spells
{
    /// <summary>
    /// Authoring ScriptableObject for school complexity catalog.
    /// </summary>
    public class SchoolComplexityCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class SchoolComplexityDefinition
        {
            [Header("Identity")]
            public SpellSchool school;
            public string displayName;

            [Header("Complexity")]
            [Range(1, 10)]
            public byte baseComplexity = 5;
            [Range(0.1f, 5f)]
            public float learningTimeMultiplier = 1.0f;
            [Range(0.1f, 5f)]
            public float masteryXpMultiplier = 1.0f;
            [Range(1, 10)]
            public byte rarity = 5;
        }

        public List<SchoolComplexityDefinition> entries = new List<SchoolComplexityDefinition>();

        private void OnValidate()
        {
            // Validate schools are unique
            var schools = new HashSet<SpellSchool>();
            foreach (var entry in entries)
            {
                if (schools.Contains(entry.school))
                {
                    Debug.LogWarning($"Duplicate school '{entry.school}' in {name}");
                }
                schools.Add(entry.school);
            }
        }
    }

    /// <summary>
    /// Baker for SchoolComplexityCatalogAuthoring.
    /// </summary>
    public class SchoolComplexityCatalogBaker : Baker<SchoolComplexityCatalogAuthoring>
    {
        public override void Bake(SchoolComplexityCatalogAuthoring authoring)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<SchoolComplexityBlob>();

            var entriesList = new List<SchoolComplexityEntry>();
            foreach (var entryDef in authoring.entries)
            {
                entriesList.Add(new SchoolComplexityEntry
                {
                    School = entryDef.school,
                    BaseComplexity = entryDef.baseComplexity,
                    LearningTimeMultiplier = entryDef.learningTimeMultiplier,
                    MasteryXpMultiplier = entryDef.masteryXpMultiplier,
                    Rarity = entryDef.rarity,
                    DisplayName = new FixedString64Bytes(entryDef.displayName)
                });
            }

            var entriesArray = builder.Allocate(ref root.Entries, entriesList.Count);
            for (int i = 0; i < entriesList.Count; i++)
            {
                entriesArray[i] = entriesList[i];
            }

            var blobAsset = builder.CreateBlobAssetReference<SchoolComplexityBlob>(Allocator.Persistent);
            builder.Dispose();

            // Create singleton entity with catalog reference
            var entity = GetEntity(TransformUsageFlags.None);
            AddBlobAsset(ref blobAsset, out _);
            AddComponent(entity, new SchoolComplexityCatalogRef { Blob = blobAsset });
        }
    }
}
#endif

