using PureDOTS.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;
using UnityEngine;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Ensures runtime buffers exist for the environment effect pipeline.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(TimeSystemGroup))]
    [UpdateAfter(typeof(EnvironmentGridBootstrapSystem))]
    public partial struct EnvironmentEffectBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EnvironmentEffectCatalogData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var entity = SystemAPI.GetSingletonEntity<EnvironmentEffectCatalogData>();
            var catalogReference = SystemAPI.GetSingleton<EnvironmentEffectCatalogData>().Catalog;

            if (!catalogReference.IsCreated)
            {
                UnityEngine.Debug.LogWarning("[EnvironmentEffectBootstrap] EnvironmentEffectCatalogData has no blob asset; skipping bootstrap.");
                state.Enabled = false;
                return;
            }

            ref var catalog = ref catalogReference.Value;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ValidateCatalog(ref catalog);
#endif

            var runtimeBuffer = entityManager.HasBuffer<EnvironmentEffectRuntime>(entity)
                ? entityManager.GetBuffer<EnvironmentEffectRuntime>(entity)
                : entityManager.AddBuffer<EnvironmentEffectRuntime>(entity);

            var effectCount = catalog.Effects.Length;
            runtimeBuffer.ResizeUninitialized(effectCount);
            for (var i = 0; i < effectCount; i++)
            {
                runtimeBuffer[i] = new EnvironmentEffectRuntime { LastUpdateTick = uint.MaxValue };
            }

            if (!entityManager.HasBuffer<EnvironmentEventPulse>(entity))
            {
                entityManager.AddBuffer<EnvironmentEventPulse>(entity);
            }

            EnsureChannelDescriptors(ref state, entity, catalogReference);

            state.Enabled = false;
        }

        private void EnsureChannelDescriptors(ref SystemState state, Entity catalogEntity, BlobAssetReference<EnvironmentEffectCatalogBlob> catalog)
        {
            var entityManager = state.EntityManager;

            var scalarDescriptors = entityManager.HasBuffer<EnvironmentScalarChannelDescriptor>(catalogEntity)
                ? entityManager.GetBuffer<EnvironmentScalarChannelDescriptor>(catalogEntity)
                : entityManager.AddBuffer<EnvironmentScalarChannelDescriptor>(catalogEntity);

            var scalarContributions = entityManager.HasBuffer<EnvironmentScalarContribution>(catalogEntity)
                ? entityManager.GetBuffer<EnvironmentScalarContribution>(catalogEntity)
                : entityManager.AddBuffer<EnvironmentScalarContribution>(catalogEntity);

            scalarDescriptors.Clear();
            scalarContributions.Clear();

            var scalarOffset = 0;

            if (SystemAPI.HasSingleton<MoistureGrid>())
            {
                var grid = SystemAPI.GetSingleton<MoistureGrid>();
                AppendScalarChannel(ref scalarDescriptors, ref scalarContributions, grid.ChannelId, grid.Metadata, ref scalarOffset);
            }

            if (SystemAPI.HasSingleton<TemperatureGrid>())
            {
                var grid = SystemAPI.GetSingleton<TemperatureGrid>();
                AppendScalarChannel(ref scalarDescriptors, ref scalarContributions, grid.ChannelId, grid.Metadata, ref scalarOffset);
            }

            if (SystemAPI.HasSingleton<BiomeGrid>())
            {
                var grid = SystemAPI.GetSingleton<BiomeGrid>();
                AppendScalarChannel(ref scalarDescriptors, ref scalarContributions, grid.ChannelId, grid.Metadata, ref scalarOffset);
            }

            var vectorDescriptors = entityManager.HasBuffer<EnvironmentVectorChannelDescriptor>(catalogEntity)
                ? entityManager.GetBuffer<EnvironmentVectorChannelDescriptor>(catalogEntity)
                : entityManager.AddBuffer<EnvironmentVectorChannelDescriptor>(catalogEntity);

            var vectorContributions = entityManager.HasBuffer<EnvironmentVectorContribution>(catalogEntity)
                ? entityManager.GetBuffer<EnvironmentVectorContribution>(catalogEntity)
                : entityManager.AddBuffer<EnvironmentVectorContribution>(catalogEntity);

            vectorDescriptors.Clear();
            vectorContributions.Clear();

            var vectorOffset = 0;

            if (SystemAPI.HasSingleton<SunlightGrid>())
            {
                var grid = SystemAPI.GetSingleton<SunlightGrid>();
                AppendVectorChannel(ref vectorDescriptors, ref vectorContributions, grid.ChannelId, grid.Metadata, ref vectorOffset);
            }

            if (SystemAPI.HasSingleton<WindField>())
            {
                var grid = SystemAPI.GetSingleton<WindField>();
                AppendVectorChannel(ref vectorDescriptors, ref vectorContributions, grid.ChannelId, grid.Metadata, ref vectorOffset);
            }
        }

        private static void AppendScalarChannel(ref DynamicBuffer<EnvironmentScalarChannelDescriptor> descriptors,
            ref DynamicBuffer<EnvironmentScalarContribution> contributions,
            FixedString64Bytes channelId, EnvironmentGridMetadata metadata, ref int offset)
        {
            var length = metadata.CellCount;
            if (length <= 0)
            {
                return;
            }

            descriptors.Add(new EnvironmentScalarChannelDescriptor
            {
                ChannelId = channelId,
                Offset = offset,
                Length = length,
                Metadata = metadata
            });

            var start = contributions.Length;
            contributions.ResizeUninitialized(start + length);
            for (var i = start; i < start + length; i++)
            {
                contributions[i] = new EnvironmentScalarContribution { Value = 0f };
            }

            offset += length;
        }

        private static void AppendVectorChannel(ref DynamicBuffer<EnvironmentVectorChannelDescriptor> descriptors,
            ref DynamicBuffer<EnvironmentVectorContribution> contributions,
            FixedString64Bytes channelId, EnvironmentGridMetadata metadata, ref int offset)
        {
            var length = metadata.CellCount;
            if (length <= 0)
            {
                return;
            }

            descriptors.Add(new EnvironmentVectorChannelDescriptor
            {
                ChannelId = channelId,
                Offset = offset,
                Length = length,
                Metadata = metadata
            });

            var start = contributions.Length;
            contributions.ResizeUninitialized(start + length);
            for (var i = start; i < start + length; i++)
            {
                contributions[i] = new EnvironmentVectorContribution { Value = float3.zero };
            }

            offset += length;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private static void ValidateCatalog(ref EnvironmentEffectCatalogBlob catalog)
        {
            var scalarCount = catalog.ScalarParameters.Length;
            var vectorCount = catalog.VectorParameters.Length;
            var pulseCount = catalog.PulseParameters.Length;
            var effectCount = catalog.Effects.Length;

            Assert.IsTrue(scalarCount + vectorCount + pulseCount == effectCount);

            for (var i = 0; i < effectCount; i++)
            {
                ref var definition = ref catalog.Effects[i];
                switch (definition.Type)
                {
                    case EnvironmentEffectType.ScalarField:
                        Assert.IsTrue(definition.ParameterIndex < scalarCount);
                        break;
                    case EnvironmentEffectType.VectorField:
                        Assert.IsTrue(definition.ParameterIndex < vectorCount);
                        break;
                    case EnvironmentEffectType.EventPulse:
                        Assert.IsTrue(definition.ParameterIndex < pulseCount);
                        break;
                    default:
                        Assert.IsTrue(false);
                        break;
                }

                Assert.IsTrue(definition.UpdateStride > 0u);
            }
        }
#endif
    }
}
