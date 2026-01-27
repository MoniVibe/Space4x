using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Power;
using PureDOTS.Runtime.Power.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Power
{
    /// <summary>
    /// Updates power source outputs based on environment conditions and wear.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PowerSystemGroup), OrderFirst = true)]
    public partial struct PowerSourceUpdateSystem : ISystem
    {
        private ComponentLookup<StarEnergyProfile> _starProfileLookup;
        private ComponentLookup<LocalSunExposure> _sunExposureLookup;
        private ComponentLookup<WindCell> _windCellLookup;
        private ComponentLookup<TerrainWindModifier> _terrainModifierLookup;
        private ComponentLookup<InfrastructureCondition> _conditionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _starProfileLookup = state.GetComponentLookup<StarEnergyProfile>(true);
            _sunExposureLookup = state.GetComponentLookup<LocalSunExposure>(true);
            _windCellLookup = state.GetComponentLookup<WindCell>(true);
            _terrainModifierLookup = state.GetComponentLookup<TerrainWindModifier>(true);
            _conditionLookup = state.GetComponentLookup<InfrastructureCondition>(true);

            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<PowerSourceDefRegistry>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<PowerSourceDefRegistry>(out var registry))
            {
                return;
            }

            var registryBlob = registry.Value;

            _starProfileLookup.Update(ref state);
            _sunExposureLookup.Update(ref state);
            _windCellLookup.Update(ref state);
            _terrainModifierLookup.Update(ref state);
            _conditionLookup.Update(ref state);

            var job = new PowerSourceUpdateJob
            {
                RegistryBlob = registryBlob,
                StarProfileLookup = _starProfileLookup,
                SunExposureLookup = _sunExposureLookup,
                WindCellLookup = _windCellLookup,
                TerrainModifierLookup = _terrainModifierLookup,
                ConditionLookup = _conditionLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }
    }

    [BurstCompile]
    [WithAll(typeof(PowerSourceState))]
    [WithNone(typeof(PlaybackGuardTag))]
    public partial struct PowerSourceUpdateJob : IJobEntity
    {
        [ReadOnly] public BlobAssetReference<PowerSourceDefRegistryBlob> RegistryBlob;
        [ReadOnly] public ComponentLookup<StarEnergyProfile> StarProfileLookup;
        [ReadOnly] public ComponentLookup<LocalSunExposure> SunExposureLookup;
        [ReadOnly] public ComponentLookup<WindCell> WindCellLookup;
        [ReadOnly] public ComponentLookup<TerrainWindModifier> TerrainModifierLookup;
        [ReadOnly] public ComponentLookup<InfrastructureCondition> ConditionLookup;

        [BurstCompile]
        private void Execute(Entity entity, ref PowerSourceState sourceState, in PowerNode node)
        {
            // Find source def
            var sourceDef = default(PowerSourceDefBlob);
            var found = false;
            for (int i = 0; i < RegistryBlob.Value.SourceDefs.Length; i++)
            {
                if (RegistryBlob.Value.SourceDefs[i].SourceDefId == sourceState.SourceDefId)
                {
                    sourceDef = RegistryBlob.Value.SourceDefs[i];
                    found = true;
                    break;
                }
            }

            if (!found || sourceState.Online == 0)
            {
                sourceState.CurrentOutput = 0;
                sourceState.MaxOutput = 0;
                return;
            }

            // Get condition/wear modifier
            var wearEfficiency = 1f - sourceState.Wear * 0.5f; // Max 50% reduction at full wear
            var conditionModifier = 1f;
            if (ConditionLookup.HasComponent(entity))
            {
                var condition = ConditionLookup[entity];
                if (condition.State == InfrastructureState.Degraded)
                    conditionModifier = 0.8f;
                else if (condition.State == InfrastructureState.Faulty)
                    conditionModifier = 0.5f;
            }

            // Compute MaxOutput based on source kind
            var maxOutput = ComputeMaxOutput(sourceDef, sourceState, entity, node);

            // Apply wear and condition
            maxOutput *= wearEfficiency * conditionModifier;

            sourceState.MaxOutput = maxOutput;
            // CurrentOutput will be set by flow solver, but initialize to MaxOutput for now
            if (sourceState.CurrentOutput == 0)
            {
                sourceState.CurrentOutput = maxOutput;
            }
        }

        [BurstCompile]
        private float ComputeMaxOutput(PowerSourceDefBlob def, PowerSourceState state, Entity entity, PowerNode node)
        {
            var baseOutput = def.RatedOutput * def.Efficiency;

            switch (def.Kind)
            {
                case PowerSourceKind.Solar:
                    return ComputeSolarOutput(def, baseOutput, entity, node);

                case PowerSourceKind.Wind:
                    return ComputeWindOutput(def, baseOutput, entity, node);

                case PowerSourceKind.Geothermal:
                    return ComputeGeothermalOutput(def, baseOutput);

                case PowerSourceKind.RTG:
                    return ComputeRTGOutput(def, baseOutput, state);

                case PowerSourceKind.FuelBurner:
                case PowerSourceKind.Reactor:
                    // Fuel availability checked elsewhere, assume available for now
                    return baseOutput;

                case PowerSourceKind.ZeroPoint:
                    return ComputeZeroPointOutput(def, baseOutput);

                case PowerSourceKind.Megastructure:
                    return ComputeMegastructureOutput(def, baseOutput);

                default:
                    return baseOutput;
            }
        }

        [BurstCompile]
        private float ComputeSolarOutput(PowerSourceDefBlob def, float baseOutput, Entity entity, PowerNode node)
        {
            // Find solar params
            var solarParams = default(SolarSourceParamsBlob);
            var found = false;
            for (int i = 0; i < RegistryBlob.Value.SolarParams.Length; i++)
            {
                if (RegistryBlob.Value.SolarParams[i].SourceDefId == def.SourceDefId)
                {
                    solarParams = RegistryBlob.Value.SolarParams[i];
                    found = true;
                    break;
                }
            }

            if (!found)
                return baseOutput;

            // Sample sun exposure
            var exposureFactor = 1f;
            if (SunExposureLookup.HasComponent(entity))
            {
                var exposure = SunExposureLookup[entity];
                exposureFactor = exposure.ExposureFactor;

                // Apply star luminosity if available
                if (SunExposureLookup.HasComponent(exposure.Star))
                {
                    var starProfile = StarProfileLookup[exposure.Star];
                    exposureFactor *= starProfile.Luminosity;
                    // Distance attenuation: inverse square law
                    var distanceFactor = 1f / (exposure.DistanceAU * exposure.DistanceAU);
                    exposureFactor *= distanceFactor;
                }
            }

            return baseOutput * exposureFactor * solarParams.PanelEfficiency * solarParams.TrackingFactor;
        }

        [BurstCompile]
        private float ComputeWindOutput(PowerSourceDefBlob def, float baseOutput, Entity entity, PowerNode node)
        {
            // Find wind params
            var windParams = default(WindSourceParamsBlob);
            var found = false;
            for (int i = 0; i < RegistryBlob.Value.WindParams.Length; i++)
            {
                if (RegistryBlob.Value.WindParams[i].SourceDefId == def.SourceDefId)
                {
                    windParams = RegistryBlob.Value.WindParams[i];
                    found = true;
                    break;
                }
            }

            if (!found)
                return baseOutput;

            // Sample wind cell
            var windIntensity = 0f;
            if (WindCellLookup.HasComponent(entity))
            {
                var windCell = WindCellLookup[entity];
                windIntensity = windCell.Intensity;

                // Apply terrain modifier
                if (TerrainModifierLookup.HasComponent(entity))
                {
                    var terrain = TerrainModifierLookup[entity];
                    windIntensity *= terrain.BaseModifier;
                }
            }

            // Wind power ∝ v^3, clamped between cut-in and cut-out
            if (windIntensity < windParams.CutInSpeed || windIntensity > windParams.CutOutSpeed)
                return 0f;

            var normalizedSpeed = math.clamp(windIntensity, windParams.CutInSpeed, windParams.RatedSpeed);
            var powerFactor = math.pow(normalizedSpeed / windParams.RatedSpeed, 3f);

            return baseOutput * powerFactor * windParams.TurbineEfficiency;
        }

        [BurstCompile]
        private float ComputeGeothermalOutput(PowerSourceDefBlob def, float baseOutput)
        {
            // Find geo params
            for (int i = 0; i < RegistryBlob.Value.GeothermalParams.Length; i++)
            {
                if (RegistryBlob.Value.GeothermalParams[i].SourceDefId == def.SourceDefId)
                {
                    var geoParams = RegistryBlob.Value.GeothermalParams[i];
                    return baseOutput * geoParams.SiteGrade;
                }
            }

            return baseOutput;
        }

        [BurstCompile]
        private float ComputeRTGOutput(PowerSourceDefBlob def, float baseOutput, PowerSourceState state)
        {
            // Find RTG params
            for (int i = 0; i < RegistryBlob.Value.RTGParams.Length; i++)
            {
                if (RegistryBlob.Value.RTGParams[i].SourceDefId == def.SourceDefId)
                {
                    var rtgParams = RegistryBlob.Value.RTGParams[i];
                    // Exponential decay: output = initial * 2^(-t/halfLife)
                    // Approximate using wear as time proxy (0 = new, 1 = fully decayed)
                    var decayFactor = math.pow(2f, -state.Wear * rtgParams.HalfLifeTicks / rtgParams.HalfLifeTicks);
                    return rtgParams.InitialOutput * decayFactor;
                }
            }

            return baseOutput;
        }

        [BurstCompile]
        private float ComputeZeroPointOutput(PowerSourceDefBlob def, float baseOutput)
        {
            // Find zero-point params
            for (int i = 0; i < RegistryBlob.Value.ZeroPointParams.Length; i++)
            {
                if (RegistryBlob.Value.ZeroPointParams[i].SourceDefId == def.SourceDefId)
                {
                    var zpParams = RegistryBlob.Value.ZeroPointParams[i];
                    // Stability affects output variance, but mostly static
                    return baseOutput * zpParams.Stability;
                }
            }

            return baseOutput;
        }

        [BurstCompile]
        private float ComputeMegastructureOutput(PowerSourceDefBlob def, float baseOutput)
        {
            // Find megastructure params
            for (int i = 0; i < RegistryBlob.Value.MegastructureParams.Length; i++)
            {
                if (RegistryBlob.Value.MegastructureParams[i].SourceDefId == def.SourceDefId)
                {
                    var megaParams = RegistryBlob.Value.MegastructureParams[i];
                    return baseOutput * megaParams.CaptureEfficiency * megaParams.Coverage;
                }
            }

            return baseOutput;
        }
    }
}

