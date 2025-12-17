using System;
using System.Collections.Generic;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Authoring
{
    using Debug = UnityEngine.Debug;

    
    [DisallowMultipleComponent]
    [AddComponentMenu("Space4X/Personality Archetype Catalog")]
    public sealed class PersonalityArchetypeCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class PersonalityArchetypeData
        {
            public string id;
            [Header("Personality Traits")]
            [Range(-1f, 1f)] public float risk = 0f;
            [Range(-1f, 1f)] public float opportunism = 0f;
            [Range(-1f, 1f)] public float caution = 0f;
            [Range(-1f, 1f)] public float zeal = 0f;
            [Header("Combat")]
            [Range(0.5f, 2f)] public float cooldownMult = 1f;
        }

        public List<PersonalityArchetypeData> archetypes = new List<PersonalityArchetypeData>();

        public sealed class Baker : Unity.Entities.Baker<PersonalityArchetypeCatalogAuthoring>
        {
            public override void Bake(PersonalityArchetypeCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.archetypes == null || authoring.archetypes.Count == 0)
                {
                    UnityDebug.LogWarning("PersonalityArchetypeCatalogAuthoring has no archetypes defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<PersonalityArchetypeCatalogBlob>();
                var archetypeArray = builder.Allocate(ref catalogBlob.Archetypes, authoring.archetypes.Count);

                for (int i = 0; i < authoring.archetypes.Count; i++)
                {
                    var archetypeData = authoring.archetypes[i];
                    archetypeArray[i] = new PersonalityArchetype
                    {
                        Id = new FixedString32Bytes(archetypeData.id ?? string.Empty),
                        Risk = math.clamp(archetypeData.risk, -1f, 1f),
                        Opportunism = math.clamp(archetypeData.opportunism, -1f, 1f),
                        Caution = math.clamp(archetypeData.caution, -1f, 1f),
                        Zeal = math.clamp(archetypeData.zeal, -1f, 1f),
                        CooldownMult = math.clamp(archetypeData.cooldownMult, 0.5f, 2f)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<PersonalityArchetypeCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PersonalityArchetypeCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

