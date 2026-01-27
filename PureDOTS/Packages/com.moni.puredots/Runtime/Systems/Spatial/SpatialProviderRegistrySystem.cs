using PureDOTS.Runtime.Spatial;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Spatial
{
    /// <summary>
    /// Bootstrap system that initializes the spatial provider registry with default providers.
    /// Runs in InitializationSystemGroup to ensure registry is available before spatial systems need it.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [BurstCompile]
    public partial struct SpatialProviderRegistrySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpatialProviderRegistry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Check if registry already initialized
            var registryEntity = SystemAPI.GetSingletonEntity<SpatialProviderRegistry>();
            var registry = SystemAPI.GetSingleton<SpatialProviderRegistry>();
            var entries = SystemAPI.GetBuffer<SpatialProviderRegistryEntry>(registryEntity);

            // Only initialize if not already done
            if (registry.Version == 0 && entries.Length == 0)
            {
                // Register default Hashed provider (ID 0)
                entries.Add(new SpatialProviderRegistryEntry
                {
                    ProviderId = SpatialGridProviderIds.Hashed,
                    ProviderName = "HashedGrid",
                    FactoryTypeId = SpatialProviderFactoryTypeIds.Hashed,
                    ConfigBlob = default
                });

                // Register default Uniform provider (ID 1)
                entries.Add(new SpatialProviderRegistryEntry
                {
                    ProviderId = SpatialGridProviderIds.Uniform,
                    ProviderName = "UniformGrid",
                    FactoryTypeId = SpatialProviderFactoryTypeIds.Uniform,
                    ConfigBlob = default
                });

                // Update registry state
                var updatedRegistry = new SpatialProviderRegistry
                {
                    NextProviderId = 2, // Start custom providers at ID 2
                    Version = 1
                };

                SystemAPI.SetSingleton(updatedRegistry);
                state.Enabled = false; // Only run once
            }
            else
            {
                state.Enabled = false; // Already initialized
            }
        }
    }

    /// <summary>
    /// Helper utilities for working with the spatial provider registry.
    /// </summary>
    [BurstCompile]
    public static class SpatialProviderRegistryHelpers
    {
        /// <summary>
        /// Attempts to create a provider instance using the registry.
        /// Returns the factory type ID if the provider was found, or -1 if not found.
        /// The caller must then dispatch to the appropriate provider type based on factory type ID.
        /// </summary>
        [BurstCompile]
        public static bool TryGetProviderFactoryType(
            byte providerId,
            in DynamicBuffer<SpatialProviderRegistryEntry> entries,
            out byte factoryTypeId)
        {
            // Find the registry entry for this provider ID
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ProviderId == providerId)
                {
                    factoryTypeId = entry.FactoryTypeId;
                    return true;
                }
            }

            factoryTypeId = 0;
            return false;
        }

        /// <summary>
        /// Looks up a provider ID by name.
        /// Returns true if found, false otherwise.
        /// </summary>
        [BurstCompile]
        public static bool TryGetProviderIdByName(
            in FixedString128Bytes providerName,
            in DynamicBuffer<SpatialProviderRegistryEntry> entries,
            out byte providerId)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ProviderName.Equals(providerName))
                {
                    providerId = entry.ProviderId;
                    return true;
                }
            }

            providerId = 0;
            return false;
        }

        /// <summary>
        /// Looks up a provider name by ID.
        /// Returns true if found, false otherwise.
        /// </summary>
        [BurstCompile]
        public static bool TryGetProviderNameById(
            byte providerId,
            in DynamicBuffer<SpatialProviderRegistryEntry> entries,
            out FixedString128Bytes providerName)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry.ProviderId == providerId)
                {
                    providerName = entry.ProviderName;
                    return true;
                }
            }

            providerName = default;
            return false;
        }

        /// <summary>
        /// Creates a HashedSpatialGridProvider if the factory type matches.
        /// Returns true if the provider was created.
        /// </summary>
        [BurstCompile]
        public static bool TryCreateHashedProvider(byte factoryTypeId, byte providerId, out HashedSpatialGridProvider provider)
        {
            if (factoryTypeId == SpatialProviderFactoryTypeIds.Hashed && providerId == SpatialGridProviderIds.Hashed)
            {
                provider = new HashedSpatialGridProvider();
                return true;
            }
            provider = default;
            return false;
        }

        /// <summary>
        /// Creates a UniformSpatialGridProvider if the factory type matches.
        /// Returns true if the provider was created.
        /// </summary>
        [BurstCompile]
        public static bool TryCreateUniformProvider(byte factoryTypeId, byte providerId, out UniformSpatialGridProvider provider)
        {
            if (factoryTypeId == SpatialProviderFactoryTypeIds.Uniform && providerId == SpatialGridProviderIds.Uniform)
            {
                provider = new UniformSpatialGridProvider();
                return true;
            }
            provider = default;
            return false;
        }
    }
}

