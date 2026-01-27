using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using PureDOTS.Environment;

namespace PureDOTS.Authoring
{
    [CreateAssetMenu(fileName = "EnvironmentEffectCatalog", menuName = "PureDOTS/Environment/Effect Catalog", order = 6)]
    public sealed class EnvironmentEffectCatalog : ScriptableObject
    {
        [Serializable]
        public struct ScalarEffectDefinition
        {
            public string effectId;
            public string channelId;
            [Min(1)] public uint updateStride;
            public float baseOffset;
            public float amplitude;
            public float frequency;
            public float noiseOffset;
            public float damping;
        }

        [Serializable]
        public struct VectorEffectDefinition
        {
            public string effectId;
            public string channelId;
            [Min(1)] public uint updateStride;
            public Vector3 baseVector;
            public Vector3 amplitude;
            public float frequency;
            public float noiseOffset;
            public float damping;
        }

        [Serializable]
        public struct PulseEffectDefinition
        {
            public string effectId;
            public string channelId;
            [Min(1)] public uint updateStride;
            [Min(0f)] public float intensity;
            [Min(1)] public uint intervalTicks;
            [Min(0)] public uint durationTicks;
            [Min(0f)] public float falloff;
        }

        [SerializeField] ScalarEffectDefinition[] _scalarEffects = Array.Empty<ScalarEffectDefinition>();
        [SerializeField] VectorEffectDefinition[] _vectorEffects = Array.Empty<VectorEffectDefinition>();
        [SerializeField] PulseEffectDefinition[] _pulseEffects = Array.Empty<PulseEffectDefinition>();

        public BlobAssetReference<EnvironmentEffectCatalogBlob> ToBlobAsset()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<EnvironmentEffectCatalogBlob>();

            var totalEffects = _scalarEffects.Length + _vectorEffects.Length + _pulseEffects.Length;
            var effects = builder.Allocate(ref root.Effects, totalEffects);
            var scalarParams = builder.Allocate(ref root.ScalarParameters, _scalarEffects.Length);
            var vectorParams = builder.Allocate(ref root.VectorParameters, _vectorEffects.Length);
            var pulseParams = builder.Allocate(ref root.PulseParameters, _pulseEffects.Length);

            var effectIndex = 0;

            for (var i = 0; i < _scalarEffects.Length; i++)
            {
                var definition = _scalarEffects[i];
                scalarParams[i] = new EnvironmentScalarEffectParameters
                {
                    BaseOffset = definition.baseOffset,
                    Amplitude = definition.amplitude,
                    Frequency = math.max(0f, definition.frequency),
                    NoiseOffset = definition.noiseOffset,
                    Damping = math.max(0f, definition.damping)
                };

                effects[effectIndex++] = new EnvironmentEffectDefinition
                {
                    EffectId = ToFixedString(definition.effectId, $"scalar_{i}"),
                    ChannelId = ToFixedString(definition.channelId, "moisture"),
                    Type = EnvironmentEffectType.ScalarField,
                    UpdateStride = math.max(1u, definition.updateStride),
                    ParameterIndex = (ushort)i
                };
            }

            for (var i = 0; i < _vectorEffects.Length; i++)
            {
                var definition = _vectorEffects[i];
                vectorParams[i] = new EnvironmentVectorEffectParameters
                {
                    BaseVector = (float3)definition.baseVector,
                    Amplitude = (float3)definition.amplitude,
                    Frequency = math.max(0f, definition.frequency),
                    NoiseOffset = definition.noiseOffset,
                    Damping = math.max(0f, definition.damping)
                };

                effects[effectIndex++] = new EnvironmentEffectDefinition
                {
                    EffectId = ToFixedString(definition.effectId, $"vector_{i}"),
                    ChannelId = ToFixedString(definition.channelId, "wind"),
                    Type = EnvironmentEffectType.VectorField,
                    UpdateStride = math.max(1u, definition.updateStride),
                    ParameterIndex = (ushort)i
                };
            }

            for (var i = 0; i < _pulseEffects.Length; i++)
            {
                var definition = _pulseEffects[i];
                pulseParams[i] = new EnvironmentPulseEffectParameters
                {
                    Intensity = definition.intensity,
                    IntervalTicks = math.max(1u, definition.intervalTicks),
                    DurationTicks = definition.durationTicks,
                    Falloff = math.max(0f, definition.falloff)
                };

                effects[effectIndex++] = new EnvironmentEffectDefinition
                {
                    EffectId = ToFixedString(definition.effectId, $"pulse_{i}"),
                    ChannelId = ToFixedString(definition.channelId, "global"),
                    Type = EnvironmentEffectType.EventPulse,
                    UpdateStride = math.max(1u, definition.updateStride),
                    ParameterIndex = (ushort)i
                };
            }

            var blob = builder.CreateBlobAssetReference<EnvironmentEffectCatalogBlob>(Allocator.Temp);
            builder.Dispose();
            return blob;
        }

        static FixedString64Bytes ToFixedString(string value, string fallback)
        {
            var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            FixedString64Bytes fixedString = text;
            return fixedString;
        }
    }

    [DisallowMultipleComponent]
    public sealed class EnvironmentEffectCatalogAuthoring : MonoBehaviour
    {
        public EnvironmentEffectCatalog catalog;
    }

    public sealed class EnvironmentEffectCatalogBaker : Baker<EnvironmentEffectCatalogAuthoring>
    {
        public override void Bake(EnvironmentEffectCatalogAuthoring authoring)
        {
            if (authoring.catalog == null)
            {
                Debug.LogWarning("EnvironmentEffectCatalogAuthoring missing catalog reference.", authoring);
                return;
            }

            var entity = GetEntity(TransformUsageFlags.None);
            var blob = authoring.catalog.ToBlobAsset();
            AddBlobAsset(ref blob, out _);
            AddComponent(entity, new EnvironmentEffectCatalogData { Catalog = blob });
        }
    }
}
