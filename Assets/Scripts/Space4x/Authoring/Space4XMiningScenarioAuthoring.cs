using System;
using PureDOTS.Runtime.Agency;
using PureDOTS.Authoring;
using PureDOTS.Environment;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Platform;
using PureDOTS.Runtime.Swarms;
using Space4X.Presentation;
using Space4X.Runtime;
using PureDOTS.Runtime.Interrupts;
using Space4X.Runtime.Breakables;
using MiningPrimitive = Space4X.Presentation.Space4XMiningPrimitive;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using RenderKeys = Space4X.Presentation.Space4XRenderKeys;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    /// <summary>
    /// Authoring component for creating carriers, mining vessels, and asteroids in the mining scenario scene.
    /// </summary>
    [DisallowMultipleComponent]
    //[RequireComponent(typeof(PureDotsConfigAuthoring))]
    //[RequireComponent(typeof(SpatialPartitionAuthoring))]
    [MovedFrom(true, "Space4X.Registry", null, "Space4XMiningDemoAuthoring")]
    public sealed class Space4XMiningScenarioAuthoring : MonoBehaviour
    {
        [SerializeField]
        private CarrierDefinition[] carriers = new CarrierDefinition[]
        {
            new CarrierDefinition
            {
                CarrierId = "CARRIER-1",
                Speed = 5f,
                PatrolCenter = new float3(0f, 0f, -8f),
                PatrolRadius = 50f,
                WaitTime = 2f,
                Position = new float3(0f, 0f, -8f),
                Disposition = EntityDispositionFlags.None,
                Alignment = AlignmentDefinition.CreateNeutral(),
                RaceId = 0,
                CultureId = 0,
                AffiliationId = "AFFILIATION-1"
            }
        };

        [SerializeField]
        private MiningVesselDefinition[] miningVessels = new MiningVesselDefinition[]
        {
            new MiningVesselDefinition
            {
                VesselId = "MINER-1",
                Speed = 10f,
                MiningEfficiency = 0.8f,
                CargoCapacity = 100f,
                MiningTickInterval = 0.5f,
                OutputSpawnThreshold = 20f,
                ResourceId = "space4x.resource.minerals",
                Position = new float3(5f, 0f, -2f),
                CarrierId = "CARRIER-1",
                ToolKind = TerrainModificationToolKind.Drill,
                ToolShapeOverride = true,
                ToolShape = TerrainModificationShape.Tunnel,
                ToolRadiusOverride = 0.9f,
                ToolStepLengthOverride = 0.35f,
                ToolDigUnitsPerMeterOverride = 18f,
                Disposition = EntityDispositionFlags.None,
                Alignment = AlignmentDefinition.CreateNeutral(),
                RaceId = 0,
                CultureId = 0,
                AffiliationId = "AFFILIATION-1",
                PilotAlignment = AlignmentDefinition.CreateNeutral(),
                PilotRaceId = 0,
                PilotCultureId = 0
            },
            new MiningVesselDefinition
            {
                VesselId = "MINER-2",
                Speed = 10f,
                MiningEfficiency = 0.8f,
                CargoCapacity = 100f,
                MiningTickInterval = 0.5f,
                OutputSpawnThreshold = 20f,
                ResourceId = "space4x.resource.minerals",
                Position = new float3(-5f, 0f, -14f),
                CarrierId = "CARRIER-1",
                ToolKind = TerrainModificationToolKind.Laser,
                ToolShapeOverride = true,
                ToolShape = TerrainModificationShape.Tunnel,
                ToolRadiusOverride = 1.4f,
                ToolStepLengthOverride = 4.5f,
                ToolYieldMultiplier = 0.55f,
                ToolHeatDeltaMultiplier = 1.35f,
                ToolInstabilityDeltaMultiplier = 1.2f,
                Disposition = EntityDispositionFlags.None,
                Alignment = AlignmentDefinition.CreateNeutral(),
                RaceId = 0,
                CultureId = 0,
                AffiliationId = "AFFILIATION-1",
                PilotAlignment = AlignmentDefinition.CreateNeutral(),
                PilotRaceId = 0,
                PilotCultureId = 0
            },
            new MiningVesselDefinition
            {
                VesselId = "MINER-3",
                Speed = 10f,
                MiningEfficiency = 0.8f,
                CargoCapacity = 100f,
                MiningTickInterval = 0.5f,
                OutputSpawnThreshold = 20f,
                ResourceId = "space4x.resource.minerals",
                Position = new float3(12f, 0f, -18f),
                CarrierId = "CARRIER-1",
                ToolKind = TerrainModificationToolKind.Microwave,
                ToolShapeOverride = true,
                ToolShape = TerrainModificationShape.Brush,
                ToolRadiusOverride = 2.6f,
                ToolDamageDeltaOverride = 6,
                ToolDamageThresholdOverride = 220,
                ToolYieldMultiplier = 0.35f,
                ToolHeatDeltaMultiplier = 1.4f,
                Disposition = EntityDispositionFlags.None,
                Alignment = AlignmentDefinition.CreateNeutral(),
                RaceId = 0,
                CultureId = 0,
                AffiliationId = "AFFILIATION-1",
                PilotAlignment = AlignmentDefinition.CreateNeutral(),
                PilotRaceId = 0,
                PilotCultureId = 0
            },
            new MiningVesselDefinition
            {
                VesselId = "MINER-4",
                Speed = 9f,
                MiningEfficiency = 0.75f,
                CargoCapacity = 120f,
                MiningTickInterval = 0.6f,
                OutputSpawnThreshold = 25f,
                ResourceId = "space4x.resource.minerals",
                Position = new float3(-12f, 0f, -6f),
                CarrierId = "CARRIER-1",
                ToolKind = TerrainModificationToolKind.Drill,
                ToolShapeOverride = true,
                ToolShape = TerrainModificationShape.Brush,
                ToolRadiusOverride = 3.2f,
                ToolStepLengthOverride = 1.1f,
                ToolYieldMultiplier = 0.9f,
                ToolHeatDeltaMultiplier = 0.8f,
                Disposition = EntityDispositionFlags.None,
                Alignment = AlignmentDefinition.CreateNeutral(),
                RaceId = 0,
                CultureId = 0,
                AffiliationId = "AFFILIATION-1",
                PilotAlignment = AlignmentDefinition.CreateNeutral(),
                PilotRaceId = 0,
                PilotCultureId = 0
            }
        };

        [SerializeField]
        private AffiliationDefinition[] affiliations = new AffiliationDefinition[]
        {
            new AffiliationDefinition
            {
                AffiliationId = "AFFILIATION-1",
                DisplayName = "Starter Fleet",
                Loyalty = 1f
            }
        };

        [SerializeField]
        private AsteroidDefinition[] asteroids = new AsteroidDefinition[]
        {
            new AsteroidDefinition
            {
                AsteroidId = "ASTEROID-1",
                ResourceType = ResourceType.Minerals,
                ResourceAmount = 500f,
                MaxResourceAmount = 500f,
                MiningRate = 10f,
                VolumeRadius = 20f,
                CoreRadiusRatio = 0.3f,
                MantleRadiusRatio = 0.7f,
                CrustMaterialId = 1,
                MantleMaterialId = 2,
                CoreMaterialId = 3,
                CoreDepositId = 1,
                CoreOreGrade = 200,
                OreGradeExponent = 2f,
                VolumeSeed = 1,
                Position = new float3(20f, 0f, 10f)
            },
            new AsteroidDefinition
            {
                AsteroidId = "ASTEROID-2",
                ResourceType = ResourceType.Minerals,
                ResourceAmount = 500f,
                MaxResourceAmount = 500f,
                MiningRate = 10f,
                VolumeRadius = 20f,
                CoreRadiusRatio = 0.3f,
                MantleRadiusRatio = 0.7f,
                CrustMaterialId = 1,
                MantleMaterialId = 2,
                CoreMaterialId = 3,
                CoreDepositId = 1,
                CoreOreGrade = 200,
                OreGradeExponent = 2f,
                VolumeSeed = 2,
                Position = new float3(-20f, 0f, -20f)
            }
        };

        [SerializeField]
        private MiningVisualSettings visuals = MiningVisualSettings.CreateDefault();

        [Header("Mining Latch Tuning")]
        [SerializeField]
        private MiningLatchSettings latchSettings = MiningLatchSettings.CreateDefault();

        public CarrierDefinition[] Carriers => carriers;
        public MiningVesselDefinition[] MiningVessels => miningVessels;
        public AsteroidDefinition[] Asteroids => asteroids;
        public MiningVisualSettings Visuals => visuals;
        public MiningLatchSettings LatchSettings => latchSettings;
        public AffiliationDefinition[] Affiliations => affiliations;

        [Serializable]
        public struct CarrierDefinition
        {
            [Tooltip("Unique identifier for this carrier")]
            public string CarrierId;
            
            [Tooltip("Movement speed of the carrier")]
            [Min(0.1f)]
            public float Speed;
            
            [Tooltip("Center point of the patrol area")]
            public float3 PatrolCenter;
            
            [Tooltip("Radius of the patrol area")]
            [Min(1f)]
            public float PatrolRadius;
            
            [Tooltip("How long to wait at each waypoint (seconds)")]
            [Min(0f)]
            public float WaitTime;
            
            [Tooltip("Starting position of the carrier")]
            public float3 Position;

            [Header("Combat Configuration")]
            [Tooltip("If true, this carrier is hostile and will engage enemies")]
            public bool IsHostile;
            
            [Tooltip("Fleet posture for combat systems")]
            public Space4XFleetPosture FleetPosture;
            
            [Tooltip("Fleet ID for registry (auto-generated if empty)")]
            public string FleetId;
            
            [Tooltip("If true, carrier can intercept other fleets")]
            public bool CanIntercept;
            
            [Tooltip("Maximum intercept speed")]
            [Min(0.1f)]
            public float InterceptSpeed;

            [Header("Disposition")]
            [Tooltip("Optional explicit disposition flags; leave None to auto-classify.")]
            public EntityDispositionFlags Disposition;

            [Header("Alignment / Affiliation")]
            public AlignmentDefinition Alignment;
            public ushort RaceId;
            public ushort CultureId;
            public string AffiliationId;
        }

        [Serializable]
        public struct MiningVesselDefinition
        {
            [Tooltip("Unique identifier for this mining vessel")]
            public string VesselId;
            
            [Tooltip("Movement speed of the vessel")]
            [Min(0.1f)]
            public float Speed;
            
            [Tooltip("Mining efficiency multiplier (0-1)")]
            [Range(0f, 1f)]
            public float MiningEfficiency;

            [Tooltip("Registry resource identifier to mine (falls back to the vessel cargo type)")]
            public string ResourceId;

            [Tooltip("Seconds between mining ticks when actively mining")]
            [Min(0.05f)]
            public float MiningTickInterval;
            
            [Tooltip("Maximum cargo capacity")]
            [Min(1f)]
            public float CargoCapacity;

            [Tooltip("Accumulated mined units before toggling spawn-ready output")]
            [Min(0f)]
            public float OutputSpawnThreshold;
            
            [Tooltip("Starting position of the vessel (relative to carrier if docked)")]
            public float3 Position;
            
            [Tooltip("Carrier ID that this vessel belongs to (must match a CarrierId)")]
            public string CarrierId;
            
            [Tooltip("If true, vessel starts docked at carrier position")]
            public bool StartDocked;

            [Header("Mining Tool")]
            public TerrainModificationToolKind ToolKind;
            [Tooltip("If true, override the default tool shape for this miner.")]
            public bool ToolShapeOverride;
            public TerrainModificationShape ToolShape;
            [Tooltip("Override tool radius (<=0 uses config).")]
            public float ToolRadiusOverride;
            [Tooltip("Multiply base tool radius (<=0 uses 1).")]
            public float ToolRadiusMultiplier;
            [Tooltip("Override step length (<=0 uses config).")]
            public float ToolStepLengthOverride;
            [Tooltip("Multiply base step length (<=0 uses 1).")]
            public float ToolStepLengthMultiplier;
            [Tooltip("Override dig units per meter (<=0 uses config).")]
            public float ToolDigUnitsPerMeterOverride;
            [Tooltip("Override minimum step length (<=0 uses config).")]
            public float ToolMinStepLengthOverride;
            [Tooltip("Override maximum step length (<=0 uses config).")]
            public float ToolMaxStepLengthOverride;
            [Tooltip("Multiply tool yield (<=0 uses 1).")]
            public float ToolYieldMultiplier;
            [Tooltip("Multiply tool heat delta (<=0 uses 1).")]
            public float ToolHeatDeltaMultiplier;
            [Tooltip("Multiply tool instability delta (<=0 uses 1).")]
            public float ToolInstabilityDeltaMultiplier;
            [Tooltip("Override microwave damage delta (<=0 uses config).")]
            public int ToolDamageDeltaOverride;
            [Tooltip("Override microwave damage threshold (<=0 uses config).")]
            public int ToolDamageThresholdOverride;

            [Header("Disposition")]
            [Tooltip("Optional explicit disposition flags; leave None to auto-classify.")]
            public EntityDispositionFlags Disposition;

            [Header("Alignment / Affiliation")]
            public AlignmentDefinition Alignment;
            public ushort RaceId;
            public ushort CultureId;
            public string AffiliationId;

            [Header("Pilot Profile")]
            public AlignmentDefinition PilotAlignment;
            public ushort PilotRaceId;
            public ushort PilotCultureId;

            [Header("Pilot Disposition")]
            [Tooltip("If true, uses the explicit pilot disposition values instead of seeding.")]
            public bool OverridePilotDisposition;
            public BehaviorDispositionDefinition PilotDisposition;
        }

        [Serializable]
        public struct BehaviorDispositionDefinition
        {
            [Range(0f, 1f)] public float Compliance;
            [Range(0f, 1f)] public float Caution;
            [Range(0f, 1f)] public float FormationAdherence;
            [Range(0f, 1f)] public float RiskTolerance;
            [Range(0f, 1f)] public float Aggression;
            [Range(0f, 1f)] public float Patience;
        }

        [Serializable]
        public struct AsteroidDefinition
        {
            [Tooltip("Unique identifier for this asteroid")]
            public string AsteroidId;
            
            [Tooltip("Type of resource in this asteroid")]
            public ResourceType ResourceType;
            
            [Tooltip("Current resource amount")]
            [Min(0f)]
            public float ResourceAmount;
            
            [Tooltip("Maximum resource amount (used for regeneration if needed)")]
            [Min(0f)]
            public float MaxResourceAmount;
            
            [Tooltip("Rate at which resources can be mined per second")]
            [Min(0.1f)]
            public float MiningRate;

            [Header("Volume")]
            [Min(0.1f)] public float VolumeRadius;
            [Range(0f, 1f)] public float CoreRadiusRatio;
            [Range(0f, 1f)] public float MantleRadiusRatio;
            [Range(0, 255)] public int CrustMaterialId;
            [Range(0, 255)] public int MantleMaterialId;
            [Range(0, 255)] public int CoreMaterialId;
            [Range(0, 255)] public int CoreDepositId;
            [Range(0, 255)] public int CoreOreGrade;
            [Min(0.1f)] public float OreGradeExponent;
            [Min(0)] public int VolumeSeed;
            
            [Tooltip("Position of the asteroid")]
            public float3 Position;
        }

        [Serializable]
        public struct AlignmentDefinition
        {
            [Range(-1f, 1f)] public float Law;
            [Range(-1f, 1f)] public float Good;
            [Range(-1f, 1f)] public float Integrity;
            public EthicAxisDefinition[] Axes;
            public OutlookDefinition[] Outlooks;

            public static AlignmentDefinition CreateNeutral()
            {
                return new AlignmentDefinition
                {
                    Law = 0f,
                    Good = 0f,
                    Integrity = 0f,
                    Axes = Array.Empty<EthicAxisDefinition>(),
                    Outlooks = Array.Empty<OutlookDefinition>()
                };
            }
        }

        [Serializable]
        public struct EthicAxisDefinition
        {
            public EthicAxisId Axis;
            [Range(-2f, 2f)] public float Value;
        }

        [Serializable]
        public struct OutlookDefinition
        {
            public OutlookId OutlookId;
            [Range(-1f, 1f)] public float Weight;
        }

        [Serializable]
        public struct AffiliationDefinition
        {
            [Tooltip("Stable identifier for this affiliation.")]
            public string AffiliationId;
            [Tooltip("Display name used by debug/integration surfaces.")]
            public string DisplayName;
            [Range(0f, 1f)] public float Loyalty;
        }

        [Serializable]
        public struct MiningVisualSettings
        {
            public MiningPrimitive CarrierPrimitive;
            [Min(0.05f)] public float CarrierScale;
            public Color CarrierColor;
            public string CarrierDescriptorKey;

            public MiningPrimitive MiningVesselPrimitive;
            [Min(0.05f)] public float MiningVesselScale;
            public Color MiningVesselColor;
            public string MiningVesselDescriptorKey;

            public MiningPrimitive AsteroidPrimitive;
            [Min(0.05f)] public float AsteroidScale;
            public Color AsteroidColor;
            public string AsteroidDescriptorKey;
            [Tooltip("Draw attack-move debug lines in the scene view/game view.")]
            public bool EnableAttackMoveDebugLines;
            [Tooltip("Disable Y depth offsets for debugging movement.")]
            public bool DisableDepthOffset;

            public static MiningVisualSettings CreateDefault()
            {
                return new MiningVisualSettings
                {
                    CarrierPrimitive = MiningPrimitive.Capsule,
                    CarrierScale = 0.5f,
                    CarrierColor = new Color(0.35f, 0.4f, 0.62f, 1f),
                    CarrierDescriptorKey = "space4x.vessel.carrier",
                    MiningVesselPrimitive = MiningPrimitive.Cylinder,
                    MiningVesselScale = 0.02f,
                    MiningVesselColor = new Color(0.25f, 0.52f, 0.84f, 1f),
                    MiningVesselDescriptorKey = "space4x.vessel.miner",
                    AsteroidPrimitive = MiningPrimitive.Sphere,
                    AsteroidScale = 20f,
                    AsteroidColor = new Color(0.52f, 0.43f, 0.34f, 1f),
                    AsteroidDescriptorKey = "space4x.prop.asteroid",
                    EnableAttackMoveDebugLines = true,
                    DisableDepthOffset = false
                };
            }
        }

        [Serializable]
        public struct MiningLatchSettings
        {
            [Min(1)] public int RegionCount;
            [Min(0.05f)] public float SurfaceEpsilon;
            [Range(0f, 1f)] public float AlignDotThreshold;
            [Min(0)] public int SettleTicks;
            [Tooltip("Reserve latch regions while approaching to avoid multiple miners stacking.")]
            public bool ReserveRegionWhileApproaching;
            [Min(1)] public int TelemetrySampleEveryTicks;
            [Min(0)] public int TelemetryMaxSamples;

            public static MiningLatchSettings CreateDefault()
            {
                var defaults = Space4XMiningLatchConfig.Default;
                return new MiningLatchSettings
                {
                    RegionCount = math.max(1, defaults.RegionCount),
                    SurfaceEpsilon = math.max(0.05f, defaults.SurfaceEpsilon),
                    AlignDotThreshold = math.clamp(defaults.AlignDotThreshold, 0f, 1f),
                    SettleTicks = (int)math.max(0u, defaults.SettleTicks),
                    ReserveRegionWhileApproaching = defaults.ReserveRegionWhileApproaching != 0,
                    TelemetrySampleEveryTicks = (int)math.max(1u, defaults.TelemetrySampleEveryTicks),
                    TelemetryMaxSamples = math.max(0, defaults.TelemetryMaxSamples)
                };
            }
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XMiningScenarioAuthoring>
        {
            private NativeHashMap<FixedString64Bytes, Entity> _carrierEntityMap;
            private NativeHashMap<FixedString64Bytes, float3> _carrierPositionMap;
            private NativeHashMap<FixedString64Bytes, byte> _carrierSideMap;
            private NativeHashMap<FixedString64Bytes, AffiliationCache> _affiliationMap;
            private NativeHashMap<FixedString64Bytes, Entity> _affiliationEntityMap;
#if UNITY_EDITOR
            private static bool s_loggedStart;
            private static bool s_loggedLatch;
            private static bool s_loggedVisual;
            private static bool s_loggedCarriers;
            private static bool s_loggedVessels;
            private static bool s_loggedAsteroids;
            private static bool s_loggedComplete;
#endif

            public override void Bake(Space4XMiningScenarioAuthoring authoring)
            {
                UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Baking... Adding RenderSemanticKey type: {typeof(RenderSemanticKey).FullName}");
#if UNITY_EDITOR
                if (!s_loggedStart)
                {
                    UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Starting bake. Carriers: {authoring.Carriers?.Length ?? 0}, Vessels: {authoring.MiningVessels?.Length ?? 0}, Asteroids: {authoring.Asteroids?.Length ?? 0}");
                    s_loggedStart = true;
                }
#endif
                AddLatchConfig(authoring);
                AddVisualConfig(authoring);

                // Build carrier entity map first
                _carrierEntityMap = new NativeHashMap<FixedString64Bytes, Entity>(
                    authoring.Carriers?.Length ?? 0, 
                    Allocator.Temp);
                _carrierPositionMap = new NativeHashMap<FixedString64Bytes, float3>(
                    authoring.Carriers?.Length ?? 0,
                    Allocator.Temp);
                _carrierSideMap = new NativeHashMap<FixedString64Bytes, byte>(
                    authoring.Carriers?.Length ?? 0,
                    Allocator.Temp);
                _affiliationMap = new NativeHashMap<FixedString64Bytes, AffiliationCache>(
                    authoring.Affiliations?.Length ?? 0,
                    Allocator.Temp);
                _affiliationEntityMap = new NativeHashMap<FixedString64Bytes, Entity>(
                    authoring.Affiliations?.Length ?? 0,
                    Allocator.Temp);

                CacheAffiliations(authoring);

                // Bake carriers first and store their entities
                BakeCarriers(authoring);
                
                // Then bake vessels (which reference carriers)
                BakeMiningVessels(authoring);
                
                // Finally bake asteroids
                BakeAsteroids(authoring);

                _carrierEntityMap.Dispose();
                _carrierPositionMap.Dispose();
                _carrierSideMap.Dispose();
                _affiliationMap.Dispose();
                _affiliationEntityMap.Dispose();
                
#if UNITY_EDITOR
                if (!s_loggedComplete)
                {
                    UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Bake complete.");
                    s_loggedComplete = true;
                }
#endif
            }

            private void AddLatchConfig(Space4XMiningScenarioAuthoring authoring)
            {
                var configEntity = GetEntity(TransformUsageFlags.None);
                var settings = authoring.latchSettings;

                var config = new Space4XMiningLatchConfig
                {
                    RegionCount = math.max(1, settings.RegionCount),
                    SurfaceEpsilon = math.max(0.05f, settings.SurfaceEpsilon),
                    AlignDotThreshold = math.clamp(settings.AlignDotThreshold, 0f, 1f),
                    SettleTicks = (uint)math.max(0, settings.SettleTicks),
                    ReserveRegionWhileApproaching = settings.ReserveRegionWhileApproaching ? (byte)1 : (byte)0,
                    TelemetrySampleEveryTicks = (uint)math.max(1, settings.TelemetrySampleEveryTicks),
                    TelemetryMaxSamples = math.max(0, settings.TelemetryMaxSamples)
                };

                AddComponent(configEntity, config);
#if UNITY_EDITOR
                if (!s_loggedLatch)
                {
                    UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Added Space4XMiningLatchConfig singleton to entity {configEntity.Index}");
                    s_loggedLatch = true;
                }
#endif
            }

            private void AddVisualConfig(Space4XMiningScenarioAuthoring authoring)
            {
                var configEntity = GetEntity(TransformUsageFlags.None);
                
                // Ensure RenderPresentationCatalog exists on this entity or create a new one if needed
                // But typically RenderCatalog is baked separately. 
                // Just in case, let's add a dummy RenderPresentationCatalog if it doesn't exist to satisfy the system?
                // No, that would be wrong. The system needs a valid blob.
                
                var visuals = authoring.visuals;

                var config = new Space4XMiningVisualConfig
                {
                    CarrierPrimitive = visuals.CarrierPrimitive,
                    MiningVesselPrimitive = visuals.MiningVesselPrimitive,
                    AsteroidPrimitive = visuals.AsteroidPrimitive,
                    CarrierScale = math.max(0.05f, visuals.CarrierScale),
                    MiningVesselScale = math.max(0.05f, visuals.MiningVesselScale),
                    AsteroidScale = math.max(0.05f, visuals.AsteroidScale),
                    CarrierColor = ToFloat4(visuals.CarrierColor),
                    MiningVesselColor = ToFloat4(visuals.MiningVesselColor),
                    AsteroidColor = ToFloat4(visuals.AsteroidColor),
                    DisableDepthOffset = visuals.DisableDepthOffset ? (byte)1 : (byte)0
                };

                AddComponent(configEntity, config);
                AddComponent(configEntity, new Space4XPresentationDebugConfig
                {
                    EnableAttackMoveDebugLines = visuals.EnableAttackMoveDebugLines ? (byte)1 : (byte)0,
                    DisableDepthBobbing = visuals.DisableDepthOffset ? (byte)1 : (byte)0
                });
#if UNITY_EDITOR
                if (!s_loggedVisual)
                {
                    UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Added Space4XMiningVisualConfig singleton to entity {configEntity.Index}");
                    s_loggedVisual = true;
                }
#endif
            }

            private void BakeCarriers(Space4XMiningScenarioAuthoring authoring)
            {
                if (authoring.Carriers == null || authoring.Carriers.Length == 0)
                {
                    return;
                }

                var carrierTint = ToFloat4(authoring.visuals.CarrierColor);

                for (int i = 0; i < authoring.Carriers.Length; i++)
                {
                    var carrier = authoring.Carriers[i];
                    if (string.IsNullOrWhiteSpace(carrier.CarrierId))
                    {
                        UnityDebug.LogWarning($"Carrier definition has empty CarrierId, skipping.");
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    var carrierIdBytes = new FixedString64Bytes(carrier.CarrierId);
                    var carrierPosition = ResolveCarrierPosition(carrier.Position, i);
                    SetLocalTransform(entity, carrierPosition, quaternion.identity, 1f);
                    AddComponent<SpatialIndexedTag>(entity);
                    
                    AddComponent(entity, new Carrier
                    {
                        CarrierId = carrierIdBytes,
                        AffiliationEntity = ResolveAffiliation(carrier.AffiliationId),
                        Speed = math.max(0.1f, carrier.Speed),
                        Acceleration = math.max(0.1f, carrier.Speed * 0.4f),
                        Deceleration = math.max(0.1f, carrier.Speed * 0.6f),
                        TurnSpeed = 0.35f,
                        SlowdownDistance = 12f,
                        ArrivalDistance = 2.5f,
                        PatrolCenter = carrier.PatrolCenter,
                        PatrolRadius = math.max(1f, carrier.PatrolRadius)
                    });

                    var scenarioSide = carrier.IsHostile ? (byte)1 : (byte)0;
                    AddComponent(entity, new ScenarioSide
                    {
                        Side = scenarioSide
                    });

                    var carrierDisposition = carrier.Disposition != EntityDispositionFlags.None
                        ? carrier.Disposition
                        : BuildCarrierDisposition(carrier);
                    if (carrierDisposition != EntityDispositionFlags.None)
                    {
                        AddComponent(entity, new EntityDisposition
                        {
                            Flags = carrierDisposition
                        });
                    }

                    AddAlignment(entity, carrier.Alignment, carrier.RaceId, carrier.CultureId);
                    AddAffiliationTag(entity, carrier.AffiliationId);
                    AddCarrierAuthorityAndCrew(entity, math.clamp(carrier.Alignment.Law, -1f, 1f));

                    AddComponent(entity, new PatrolBehavior
                    {
                        CurrentWaypoint = float3.zero, // Will be initialized by CarrierPatrolSystem
                        WaitTime = math.max(0f, carrier.WaitTime),
                        WaitTimer = 0f
                    });

                    AddComponent(entity, new MovementCommand
                    {
                        TargetPosition = float3.zero,
                        ArrivalThreshold = 1f
                    });

                    AddComponent(entity, new VesselMovement
                    {
                        Velocity = float3.zero,
                        BaseSpeed = math.max(0.1f, carrier.Speed),
                        CurrentSpeed = 0f,
                        Acceleration = math.max(0.1f, carrier.Speed * 0.4f),
                        Deceleration = math.max(0.1f, carrier.Speed * 0.6f),
                        TurnSpeed = 0.35f,
                        SlowdownDistance = 12f,
                        ArrivalDistance = 2.5f,
                        DesiredRotation = quaternion.identity,
                        IsMoving = 0,
                        LastMoveTick = 0
                    });

                    AddComponent(entity, new VesselAIState
                    {
                        CurrentState = VesselAIState.State.Idle,
                        CurrentGoal = VesselAIState.Goal.None,
                        TargetEntity = Entity.Null,
                        TargetPosition = float3.zero,
                        StateTimer = 0f,
                        StateStartTick = 0
                    });

                    AddComponent(entity, new EntityIntent
                    {
                        Mode = IntentMode.Idle,
                        TargetEntity = Entity.Null,
                        TargetPosition = float3.zero,
                        TriggeringInterrupt = InterruptType.None,
                        IntentSetTick = 0,
                        Priority = InterruptPriority.Low,
                        IsValid = 0
                    });

                    AddBuffer<Interrupt>(entity);

                    AddComponent(entity, new VesselPhysicalProperties
                    {
                        Radius = 2.6f,
                        BaseMass = 120f,
                        HullDensity = 1.2f,
                        CargoMassPerUnit = 0.02f,
                        Restitution = 0.08f,
                        TangentialDamping = 0.25f
                    });

                    // Add ResourceStorage buffer for the carrier
                    var resourceBuffer = AddBuffer<ResourceStorage>(entity);
                    // Buffer starts empty, will be populated by mining vessels

                    // Add combat components if configured
                    if (carrier.IsHostile || !string.IsNullOrWhiteSpace(carrier.FleetId) || carrier.CanIntercept)
                    {
                        var fleetId = string.IsNullOrWhiteSpace(carrier.FleetId) 
                            ? carrierIdBytes 
                            : new FixedString64Bytes(carrier.FleetId);
                        
                        AddComponent(entity, new Space4XFleet
                        {
                            FleetId = fleetId,
                            ShipCount = 1,
                            Posture = carrier.FleetPosture,
                            TaskForce = 0
                        });

                        AddComponent(entity, new FleetMovementBroadcast
                        {
                            Position = carrierPosition,
                            Velocity = float3.zero,
                            LastUpdateTick = 0,
                            AllowsInterception = 1,
                            TechTier = 1
                        });

                        if (carrier.CanIntercept && carrier.InterceptSpeed > 0f)
                        {
                            AddComponent(entity, new InterceptCapability
                            {
                                MaxSpeed = math.max(0.1f, carrier.InterceptSpeed),
                                TechTier = 1,
                                AllowIntercept = 1
                            });
                        }
                    }

                    AssignRenderPresentation(entity, RenderKeys.Carrier, carrierTint);
                    if (i == 0)
                    {
                        SpawnSwarmDrones(entity, carrierPosition, carrierTint);
                        SpawnBreakablePieces(entity, carrierPosition, carrierTint);
                    }

                    // Store entity in map for vessel references
                    _carrierEntityMap.TryAdd(carrierIdBytes, entity);
                    _carrierPositionMap.TryAdd(carrierIdBytes, carrierPosition);
                    _carrierSideMap.TryAdd(carrierIdBytes, scenarioSide);
#if UNITY_EDITOR
                    if (!s_loggedCarriers)
                    {
                        UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Created carrier entity: {carrier.CarrierId} at position {carrier.Position}, Entity={entity.Index}");
                        s_loggedCarriers = true;
                    }
#endif
                }
            }

            private void BakeMiningVessels(Space4XMiningScenarioAuthoring authoring)
            {
                if (authoring.MiningVessels == null || authoring.MiningVessels.Length == 0)
                {
                    return;
                }

                var miningVesselTint = ToFloat4(authoring.visuals.MiningVesselColor);

                for (int i = 0; i < authoring.MiningVessels.Length; i++)
                {
                    var vessel = authoring.MiningVessels[i];
                    if (string.IsNullOrWhiteSpace(vessel.VesselId))
                    {
                        UnityDebug.LogWarning($"Mining vessel definition has empty VesselId, skipping.");
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    
                    // Find the carrier entity
                    Entity carrierEntity = Entity.Null;
                    float3 vesselPosition = vessel.Position;
                    float3 carrierPosition = default;
                    var hasCarrierId = !string.IsNullOrWhiteSpace(vessel.CarrierId);
                    FixedString64Bytes carrierIdBytes = default;
                    if (hasCarrierId)
                    {
                        carrierIdBytes = new FixedString64Bytes(vessel.CarrierId);
                        if (_carrierEntityMap.TryGetValue(carrierIdBytes, out var foundCarrier))
                        {
                            carrierEntity = foundCarrier;
                            _carrierPositionMap.TryGetValue(carrierIdBytes, out carrierPosition);
                            // If vessel should start docked, use carrier position
                            if (vessel.StartDocked)
                            {
                                // If docked and no explicit offset is configured, place deterministically near the carrier
                                // so smoke scenes don't collapse all vessels to the origin.
                                if (math.lengthsq(vessel.Position) < 0.01f)
                                {
                                    vesselPosition = carrierPosition + new float3(2f + i * 2.5f, 0f, -2f - i * 1.5f);
                                }
                            }
                        }
                        else
                        {
                            UnityDebug.LogWarning($"Mining vessel '{vessel.VesselId}' references carrier '{vessel.CarrierId}' which doesn't exist. Vessel will not function.");
                        }
                    }
                    
                    byte scenarioSide = 0;
                    if (hasCarrierId && _carrierSideMap.TryGetValue(carrierIdBytes, out var carrierSide))
                    {
                        scenarioSide = carrierSide;
                    }

                    vesselPosition = ResolveMiningVesselPosition(vesselPosition, i);
                    SetLocalTransform(entity, vesselPosition, quaternion.identity, 1f);
                    AddComponent<SpatialIndexedTag>(entity);

                    AddComponent(entity, new MiningVessel
                    {
                        VesselId = new FixedString64Bytes(vessel.VesselId),
                        CarrierEntity = carrierEntity,
                        MiningEfficiency = math.clamp(vessel.MiningEfficiency, 0f, 1f),
                        Speed = math.max(0.1f, vessel.Speed),
                        CargoCapacity = math.max(1f, vessel.CargoCapacity),
                        CurrentCargo = 0f,
                        CargoResourceType = ResourceType.Minerals
                    });

                    AddComponent(entity, new ScenarioSide
                    {
                        Side = scenarioSide
                    });

                    var vesselDisposition = vessel.Disposition != EntityDispositionFlags.None
                        ? vessel.Disposition
                        : BuildMiningDisposition(scenarioSide);
                    if (vesselDisposition != EntityDispositionFlags.None)
                    {
                        AddComponent(entity, new EntityDisposition
                        {
                            Flags = vesselDisposition
                        });
                    }

                    AddAlignment(entity, vessel.Alignment, vessel.RaceId, vessel.CultureId);
                    AddAffiliationTag(entity, vessel.AffiliationId);

                    var pilot = CreateAdditionalEntity(TransformUsageFlags.None);
                    AddAlignment(pilot, vessel.PilotAlignment, vessel.PilotRaceId, vessel.PilotCultureId);
                    if (vessel.OverridePilotDisposition)
                    {
                        AddComponent(pilot, BehaviorDisposition.FromValues(
                            vessel.PilotDisposition.Compliance,
                            vessel.PilotDisposition.Caution,
                            vessel.PilotDisposition.FormationAdherence,
                            vessel.PilotDisposition.RiskTolerance,
                            vessel.PilotDisposition.Aggression,
                            vessel.PilotDisposition.Patience));
                    }
                    else
                    {
                        AddComponent(pilot, new BehaviorDispositionSeedRequest
                        {
                            Seed = 0u,
                            SeedSalt = (uint)(i + 1)
                        });
                    }
                    AddComponent(entity, new VesselPilotLink
                    {
                        Pilot = pilot
                    });

                    var resourceId = ResolveResourceId(vessel);

                    AddComponent(entity, new MiningOrder
                    {
                        ResourceId = resourceId,
                        Source = MiningOrderSource.Scripted,
                        Status = MiningOrderStatus.Pending,
                        PreferredTarget = Entity.Null,
                        TargetEntity = Entity.Null,
                        IssuedTick = 0
                    });

                    AddComponent(entity, new MiningState
                    {
                        Phase = MiningPhase.Idle,
                        ActiveTarget = Entity.Null,
                        MiningTimer = 0f,
                        TickInterval = vessel.MiningTickInterval > 0f ? vessel.MiningTickInterval : 0.5f,
                        PhaseTimer = 0f
                    });

                    AddComponent(entity, new MiningYield
                    {
                        ResourceId = resourceId,
                        PendingAmount = 0f,
                        SpawnThreshold = vessel.OutputSpawnThreshold > 0f ? vessel.OutputSpawnThreshold : math.max(1f, vessel.CargoCapacity * 0.25f),
                        SpawnReady = 0
                    });

                    AddComponent(entity, new MiningJob
                    {
                        State = MiningJobState.None,
                        TargetAsteroid = Entity.Null,
                        MiningProgress = 0f
                    });

                    AddComponent(entity, new Space4XMiningToolProfile
                    {
                        ToolKind = vessel.ToolKind,
                        HasShapeOverride = vessel.ToolShapeOverride ? (byte)1 : (byte)0,
                        Shape = vessel.ToolShape,
                        RadiusOverride = vessel.ToolRadiusOverride,
                        RadiusMultiplier = vessel.ToolRadiusMultiplier,
                        StepLengthOverride = vessel.ToolStepLengthOverride,
                        StepLengthMultiplier = vessel.ToolStepLengthMultiplier,
                        DigUnitsPerMeterOverride = vessel.ToolDigUnitsPerMeterOverride,
                        MinStepLengthOverride = vessel.ToolMinStepLengthOverride,
                        MaxStepLengthOverride = vessel.ToolMaxStepLengthOverride,
                        YieldMultiplier = vessel.ToolYieldMultiplier,
                        HeatDeltaMultiplier = vessel.ToolHeatDeltaMultiplier,
                        InstabilityDeltaMultiplier = vessel.ToolInstabilityDeltaMultiplier,
                        DamageDeltaOverride = (byte)math.clamp(vessel.ToolDamageDeltaOverride, 0, 255),
                        DamageThresholdOverride = (byte)math.clamp(vessel.ToolDamageThresholdOverride, 0, 255)
                    });

                    AddComponent(entity, new VesselAIState
                    {
                        CurrentState = VesselAIState.State.Idle,
                        CurrentGoal = VesselAIState.Goal.None,
                        TargetEntity = Entity.Null,
                        TargetPosition = float3.zero,
                        StateTimer = 0f,
                        StateStartTick = 0
                    });

                    AddComponent(entity, new VesselMovement
                    {
                        Velocity = float3.zero,
                        BaseSpeed = math.max(0.1f, vessel.Speed),
                        CurrentSpeed = 0f,
                        DesiredRotation = quaternion.identity,
                        IsMoving = 0,
                        LastMoveTick = 0
                    });

                    AddComponent(entity, new VesselPhysicalProperties
                    {
                        Radius = 0.6f,
                        BaseMass = 6f,
                        HullDensity = 1.05f,
                        CargoMassPerUnit = 0.04f,
                        Restitution = 0.15f,
                        TangentialDamping = 0.3f
                    });

                    AddComponent<PickableTag>(entity);
                    AddComponent(entity, new HandPickable
                    {
                        Mass = 5f,
                        MaxHoldDistance = 75f,
                        ThrowImpulseMultiplier = 1f,
                        FollowLerp = 0.35f
                    });
                    AddComponent(entity, new Space4X.Runtime.Interaction.Space4XHandPickable
                    {
                        MaxMass = 250f,
                        ThrowSpeedMultiplier = 1f,
                        SlingshotSpeedMultiplier = 1.5f
                    });

                    AssignRenderPresentation(entity, RenderKeys.Miner, miningVesselTint);

                    AddBuffer<SpawnResourceRequest>(entity);
#if UNITY_EDITOR
                    if (!s_loggedVessels)
                    {
                        UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Created mining vessel entity: {vessel.VesselId} at position {vessel.Position}, Entity={entity.Index}, Carrier={carrierEntity.Index}");
                        s_loggedVessels = true;
                    }
#endif
                }
            }

            private void BakeAsteroids(Space4XMiningScenarioAuthoring authoring)
            {
                if (authoring.Asteroids == null || authoring.Asteroids.Length == 0)
                {
                    return;
                }

                var asteroidTint = ToFloat4(authoring.visuals.AsteroidColor);

                for (int i = 0; i < authoring.Asteroids.Length; i++)
                {
                    var asteroid = authoring.Asteroids[i];
                    if (string.IsNullOrWhiteSpace(asteroid.AsteroidId))
                    {
                        UnityDebug.LogWarning($"Asteroid definition has empty AsteroidId, skipping.");
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    var asteroidPosition = ResolveAsteroidPosition(asteroid.Position, i);
                    SetLocalTransform(entity, asteroidPosition, quaternion.identity, 1f);
                    AddComponent<SpatialIndexedTag>(entity);

                    AddComponent(entity, new Asteroid
                    {
                        AsteroidId = new FixedString64Bytes(asteroid.AsteroidId),
                        ResourceType = asteroid.ResourceType,
                        ResourceAmount = math.max(0f, asteroid.ResourceAmount),
                        MaxResourceAmount = math.max(asteroid.ResourceAmount, asteroid.MaxResourceAmount),
                        MiningRate = math.max(0.1f, asteroid.MiningRate)
                    });

                    var resourceTypeId = GetResourceTypeId(asteroid.ResourceType);
                    if (!resourceTypeId.IsEmpty)
                    {
                        AddComponent(entity, new ResourceTypeId { Value = resourceTypeId });
                    }

                    var maxWorkers = math.clamp((int)math.ceil(asteroid.MiningRate / 5f), 1, 16);
                    AddComponent(entity, new ResourceSourceConfig
                    {
                        GatherRatePerWorker = math.max(0.1f, asteroid.MiningRate),
                        MaxSimultaneousWorkers = (ushort)maxWorkers,
                        RespawnSeconds = 0f,
                        Flags = 0
                    });

                    AddComponent(entity, new ResourceSourceState
                    {
                        UnitsRemaining = math.max(0f, asteroid.ResourceAmount)
                    });

                    AddComponent(entity, new LastRecordedTick { Tick = 0 });
                    AddComponent<RewindableTag>(entity);
                    AddComponent(entity, new HistoryTier
                    {
                        Tier = HistoryTier.TierType.LowVisibility,
                        OverrideStrideSeconds = 0f
                    });
                    AddBuffer<ResourceHistorySample>(entity);
                    AddBuffer<Space4XMiningLatchReservation>(entity);

                    AddComponent(entity, new Space4XAsteroidVolumeConfig
                    {
                        Radius = math.max(0.1f, asteroid.VolumeRadius),
                        CoreRadiusRatio = math.clamp(asteroid.CoreRadiusRatio, 0f, 1f),
                        MantleRadiusRatio = math.clamp(asteroid.MantleRadiusRatio, 0f, 1f),
                        CrustMaterialId = (byte)math.clamp(asteroid.CrustMaterialId, 0, 255),
                        MantleMaterialId = (byte)math.clamp(asteroid.MantleMaterialId, 0, 255),
                        CoreMaterialId = (byte)math.clamp(asteroid.CoreMaterialId, 0, 255),
                        CoreDepositId = (byte)math.clamp(asteroid.CoreDepositId, 0, 255),
                        CoreOreGrade = (byte)math.clamp(asteroid.CoreOreGrade, 0, 255),
                        OreGradeExponent = math.max(0.1f, asteroid.OreGradeExponent),
                        Seed = (uint)math.max(0, asteroid.VolumeSeed)
                    });

                    AddComponent(entity, new Space4XAsteroidCenter
                    {
                        Position = asteroidPosition
                    });

                    AddComponent<PickableTag>(entity);
                    AddComponent(entity, new HandPickable
                    {
                        Mass = math.max(asteroid.VolumeRadius * 5f, 1f),
                        MaxHoldDistance = 150f,
                        ThrowImpulseMultiplier = 1f,
                        FollowLerp = 0.2f
                    });
                    AddComponent(entity, new Space4X.Runtime.Interaction.Space4XHandPickable
                    {
                        MaxMass = 50000f,
                        ThrowSpeedMultiplier = 0.6f,
                        SlingshotSpeedMultiplier = 0.8f
                    });
                    AssignRenderPresentation(entity, RenderKeys.Asteroid, asteroidTint);
#if UNITY_EDITOR
                    if (!s_loggedAsteroids)
                    {
                        UnityDebug.Log($"[Space4XMiningScenarioAuthoring.Baker] Created asteroid entity: {asteroid.AsteroidId} at position {asteroid.Position}, Entity={entity.Index}");
                        s_loggedAsteroids = true;
                    }
#endif
                }
            }

            private void AssignRenderPresentation(
                Entity entity,
                ushort semanticKey,
                in float4 tint,
                byte highlightMask = 0,
                bool enableThemeOverride = false,
                ushort themeOverride = 0)
            {
                AddComponent(entity, new RenderSemanticKey
                {
                    Value = semanticKey
                });

                AddComponent(entity, new RenderVariantKey
                {
                    Value = 0
                });

                AddComponent(entity, new RenderFlags
                {
                    Visible = 1,
                    ShadowCaster = 1,
                    HighlightMask = highlightMask
                });

                AddComponent<MeshPresenter>(entity);
                SetComponentEnabled<MeshPresenter>(entity, true);
                AddComponent<SpritePresenter>(entity);
                SetComponentEnabled<SpritePresenter>(entity, false);
                AddComponent<DebugPresenter>(entity);
                SetComponentEnabled<DebugPresenter>(entity, false);

                AddComponent(entity, new RenderThemeOverride
                {
                    Value = themeOverride
                });
                SetComponentEnabled<RenderThemeOverride>(entity, enableThemeOverride);

                AddComponent(entity, new RenderTint
                {
                    Value = tint
                });
                AddComponent(entity, new RenderTexSlice
                {
                    Value = 0
                });
                AddComponent(entity, new RenderUvTransform
                {
                    Value = new float4(1f, 1f, 0f, 0f)
                });
            }

            private void SpawnSwarmDrones(Entity anchorEntity, float3 anchorPosition, in float4 anchorTint)
            {
                const int droneCount = 12;
                float4 droneTint = new float4(anchorTint.x, anchorTint.y, anchorTint.z, 1f);

                AddComponent(anchorEntity, new Space4X.Runtime.Space4XSwarmDemoState
                {
                    Phase = Space4X.Runtime.Space4XSwarmDemoPhase.Screen,
                    NextPhaseTick = 0u,
                    AttackTarget = Entity.Null,
                    TugDirection = new float3(1f, 0f, 0f)
                });

                AddComponent(anchorEntity, new PureDOTS.Runtime.Agency.ControllerIntegrityState
                {
                    Integrity01 = 1f,
                    CompromisedBy = Entity.Null,
                    LastIntegrityTick = 0u,
                    IsCompromised = 0,
                    Reserved0 = 0,
                    Reserved1 = 0
                });

                AddComponent(anchorEntity, new CompromiseState
                {
                    IsCompromised = 0,
                    Suspicion = 0,
                    Severity = 0,
                    Kind = CompromiseKind.Infiltration,
                    Source = Entity.Null,
                    SinceTick = 0u,
                    LastEvidenceTick = 0u
                });

                AddComponent(anchorEntity, new Space4X.Runtime.Space4XSmokeCompromiseBeatConfig
                {
                    CommsDropStartSeconds = 8f,
                    CommsDropDurationSeconds = 6f,
                    CommsQualityDuringDrop = 0f,
                    ControllerCompromiseSeconds = 18f,
                    ControllerCompromiseSeverity = 220,
                    ControllerCompromiseKind = CompromiseKind.HostileOverride,
                    HackStartSeconds = 22f,
                    HackDroneCount = 4,
                    HackSeverity = 200,
                    Initialized = 0,
                    HackApplied = 0,
                    CompromiseApplied = 0,
                    CommsDropApplied = 0,
                    CommsDropStartTick = 0u,
                    CommsDropEndTick = 0u,
                    ControllerCompromiseTick = 0u,
                    HackStartTick = 0u,
                    HackerEntity = Entity.Null
                });

                AddComponent(anchorEntity, new CompromiseDoctrine
                {
                    QuarantineThreshold = 64,
                    PurgeThreshold = 200,
                    PreferredResponse = CompromiseResponseMode.Disconnect,
                    FriendlyFirePenaltyMode = FriendlyFirePenaltyMode.WaivedIfCompromised,
                    RecoveryBudgetTicks = 0u
                });

                AddComponent(anchorEntity, new SwarmThrustState
                {
                    DesiredDirection = new float3(1f, 0f, 0f),
                    CurrentThrust = 0f,
                    Active = false
                });

                for (int i = 0; i < droneCount; i++)
                {
                    var droneEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    float radius = 6f + (i % 4) * 1.5f;
                    float angularSpeed = 1.2f + (i % 3) * 0.35f;
                    float phase = math.radians((360f / droneCount) * i);
                    float elevation = (i % 5 - 2) * 0.25f;

                    float3 offset = new float3(
                        math.cos(phase) * radius,
                        elevation,
                        math.sin(phase) * radius);

                    SetLocalTransform(droneEntity, anchorPosition + offset, quaternion.identity, 0.35f);
                    AddComponent(droneEntity, new AgencySelfPreset { Kind = AgencySelfPresetKind.Tool });
                    AddComponent<DroneTag>(droneEntity);
                    AddComponent(droneEntity, new DroneOrbit
                    {
                        AnchorShip = anchorEntity,
                        Radius = radius,
                        AngularSpeed = angularSpeed,
                        PhaseOffset = phase,
                        Elevation = elevation
                    });
                    AddComponent(droneEntity, new SwarmBehavior
                    {
                        Mode = SwarmMode.Screen,
                        Target = Entity.Null
                    });
                    AddComponent(droneEntity, new PureDOTS.Runtime.Agency.ControlOrderState
                    {
                        Kind = PureDOTS.Runtime.Agency.ControlOrderKind.Screen,
                        FallbackKind = PureDOTS.Runtime.Agency.ControlOrderKind.Return,
                        TargetEntity = Entity.Null,
                        AnchorEntity = anchorEntity,
                        AnchorPosition = anchorPosition,
                        Radius = radius,
                        IssuedTick = 0u,
                        ExpiryTick = 0u,
                        LastUpdatedTick = 0u,
                        Sequence = 0u,
                        RequiresHeartbeat = 1,
                        Reserved0 = 0,
                        Reserved1 = 0
                    });
                    AddComponent(droneEntity, new PureDOTS.Runtime.Agency.ControlLinkState
                    {
                        ControllerEntity = anchorEntity,
                        CompromiseSource = Entity.Null,
                        LastHeartbeatTick = 0u,
                        CommsQuality01 = 1f,
                        IsCompromised = 0,
                        IsLost = 0,
                        Reserved0 = 0
                    });
                    AddComponent(droneEntity, new CompromiseDoctrine
                    {
                        QuarantineThreshold = 64,
                        PurgeThreshold = 200,
                        PreferredResponse = CompromiseResponseMode.Disconnect,
                        FriendlyFirePenaltyMode = FriendlyFirePenaltyMode.WaivedIfCompromised,
                        RecoveryBudgetTicks = 0u
                    });
                    var claims = AddBuffer<ControlClaim>(droneEntity);
                    claims.Add(new ControlClaim
                    {
                        Controller = anchorEntity,
                        SourceSeat = Entity.Null,
                        Domains = AgencyDomain.FlightOps | AgencyDomain.Movement | AgencyDomain.Combat,
                        Pressure = 0.9f,
                        Legitimacy = 0.85f,
                        Hostility = 0f,
                        Consent = 0.95f,
                        EstablishedTick = 0u,
                        ExpireTick = 0u,
                        SourceKind = ControlClaimSourceKind.Scripted
                    });

                    AssignRenderPresentation(droneEntity, RenderKeys.StrikeCraft, droneTint);
                }
            }

            private void SpawnBreakablePieces(Entity anchorEntity, float3 anchorPosition, in float4 anchorTint)
            {
                var profile = BuildBreakProfileBlob();
                AddBlobAsset(ref profile, out _);

                var breakableRoot = new Space4XBreakableRoot
                {
                    Profile = profile,
                    BreakTick = 0u,
                    IsBroken = 0,
                    Damage = 0f,
                    Instability = 0f,
                    Reserved0 = 0,
                    Reserved1 = 0
                };
                AddComponent(anchorEntity, breakableRoot);
                AddComponent(anchorEntity, new Space4XBreakableDamagePulse
                {
                    DamageAmount = 120f,
                    DelaySeconds = 12f,
                    TriggerTick = 0u,
                    Fired = 0,
                    DamageType = PureDOTS.Runtime.Combat.DamageType.Physical,
                    DamageFlags = PureDOTS.Runtime.Combat.DamageFlags.AoE
                });

                var edgeBuffer = AddBuffer<Space4XBreakableEdgeState>(anchorEntity);
                ref var edges = ref breakableRoot.Profile.Value.Edges;
                for (int i = 0; i < edges.Length; i++)
                {
                    edgeBuffer.Add(new Space4XBreakableEdgeState
                    {
                        EdgeIndex = i,
                        BrokenTick = 0u,
                        IsBroken = 0,
                        Reserved0 = 0,
                        Reserved1 = 0
                    });
                }

                float4 pieceTint = new float4(anchorTint.x, anchorTint.y, anchorTint.z, 1f);
                ref var pieces = ref breakableRoot.Profile.Value.Pieces;

                for (int i = 0; i < pieces.Length; i++)
                {
                    var pieceDef = pieces[i];
                    var pieceEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    SetLocalTransform(pieceEntity, anchorPosition + pieceDef.LocalOffset, quaternion.identity, 0.55f);
                    AddComponent(pieceEntity, new Space4XBreakablePiece
                    {
                        Root = anchorEntity,
                        PieceIndex = pieceDef.PieceId,
                        LocalOffset = pieceDef.LocalOffset,
                        AttachmentGroup = pieceDef.AttachmentGroup
                    });
                    AddComponent(pieceEntity, new Space4XBreakablePieceState
                    {
                        Damage01 = 0f,
                        Instability01 = 0f
                    });

                    AssignRenderPresentation(pieceEntity, RenderKeys.Carrier, pieceTint);
                }
            }

            private BlobAssetReference<ShipBreakProfileBlob> BuildBreakProfileBlob()
            {
                using var builder = new BlobBuilder(Allocator.Temp);
                ref var root = ref builder.ConstructRoot<ShipBreakProfileBlob>();
                root.BreakDelaySeconds = 14f;
                root.MaxFragments = 6;
                root.MinFragmentMass = 0.1f;

                var pieces = builder.Allocate(ref root.Pieces, 4);
                pieces[0] = new ShipBreakPieceDef
                {
                    PieceId = 0,
                    LocalOffset = new float3(0f, 0f, 0f),
                    MassFraction = 0.45f,
                    AttachmentGroup = 0,
                    ColliderPreset = 0,
                    VisualPreset = 0,
                    IsCore = 1,
                    ProvidesFlags = ShipCapabilityFlags.Command | ShipCapabilityFlags.Power | ShipCapabilityFlags.Sensors | ShipCapabilityFlags.LifeSupport,
                    ThrustContribution = 0f,
                    PowerGeneration = 2.5f,
                    SensorRangeMultiplier = 1.1f,
                    WeaponHardpointCount = 0
                };
                pieces[1] = new ShipBreakPieceDef
                {
                    PieceId = 1,
                    LocalOffset = new float3(-6f, 0f, 0f),
                    MassFraction = 0.2f,
                    AttachmentGroup = 1,
                    ColliderPreset = 0,
                    VisualPreset = 0,
                    IsCore = 0,
                    ProvidesFlags = ShipCapabilityFlags.Propulsion | ShipCapabilityFlags.Steering | ShipCapabilityFlags.Power,
                    ThrustContribution = 6f,
                    PowerGeneration = 1.5f,
                    SensorRangeMultiplier = 1f,
                    WeaponHardpointCount = 0
                };
                pieces[2] = new ShipBreakPieceDef
                {
                    PieceId = 2,
                    LocalOffset = new float3(4f, 0f, -2f),
                    MassFraction = 0.2f,
                    AttachmentGroup = 2,
                    ColliderPreset = 0,
                    VisualPreset = 0,
                    IsCore = 0,
                    ProvidesFlags = ShipCapabilityFlags.Weapons,
                    ThrustContribution = 0f,
                    PowerGeneration = 0f,
                    SensorRangeMultiplier = 1f,
                    WeaponHardpointCount = 2
                };
                pieces[3] = new ShipBreakPieceDef
                {
                    PieceId = 3,
                    LocalOffset = new float3(2f, 0f, 4f),
                    MassFraction = 0.15f,
                    AttachmentGroup = 3,
                    ColliderPreset = 0,
                    VisualPreset = 0,
                    IsCore = 0,
                    ProvidesFlags = ShipCapabilityFlags.Hangar | ShipCapabilityFlags.Cargo,
                    ThrustContribution = 0f,
                    PowerGeneration = 0f,
                    SensorRangeMultiplier = 1f,
                    WeaponHardpointCount = 0
                };

                var edges = builder.Allocate(ref root.Edges, 3);
                edges[0] = new ShipBreakEdgeDef
                {
                    PieceA = 0,
                    PieceB = 1,
                    BreakDamageThreshold = 0.6f,
                    BreakInstabilityThreshold = 0.8f,
                    BreakMode = Space4XBreakMode.Threshold,
                    IsCriticalPath = 1
                };
                edges[1] = new ShipBreakEdgeDef
                {
                    PieceA = 0,
                    PieceB = 2,
                    BreakDamageThreshold = 0.6f,
                    BreakInstabilityThreshold = 0.8f,
                    BreakMode = Space4XBreakMode.Threshold,
                    IsCriticalPath = 0
                };
                edges[2] = new ShipBreakEdgeDef
                {
                    PieceA = 0,
                    PieceB = 3,
                    BreakDamageThreshold = 0.6f,
                    BreakInstabilityThreshold = 0.8f,
                    BreakMode = Space4XBreakMode.Threshold,
                    IsCriticalPath = 0
                };

                root.AliveRequired = ShipCapabilityFlags.Command | ShipCapabilityFlags.Power;
                root.MobileRequiredAny = ShipCapabilityFlags.Propulsion | ShipCapabilityFlags.Towable;
                root.CombatRequiredAny = ShipCapabilityFlags.Weapons | ShipCapabilityFlags.Hangar;
                root.FtlRequiredAll = ShipCapabilityFlags.Ftl | ShipCapabilityFlags.Power | ShipCapabilityFlags.Sensors;

                return builder.CreateBlobAssetReference<ShipBreakProfileBlob>(Allocator.Persistent);
            }

            private static float4 ToFloat4(Color color)
            {
                return new float4(color.r, color.g, color.b, color.a);
            }

            private void SetLocalTransform(Entity entity, float3 position, quaternion rotation, float scale)
            {
                var transform = LocalTransform.FromPositionRotationScale(position, rotation, scale);
                AddComponent(entity, transform);
            }

            private static bool IsNearZero(in float3 value)
            {
                return math.lengthsq(value) < 0.0001f;
            }

            private static float3 ResolveCarrierPosition(in float3 position, int index)
            {
                if (!IsNearZero(position))
                {
                    return position;
                }

                return index switch
                {
                    0 => new float3(0f, 0f, -8f),
                    1 => new float3(12f, 0f, -8f),
                    _ => new float3(index * 12f, 0f, -8f)
                };
            }

            private static float3 ResolveMiningVesselPosition(in float3 position, int index)
            {
                if (!IsNearZero(position))
                {
                    return position;
                }

                return index switch
                {
                    0 => new float3(5f, 0f, -2f),
                    1 => new float3(-5f, 0f, -14f),
                    _ => new float3(5f + index * 6f, 0f, -2f - index * 6f)
                };
            }

            private static float3 ResolveAsteroidPosition(in float3 position, int index)
            {
                if (!IsNearZero(position))
                {
                    return position;
                }

                return index switch
                {
                    0 => new float3(20f, 0f, 10f),
                    1 => new float3(-20f, 0f, -20f),
                    _ => new float3(20f + index * 10f, 0f, 10f - index * 12f)
                };
            }


            private struct AffiliationCache
            {
                public FixedString64Bytes DisplayName;
                public float Loyalty;
            }

            private void CacheAffiliations(Space4XMiningScenarioAuthoring authoring)
            {
                if (authoring.Affiliations == null)
                {
                    return;
                }

                foreach (var affiliation in authoring.Affiliations)
                {
                    if (string.IsNullOrWhiteSpace(affiliation.AffiliationId))
                    {
                        continue;
                    }

                    var key = new FixedString64Bytes(affiliation.AffiliationId);
                    if (!_affiliationMap.ContainsKey(key))
                    {
                        _affiliationMap.Add(key, new AffiliationCache
                        {
                            DisplayName = string.IsNullOrWhiteSpace(affiliation.DisplayName)
                                ? new FixedString64Bytes(affiliation.AffiliationId)
                                : new FixedString64Bytes(affiliation.DisplayName),
                            Loyalty = math.saturate(affiliation.Loyalty)
                        });
                    }
                }
            }

            private Entity ResolveAffiliation(string affiliationId)
            {
                if (string.IsNullOrWhiteSpace(affiliationId))
                {
                    return Entity.Null;
                }

                var key = new FixedString64Bytes(affiliationId);
                if (_affiliationEntityMap.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                if (!_affiliationMap.TryGetValue(key, out var definition))
                {
                    return Entity.Null;
                }

                var entity = CreateAdditionalEntity(TransformUsageFlags.None);
                var name = definition.DisplayName.IsEmpty ? key : definition.DisplayName;
                AddComponent(entity, new AffiliationRelation { AffiliationName = name });
                _affiliationEntityMap[key] = entity;
                return entity;
            }

            private void AddAffiliationTag(Entity entity, string affiliationId)
            {
                if (string.IsNullOrWhiteSpace(affiliationId))
                {
                    return;
                }

                var key = new FixedString64Bytes(affiliationId);
                if (!_affiliationMap.TryGetValue(key, out var definition))
                {
                    return;
                }

                var target = ResolveAffiliation(affiliationId);
                if (target == Entity.Null)
                {
                    return;
                }

                var buffer = AddBuffer<AffiliationTag>(entity);
                buffer.Add(new AffiliationTag
                {
                    Type = AffiliationType.Fleet,
                    Target = target,
                    Loyalty = (half)math.saturate(definition.Loyalty)
                });
            }

            private void AddCarrierAuthorityAndCrew(Entity carrierEntity, float lawfulness)
            {
                AddComponent(carrierEntity, new CaptainOrder
                {
                    Type = CaptainOrderType.None,
                    Status = CaptainOrderStatus.None,
                    Priority = 0,
                    TargetEntity = Entity.Null,
                    TargetPosition = float3.zero,
                    IssuedTick = 0,
                    TimeoutTick = 0,
                    IssuingAuthority = Entity.Null
                });
                AddComponent(carrierEntity, CaptainState.Default);
                AddComponent(carrierEntity, CaptainReadiness.Standard);

                var crew = AddBuffer<PlatformCrewMember>(carrierEntity);
                var config = StrikeCraftPilotProfileConfig.Default;

                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = CreateCrewEntity(lawfulness, config,
                        new IndividualStats
                        {
                            Command = (half)90,
                            Tactics = (half)70,
                            Logistics = (half)60,
                            Diplomacy = (half)60,
                            Engineering = (half)40,
                            Resolve = (half)85
                        },
                        BehaviorDisposition.FromValues(0.8f, 0.6f, 0.8f, 0.4f, 0.45f, 0.7f)),
                    RoleId = 0
                });

                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = CreateCrewEntity(lawfulness, config,
                        new IndividualStats
                        {
                            Command = (half)75,
                            Tactics = (half)55,
                            Logistics = (half)80,
                            Diplomacy = (half)50,
                            Engineering = (half)45,
                            Resolve = (half)70
                        },
                        BehaviorDisposition.FromValues(0.75f, 0.6f, 0.7f, 0.45f, 0.4f, 0.7f)),
                    RoleId = 0
                });

                crew.Add(new PlatformCrewMember
                {
                    CrewEntity = CreateCrewEntity(lawfulness, config,
                        new IndividualStats
                        {
                            Command = (half)65,
                            Tactics = (half)80,
                            Logistics = (half)50,
                            Diplomacy = (half)45,
                            Engineering = (half)40,
                            Resolve = (half)60
                        },
                        BehaviorDisposition.FromValues(0.65f, 0.55f, 0.7f, 0.5f, 0.45f, 0.6f)),
                    RoleId = 0
                });
            }

            private Entity CreateCrewEntity(
                float lawfulness,
                in StrikeCraftPilotProfileConfig config,
                in IndividualStats stats,
                in BehaviorDisposition disposition)
            {
                var crew = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(crew, AlignmentTriplet.FromFloats(lawfulness, 0f, 0f));
                AddComponent(crew, stats);
                AddComponent(crew, disposition);

                var outlookId = ResolveOutlookId(config, lawfulness);
                var outlookEntries = AddBuffer<OutlookEntry>(crew);
                var outlooks = AddBuffer<TopOutlook>(crew);
                outlookEntries.Add(new OutlookEntry
                {
                    OutlookId = outlookId,
                    Weight = (half)1f
                });
                outlooks.Add(new TopOutlook
                {
                    OutlookId = outlookId,
                    Weight = (half)1f
                });

                return crew;
            }

            private static OutlookId ResolveOutlookId(in StrikeCraftPilotProfileConfig config, float lawfulness)
            {
                if (lawfulness >= config.LoyalistLawThreshold)
                {
                    return config.FriendlyOutlook;
                }

                if (lawfulness <= config.MutinousLawThreshold)
                {
                    return config.HostileOutlook;
                }

                return config.NeutralOutlook;
            }

            private void AddAlignment(Entity entity, AlignmentDefinition alignment, ushort raceId, ushort cultureId)
            {
                AddComponent(entity, AlignmentTriplet.FromFloats(alignment.Law, alignment.Good, alignment.Integrity));
                AddComponent(entity, new RaceId { Value = raceId });
                AddComponent(entity, new CultureId { Value = cultureId });

                var axisBuffer = AddBuffer<EthicAxisValue>(entity);
                if (alignment.Axes != null)
                {
                    for (int i = 0; i < alignment.Axes.Length; i++)
                    {
                        var axis = alignment.Axes[i];
                        axisBuffer.Add(new EthicAxisValue
                        {
                            Axis = axis.Axis,
                            Value = (half)math.clamp(axis.Value, -2f, 2f)
                        });
                    }
                }

                var outlookBuffer = AddBuffer<OutlookEntry>(entity);
                if (alignment.Outlooks != null)
                {
                    for (int i = 0; i < alignment.Outlooks.Length; i++)
                    {
                        var outlook = alignment.Outlooks[i];
                        outlookBuffer.Add(new OutlookEntry
                        {
                            OutlookId = outlook.OutlookId,
                            Weight = (half)math.clamp(outlook.Weight, -1f, 1f)
                        });
                    }
                }
            }

            private static EntityDispositionFlags BuildCarrierDisposition(in CarrierDefinition carrier)
            {
                var flags = EntityDispositionFlags.Support;
                var hasCombat = carrier.IsHostile || carrier.CanIntercept;
                if (hasCombat)
                {
                    flags |= EntityDispositionFlags.Combatant | EntityDispositionFlags.Military;
                }
                else
                {
                    flags |= EntityDispositionFlags.Civilian;
                }

                if (carrier.IsHostile)
                {
                    flags |= EntityDispositionFlags.Hostile;
                }

                return flags;
            }

            private static EntityDispositionFlags BuildMiningDisposition(byte scenarioSide)
            {
                var flags = EntityDispositionFlags.Mining | EntityDispositionFlags.Civilian;
                if (scenarioSide == 1)
                {
                    flags |= EntityDispositionFlags.Hostile;
                }

                return flags;
            }

            private static FixedString64Bytes ResolveResourceId(MiningVesselDefinition vessel)
            {
                if (!string.IsNullOrWhiteSpace(vessel.ResourceId))
                {
                    return new FixedString64Bytes(vessel.ResourceId.Trim());
                }

                return GetResourceTypeId(ResourceType.Minerals);
            }

            private static FixedString64Bytes GetResourceTypeId(ResourceType resourceType)
            {
                switch (resourceType)
                {
                    case ResourceType.Minerals:
                        return new FixedString64Bytes("space4x.resource.minerals");
                    case ResourceType.RareMetals:
                        return new FixedString64Bytes("space4x.resource.rare_metals");
                    case ResourceType.EnergyCrystals:
                        return new FixedString64Bytes("space4x.resource.energy_crystals");
                    case ResourceType.OrganicMatter:
                        return new FixedString64Bytes("space4x.resource.organic_matter");
                    default:
                        return new FixedString64Bytes("space4x.resource.unknown");
                }
            }
        }
    }
}
