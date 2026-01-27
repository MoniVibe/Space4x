using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Runtime.Spatial
{
    /// <summary>
    /// Factory interface for creating spatial grid provider instances.
    /// Implementations create provider structs that implement <see cref="ISpatialGridProvider"/>.
    /// </summary>
    public interface ISpatialProviderFactory
    {
        /// <summary>
        /// Creates a provider instance for the given provider ID.
        /// Returns true if the provider was created successfully, false otherwise.
        /// </summary>
        bool TryCreateProvider(byte providerId, out ISpatialGridProvider provider);
    }

    /// <summary>
    /// Singleton component storing the spatial provider registry state.
    /// Created by <see cref="PureDOTS.Systems.Spatial.SpatialProviderRegistrySystem"/> at bootstrap.
    /// </summary>
    public struct SpatialProviderRegistry : IComponentData
    {
        /// <summary>
        /// Next available provider ID for registration.
        /// Starts at 2 (after default providers: 0=Hashed, 1=Uniform).
        /// </summary>
        public byte NextProviderId;

        /// <summary>
        /// Version counter incremented whenever providers are registered or unregistered.
        /// </summary>
        public uint Version;
    }

    /// <summary>
    /// Buffer entry mapping a provider ID to its metadata and factory information.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct SpatialProviderRegistryEntry : IBufferElementData
    {
        /// <summary>
        /// Unique identifier for this provider (typically 0-255).
        /// </summary>
        public byte ProviderId;

        /// <summary>
        /// Human-readable name for the provider (e.g., "HashedGrid", "UniformGrid").
        /// </summary>
        public FixedString128Bytes ProviderName;

        /// <summary>
        /// Factory type identifier (used internally to select the correct factory).
        /// </summary>
        public byte FactoryTypeId;

        /// <summary>
        /// Reserved for future provider-specific configuration blob asset reference.
        /// </summary>
        public BlobAssetReference<byte> ConfigBlob;
    }

    /// <summary>
    /// Known factory type identifiers for built-in providers.
    /// Custom providers should use IDs >= <see cref="Custom"/>.
    /// </summary>
    public static class SpatialProviderFactoryTypeIds
    {
        public const byte Hashed = 0;
        public const byte Uniform = 1;
        public const byte Custom = 2;
    }

    /// <summary>
    /// Factory implementation for creating built-in hashed grid providers.
    /// </summary>
    public struct HashedSpatialGridProviderFactory : ISpatialProviderFactory
    {
        public bool TryCreateProvider(byte providerId, out ISpatialGridProvider provider)
        {
            if (providerId == SpatialGridProviderIds.Hashed)
            {
                provider = new HashedSpatialGridProvider();
                return true;
            }

            provider = default;
            return false;
        }
    }

    /// <summary>
    /// Factory implementation for creating built-in uniform grid providers.
    /// </summary>
    public struct UniformSpatialGridProviderFactory : ISpatialProviderFactory
    {
        public bool TryCreateProvider(byte providerId, out ISpatialGridProvider provider)
        {
            if (providerId == SpatialGridProviderIds.Uniform)
            {
                provider = new UniformSpatialGridProvider();
                return true;
            }

            provider = default;
            return false;
        }
    }
}

