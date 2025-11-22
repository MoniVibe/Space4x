using System;
using PureDOTS.Authoring;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Spatial;
using Space4X.Presentation;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Space4X.Registry
{
    /// <summary>
    /// Authoring component for creating carriers, mining vessels, and asteroids in the mining demo scene.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PureDotsConfigAuthoring))]
    [RequireComponent(typeof(SpatialPartitionAuthoring))]
    public sealed class Space4XMiningDemoAuthoring : MonoBehaviour
    {
        [SerializeField]
        private CarrierDefinition[] carriers = new CarrierDefinition[]
        {
            new CarrierDefinition
            {
                CarrierId = "CARRIER-1",
                Speed = 5f,
                PatrolCenter = new float3(0f, 0f, 0f),
                PatrolRadius = 50f,
                WaitTime = 2f,
                Position = new float3(0f, 0f, 0f)
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
                Position = new float3(5f, 0f, 0f),
                CarrierId = "CARRIER-1"
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
                Position = new float3(-5f, 0f, 0f),
                CarrierId = "CARRIER-1"
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
                Position = new float3(20f, 0f, 0f)
            },
            new AsteroidDefinition
            {
                AsteroidId = "ASTEROID-2",
                ResourceType = ResourceType.Minerals,
                ResourceAmount = 500f,
                MaxResourceAmount = 500f,
                MiningRate = 10f,
                Position = new float3(-20f, 0f, 0f)
            }
        };

        [SerializeField]
        private MiningVisualSettings visuals = MiningVisualSettings.CreateDefault();

        public CarrierDefinition[] Carriers => carriers;
        public MiningVesselDefinition[] MiningVessels => miningVessels;
        public AsteroidDefinition[] Asteroids => asteroids;
        public MiningVisualSettings Visuals => visuals;

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
            
            [Tooltip("Starting position of the vessel")]
            public float3 Position;
            
            [Tooltip("Carrier ID that this vessel belongs to (must match a CarrierId)")]
            public string CarrierId;
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
        public struct MiningVisualSettings
        {
            public Space4XMiningPrimitive CarrierPrimitive;
            [Min(0.05f)] public float CarrierScale;
            public Color CarrierColor;
            public string CarrierDescriptorKey;

            public Space4XMiningPrimitive MiningVesselPrimitive;
            [Min(0.05f)] public float MiningVesselScale;
            public Color MiningVesselColor;
            public string MiningVesselDescriptorKey;

            public Space4XMiningPrimitive AsteroidPrimitive;
            [Min(0.05f)] public float AsteroidScale;
            public Color AsteroidColor;
            public string AsteroidDescriptorKey;

            public static MiningVisualSettings CreateDefault()
            {
                return new MiningVisualSettings
                {
                    CarrierPrimitive = Space4XMiningPrimitive.Capsule,
                    CarrierScale = 3f,
                    CarrierColor = new Color(0.35f, 0.4f, 0.62f, 1f),
                    CarrierDescriptorKey = "space4x.vessel.carrier",
                    MiningVesselPrimitive = Space4XMiningPrimitive.Cylinder,
                    MiningVesselScale = 1.2f,
                    MiningVesselColor = new Color(0.25f, 0.52f, 0.84f, 1f),
                    MiningVesselDescriptorKey = "space4x.vessel.miner",
                    AsteroidPrimitive = Space4XMiningPrimitive.Sphere,
                    AsteroidScale = 2.25f,
                    AsteroidColor = new Color(0.52f, 0.43f, 0.34f, 1f),
                    AsteroidDescriptorKey = "space4x.prop.asteroid"
                };
            }
        }

        private sealed class Baker : Unity.Entities.Baker<Space4XMiningDemoAuthoring>
        {
            private NativeHashMap<FixedString64Bytes, Entity> _carrierEntityMap;
#if UNITY_EDITOR
            private static bool s_loggedStart;
            private static bool s_loggedVisual;
            private static bool s_loggedCarriers;
            private static bool s_loggedVessels;
            private static bool s_loggedAsteroids;
            private static bool s_loggedComplete;
#endif

            public override void Bake(Space4XMiningDemoAuthoring authoring)
            {
#if UNITY_EDITOR
                if (!s_loggedStart)
                {
                    Debug.Log($"[Space4XMiningDemoAuthoring.Baker] Starting bake. Carriers: {authoring.Carriers?.Length ?? 0}, Vessels: {authoring.MiningVessels?.Length ?? 0}, Asteroids: {authoring.Asteroids?.Length ?? 0}");
                    s_loggedStart = true;
                }
#endif
                
                AddVisualConfig(authoring);

                // Build carrier entity map first
                _carrierEntityMap = new NativeHashMap<FixedString64Bytes, Entity>(
                    authoring.Carriers?.Length ?? 0, 
                    Allocator.Temp);

                // Bake carriers first and store their entities
                BakeCarriers(authoring);
                
                // Then bake vessels (which reference carriers)
                BakeMiningVessels(authoring);
                
                // Finally bake asteroids
                BakeAsteroids(authoring);

                _carrierEntityMap.Dispose();
                
#if UNITY_EDITOR
                if (!s_loggedComplete)
                {
                    Debug.Log($"[Space4XMiningDemoAuthoring.Baker] Bake complete.");
                    s_loggedComplete = true;
                }
#endif
            }

            private void AddVisualConfig(Space4XMiningDemoAuthoring authoring)
            {
                var configEntity = GetEntity(TransformUsageFlags.None);
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
                    Debug.Log($"[Space4XMiningDemoAuthoring.Baker] Added Space4XMiningVisualConfig singleton to entity {configEntity.Index}");
                    s_loggedVisual = true;
                }
#endif
            }

            private void BakeCarriers(Space4XMiningDemoAuthoring authoring)
            {
                if (authoring.Carriers == null || authoring.Carriers.Length == 0)
                {
                    return;
                }

                var visuals = authoring.visuals;

                foreach (var carrier in authoring.Carriers)
                {
                    if (string.IsNullOrWhiteSpace(carrier.CarrierId))
                    {
                        Debug.LogWarning($"Carrier definition has empty CarrierId, skipping.");
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(carrier.Position, quaternion.identity, 1f));
                    AddComponent<SpatialIndexedTag>(entity);
                    
                    var carrierIdBytes = new FixedString64Bytes(carrier.CarrierId);
                    
                    AddComponent(entity, new Carrier
                    {
                        CarrierId = carrierIdBytes,
                        AffiliationEntity = Entity.Null, // Can be set later if affiliations are used
                        Speed = math.max(0.1f, carrier.Speed),
                        PatrolCenter = carrier.PatrolCenter,
                        PatrolRadius = math.max(1f, carrier.PatrolRadius)
                    });

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

                    var carrierBinding = CreatePresentationBinding(
                        visuals.CarrierDescriptorKey,
                        visuals.CarrierScale,
                        visuals.CarrierColor,
                        (uint)math.hash(new float4(carrier.Position, carrier.Speed)),
                        $"carrier '{carrier.CarrierId}'");
                    if (carrierBinding.HasValue)
                    {
                        AddComponent(entity, carrierBinding.Value);
                    }

                    // Store entity in map for vessel references
                    _carrierEntityMap.TryAdd(carrierIdBytes, entity);
#if UNITY_EDITOR
                    if (!s_loggedCarriers)
                    {
                        Debug.Log($"[Space4XMiningDemoAuthoring.Baker] Created carrier entity: {carrier.CarrierId} at position {carrier.Position}, Entity={entity.Index}");
                        s_loggedCarriers = true;
                    }
#endif
                }
            }

            private void BakeMiningVessels(Space4XMiningDemoAuthoring authoring)
            {
                if (authoring.MiningVessels == null || authoring.MiningVessels.Length == 0)
                {
                    return;
                }

                var visuals = authoring.visuals;

                foreach (var vessel in authoring.MiningVessels)
                {
                    if (string.IsNullOrWhiteSpace(vessel.VesselId))
                    {
                        Debug.LogWarning($"Mining vessel definition has empty VesselId, skipping.");
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(vessel.Position, quaternion.identity, 1f));
                    AddComponent<SpatialIndexedTag>(entity);

                    // Find the carrier entity
                    Entity carrierEntity = Entity.Null;
                    if (!string.IsNullOrWhiteSpace(vessel.CarrierId))
                    {
                        var carrierIdBytes = new FixedString64Bytes(vessel.CarrierId);
                        if (_carrierEntityMap.TryGetValue(carrierIdBytes, out var foundCarrier))
                        {
                            carrierEntity = foundCarrier;
                        }
                        else
                        {
                            Debug.LogWarning($"Mining vessel '{vessel.VesselId}' references carrier '{vessel.CarrierId}' which doesn't exist. Vessel will not function.");
                        }
                    }

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

                    var vesselBinding = CreatePresentationBinding(
                        visuals.MiningVesselDescriptorKey,
                        visuals.MiningVesselScale,
                        visuals.MiningVesselColor,
                        (uint)math.hash(new float4(vessel.Position, vessel.Speed)),
                        $"mining vessel '{vessel.VesselId}'");
                    if (vesselBinding.HasValue)
                    {
                        AddComponent(entity, vesselBinding.Value);
                    }

                    AddBuffer<SpawnResourceRequest>(entity);
#if UNITY_EDITOR
                    if (!s_loggedVessels)
                    {
                        Debug.Log($"[Space4XMiningDemoAuthoring.Baker] Created mining vessel entity: {vessel.VesselId} at position {vessel.Position}, Entity={entity.Index}, Carrier={carrierEntity.Index}");
                        s_loggedVessels = true;
                    }
#endif
                }
            }

            private void BakeAsteroids(Space4XMiningDemoAuthoring authoring)
            {
                if (authoring.Asteroids == null || authoring.Asteroids.Length == 0)
                {
                    return;
                }

                var visuals = authoring.visuals;

                foreach (var asteroid in authoring.Asteroids)
                {
                    if (string.IsNullOrWhiteSpace(asteroid.AsteroidId))
                    {
                        Debug.LogWarning($"Asteroid definition has empty AsteroidId, skipping.");
                        continue;
                    }

                    var entity = CreateAdditionalEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);
                    AddComponent(entity, LocalTransform.FromPositionRotationScale(asteroid.Position, quaternion.identity, 1f));
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
                        MaxSimultaneousWorkers = maxWorkers,
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

                    var asteroidBinding = CreatePresentationBinding(
                        visuals.AsteroidDescriptorKey,
                        visuals.AsteroidScale,
                        visuals.AsteroidColor,
                        (uint)math.hash(asteroid.Position),
                        $"asteroid '{asteroid.AsteroidId}'");
                    if (asteroidBinding.HasValue)
                    {
                        AddComponent(entity, asteroidBinding.Value);
                    }
#if UNITY_EDITOR
                    if (!s_loggedAsteroids)
                    {
                        Debug.Log($"[Space4XMiningDemoAuthoring.Baker] Created asteroid entity: {asteroid.AsteroidId} at position {asteroid.Position}, Entity={entity.Index}");
                        s_loggedAsteroids = true;
                    }
#endif
                }
            }

            private static float4 ToFloat4(Color color)
            {
                return new float4(color.r, color.g, color.b, color.a);
            }

            private Space4XPresentationBinding? CreatePresentationBinding(string descriptorKey, float scale, Color color, uint variantSeed, string typeName)
            {
                var descriptor = ResolveDescriptor(descriptorKey, typeName);
                if (!descriptor.IsValid)
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"[Space4XMiningDemoAuthoring.Baker] Unable to resolve descriptor for {typeName}. Presentation binding skipped.");
#endif
                    return null;
                }

                var binding = Space4XPresentationBinding.Create(descriptor);
                binding.ScaleMultiplier = math.max(0.05f, scale);
                binding.Tint = ToFloat4(color);
                binding.VariantSeed = variantSeed;
                binding.Flags = Space4XPresentationFlagUtility.WithOverrides(true, true, false);
                return binding;
            }

            private Hash128 ResolveDescriptor(string descriptorKey, string typeName)
            {
                var key = string.IsNullOrWhiteSpace(descriptorKey) ? string.Empty : descriptorKey.Trim();
                if (!string.IsNullOrEmpty(key) && PresentationKeyUtility.TryParseKey(key, out var descriptor, out _))
                {
                    return descriptor;
                }

                const string fallbackKey = "space4x.placeholder";
                if (PresentationKeyUtility.TryParseKey(fallbackKey, out var fallbackDescriptor, out _))
                {
#if UNITY_EDITOR
                    Debug.LogWarning($"[Space4XMiningDemoAuthoring.Baker] Descriptor '{descriptorKey}' for {typeName} is invalid. Falling back to '{fallbackKey}'.");
#endif
                    return fallbackDescriptor;
                }

                return default;
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
