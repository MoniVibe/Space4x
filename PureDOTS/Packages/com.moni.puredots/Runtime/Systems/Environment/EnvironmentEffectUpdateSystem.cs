using PureDOTS.Environment;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Jobs;

namespace PureDOTS.Systems.Environment
{
    /// <summary>
    /// Applies all configured environment effects each environment tick using data-driven definitions.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(EnvironmentSystemGroup), OrderFirst = true)]
    public partial struct EnvironmentEffectUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<EnvironmentEffectCatalogData>();
            state.RequireForUpdate<EnvironmentEffectRuntime>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var currentTick = timeState.Tick;
            var fixedDeltaTime = math.max(timeState.FixedDeltaTime, 0f);

            var catalogEntity = SystemAPI.GetSingletonEntity<EnvironmentEffectCatalogData>();
            var catalogData = SystemAPI.GetComponent<EnvironmentEffectCatalogData>(catalogEntity);
            var catalog = catalogData.Catalog;
            if (!catalog.IsCreated || catalog.Value.Effects.Length == 0)
            {
                return;
            }

            var runtimeBuffer = SystemAPI.GetBuffer<EnvironmentEffectRuntime>(catalogEntity);
            var pulseBuffer = SystemAPI.GetBuffer<EnvironmentEventPulse>(catalogEntity);
            pulseBuffer.Clear();

            // Prepare channel contexts.
            var scalarDescriptorsBuffer = SystemAPI.GetBuffer<EnvironmentScalarChannelDescriptor>(catalogEntity);
            var vectorDescriptorsBuffer = SystemAPI.GetBuffer<EnvironmentVectorChannelDescriptor>(catalogEntity);
            var scalarContributionBuffer = SystemAPI.GetBuffer<EnvironmentScalarContribution>(catalogEntity);
            var vectorContributionBuffer = SystemAPI.GetBuffer<EnvironmentVectorContribution>(catalogEntity);

            var scalarContributionArray = scalarContributionBuffer.Reinterpret<float>().AsNativeArray();
            var vectorContributionArray = vectorContributionBuffer.Reinterpret<float3>().AsNativeArray();

            ClearScalarContributions(scalarDescriptorsBuffer, scalarContributionArray);
            ClearVectorContributions(vectorDescriptorsBuffer, vectorContributionArray);

            var scalarMoisturePrepared = SystemAPI.TryGetSingletonRW(out RefRW<MoistureGrid> moistureGrid);
            var scalarTemperaturePrepared = SystemAPI.TryGetSingletonRW(out RefRW<TemperatureGrid> temperatureGrid);
            var scalarBiomePrepared = SystemAPI.TryGetSingletonRW(out RefRW<BiomeGrid> biomeGrid);

            var vectorSunlightPrepared = SystemAPI.TryGetSingletonRW(out RefRW<SunlightGrid> sunlightGrid);
            var vectorWindPrepared = SystemAPI.TryGetSingletonRW(out RefRW<WindField> windGrid);

            ref var effects = ref catalog.Value.Effects;
            ref var scalarParams = ref catalog.Value.ScalarParameters;
            ref var vectorParams = ref catalog.Value.VectorParameters;
            ref var pulseParams = ref catalog.Value.PulseParameters;

            var updateMoisture = false;
            var updateTemperature = false;
            var updateBiome = false;
            var updateSunlight = false;
            var updateWind = false;

            for (var i = 0; i < effects.Length; i++)
            {
                ref var definition = ref effects[i];
                var runtime = runtimeBuffer[i];
                if (!EnvironmentEffectUtility.ShouldUpdate(currentTick, runtime.LastUpdateTick, definition.UpdateStride))
                {
                    continue;
                }

                var tickDelta = EnvironmentEffectUtility.TickDelta(currentTick, runtime.LastUpdateTick);
                var timeFactor = tickDelta * fixedDeltaTime;
                runtime.LastUpdateTick = currentTick;
                runtimeBuffer[i] = runtime;

                switch (definition.Type)
                {
                    case EnvironmentEffectType.ScalarField:
                        if (definition.ParameterIndex >= scalarParams.Length)
                        {
                            break;
                        }

                        ref var scalarParameters = ref scalarParams[definition.ParameterIndex];
                        if (TryGetScalarChannelSlice(scalarDescriptorsBuffer, scalarContributionArray, definition.ChannelId, out var descriptor, out var slice))
                        {
                            ScheduleScalarJob(ref state, descriptor.Metadata, slice, scalarParameters, currentTick, timeFactor);

                            if (scalarMoisturePrepared && MatchesChannel(definition.ChannelId, moistureGrid.ValueRO.ChannelId))
                            {
                                updateMoisture = true;
                            }
                            else if (scalarTemperaturePrepared && MatchesChannel(definition.ChannelId, temperatureGrid.ValueRO.ChannelId))
                            {
                                updateTemperature = true;
                            }
                            else if (scalarBiomePrepared && MatchesChannel(definition.ChannelId, biomeGrid.ValueRO.ChannelId))
                            {
                                updateBiome = true;
                            }
                        }
                        break;

                    case EnvironmentEffectType.VectorField:
                        if (definition.ParameterIndex >= vectorParams.Length)
                        {
                            break;
                        }

                        ref var vectorParameters = ref vectorParams[definition.ParameterIndex];
                        if (TryGetVectorChannelSlice(vectorDescriptorsBuffer, vectorContributionArray, definition.ChannelId, out var vectorDescriptor, out var vectorSlice))
                        {
                            ScheduleVectorJob(ref state, vectorDescriptor.Metadata, vectorSlice, vectorParameters, currentTick, timeFactor);

                            if (vectorWindPrepared && MatchesChannel(definition.ChannelId, windGrid.ValueRO.ChannelId))
                            {
                                updateWind = true;
                            }
                            else if (vectorSunlightPrepared && MatchesChannel(definition.ChannelId, sunlightGrid.ValueRO.ChannelId))
                            {
                                updateSunlight = true;
                            }
                        }
                        break;

                    case EnvironmentEffectType.EventPulse:
                        if (definition.ParameterIndex >= pulseParams.Length)
                        {
                            break;
                        }

                        ref var pulse = ref pulseParams[definition.ParameterIndex];
                        EmitPulse(ref pulseBuffer, definition.EffectId, definition.ChannelId, currentTick, pulse);
                        break;
                }
            }

            state.Dependency.Complete();

            // Check terrain version and propagate to grids
            uint currentTerrainVersion = 0;
            if (SystemAPI.TryGetSingleton<PureDOTS.Environment.TerrainVersion>(out var terrainVersion))
            {
                currentTerrainVersion = terrainVersion.Value;
            }

            if (updateMoisture && scalarMoisturePrepared)
            {
                if (currentTerrainVersion != moistureGrid.ValueRO.LastTerrainVersion)
                {
                    moistureGrid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                }
                moistureGrid.ValueRW.LastUpdateTick = currentTick;
            }

            if (updateTemperature && scalarTemperaturePrepared)
            {
                if (currentTerrainVersion != temperatureGrid.ValueRO.LastTerrainVersion)
                {
                    temperatureGrid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                }
                temperatureGrid.ValueRW.LastUpdateTick = currentTick;
            }

            if (updateBiome && scalarBiomePrepared)
            {
                if (currentTerrainVersion != biomeGrid.ValueRO.LastTerrainVersion)
                {
                    biomeGrid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                }
                biomeGrid.ValueRW.LastUpdateTick = currentTick;
            }

            if (updateSunlight && vectorSunlightPrepared)
            {
                if (currentTerrainVersion != sunlightGrid.ValueRO.LastTerrainVersion)
                {
                    sunlightGrid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                }
                sunlightGrid.ValueRW.LastUpdateTick = currentTick;
            }

            if (updateWind && vectorWindPrepared)
            {
                if (currentTerrainVersion != windGrid.ValueRO.LastTerrainVersion)
                {
                    windGrid.ValueRW.LastTerrainVersion = currentTerrainVersion;
                }
                windGrid.ValueRW.LastUpdateTick = currentTick;
            }
        }

        static bool MatchesChannel(FixedString64Bytes request, FixedString64Bytes channel)
        {
            return request == channel;
        }

        static void ClearScalarContributions(DynamicBuffer<EnvironmentScalarChannelDescriptor> descriptors, NativeArray<float> contributions)
        {
            for (var i = 0; i < descriptors.Length; i++)
            {
                var descriptor = descriptors[i];
                if (descriptor.Length <= 0)
                {
                    continue;
                }

                var slice = contributions.GetSubArray(descriptor.Offset, descriptor.Length);
                for (var j = 0; j < slice.Length; j++)
                {
                    slice[j] = 0f;
                }
            }
        }

        static void ClearVectorContributions(DynamicBuffer<EnvironmentVectorChannelDescriptor> descriptors, NativeArray<float3> contributions)
        {
            for (var i = 0; i < descriptors.Length; i++)
            {
                var descriptor = descriptors[i];
                if (descriptor.Length <= 0)
                {
                    continue;
                }

                var slice = contributions.GetSubArray(descriptor.Offset, descriptor.Length);
                for (var j = 0; j < slice.Length; j++)
                {
                    slice[j] = float3.zero;
                }
            }
        }

        static bool TryGetScalarChannelSlice(DynamicBuffer<EnvironmentScalarChannelDescriptor> descriptors, NativeArray<float> contributions,
            FixedString64Bytes channelId, out EnvironmentScalarChannelDescriptor descriptor, out NativeArray<float> slice)
        {
            for (var i = 0; i < descriptors.Length; i++)
            {
                descriptor = descriptors[i];
                if (descriptor.ChannelId == channelId)
                {
                    slice = contributions.GetSubArray(descriptor.Offset, descriptor.Length);
                    return true;
                }
            }

            descriptor = default;
            slice = default;
            return false;
        }

        static bool TryGetVectorChannelSlice(DynamicBuffer<EnvironmentVectorChannelDescriptor> descriptors, NativeArray<float3> contributions,
            FixedString64Bytes channelId, out EnvironmentVectorChannelDescriptor descriptor, out NativeArray<float3> slice)
        {
            for (var i = 0; i < descriptors.Length; i++)
            {
                descriptor = descriptors[i];
                if (descriptor.ChannelId == channelId)
                {
                    slice = contributions.GetSubArray(descriptor.Offset, descriptor.Length);
                    return true;
                }
            }

            descriptor = default;
            slice = default;
            return false;
        }

        static void ScheduleScalarJob(ref SystemState state, EnvironmentGridMetadata metadata, NativeArray<float> contributions,
            in EnvironmentScalarEffectParameters parameters, uint currentTick, float timeSeconds)
        {
            if (!contributions.IsCreated || contributions.Length == 0)
            {
                return;
            }

            var job = new ScalarEffectJob
            {
                Contributions = contributions,
                Parameters = parameters,
                CurrentTick = currentTick,
                TimeSeconds = timeSeconds,
                Width = math.max(1, metadata.Resolution.x)
            };

            state.Dependency = job.Schedule(contributions.Length, 64, state.Dependency);
        }

        static void ScheduleVectorJob(ref SystemState state, EnvironmentGridMetadata metadata, NativeArray<float3> contributions,
            in EnvironmentVectorEffectParameters parameters, uint currentTick, float timeSeconds)
        {
            if (!contributions.IsCreated || contributions.Length == 0)
            {
                return;
            }

            var job = new VectorEffectJob
            {
                Contributions = contributions,
                Parameters = parameters,
                CurrentTick = currentTick,
                TimeSeconds = timeSeconds,
                Width = math.max(1, metadata.Resolution.x)
            };

            state.Dependency = job.Schedule(contributions.Length, 64, state.Dependency);
        }

        static void EmitPulse(ref DynamicBuffer<EnvironmentEventPulse> buffer, FixedString64Bytes effectId, FixedString64Bytes channelId,
            uint currentTick, in EnvironmentPulseEffectParameters parameters)
        {
            if (parameters.IntervalTicks == 0)
            {
                return;
            }

            if (currentTick % parameters.IntervalTicks != 0)
            {
                return;
            }

            buffer.Add(new EnvironmentEventPulse
            {
                EffectId = effectId,
                ChannelId = channelId,
                Intensity = parameters.Intensity,
                Tick = currentTick
            });
        }

        [BurstCompile]
        private struct ScalarEffectJob : IJobParallelFor
        {
            public NativeArray<float> Contributions;
            public EnvironmentScalarEffectParameters Parameters;
            public uint CurrentTick;
            public float TimeSeconds;
            public int Width;

            public void Execute(int index)
            {
                var cellX = index % Width;
                var phase = Parameters.NoiseOffset * (cellX + (index / Width));
                var oscillation = math.sin(TimeSeconds * Parameters.Frequency + phase);
                var damping = Parameters.Damping > 0f ? math.exp(-Parameters.Damping * TimeSeconds) : 1f;
                var value = Parameters.BaseOffset + Parameters.Amplitude * oscillation * damping;
                Contributions[index] += value;
            }
        }

        [BurstCompile]
        private struct VectorEffectJob : IJobParallelFor
        {
            public NativeArray<float3> Contributions;
            public EnvironmentVectorEffectParameters Parameters;
            public uint CurrentTick;
            public float TimeSeconds;
            public int Width;

            public void Execute(int index)
            {
                var cellX = index % Width;
                var cellY = index / Width;
                var phase = Parameters.NoiseOffset * (cellX + cellY);
                var wave = TimeSeconds * Parameters.Frequency + phase;
                var sinWave = math.sin(wave);
                var cosWave = math.cos(wave * 0.5f);
                var damping = Parameters.Damping > 0f ? math.exp(-Parameters.Damping * TimeSeconds) : 1f;

                var contribution = Parameters.BaseVector;
                contribution += new float3(
                    Parameters.Amplitude.x * sinWave,
                    Parameters.Amplitude.y * cosWave,
                    Parameters.Amplitude.z * sinWave);

                Contributions[index] += contribution * damping;
            }
        }
    }
}
