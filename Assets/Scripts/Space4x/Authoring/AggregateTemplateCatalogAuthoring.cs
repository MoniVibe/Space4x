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
    [AddComponentMenu("Space4X/Aggregate Template Catalog")]
    public sealed class AggregateTemplateCatalogAuthoring : MonoBehaviour
    {
        [Serializable]
        public class AggregateTemplateData
        {
            public string id;
            [Header("Hull Mix (percentages, should sum to 100)")]
            [Range(0, 100)] public byte hullLightPct = 33;
            [Range(0, 100)] public byte hullCarrierPct = 33;
            [Range(0, 100)] public byte hullHeavyPct = 34;
            [Header("Tech Bounds")]
            [Range(0f, 10f)] public float techFloor = 0f;
            [Range(0f, 10f)] public float techCap = 10f;
            [Header("Crew & Logistics")]
            [Range(0f, 1f)] public float crewGradeMean = 0.5f;
            [Range(0f, 1f)] public float logisticsTolerance = 0.5f;
        }

        public List<AggregateTemplateData> templates = new List<AggregateTemplateData>();

        private void OnValidate()
        {
            // Normalize hull percentages to sum to 100
            foreach (var template in templates)
            {
                var sum = template.hullLightPct + template.hullCarrierPct + template.hullHeavyPct;
                if (sum != 100 && sum > 0)
                {
                    var factor = 100f / sum;
                    template.hullLightPct = (byte)Mathf.RoundToInt(template.hullLightPct * factor);
                    template.hullCarrierPct = (byte)Mathf.RoundToInt(template.hullCarrierPct * factor);
                    template.hullHeavyPct = (byte)Mathf.RoundToInt(template.hullHeavyPct * factor);
                }
            }
        }

        public sealed class Baker : Unity.Entities.Baker<AggregateTemplateCatalogAuthoring>
        {
            public override void Bake(AggregateTemplateCatalogAuthoring authoring)
            {
                if (authoring == null || authoring.templates == null || authoring.templates.Count == 0)
                {
                    UnityDebug.LogWarning("AggregateTemplateCatalogAuthoring has no templates defined.");
                    return;
                }

                using var builder = new BlobBuilder(Allocator.Temp);
                ref var catalogBlob = ref builder.ConstructRoot<AggregateTemplateCatalogBlob>();
                var templateArray = builder.Allocate(ref catalogBlob.Templates, authoring.templates.Count);

                for (int i = 0; i < authoring.templates.Count; i++)
                {
                    var templateData = authoring.templates[i];
                    templateArray[i] = new AggregateTemplate
                    {
                        Id = new FixedString32Bytes(templateData.id ?? string.Empty),
                        HullLightPct = templateData.hullLightPct,
                        HullCarrierPct = templateData.hullCarrierPct,
                        HullHeavyPct = templateData.hullHeavyPct,
                        TechFloor = math.clamp(templateData.techFloor, 0f, 10f),
                        TechCap = math.max(templateData.techCap, templateData.techFloor),
                        CrewGradeMean = math.clamp(templateData.crewGradeMean, 0f, 1f),
                        LogisticsTolerance = math.clamp(templateData.logisticsTolerance, 0f, 1f)
                    };
                }

                var blobAsset = builder.CreateBlobAssetReference<AggregateTemplateCatalogBlob>(Allocator.Persistent);
                builder.Dispose();

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new AggregateTemplateCatalogSingleton { Catalog = blobAsset });
            }
        }
    }
}

