using System;
using PureDOTS.Authoring;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Space4X.Presentation;
using Space4X.Runtime;
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
                Alignment = AlignmentDefinition.CreateNeutral(),
                RaceId = 0,
                CultureId = 0,
                AffiliationId = "AFFILIATION-1"
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
                Alignment = AlignmentDefinition.CreateNeutral(),
                RaceId = 0,
                CultureId = 0,
                AffiliationId = "AFFILIATION-1"
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
                Position = new float3(20f, 0f, 10f)
            },
            new AsteroidDefinition
            {
                AsteroidId = "ASTEROID-2",
                ResourceType = ResourceType.Minerals,
                ResourceAmount = 500f,
                MaxResourceAmount = 500f,
                MiningRate = 10f,
                Position = new float3(-20f, 0f, -20f)
            }
        };

        [SerializeField]
        private MiningVisualSettings visuals = MiningVisualSettings.CreateDefault();

        public CarrierDefinition[] Carriers => carriers;
        public MiningVesselDefinition[] MiningVessels => miningVessels;
        public AsteroidDefinition[] Asteroids => asteroids;
        public MiningVisualSettings Visuals => visuals;
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

            [Header("Alignment / Affiliation")]
            public AlignmentDefinition Alignment;
            public ushort RaceId;
            public ushort CultureId;
            public string AffiliationId;
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
                    AsteroidDescriptorKey = "space4x.prop.asteroid"
                };
            }
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XMiningScenarioAuthoring>
        {
            private NativeHashMap<FixedString64Bytes, Entity> _carrierEntityMap;
            private NativeHashMap<FixedString64Bytes, float3> _carrierPositionMap;
            private NativeHashMap<FixedString64Bytes, AffiliationCache> _affiliationMap;
            private NativeHashMap<FixedString64Bytes, Entity> _affiliationEntityMap;
#if UNITY_EDITOR
            private static bool s_loggedStart;
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
                
                AddVisualConfig(authoring);

                // Build carrier entity map first
                _carrierEntityMap = new NativeHashMap<FixedString64Bytes, Entity>(
                    authoring.Carriers?.Length ?? 0, 
                    Allocator.Temp);
                _carrierPositionMap = new NativeHashMap<FixedString64Bytes, float3>(
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
                    AsteroidColor = ToFloat4(visuals.AsteroidColor)
                };

                AddComponent(configEntity, config);
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
                        PatrolCenter = carrier.PatrolCenter,
                        PatrolRadius = math.max(1f, carrier.PatrolRadius)
                    });

                    AddAlignment(entity, carrier.Alignment, carrier.RaceId, carrier.CultureId);
                    AddAffiliationTag(entity, carrier.AffiliationId);

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

                    // Store entity in map for vessel references
                    _carrierEntityMap.TryAdd(carrierIdBytes, entity);
                    _carrierPositionMap.TryAdd(carrierIdBytes, carrierPosition);
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
                    if (!string.IsNullOrWhiteSpace(vessel.CarrierId))
                    {
                        var carrierIdBytes = new FixedString64Bytes(vessel.CarrierId);
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

                    AddAlignment(entity, vessel.Alignment, vessel.RaceId, vessel.CultureId);
                    AddAffiliationTag(entity, vessel.AffiliationId);

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
                        TickInterval = vessel.MiningTickInterval > 0f ? vessel.MiningTickInterval : 0.5f
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
