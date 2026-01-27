using PureDOTS.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Environment
{
    public readonly struct EnvironmentScalarSample
    {
        public readonly float Base;
        public readonly float Contribution;

        public EnvironmentScalarSample(float baseValue, float contribution)
        {
            Base = baseValue;
            Contribution = contribution;
        }

        public float Value => Base + Contribution;
    }

    public readonly struct EnvironmentSunlightSample
    {
        public readonly SunlightSample Base;
        public readonly SunlightSample Contribution;

        public EnvironmentSunlightSample(SunlightSample baseSample, SunlightSample contribution)
        {
            Base = baseSample;
            Contribution = contribution;
        }

        public SunlightSample Value
        {
            get
            {
                var occluder = math.clamp((int)Base.OccluderCount + Contribution.OccluderCount, 0, ushort.MaxValue);
                return new SunlightSample
                {
                    DirectLight = Base.DirectLight + Contribution.DirectLight,
                    AmbientLight = Base.AmbientLight + Contribution.AmbientLight,
                    OccluderCount = (ushort)occluder
                };
            }
        }
    }

    public readonly struct EnvironmentWindSample
    {
        public readonly WindSample Base;
        public readonly float2 DirectionContribution;
        public readonly float StrengthContribution;

        public EnvironmentWindSample(WindSample baseSample, float2 directionContribution, float strengthContribution)
        {
            Base = baseSample;
            DirectionContribution = directionContribution;
            StrengthContribution = strengthContribution;
        }

        public WindSample Value
        {
            get
            {
                var direction = Base.Direction + DirectionContribution;
                if (math.lengthsq(direction) > 1e-6f)
                {
                    direction = math.normalize(direction);
                }
                else
                {
                    direction = Base.Direction;
                }

                var strength = math.max(0f, Base.Strength + StrengthContribution);
                return new WindSample
                {
                    Direction = direction,
                    Strength = strength
                };
            }
        }
    }

    /// <summary>
    /// Convenience helpers for sampling shared environment state from systems without
    /// needing to duplicate singleton lookup boilerplate.
    /// </summary>
    public struct EnvironmentSampler
    {
        private EntityManager _entityManager;

        private EntityQuery _moistureGridQuery;
        private EntityQuery _temperatureGridQuery;
        private EntityQuery _sunlightGridQuery;
        private EntityQuery _windFieldQuery;
        private EntityQuery _biomeGridQuery;
        private EntityQuery _catalogQuery;
        private EntityQuery _climateQuery;

        private ComponentLookup<MoistureGrid> _moistureGridLookup;
        private BufferLookup<MoistureGridRuntimeCell> _moistureRuntimeLookup;
        private ComponentLookup<TemperatureGrid> _temperatureGridLookup;
        private ComponentLookup<SunlightGrid> _sunlightGridLookup;
        private BufferLookup<SunlightGridRuntimeSample> _sunlightRuntimeLookup;
        private ComponentLookup<WindField> _windFieldLookup;
        private ComponentLookup<BiomeGrid> _biomeGridLookup;
        private ComponentLookup<ClimateState> _climateStateLookup;

        private BufferLookup<EnvironmentScalarChannelDescriptor> _scalarDescriptorLookup;
        private BufferLookup<EnvironmentScalarContribution> _scalarContributionLookup;
        private BufferLookup<EnvironmentVectorChannelDescriptor> _vectorDescriptorLookup;
        private BufferLookup<EnvironmentVectorContribution> _vectorContributionLookup;

        public EnvironmentSampler(ref SystemState state)
        {
            _entityManager = state.EntityManager;

            _moistureGridLookup = state.GetComponentLookup<MoistureGrid>(true);
            _moistureRuntimeLookup = state.GetBufferLookup<MoistureGridRuntimeCell>(true);
            _temperatureGridLookup = state.GetComponentLookup<TemperatureGrid>(true);
            _sunlightGridLookup = state.GetComponentLookup<SunlightGrid>(true);
            _sunlightRuntimeLookup = state.GetBufferLookup<SunlightGridRuntimeSample>(true);
            _windFieldLookup = state.GetComponentLookup<WindField>(true);
            _biomeGridLookup = state.GetComponentLookup<BiomeGrid>(true);
            _climateStateLookup = state.GetComponentLookup<ClimateState>(true);

            _scalarDescriptorLookup = state.GetBufferLookup<EnvironmentScalarChannelDescriptor>(true);
            _scalarContributionLookup = state.GetBufferLookup<EnvironmentScalarContribution>(true);
            _vectorDescriptorLookup = state.GetBufferLookup<EnvironmentVectorChannelDescriptor>(true);
            _vectorContributionLookup = state.GetBufferLookup<EnvironmentVectorContribution>(true);

            _moistureGridQuery = state.GetEntityQuery(ComponentType.ReadOnly<MoistureGrid>());
            _temperatureGridQuery = state.GetEntityQuery(ComponentType.ReadOnly<TemperatureGrid>());
            _sunlightGridQuery = state.GetEntityQuery(ComponentType.ReadOnly<SunlightGrid>());
            _windFieldQuery = state.GetEntityQuery(ComponentType.ReadOnly<WindField>());
            _biomeGridQuery = state.GetEntityQuery(ComponentType.ReadOnly<BiomeGrid>());
            _catalogQuery = state.GetEntityQuery(ComponentType.ReadOnly<EnvironmentEffectCatalogData>());
            _climateQuery = state.GetEntityQuery(ComponentType.ReadOnly<ClimateState>());

            Update(ref state);
        }

        public void Update(ref SystemState state)
        {
            _entityManager = state.EntityManager;

            _moistureGridLookup.Update(ref state);
            _moistureRuntimeLookup.Update(ref state);
            _temperatureGridLookup.Update(ref state);
            _sunlightGridLookup.Update(ref state);
            _sunlightRuntimeLookup.Update(ref state);
            _windFieldLookup.Update(ref state);
            _biomeGridLookup.Update(ref state);
            _climateStateLookup.Update(ref state);

            _scalarDescriptorLookup.Update(ref state);
            _scalarContributionLookup.Update(ref state);
            _vectorDescriptorLookup.Update(ref state);
            _vectorContributionLookup.Update(ref state);
        }

        public EnvironmentScalarSample SampleMoistureDetailed(float3 worldPosition, float defaultValue = 0f)
        {
            if (!TryGetSingletonEntity(_moistureGridQuery, out var gridEntity))
            {
                return new EnvironmentScalarSample(defaultValue, 0f);
            }

            var grid = _moistureGridLookup[gridEntity];

            float baseValue;
            if (_moistureRuntimeLookup.HasBuffer(gridEntity))
            {
                var runtime = _moistureRuntimeLookup[gridEntity].AsNativeArray();
                baseValue = EnvironmentGridMath.SampleBilinear(grid.Metadata, runtime, worldPosition, defaultValue);
            }
            else
            {
                baseValue = grid.SampleBilinear(worldPosition, defaultValue);
            }

            var contribution = SampleScalarContribution(grid.ChannelId, worldPosition, 0f);
            return new EnvironmentScalarSample(baseValue, contribution);
        }

        public float SampleMoisture(float3 worldPosition, float defaultValue = 0f)
        {
            return SampleMoistureDetailed(worldPosition, defaultValue).Value;
        }

        public EnvironmentScalarSample SampleTemperatureDetailed(float3 worldPosition, float defaultValue = 0f)
        {
            if (!TryGetSingletonEntity(_temperatureGridQuery, out var gridEntity))
            {
                return new EnvironmentScalarSample(defaultValue, 0f);
            }

            var grid = _temperatureGridLookup[gridEntity];
            var baseValue = grid.SampleBilinear(worldPosition, defaultValue);
            var contribution = SampleScalarContribution(grid.ChannelId, worldPosition, 0f);
            return new EnvironmentScalarSample(baseValue, contribution);
        }

        public float SampleTemperature(float3 worldPosition, float defaultValue = 0f)
        {
            return SampleTemperatureDetailed(worldPosition, defaultValue).Value;
        }

        public EnvironmentSunlightSample SampleSunlightDetailed(float3 worldPosition, SunlightSample defaultValue = default)
        {
            if (!TryGetSingletonEntity(_sunlightGridQuery, out var gridEntity))
            {
                return new EnvironmentSunlightSample(defaultValue, default);
            }

            var grid = _sunlightGridLookup[gridEntity];
            SunlightSample baseSample;
            if (_sunlightRuntimeLookup.HasBuffer(gridEntity))
            {
                var runtime = _sunlightRuntimeLookup[gridEntity];
                var runtimeSamples = runtime.Reinterpret<SunlightSample>().AsNativeArray();
                baseSample = EnvironmentGridMath.SampleBilinear(grid.Metadata, runtimeSamples, worldPosition, defaultValue);
            }
            else
            {
                baseSample = grid.SampleBilinear(worldPosition, defaultValue);
            }

            var vectorContribution = SampleVectorContribution(grid.ChannelId, worldPosition);
            var contributionSample = new SunlightSample
            {
                DirectLight = vectorContribution.x,
                AmbientLight = vectorContribution.y,
                OccluderCount = (ushort)math.clamp(math.round(vectorContribution.z), 0f, ushort.MaxValue)
            };

            return new EnvironmentSunlightSample(baseSample, contributionSample);
        }

        public SunlightSample SampleSunlight(float3 worldPosition, SunlightSample defaultValue = default)
        {
            return SampleSunlightDetailed(worldPosition, defaultValue).Value;
        }

        public EnvironmentWindSample SampleWindDetailed(float3 worldPosition, WindSample defaultValue = default)
        {
            if (!TryGetSingletonEntity(_windFieldQuery, out var gridEntity))
            {
                return new EnvironmentWindSample(defaultValue, float2.zero, 0f);
            }

            var grid = _windFieldLookup[gridEntity];
            var baseSample = grid.SampleBilinear(worldPosition, defaultValue);
            var vectorContribution = SampleVectorContribution(grid.ChannelId, worldPosition);
            var directionContribution = new float2(vectorContribution.x, vectorContribution.y);
            var strengthContribution = vectorContribution.z;

            return new EnvironmentWindSample(baseSample, directionContribution, strengthContribution);
        }

        public WindSample SampleWind(float3 worldPosition, WindSample defaultValue = default)
        {
            return SampleWindDetailed(worldPosition, defaultValue).Value;
        }

        public BiomeType SampleBiome(float3 worldPosition, BiomeType defaultValue = BiomeType.Unknown)
        {
            if (!TryGetSingletonEntity(_biomeGridQuery, out var gridEntity))
            {
                return defaultValue;
            }

            var grid = _biomeGridLookup[gridEntity];
            return grid.SampleNearest(worldPosition, defaultValue);
        }

        public bool TryGetClimateState(out PureDOTS.Environment.ClimateState climateState)
        {
            if (!TryGetSingletonEntity(_climateQuery, out var climateEntity))
            {
                climateState = default;
                return false;
            }

            climateState = _climateStateLookup[climateEntity];
            return true;
        }

        public PureDOTS.Environment.ClimateState GetClimateStateOrDefault()
        {
            return TryGetClimateState(out var climate) ? climate : default;
        }

        private bool TryGetSingletonEntity(EntityQuery query, out Entity entity)
        {
            if (query.IsEmptyIgnoreFilter)
            {
                entity = Entity.Null;
                return false;
            }

            entity = query.GetSingletonEntity();
            return entity != Entity.Null;
        }

        private float SampleScalarContribution(FixedString64Bytes channelId, float3 worldPosition, float defaultValue)
        {
            if (channelId.Length == 0)
            {
                return defaultValue;
            }

            if (!TryGetSingletonEntity(_catalogQuery, out var catalogEntity))
            {
                return defaultValue;
            }

            if (!_scalarDescriptorLookup.HasBuffer(catalogEntity) || !_scalarContributionLookup.HasBuffer(catalogEntity))
            {
                return defaultValue;
            }

            var descriptors = _scalarDescriptorLookup[catalogEntity];
            var contributions = _scalarContributionLookup[catalogEntity].Reinterpret<float>().AsNativeArray();

            for (var i = 0; i < descriptors.Length; i++)
            {
                var descriptor = descriptors[i];
                if (descriptor.ChannelId != channelId)
                {
                    continue;
                }

                var slice = contributions.GetSubArray(descriptor.Offset, descriptor.Length);
                return EnvironmentGridMath.SampleBilinear(descriptor.Metadata, slice, worldPosition, defaultValue);
            }

            return defaultValue;
        }

        private float3 SampleVectorContribution(FixedString64Bytes channelId, float3 worldPosition)
        {
            if (channelId.Length == 0)
            {
                return float3.zero;
            }

            if (!TryGetSingletonEntity(_catalogQuery, out var catalogEntity))
            {
                return float3.zero;
            }

            if (!_vectorDescriptorLookup.HasBuffer(catalogEntity) || !_vectorContributionLookup.HasBuffer(catalogEntity))
            {
                return float3.zero;
            }

            var descriptors = _vectorDescriptorLookup[catalogEntity];
            var contributions = _vectorContributionLookup[catalogEntity].Reinterpret<float3>().AsNativeArray();

            for (var i = 0; i < descriptors.Length; i++)
            {
                var descriptor = descriptors[i];
                if (descriptor.ChannelId != channelId)
                {
                    continue;
                }

                var slice = contributions.GetSubArray(descriptor.Offset, descriptor.Length);
                return EnvironmentGridMath.SampleBilinearVector(descriptor.Metadata, slice, worldPosition, float3.zero);
            }

            return float3.zero;
        }
    }

    public static class EnvironmentSampling
    {
        public static EnvironmentSampler CreateSampler(ref SystemState state)
        {
            return new EnvironmentSampler(ref state);
        }
    }
}

