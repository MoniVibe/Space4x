using PureDOTS.Runtime.Components;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Orbitals
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XReferenceFrameScenarioBootstrapSystem))]
    public partial struct Space4XFrameMembershipBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XReferenceFrameStarSystemTag>(out var systemFrame))
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (transform, entity) in SystemAPI.Query<RefRO<LocalTransform>>()
                         .WithAll<VesselMovement>()
                         .WithNone<Space4XFrameMembership, Prefab>()
                         .WithEntityAccess())
            {
                var position = transform.ValueRO.Position;
                ecb.AddComponent(entity, new Space4XFrameMembership
                {
                    Frame = systemFrame,
                    LocalPosition = position,
                    LocalVelocity = float3.zero
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XReferenceFrameBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            if (SystemAPI.TryGetSingletonEntity<Space4XReferenceFrameRootTag>(out _))
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            var root = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(root, new Space4XReferenceFrameRootTag());
            state.EntityManager.AddComponentData(root, new Space4XReferenceFrame
            {
                Kind = Space4XReferenceFrameKind.Galaxy,
                ParentFrame = Entity.Null,
                IsOnRails = 1,
                EpochTick = timeState.Tick,
                PositionInParent = double3.zero,
                VelocityInParent = double3.zero
            });
            state.EntityManager.AddComponentData(root, new Space4XFrameTransform
            {
                PositionWorld = double3.zero,
                VelocityWorld = double3.zero,
                UpdatedTick = timeState.Tick
            });
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XReferenceFrameBootstrapSystem))]
    public partial struct Space4XReferenceFrameScenarioBootstrapSystem : ISystem
    {
        private bool _initialized;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (_initialized)
            {
                state.Enabled = false;
                return;
            }

            if (!SystemAPI.TryGetSingleton<Space4XReferenceFrameConfig>(out var config) || config.Enabled == 0)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<Space4XReferenceFrameRootTag>(out var root))
            {
                return;
            }

            if (SystemAPI.QueryBuilder().WithAll<Space4XReferenceFrameStarSystemTag>().Build().CalculateEntityCount() > 0)
            {
                _initialized = true;
                state.Enabled = false;
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();

            var systemFrame = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(systemFrame, new Space4XReferenceFrame
            {
                Kind = Space4XReferenceFrameKind.StarSystem,
                ParentFrame = root,
                IsOnRails = 0,
                EpochTick = timeState.Tick,
                PositionInParent = double3.zero,
                VelocityInParent = double3.zero
            });
            state.EntityManager.AddComponentData(systemFrame, new Space4XReferenceFrameStarSystemTag());
            state.EntityManager.AddComponentData(systemFrame, new Space4XFrameTransform
            {
                PositionWorld = double3.zero,
                VelocityWorld = double3.zero,
                UpdatedTick = timeState.Tick
            });

            var planetFrame = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(planetFrame, new Space4XReferenceFrame
            {
                Kind = Space4XReferenceFrameKind.Planet,
                ParentFrame = systemFrame,
                IsOnRails = 1,
                EpochTick = timeState.Tick,
                PositionInParent = double3.zero,
                VelocityInParent = double3.zero
            });
            state.EntityManager.AddComponentData(planetFrame, new Space4XReferenceFramePlanetTag());
            state.EntityManager.AddComponentData(planetFrame, new Space4XOrbitalElements
            {
                SemiMajorAxis = 200.0,
                Eccentricity = 0.0,
                Inclination = 0.0,
                LongitudeOfAscendingNode = 0.0,
                ArgumentOfPeriapsis = 0.0,
                MeanAnomalyAtEpoch = 0.0,
                Mu = 80000.0,
                EpochTick = timeState.Tick
            });
            state.EntityManager.AddComponentData(planetFrame, new Space4XSOIRegion
            {
                EnterRadius = 80.0,
                ExitRadius = 100.0
            });
            state.EntityManager.AddComponentData(planetFrame, new Space4XFrameTransform
            {
                PositionWorld = new double3(200.0, 0.0, 0.0),
                VelocityWorld = double3.zero,
                UpdatedTick = timeState.Tick
            });

            var probe = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(probe, new Space4XReferenceFrameProbeTag());
            state.EntityManager.AddComponentData(probe, new Space4XFrameMotionTag());
            state.EntityManager.AddComponentData(probe, new Space4XFrameDrivenTransformTag());
            state.EntityManager.AddComponentData(probe, new Space4XFrameMembership
            {
                Frame = systemFrame,
                LocalPosition = new float3(240f, 0f, 0f),
                LocalVelocity = float3.zero
            });
            state.EntityManager.AddComponentData(probe, LocalTransform.FromPosition(new float3(240f, 0f, 0f)));

            _initialized = true;
            state.Enabled = false;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.GameplaySystemGroup))]
    public partial struct Space4XOrbitalEphemerisSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var deltaTime = timeState.FixedDeltaTime > 0f ? timeState.FixedDeltaTime : timeState.DeltaTime;
            var deltaSeconds = (double)deltaTime;

            foreach (var (frame, elements) in SystemAPI.Query<RefRW<Space4XReferenceFrame>, RefRO<Space4XOrbitalElements>>())
            {
                if (frame.ValueRO.IsOnRails == 0)
                {
                    continue;
                }

                var a = math.max(0.0, elements.ValueRO.SemiMajorAxis);
                if (a <= 0.0)
                {
                    continue;
                }

                var eccentricity = math.clamp(elements.ValueRO.Eccentricity, 0.0, 0.999999);
                var mu = elements.ValueRO.Mu > 0.0 ? elements.ValueRO.Mu : 1.0;
                var meanMotion = math.sqrt(mu / (a * a * a));
                var elapsedTicks = currentTick >= elements.ValueRO.EpochTick
                    ? currentTick - elements.ValueRO.EpochTick
                    : 0u;
                var meanAnomaly = elements.ValueRO.MeanAnomalyAtEpoch + meanMotion * (elapsedTicks * deltaSeconds);

                meanAnomaly = WrapAngle(meanAnomaly);

                var eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, eccentricity);
                var cosE = math.cos(eccentricAnomaly);
                var sinE = math.sin(eccentricAnomaly);
                var oneMinusECosE = 1.0 - eccentricity * cosE;
                if (oneMinusECosE <= 0.0)
                {
                    continue;
                }

                var radius = a * oneMinusECosE;
                var cosNu = (cosE - eccentricity) / oneMinusECosE;
                var sinNu = (math.sqrt(1.0 - eccentricity * eccentricity) * sinE) / oneMinusECosE;

                var positionPf = new double3(radius * cosNu, radius * sinNu, 0.0);

                var velocityScale = math.sqrt(mu * a) / radius;
                var velocityPf = new double3(
                    -velocityScale * sinE,
                    velocityScale * math.sqrt(1.0 - eccentricity * eccentricity) * cosE,
                    0.0);

                RotateToWorld(positionPf, velocityPf, elements.ValueRO, out var position, out var velocity);

                frame.ValueRW.PositionInParent = position;
                frame.ValueRW.VelocityInParent = velocity;
            }
        }

        private static double WrapAngle(double angle)
        {
            var twoPi = math.PI * 2.0;
            var wrapped = angle - twoPi * math.floor(angle / twoPi);
            if (wrapped > math.PI)
            {
                wrapped -= twoPi;
            }
            return wrapped;
        }

        private static double SolveEccentricAnomaly(double meanAnomaly, double eccentricity)
        {
            var eccentricAnomaly = eccentricity > 0.8 ? math.PI : meanAnomaly;
            for (var i = 0; i < 12; i++)
            {
                var sinE = math.sin(eccentricAnomaly);
                var cosE = math.cos(eccentricAnomaly);
                var f = eccentricAnomaly - eccentricity * sinE - meanAnomaly;
                var fPrime = 1.0 - eccentricity * cosE;
                if (math.abs(fPrime) < 1e-10)
                {
                    break;
                }
                var delta = f / fPrime;
                eccentricAnomaly -= delta;
                if (math.abs(delta) < 1e-10)
                {
                    break;
                }
            }
            return eccentricAnomaly;
        }

        private static void RotateToWorld(
            double3 positionPf,
            double3 velocityPf,
            in Space4XOrbitalElements elements,
            out double3 position,
            out double3 velocity)
        {
            var cosO = math.cos(elements.LongitudeOfAscendingNode);
            var sinO = math.sin(elements.LongitudeOfAscendingNode);
            var cosI = math.cos(elements.Inclination);
            var sinI = math.sin(elements.Inclination);
            var cosW = math.cos(elements.ArgumentOfPeriapsis);
            var sinW = math.sin(elements.ArgumentOfPeriapsis);

            var m11 = cosO * cosW - sinO * sinW * cosI;
            var m12 = -cosO * sinW - sinO * cosW * cosI;
            var m13 = sinO * sinI;

            var m21 = sinO * cosW + cosO * sinW * cosI;
            var m22 = -sinO * sinW + cosO * cosW * cosI;
            var m23 = -cosO * sinI;

            var m31 = sinW * sinI;
            var m32 = cosW * sinI;
            var m33 = cosI;

            position = new double3(
                m11 * positionPf.x + m12 * positionPf.y + m13 * positionPf.z,
                m21 * positionPf.x + m22 * positionPf.y + m23 * positionPf.z,
                m31 * positionPf.x + m32 * positionPf.y + m33 * positionPf.z);

            velocity = new double3(
                m11 * velocityPf.x + m12 * velocityPf.y + m13 * velocityPf.z,
                m21 * velocityPf.x + m22 * velocityPf.y + m23 * velocityPf.z,
                m31 * velocityPf.x + m32 * velocityPf.y + m33 * velocityPf.z);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XSOITransitionSystem))]
    public partial struct Space4XFrameMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var deltaTime = timeState.FixedDeltaTime > 0f ? timeState.FixedDeltaTime : timeState.DeltaTime;

            foreach (var membership in SystemAPI.Query<RefRW<Space4XFrameMembership>>().WithAll<Space4XFrameMotionTag>())
            {
                var position = membership.ValueRO.LocalPosition;
                var velocity = membership.ValueRO.LocalVelocity;
                position += velocity * deltaTime;
                membership.ValueRW.LocalPosition = position;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XOrbitalEphemerisSystem))]
    public partial struct Space4XFrameTransformSystem : ISystem
    {
        private ComponentLookup<Space4XFrameTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            _transformLookup.Update(ref state);

            foreach (var (frame, transform) in SystemAPI
                         .Query<RefRO<Space4XReferenceFrame>, RefRW<Space4XFrameTransform>>())
            {
                var parentPosition = double3.zero;
                var parentVelocity = double3.zero;
                var parent = frame.ValueRO.ParentFrame;
                if (parent != Entity.Null && _transformLookup.HasComponent(parent))
                {
                    var parentTransform = _transformLookup[parent];
                    parentPosition = parentTransform.PositionWorld;
                    parentVelocity = parentTransform.VelocityWorld;
                }

                transform.ValueRW.PositionWorld = parentPosition + frame.ValueRO.PositionInParent;
                transform.ValueRW.VelocityWorld = parentVelocity + frame.ValueRO.VelocityInParent;
                transform.ValueRW.UpdatedTick = timeState.Tick;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFrameTransformSystem))]
    public partial struct Space4XSOITransitionSystem : ISystem
    {
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<TimeState>();
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            _frameTransformLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var soiFrames = new NativeList<SoiFrameInfo>(Allocator.Temp);

            foreach (var (frame, transform, soi, entity) in SystemAPI
                         .Query<RefRO<Space4XReferenceFrame>, RefRO<Space4XFrameTransform>, RefRO<Space4XSOIRegion>>()
                         .WithEntityAccess())
            {
                var enterRadius = soi.ValueRO.EnterRadius;
                var exitRadius = soi.ValueRO.ExitRadius > 0.0 ? soi.ValueRO.ExitRadius : enterRadius;
                if (config.EnterSOIMultiplier > 0f)
                {
                    enterRadius *= config.EnterSOIMultiplier;
                }
                if (config.ExitSOIMultiplier > 0f)
                {
                    exitRadius *= config.ExitSOIMultiplier;
                }

                soiFrames.Add(new SoiFrameInfo
                {
                    Frame = entity,
                    Parent = frame.ValueRO.ParentFrame,
                    PositionWorld = transform.ValueRO.PositionWorld,
                    EnterRadius = enterRadius,
                    ExitRadius = exitRadius
                });
            }

            if (soiFrames.Length == 0)
            {
                soiFrames.Dispose();
                ecb.Dispose();
                return;
            }

            foreach (var (membership, entity) in SystemAPI.Query<RefRO<Space4XFrameMembership>>().WithEntityAccess())
            {
                var hasTransition = SystemAPI.HasComponent<Space4XFrameTransition>(entity);
                if (hasTransition)
                {
                    var transition = SystemAPI.GetComponent<Space4XFrameTransition>(entity);
                    if (transition.Pending != 0)
                    {
                        continue;
                    }
                }

                var currentFrame = membership.ValueRO.Frame;
                if (currentFrame == Entity.Null || !_frameTransformLookup.HasComponent(currentFrame))
                {
                    continue;
                }

                var frameTransform = _frameTransformLookup[currentFrame];
                var worldPosition = frameTransform.PositionWorld + new double3(
                    membership.ValueRO.LocalPosition.x,
                    membership.ValueRO.LocalPosition.y,
                    membership.ValueRO.LocalPosition.z);
                var worldVelocity = frameTransform.VelocityWorld + new double3(
                    membership.ValueRO.LocalVelocity.x,
                    membership.ValueRO.LocalVelocity.y,
                    membership.ValueRO.LocalVelocity.z);

                if (TryResolveCurrentSoi(currentFrame, soiFrames.AsArray(), out var currentSoi))
                {
                    var distance = math.length(worldPosition - currentSoi.PositionWorld);
                    if (distance > currentSoi.ExitRadius && currentSoi.Parent != Entity.Null)
                    {
                        QueueTransition(entity, currentFrame, currentSoi.Parent, worldPosition, worldVelocity, timeState.Tick, hasTransition, ref ecb);
                        continue;
                    }
                }

                var bestIndex = -1;
                var bestDistance = double.MaxValue;
                for (var i = 0; i < soiFrames.Length; i++)
                {
                    if (soiFrames[i].Parent != currentFrame)
                    {
                        continue;
                    }

                    var distance = math.length(worldPosition - soiFrames[i].PositionWorld);
                    if (distance <= soiFrames[i].EnterRadius && distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestIndex = i;
                    }
                }

                if (bestIndex >= 0)
                {
                    var target = soiFrames[bestIndex];
                    QueueTransition(entity, currentFrame, target.Frame, worldPosition, worldVelocity, timeState.Tick, hasTransition, ref ecb);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
            soiFrames.Dispose();
        }

        private static bool TryResolveCurrentSoi(
            Entity frame,
            NativeArray<SoiFrameInfo> soiFrames,
            out SoiFrameInfo info)
        {
            for (var i = 0; i < soiFrames.Length; i++)
            {
                if (soiFrames[i].Frame == frame)
                {
                    info = soiFrames[i];
                    return true;
                }
            }

            info = default;
            return false;
        }

        private static void QueueTransition(
            Entity entity,
            Entity fromFrame,
            Entity toFrame,
            double3 worldPosition,
            double3 worldVelocity,
            uint tick,
            bool hasTransition,
            ref EntityCommandBuffer ecb)
        {
            var transition = new Space4XFrameTransition
            {
                FromFrame = fromFrame,
                ToFrame = toFrame,
                WorldPosition = worldPosition,
                WorldVelocity = worldVelocity,
                TransitionTick = tick,
                Pending = 1
            };

            if (hasTransition)
            {
                ecb.SetComponent(entity, transition);
            }
            else
            {
                ecb.AddComponent(entity, transition);
            }
        }

        private struct SoiFrameInfo
        {
            public Entity Frame;
            public Entity Parent;
            public double3 PositionWorld;
            public double EnterRadius;
            public double ExitRadius;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XSOITransitionSystem))]
    public partial struct Space4XFrameMembershipSyncSystem : ISystem
    {
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            state.RequireForUpdate<TimeState>();
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            _frameTransformLookup.Update(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var processedCount = 0;

            foreach (var (transition, entity) in SystemAPI.Query<RefRO<Space4XFrameTransition>>().WithEntityAccess())
            {
                if (transition.ValueRO.Pending == 0)
                {
                    continue;
                }

                var toFrame = transition.ValueRO.ToFrame;
                if (toFrame == Entity.Null || !_frameTransformLookup.HasComponent(toFrame))
                {
                    continue;
                }

                var frameTransform = _frameTransformLookup[toFrame];
                var localPosition = transition.ValueRO.WorldPosition - frameTransform.PositionWorld;
                var localVelocity = transition.ValueRO.WorldVelocity - frameTransform.VelocityWorld;

                var membership = new Space4XFrameMembership
                {
                    Frame = toFrame,
                    LocalPosition = new float3((float)localPosition.x, (float)localPosition.y, (float)localPosition.z),
                    LocalVelocity = new float3((float)localVelocity.x, (float)localVelocity.y, (float)localVelocity.z)
                };

                if (SystemAPI.HasComponent<Space4XFrameMembership>(entity))
                {
                    ecb.SetComponent(entity, membership);
                }
                else
                {
                    ecb.AddComponent(entity, membership);
                }

                ecb.RemoveComponent<Space4XFrameTransition>(entity);
                processedCount++;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();

            if (!SystemAPI.TryGetSingletonEntity<Space4XFrameTransitionMetrics>(out var entity))
            {
                entity = state.EntityManager.CreateEntity(typeof(Space4XFrameTransitionMetrics));
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            state.EntityManager.SetComponentData(entity, new Space4XFrameTransitionMetrics
            {
                Tick = timeState.Tick,
                ProcessedCount = processedCount
            });
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFrameMembershipSyncSystem))]
    [UpdateBefore(typeof(Space4XFrameLocalTransformSystem))]
    public partial struct Space4XFrameMembershipFromTransformSystem : ISystem
    {
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;
        private ComponentLookup<VesselMovement> _movementLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
            _movementLookup = state.GetComponentLookup<VesselMovement>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            _frameTransformLookup.Update(ref state);
            _movementLookup.Update(ref state);

            foreach (var (membership, transform, entity) in SystemAPI
                         .Query<RefRW<Space4XFrameMembership>, RefRO<LocalTransform>>()
                         .WithNone<Space4XFrameDrivenTransformTag>()
                         .WithEntityAccess())
            {
                var frame = membership.ValueRO.Frame;
                if (frame == Entity.Null || !_frameTransformLookup.HasComponent(frame))
                {
                    continue;
                }

                var frameTransform = _frameTransformLookup[frame];
                var worldPosition = transform.ValueRO.Position;
                var localPosition = new double3(worldPosition.x, worldPosition.y, worldPosition.z) - frameTransform.PositionWorld;

                var worldVelocity = float3.zero;
                if (_movementLookup.HasComponent(entity))
                {
                    worldVelocity = _movementLookup[entity].Velocity;
                }

                var localVelocity = new double3(worldVelocity.x, worldVelocity.y, worldVelocity.z) - frameTransform.VelocityWorld;

                membership.ValueRW.LocalPosition = new float3((float)localPosition.x, (float)localPosition.y, (float)localPosition.z);
                membership.ValueRW.LocalVelocity = new float3((float)localVelocity.x, (float)localVelocity.y, (float)localVelocity.z);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XFrameMembershipSyncSystem))]
    public partial struct Space4XFrameLocalTransformSystem : ISystem
    {
        private ComponentLookup<Space4XFrameTransform> _frameTransformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XReferenceFrameConfig>();
            _frameTransformLookup = state.GetComponentLookup<Space4XFrameTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XReferenceFrameConfig>();
            if (config.Enabled == 0)
            {
                return;
            }

            _frameTransformLookup.Update(ref state);

            foreach (var (membership, transform) in SystemAPI
                         .Query<RefRO<Space4XFrameMembership>, RefRW<LocalTransform>>()
                         .WithAll<Space4XFrameDrivenTransformTag>())
            {
                var frame = membership.ValueRO.Frame;
                if (frame == Entity.Null || !_frameTransformLookup.HasComponent(frame))
                {
                    continue;
                }

                var frameTransform = _frameTransformLookup[frame];
                var worldPosition = frameTransform.PositionWorld + new double3(
                    membership.ValueRO.LocalPosition.x,
                    membership.ValueRO.LocalPosition.y,
                    membership.ValueRO.LocalPosition.z);

                var updated = transform.ValueRO;
                updated.Position = new float3((float)worldPosition.x, (float)worldPosition.y, (float)worldPosition.z);
                transform.ValueRW = updated;
            }
        }
    }
}
