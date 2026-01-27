using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Environment
{
    public enum EnvironmentEffectType : byte
    {
        ScalarField = 0,
        VectorField = 1,
        EventPulse = 2
    }

    /// <summary>
    /// Blob payload describing the catalog of environment effects.
    /// </summary>
    public struct EnvironmentEffectCatalogBlob
    {
        public BlobArray<EnvironmentEffectDefinition> Effects;
        public BlobArray<EnvironmentScalarEffectParameters> ScalarParameters;
        public BlobArray<EnvironmentVectorEffectParameters> VectorParameters;
        public BlobArray<EnvironmentPulseEffectParameters> PulseParameters;
    }

    /// <summary>
    /// General effect definition referencing shared parameter arrays.
    /// </summary>
    public struct EnvironmentEffectDefinition
    {
        public FixedString64Bytes EffectId;
        public FixedString64Bytes ChannelId;
        public EnvironmentEffectType Type;
        public uint UpdateStride;
        public ushort ParameterIndex;
    }

    public struct EnvironmentScalarEffectParameters
    {
        public float BaseOffset;
        public float Amplitude;
        public float Frequency;
        public float NoiseOffset;
        public float Damping;
    }

    public struct EnvironmentVectorEffectParameters
    {
        public float3 BaseVector;
        public float3 Amplitude;
        public float Frequency;
        public float NoiseOffset;
        public float Damping;
    }

    public struct EnvironmentPulseEffectParameters
    {
        public float Intensity;
        public uint IntervalTicks;
        public uint DurationTicks;
        public float Falloff;
    }

    /// <summary>
    /// Singleton component storing the active catalog blob.
    /// </summary>
    public struct EnvironmentEffectCatalogData : IComponentData
    {
        public BlobAssetReference<EnvironmentEffectCatalogBlob> Catalog;
    }

    /// <summary>
    /// Runtime state per effect instance tracking cadence.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EnvironmentEffectRuntime : IBufferElementData
    {
        public uint LastUpdateTick;
    }

    /// <summary>
    /// Descriptor linking a scalar channel to its contribution slice.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EnvironmentScalarChannelDescriptor : IBufferElementData
    {
        public FixedString64Bytes ChannelId;
        public int Offset;
        public int Length;
        public EnvironmentGridMetadata Metadata;
    }

    /// <summary>
    /// Descriptor linking a vector channel to its contribution slice.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct EnvironmentVectorChannelDescriptor : IBufferElementData
    {
        public FixedString64Bytes ChannelId;
        public int Offset;
        public int Length;
        public EnvironmentGridMetadata Metadata;
    }
}
