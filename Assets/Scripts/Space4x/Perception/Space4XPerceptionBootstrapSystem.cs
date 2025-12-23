using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Perception;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Space4X.Perception
{
    /// <summary>
    /// Seeds perception-related components for core Space4X entities (ships + strike craft).
    /// Keeps all authoring optional; headless scenarios still get valid SensorSignature/MediumContext.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XPerceptionBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            var shipDetectable = new Detectable
            {
                Visibility = 0.8f,
                Audibility = 0f,
                ThreatLevel = 0,
                Category = DetectableCategory.Structure
            };
            var shipSignature = new SensorSignature
            {
                VisualSignature = 0.4f,
                AuditorySignature = 0f,
                OlfactorySignature = 0f,
                EMSignature = 0.9f,
                GraviticSignature = 0.8f,
                ExoticSignature = 0.1f,
                ParanormalSignature = 0f
            };

            foreach (var (_, _, entity) in SystemAPI.Query<RefRO<PureDOTS.Runtime.Ships.ShipAggregate>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                EnsurePerception(ref ecb, em, entity, shipDetectable, shipSignature);
            }

            var craftDetectable = new Detectable
            {
                Visibility = 0.6f,
                Audibility = 0f,
                ThreatLevel = 0,
                Category = DetectableCategory.Neutral
            };
            var craftSignature = new SensorSignature
            {
                VisualSignature = 0.5f,
                AuditorySignature = 0f,
                OlfactorySignature = 0f,
                EMSignature = 0.7f,
                GraviticSignature = 0.3f,
                ExoticSignature = 0f,
                ParanormalSignature = 0f
            };

            foreach (var (_, _, entity) in SystemAPI.Query<RefRO<StrikeCraftProfile>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                EnsurePerception(ref ecb, em, entity, craftDetectable, craftSignature);

                // Ensure strike craft can sense/track too (optional authoring).
                if (!em.HasComponent<SenseCapability>(entity))
                {
                    ecb.AddComponent(entity, new SenseCapability
                    {
                        EnabledChannels = PerceptionChannel.EM | PerceptionChannel.Gravitic,
                        Range = 350f,
                        FieldOfView = 360f,
                        Acuity = 1f,
                        UpdateInterval = 0.25f,
                        MaxTrackedTargets = 12,
                        Flags = 0
                    });
                    ecb.AddBuffer<SenseOrganState>(entity);
                }

                if (!em.HasBuffer<PerceivedEntity>(entity))
                {
                    ecb.AddBuffer<PerceivedEntity>(entity);
                }

                if (!em.HasComponent<PerceptionState>(entity))
                {
                    ecb.AddComponent<PerceptionState>(entity);
                }

                if (!em.HasComponent<SignalPerceptionState>(entity))
                {
                    ecb.AddComponent<SignalPerceptionState>(entity);
                }
            }

            ecb.Playback(em);
            ecb.Dispose();
        }

        private static void EnsurePerception(
            ref EntityCommandBuffer ecb,
            EntityManager entityManager,
            Entity entity,
            in Detectable detectable,
            in SensorSignature signature)
        {
            if (!entityManager.HasComponent<Detectable>(entity))
            {
                ecb.AddComponent(entity, detectable);
            }

            if (!entityManager.HasComponent<SensorSignature>(entity))
            {
                ecb.AddComponent(entity, signature);
            }

            if (!entityManager.HasComponent<MediumContext>(entity))
            {
                ecb.AddComponent(entity, MediumContext.Vacuum);
            }
        }
    }
}


