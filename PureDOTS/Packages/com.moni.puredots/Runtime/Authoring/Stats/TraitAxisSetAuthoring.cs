using System;
using PureDOTS.Runtime.Stats;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace PureDOTS.Authoring.Stats
{
    /// <summary>
    /// Authoring helper that assigns trait axis catalog references and initial axis values.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TraitAxisSetAuthoring : MonoBehaviour
    {
        [Header("Catalog Reference")]
        [Tooltip("Optional trait axis catalog asset defining available axes and ranges.")]
        public TraitAxisCatalogAsset catalogAsset;

        [Header("Initial Axis Values")]
        public AxisValueEntry[] axisValues = Array.Empty<AxisValueEntry>();

        [Header("Drift Configuration")]
        public bool addTraitDriftState = true;
        [Tooltip("Per-tick decay applied when drift fires (e.g., 0.001).")]
        [Range(0f, 1f)] public float decayRatePerTick = 0.001f;
        [Tooltip("Tick interval between decay applications.")]
        [Min(1)] public uint driftInterval = 60;
        [Tooltip("Resistance exponent (higher = stronger resistance near extremes).")]
        [Range(0.1f, 8f)] public float resistanceExponent = 2f;

        [Serializable]
        public struct AxisValueEntry
        {
            public string axisId;
            [Tooltip("Axis value (-100 to 100 for bipolar axes).")]
            [Range(-100f, 100f)] public float value;
        }

        private sealed class Baker : Baker<TraitAxisSetAuthoring>
        {
            public override void Bake(TraitAxisSetAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                BlobAssetReference<TraitAxisCatalogBlob> catalogBlob = default;
                if (authoring.catalogAsset != null && authoring.catalogAsset.AxisCount > 0)
                {
                    catalogBlob = authoring.catalogAsset.BuildBlobAsset(Allocator.Persistent);
                    AddBlobAsset(ref catalogBlob, out _);
                }

                AddComponent(entity, new TraitAxisSet
                {
                    Catalog = catalogBlob
                });

                var axisBuffer = AddBuffer<TraitAxisValue>(entity);

                if (authoring.axisValues is { Length: > 0 })
                {
                    foreach (var entry in authoring.axisValues)
                    {
                        if (string.IsNullOrWhiteSpace(entry.axisId))
                        {
                            continue;
                        }

                        var axisId = entry.axisId.Trim();
                        float value = entry.value;

                        if (authoring.catalogAsset != null &&
                            authoring.catalogAsset.TryGetDefinition(axisId, out var definition))
                        {
                            value = math.clamp(value, definition.minValue, definition.maxValue);
                        }
                        else
                        {
                            value = math.clamp(value, -100f, 100f);
                        }

                        axisBuffer.Add(new TraitAxisValue
                        {
                            AxisId = new FixedString32Bytes(axisId),
                            Value = value
                        });
                    }
                }

                if (authoring.addTraitDriftState)
                {
                    AddComponent(entity, new TraitDriftState
                    {
                        DecayRatePerTick = math.max(0f, authoring.decayRatePerTick),
                        DriftInterval = math.max(1u, authoring.driftInterval),
                        ResistanceExponent = math.max(0.1f, authoring.resistanceExponent),
                        LastDriftTick = 0
                    });
                }
            }
        }
    }
}

