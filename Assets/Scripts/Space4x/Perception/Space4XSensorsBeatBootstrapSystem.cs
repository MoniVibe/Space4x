using PureDOTS.Runtime.AI;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Perception;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Perception
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4x.Scenario.Space4XMiningScenarioSystem))]
    public partial struct Space4XSensorsBeatBootstrapSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XSensorsBeatConfig>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XSensorsBeatConfig>();
            if (config.SensorsEnsured != 0)
            {
                return;
            }

            _carrierLookup.Update(ref state);
            var observer = ResolveCarrier(config.ObserverCarrierId, ref state);
            var target = ResolveCarrier(config.TargetCarrierId, ref state);
            if (observer == Entity.Null || target == Entity.Null)
            {
                return;
            }

            var em = state.EntityManager;
            var range = config.ObserverRange > 0f ? config.ObserverRange : 350f;
            var updateInterval = config.ObserverUpdateInterval > 0f ? config.ObserverUpdateInterval : 0.25f;
            var maxTracked = config.ObserverMaxTrackedTargets > 0 ? config.ObserverMaxTrackedTargets : (byte)12;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            var detectable = new Detectable
            {
                Visibility = 0.8f,
                Audibility = 0f,
                ThreatLevel = 0,
                Category = DetectableCategory.Structure
            };
            var signature = new SensorSignature
            {
                VisualSignature = 0.4f,
                AuditorySignature = 0f,
                OlfactorySignature = 0f,
                EMSignature = 0.9f,
                GraviticSignature = 0.8f,
                ExoticSignature = 0.1f,
                ParanormalSignature = 0f
            };

            EnsurePerception(ref ecb, em, observer, detectable, signature);
            EnsurePerception(ref ecb, em, target, detectable, signature);
            if (!em.HasComponent<SenseCapability>(observer))
            {
                ecb.AddComponent(observer, new SenseCapability
                {
                    EnabledChannels = PerceptionChannel.EM | PerceptionChannel.Gravitic,
                    Range = range,
                    FieldOfView = 360f,
                    Acuity = 1f,
                    UpdateInterval = updateInterval,
                    MaxTrackedTargets = maxTracked,
                    Flags = 0
                });
            }
            else
            {
                var capability = em.GetComponentData<SenseCapability>(observer);
                capability.EnabledChannels |= PerceptionChannel.EM | PerceptionChannel.Gravitic;
                capability.Range = capability.Range < range ? range : capability.Range;
                capability.FieldOfView = capability.FieldOfView < 360f ? 360f : capability.FieldOfView;
                capability.Acuity = capability.Acuity <= 0f ? 1f : capability.Acuity;
                capability.UpdateInterval = updateInterval;
                capability.MaxTrackedTargets = maxTracked;
                ecb.SetComponent(observer, capability);
            }

            if (!em.HasBuffer<SenseOrganState>(observer))
            {
                ecb.AddBuffer<SenseOrganState>(observer);
            }

            if (!em.HasBuffer<PerceivedEntity>(observer))
            {
                ecb.AddBuffer<PerceivedEntity>(observer);
            }

            if (!em.HasComponent<PerceptionState>(observer))
            {
                ecb.AddComponent(observer, new PerceptionState());
            }

            if (!em.HasComponent<SignalPerceptionState>(observer))
            {
                ecb.AddComponent(observer, new SignalPerceptionState());
            }

            ecb.Playback(em);
            ecb.Dispose();

            config.SensorsEnsured = 1;
            SystemAPI.SetSingleton(config);
        }

        private Entity ResolveCarrier(FixedString64Bytes carrierId, ref SystemState state)
        {
            if (carrierId.IsEmpty)
            {
                return Entity.Null;
            }

            foreach (var (carrier, entity) in SystemAPI.Query<RefRO<Carrier>>().WithEntityAccess())
            {
                if (carrier.ValueRO.CarrierId.Equals(carrierId))
                {
                    return entity;
                }
            }

            return Entity.Null;
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
            else
            {
                var current = entityManager.GetComponentData<Detectable>(entity);
                current.Visibility = current.Visibility < detectable.Visibility ? detectable.Visibility : current.Visibility;
                current.Audibility = current.Audibility < detectable.Audibility ? detectable.Audibility : current.Audibility;
                current.ThreatLevel = current.ThreatLevel < detectable.ThreatLevel ? detectable.ThreatLevel : current.ThreatLevel;
                ecb.SetComponent(entity, current);
            }

            if (!entityManager.HasComponent<SensorSignature>(entity))
            {
                ecb.AddComponent(entity, signature);
            }
            else
            {
                var current = entityManager.GetComponentData<SensorSignature>(entity);
                current.VisualSignature = current.VisualSignature < signature.VisualSignature ? signature.VisualSignature : current.VisualSignature;
                current.AuditorySignature = current.AuditorySignature < signature.AuditorySignature ? signature.AuditorySignature : current.AuditorySignature;
                current.OlfactorySignature = current.OlfactorySignature < signature.OlfactorySignature ? signature.OlfactorySignature : current.OlfactorySignature;
                current.EMSignature = current.EMSignature < signature.EMSignature ? signature.EMSignature : current.EMSignature;
                current.GraviticSignature = current.GraviticSignature < signature.GraviticSignature ? signature.GraviticSignature : current.GraviticSignature;
                current.ExoticSignature = current.ExoticSignature < signature.ExoticSignature ? signature.ExoticSignature : current.ExoticSignature;
                current.ParanormalSignature = current.ParanormalSignature < signature.ParanormalSignature ? signature.ParanormalSignature : current.ParanormalSignature;
                ecb.SetComponent(entity, current);
            }

            if (!entityManager.HasComponent<MediumContext>(entity))
            {
                ecb.AddComponent(entity, MediumContext.Vacuum);
            }
        }
    }
}
