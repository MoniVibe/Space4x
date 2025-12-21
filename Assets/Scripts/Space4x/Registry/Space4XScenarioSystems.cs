using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;
using PureDOTS.Runtime.Spatial;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Space4X.Registry
{
    /// <summary>
    /// Manages carrier patrol behavior, generating waypoints and waiting at them.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct CarrierPatrolSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private Random _random;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _random = new Random(12345u);
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);

            // Use TimeState.FixedDeltaTime for consistency with PureDOTS patterns
            var deltaTime = SystemAPI.TryGetSingleton<TimeState>(out var timeState) 
                ? timeState.FixedDeltaTime 
                : SystemAPI.Time.DeltaTime;

            var carrierQuery = SystemAPI.QueryBuilder()
                .WithAll<Carrier, PatrolBehavior, MovementCommand, LocalTransform>()
                .Build();
            var carrierCount = carrierQuery.CalculateEntityCount();

            if (carrierCount == 0)
            {
                // Only log warning once per session, not every frame
                return;
            }

            foreach (var (carrier, patrol, movement, transform, entity) in SystemAPI.Query<RefRO<Carrier>, RefRW<PatrolBehavior>, RefRW<MovementCommand>, RefRW<LocalTransform>>().WithEntityAccess())
            {
                var carrierData = carrier.ValueRO;
                var position = transform.ValueRO.Position;
                var movementCmd = movement.ValueRO;
                var patrolBehavior = patrol.ValueRO;

                // Initialize waypoint if it's uninitialized (zero vector or very close to current position)
                var waypointInitialized = math.lengthsq(patrolBehavior.CurrentWaypoint) > 0.01f;
                if (!waypointInitialized || math.distance(position, patrolBehavior.CurrentWaypoint) < 0.01f)
                {
                    // Generate initial waypoint
                    var angle = _random.NextFloat(0f, math.PI * 2f);
                    var radius = _random.NextFloat(0f, carrierData.PatrolRadius);
                    var offset = new float3(
                        math.cos(angle) * radius,
                        0f,
                        math.sin(angle) * radius
                    );
                    patrolBehavior.CurrentWaypoint = carrierData.PatrolCenter + offset;
                    patrolBehavior.WaitTimer = 0f;
                    waypointInitialized = true;
                }

                // Check if we've arrived at the current waypoint
                var distanceToWaypoint = math.distance(position, patrolBehavior.CurrentWaypoint);
                var arrivalThreshold = movementCmd.ArrivalThreshold > 0f ? movementCmd.ArrivalThreshold : 1f;

                if (distanceToWaypoint <= arrivalThreshold)
                {
                    // Update wait timer
                    patrolBehavior.WaitTimer += deltaTime;

                    if (patrolBehavior.WaitTimer >= patrolBehavior.WaitTime)
                    {
                        // Generate new waypoint within patrol radius
                        var angle = _random.NextFloat(0f, math.PI * 2f);
                        var radius = _random.NextFloat(0f, carrierData.PatrolRadius);
                        var offset = new float3(
                            math.cos(angle) * radius,
                            0f,
                            math.sin(angle) * radius
                        );
                        var newWaypoint = carrierData.PatrolCenter + offset;

                        patrolBehavior.CurrentWaypoint = newWaypoint;
                        patrolBehavior.WaitTimer = 0f;

                        movement.ValueRW = new MovementCommand
                        {
                            TargetPosition = newWaypoint,
                            ArrivalThreshold = arrivalThreshold
                        };
                    }
                }
                else
                {
                    // Move towards waypoint
                    var toWaypoint = patrolBehavior.CurrentWaypoint - position;
                    var distanceSq = math.lengthsq(toWaypoint);
                    
                    if (distanceSq > 0.0001f) // Safety check to avoid normalizing zero vector
                    {
                        var direction = math.normalize(toWaypoint);
                        var movementSpeed = carrierData.Speed * deltaTime;
                        var newPosition = position + direction * movementSpeed;

                        transform.ValueRW = LocalTransform.FromPositionRotationScale(newPosition, transform.ValueRO.Rotation, transform.ValueRO.Scale);

                        // Update movement command target if needed
                        if (math.distance(position, movementCmd.TargetPosition) > arrivalThreshold * 2f)
                        {
                            movement.ValueRW = new MovementCommand
                            {
                                TargetPosition = patrolBehavior.CurrentWaypoint,
                                ArrivalThreshold = arrivalThreshold
                            };
                        }
                    }
                }

                patrol.ValueRW = patrolBehavior;
            }
        }
    }

    /// <summary>
    /// Manages mining vessel behavior: moving to asteroids, mining, returning to carrier, and transferring resources.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CarrierPatrolSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct MiningVesselSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Asteroid> _asteroidLookup;
        private BufferLookup<ResourceStorage> _resourceStorageLookup;
        private EntityQuery _asteroidQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(false);
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _asteroidLookup = state.GetComponentLookup<Asteroid>(false);
            _resourceStorageLookup = state.GetBufferLookup<ResourceStorage>(false);

            _asteroidQuery = SystemAPI.QueryBuilder()
                .WithAll<Asteroid, LocalTransform>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _transformLookup.Update(ref state);
            _carrierLookup.Update(ref state);
            _asteroidLookup.Update(ref state);
            _resourceStorageLookup.Update(ref state);

            // Use TimeState.FixedDeltaTime for consistency with PureDOTS patterns
            var deltaTime = SystemAPI.TryGetSingleton<TimeState>(out var timeState) 
                ? timeState.FixedDeltaTime 
                : SystemAPI.Time.DeltaTime;

            // Collect available asteroids
            var asteroidList = new NativeList<(Entity entity, float3 position, Asteroid asteroid)>(Allocator.Temp);
            foreach (var (asteroid, transform, entity) in SystemAPI.Query<RefRO<Asteroid>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                if (asteroid.ValueRO.ResourceAmount > 0f)
                {
                    asteroidList.Add((entity, transform.ValueRO.Position, asteroid.ValueRO));
                }
            }

            var vesselQuery = SystemAPI.QueryBuilder()
                .WithAll<MiningVessel, MiningJob, LocalTransform>()
                .WithNone<MiningOrder>()
                .Build();
            var vesselCount = vesselQuery.CalculateEntityCount();

            // Warnings removed - entities will be created when Space4XMiningScenarioAuthoring is properly configured
            if (vesselCount == 0 || asteroidList.Length == 0)
            {
                return;
            }

            foreach (var (vessel, job, transform, entity) in SystemAPI.Query<RefRW<MiningVessel>, RefRW<MiningJob>, RefRW<LocalTransform>>().WithNone<MiningOrder>().WithEntityAccess())
            {
                var vesselData = vessel.ValueRO;
                var jobData = job.ValueRO;
                var position = transform.ValueRO.Position;

                switch (jobData.State)
                {
                    case MiningJobState.None:
                        // Find nearest asteroid
                        Entity? nearestAsteroid = null;
                        float nearestDistance = float.MaxValue;

                        for (int i = 0; i < asteroidList.Length; i++)
                        {
                            var asteroidEntry = asteroidList[i];
                            var distance = math.distance(position, asteroidEntry.position);
                            if (distance < nearestDistance)
                            {
                                nearestDistance = distance;
                                nearestAsteroid = asteroidEntry.entity;
                            }
                        }

                        if (nearestAsteroid.HasValue)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.MovingToAsteroid,
                                TargetAsteroid = nearestAsteroid.Value,
                                MiningProgress = 0f
                            };
                        }
                        break;

                    case MiningJobState.MovingToAsteroid:
                        if (!_asteroidLookup.HasComponent(jobData.TargetAsteroid))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        var asteroidTransform = _transformLookup[jobData.TargetAsteroid];
                        var asteroidPosition = asteroidTransform.Position;
                        var distanceToAsteroid = math.distance(position, asteroidPosition);

                        if (distanceToAsteroid <= 2f)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.Mining,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                        }
                        else
                        {
                            var toAsteroid = asteroidPosition - position;
                            var distanceSq = math.lengthsq(toAsteroid);
                            
                            if (distanceSq > 0.0001f) // Safety check to avoid normalizing zero vector
                            {
                                var direction = math.normalize(toAsteroid);
                                var movementSpeed = vesselData.Speed * deltaTime;
                                var newPosition = position + direction * movementSpeed;
                                transform.ValueRW = LocalTransform.FromPositionRotationScale(newPosition, transform.ValueRO.Rotation, transform.ValueRO.Scale);
                            }
                        }
                        break;

                    case MiningJobState.Mining:
                        if (!_asteroidLookup.HasComponent(jobData.TargetAsteroid))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        var asteroid = _asteroidLookup[jobData.TargetAsteroid];
                        if (asteroid.ResourceAmount <= 0f || vesselData.CurrentCargo >= vesselData.CargoCapacity)
                        {
                            // Start returning to carrier
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.ReturningToCarrier,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                            break;
                        }

                        // Calculate mining rate
                        var miningRate = vesselData.MiningEfficiency * asteroid.MiningRate * deltaTime;
                        var amountToMine = math.min(miningRate, asteroid.ResourceAmount);
                        amountToMine = math.min(amountToMine, vesselData.CargoCapacity - vesselData.CurrentCargo);

                        // Update asteroid resource amount
                        var asteroidRef = _asteroidLookup.GetRefRW(jobData.TargetAsteroid);
                        asteroidRef.ValueRW.ResourceAmount -= amountToMine;

                        // Update vessel cargo
                        vessel.ValueRW.CurrentCargo += amountToMine;

                        // Update mining progress
                        job.ValueRW = new MiningJob
                        {
                            State = MiningJobState.Mining,
                            TargetAsteroid = jobData.TargetAsteroid,
                            MiningProgress = jobData.MiningProgress + miningRate
                        };

                        if (vessel.ValueRO.CurrentCargo >= vessel.ValueRO.CargoCapacity || asteroidRef.ValueRO.ResourceAmount <= 0f)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.ReturningToCarrier,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                        }
                        break;

                    case MiningJobState.ReturningToCarrier:
                        if (!_carrierLookup.HasComponent(vesselData.CarrierEntity))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        var carrierTransform = _transformLookup[vesselData.CarrierEntity];
                        var carrierPosition = carrierTransform.Position;
                        var distanceToCarrier = math.distance(position, carrierPosition);

                        if (distanceToCarrier <= 3f)
                        {
                            job.ValueRW = new MiningJob
                            {
                                State = MiningJobState.TransferringResources,
                                TargetAsteroid = jobData.TargetAsteroid,
                                MiningProgress = 0f
                            };
                        }
                        else
                        {
                            var toCarrier = carrierPosition - position;
                            var distanceSq = math.lengthsq(toCarrier);
                            
                            if (distanceSq > 0.0001f) // Safety check to avoid normalizing zero vector
                            {
                                var direction = math.normalize(toCarrier);
                                var movementSpeed = vesselData.Speed * deltaTime;
                                var newPosition = position + direction * movementSpeed;
                                transform.ValueRW = LocalTransform.FromPositionRotationScale(newPosition, transform.ValueRO.Rotation, transform.ValueRO.Scale);
                            }
                        }
                        break;

                    case MiningJobState.TransferringResources:
                        if (!_carrierLookup.HasComponent(vesselData.CarrierEntity))
                        {
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            break;
                        }

                        if (vessel.ValueRO.CurrentCargo <= 0f)
                        {
                            // Reset and start new mining cycle
                            job.ValueRW = new MiningJob { State = MiningJobState.None };
                            vessel.ValueRW.CurrentCargo = 0f;
                            break;
                        }

                        // Determine resource type from asteroid if available
                        var resourceType = ResourceType.Minerals;
                        if (_asteroidLookup.HasComponent(jobData.TargetAsteroid))
                        {
                            resourceType = _asteroidLookup[jobData.TargetAsteroid].ResourceType;
                        }

                        // Transfer resources to carrier's ResourceStorage buffer
                        if (_resourceStorageLookup.HasBuffer(vesselData.CarrierEntity))
                        {
                            var resourceBuffer = _resourceStorageLookup[vesselData.CarrierEntity];
                            var cargoToTransfer = vessel.ValueRO.CurrentCargo;

                            // Find or create resource storage slot for this type
                            bool foundSlot = false;
                            for (int i = 0; i < resourceBuffer.Length; i++)
                            {
                                if (resourceBuffer[i].Type == resourceType)
                                {
                                    var storage = resourceBuffer[i];
                                    var remaining = storage.AddAmount(cargoToTransfer);
                                    resourceBuffer[i] = storage;

                                    // Update vessel cargo
                                    vessel.ValueRW.CurrentCargo = remaining;
                                    foundSlot = true;
                                    break;
                                }
                            }

                            if (!foundSlot && resourceBuffer.Length < 4)
                            {
                                var newStorage = ResourceStorage.Create(resourceType);
                                var remaining = newStorage.AddAmount(cargoToTransfer);
                                resourceBuffer.Add(newStorage);

                                vessel.ValueRW.CurrentCargo = remaining;
                            }

                            if (vessel.ValueRO.CurrentCargo <= 0f)
                            {
                                job.ValueRW = new MiningJob { State = MiningJobState.None };
                            }
                        }
                        break;
                }
            }

            asteroidList.Dispose();
        }
    }
}

