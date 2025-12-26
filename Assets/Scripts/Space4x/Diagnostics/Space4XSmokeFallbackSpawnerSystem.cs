#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using PureDOTS.Rendering;
using PureDOTS.Runtime.Spatial;
using Space4X.Presentation;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;
using UnityTime = UnityEngine.Time;

namespace Space4X.Diagnostics
{
    /// <summary>
    /// Dev-only diagnostic that detects when the smoke SubScene fails to load or produces no entities.
    /// Logs errors instead of spawning fallback entities (per "no illusions" rule: presentation must reflect headless progress only).
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XSmokeFallbackSpawnerSystem : ISystem
    {
        private bool _spawned;
        private bool _deadlineInitialized;
        private double _fallbackDeadline;

        public void OnCreate(ref SystemState state)
        {
            // Always run in dev builds; use realtime so we do not depend on a TimeState singleton.
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_spawned)
            {
                state.Enabled = false;
                return;
            }

            var worldName = state.WorldUnmanaged.Name.ToString();
            if (!string.Equals(worldName, "Game World", StringComparison.Ordinal))
            {
                state.Enabled = false;
                return;
            }

            if (!_deadlineInitialized)
            {
                _fallbackDeadline = UnityTime.realtimeSinceStartup + 5.0; // give SubScene streaming a chance to finish
                _deadlineInitialized = true;
            }

            if (UnityTime.realtimeSinceStartup < _fallbackDeadline)
            {
                return;
            }

            var em = state.EntityManager;
            var carrierCount = Count<Carrier>(em);
            var miningCount = Count<MiningVessel>(em);
            var asteroidCount = Count<Asteroid>(em);

            if (carrierCount > 0 || miningCount > 0 || asteroidCount > 0)
            {
                state.Enabled = false;
                return;
            }

            // At this point no gameplay entities exist. As a final check, ensure the SubScene never resolved.
            var sectionAvailable = TryCountComponent(em, "Unity.Scenes.ResolvedSectionEntity, Unity.Scenes", out var sectionCount);
            if (sectionAvailable && sectionCount > 0)
            {
                UnityDebug.LogError("[Space4XSmokeFallbackSpawner] PARITY VIOLATION: Game World has no mining entities even though a SubScene section resolved. This indicates the scenario JSON or spawn systems are not producing entities. Presentation cannot show what headless does not simulate. Check headless telemetry/logs.");
            }
            else
            {
                UnityDebug.LogError("[Space4XSmokeFallbackSpawner] PARITY VIOLATION: SubScene never resolved. The smoke scene requires the same SubScene as headless. No fallback entities will be spawned - presentation must reflect headless progress only.");
            }

            // Do NOT spawn fallback entities - this violates the "no illusions" rule.
            // If entities are missing, fix the scenario/spawn systems, don't fake them in presentation.
            _spawned = true;
            state.Enabled = false;
        }

        private static int Count<T>(EntityManager em) where T : IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            if (query.IsEmptyIgnoreFilter)
            {
                return 0;
            }

            return query.CalculateEntityCount();
        }

        private static bool TryCountComponent(EntityManager em, string assemblyQualifiedName, out int count)
        {
            var componentType = Type.GetType(assemblyQualifiedName);
            if (componentType == null)
            {
                count = 0;
                return false;
            }

            using var query = em.CreateEntityQuery(ComponentType.ReadOnly(componentType));
            if (query.IsEmptyIgnoreFilter)
            {
                count = 0;
                return true;
            }

            count = query.CalculateEntityCount();
            return true;
        }

        private static void SpawnFallbackSimulation(EntityManager em)
        {
            var carrier = SpawnCarrier(em);
            var asteroid = SpawnAsteroid(em);
            SpawnMiner(em, carrier, asteroid);
        }

        private static Entity SpawnCarrier(EntityManager em)
        {
            var carrier = em.CreateEntity();
            var carrierTransform = LocalTransform.FromPositionRotationScale(new float3(0f, 0f, -8f), quaternion.identity, 1f);
            em.AddComponentData(carrier, carrierTransform);
            em.AddComponentData(carrier, new LocalToWorld { Value = float4x4.TRS(carrierTransform.Position, carrierTransform.Rotation, carrierTransform.Scale) });
            em.AddComponent<SpatialIndexedTag>(carrier);
            em.AddComponentData(carrier, new Carrier
            {
                CarrierId = new FixedString64Bytes("FALLBACK-CARRIER"),
                AffiliationEntity = Entity.Null,
                Speed = 5f,
                PatrolCenter = carrierTransform.Position,
                PatrolRadius = 30f
            });

            em.AddComponentData(carrier, new PatrolBehavior
            {
                CurrentWaypoint = float3.zero,
                WaitTime = 1f,
                WaitTimer = 0f
            });

            em.AddComponentData(carrier, new MovementCommand
            {
                TargetPosition = float3.zero,
                ArrivalThreshold = 1f
            });

            em.AddBuffer<ResourceStorage>(carrier);

            AssignRenderComponents(em, carrier, Space4XRenderKeys.Carrier, new float4(0.35f, 0.4f, 0.62f, 1f));
            return carrier;
        }

        private static void SpawnMiner(EntityManager em, Entity carrier, Entity asteroid)
        {
            var miner = em.CreateEntity();
            var minerTransform = LocalTransform.FromPositionRotationScale(new float3(5f, 0f, -2f), quaternion.identity, 1f);
            em.AddComponentData(miner, minerTransform);
            em.AddComponentData(miner, new LocalToWorld { Value = float4x4.TRS(minerTransform.Position, minerTransform.Rotation, minerTransform.Scale) });
            em.AddComponent<SpatialIndexedTag>(miner);

            var resourceId = new FixedString64Bytes("space4x.resource.minerals");

            em.AddComponentData(miner, new MiningVessel
            {
                VesselId = new FixedString64Bytes("FALLBACK-MINER"),
                CarrierEntity = carrier,
                MiningEfficiency = 0.8f,
                Speed = 10f,
                CargoCapacity = 100f,
                CurrentCargo = 0f,
                CargoResourceType = ResourceType.Minerals
            });

            em.AddComponentData(miner, new MiningOrder
            {
                ResourceId = resourceId,
                Source = MiningOrderSource.Scripted,
                Status = MiningOrderStatus.Pending,
                PreferredTarget = asteroid,
                TargetEntity = asteroid,
                IssuedTick = 0
            });

            em.AddComponentData(miner, new MiningState
            {
                Phase = MiningPhase.Idle,
                ActiveTarget = asteroid,
                MiningTimer = 0f,
                TickInterval = 0.5f,
                PhaseTimer = 0f
            });

            em.AddComponentData(miner, new MiningYield
            {
                ResourceId = resourceId,
                PendingAmount = 0f,
                SpawnThreshold = 25f,
                SpawnReady = 0
            });

            em.AddComponentData(miner, new MiningJob
            {
                State = MiningJobState.None,
                TargetAsteroid = asteroid,
                MiningProgress = 0f
            });

            em.AddComponentData(miner, new VesselAIState
            {
                CurrentState = VesselAIState.State.Idle,
                CurrentGoal = VesselAIState.Goal.None,
                TargetEntity = asteroid,
                TargetPosition = float3.zero,
                StateTimer = 0f,
                StateStartTick = 0
            });

            em.AddComponentData(miner, new VesselMovement
            {
                Velocity = float3.zero,
                BaseSpeed = 10f,
                CurrentSpeed = 0f,
                DesiredRotation = quaternion.identity,
                IsMoving = 0,
                LastMoveTick = 0
            });

            em.AddBuffer<SpawnResourceRequest>(miner);

            AssignRenderComponents(em, miner, Space4XRenderKeys.Miner, new float4(0.25f, 0.52f, 0.84f, 1f));
        }

        private static Entity SpawnAsteroid(EntityManager em)
        {
            var asteroid = em.CreateEntity();
            var asteroidTransform = LocalTransform.FromPositionRotationScale(new float3(20f, 0f, 10f), quaternion.identity, 2f);
            em.AddComponentData(asteroid, asteroidTransform);
            em.AddComponentData(asteroid, new LocalToWorld { Value = float4x4.TRS(asteroidTransform.Position, asteroidTransform.Rotation, asteroidTransform.Scale) });
            em.AddComponent<SpatialIndexedTag>(asteroid);

            em.AddComponentData(asteroid, new Asteroid
            {
                AsteroidId = new FixedString64Bytes("FALLBACK-ASTEROID"),
                ResourceType = ResourceType.Minerals,
                ResourceAmount = 500f,
                MaxResourceAmount = 500f,
                MiningRate = 10f
            });

            em.AddComponentData(asteroid, new ResourceTypeId
            {
                Value = new FixedString64Bytes("space4x.resource.minerals")
            });

            em.AddComponentData(asteroid, new ResourceSourceConfig
            {
                GatherRatePerWorker = 10f,
                MaxSimultaneousWorkers = 4,
                RespawnSeconds = 0f,
                Flags = 0
            });

            em.AddComponentData(asteroid, new ResourceSourceState
            {
                UnitsRemaining = 500f,
                LastHarvestTick = 0
            });

            em.AddComponent<PureDOTS.Runtime.Components.RewindableTag>(asteroid);
            em.AddComponentData(asteroid, new PureDOTS.Runtime.Components.LastRecordedTick { Tick = 0 });

            AssignRenderComponents(em, asteroid, Space4XRenderKeys.Asteroid, new float4(0.52f, 0.43f, 0.34f, 1f));
            return asteroid;
        }

        private static void AssignRenderComponents(EntityManager em, Entity entity, ushort semanticKey, in float4 tint)
        {
            em.AddComponentData(entity, new RenderSemanticKey { Value = semanticKey });
            em.AddComponentData(entity, new RenderVariantKey { Value = 0 });
            em.AddComponentData(entity, new RenderFlags { Visible = 1, ShadowCaster = 1, HighlightMask = 0 });

            em.AddComponent<MeshPresenter>(entity);
            em.SetComponentEnabled<MeshPresenter>(entity, true);
            em.AddComponent<SpritePresenter>(entity);
            em.SetComponentEnabled<SpritePresenter>(entity, false);
            em.AddComponent<DebugPresenter>(entity);
            em.SetComponentEnabled<DebugPresenter>(entity, false);

            em.AddComponentData(entity, new RenderThemeOverride { Value = 0 });
            em.SetComponentEnabled<RenderThemeOverride>(entity, false);
            em.AddComponentData(entity, new RenderTint { Value = tint });
            em.AddComponentData(entity, new RenderTexSlice { Value = 0 });
            em.AddComponentData(entity, new RenderUvTransform { Value = new float4(1f, 1f, 0f, 0f) });
        }

    }
}
#endif
