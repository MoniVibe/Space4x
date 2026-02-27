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
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XPerceptionBootstrapSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

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

            var shipQuery = SystemAPI.QueryBuilder()
                .WithAll<PureDOTS.Runtime.Ships.ShipAggregate, LocalTransform>()
                .Build();
            using var ships = shipQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < ships.Length; i++)
            {
                EnsurePerception(em, ships[i], shipDetectable, shipSignature);
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

            var craftQuery = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftProfile, LocalTransform>()
                .Build();
            using var crafts = craftQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < crafts.Length; i++)
            {
                EnsurePerception(em, crafts[i], craftDetectable, craftSignature);
            }
        }

        private static void EnsurePerception(
            EntityManager entityManager,
            Entity entity,
            in Detectable detectable,
            in SensorSignature signature)
        {
            if (!entityManager.HasComponent<Detectable>(entity))
            {
                TryAddComponent(entityManager, entity, detectable);
            }

            if (!entityManager.HasComponent<SensorSignature>(entity))
            {
                TryAddComponent(entityManager, entity, signature);
            }

            if (!entityManager.HasComponent<MediumContext>(entity))
            {
                TryAddComponent(entityManager, entity, MediumContext.Vacuum);
            }
        }

        private static void TryAddComponent<T>(EntityManager entityManager, Entity entity, in T data)
            where T : unmanaged, IComponentData
        {
            try
            {
                entityManager.AddComponentData(entity, data);
            }
            catch (System.InvalidOperationException)
            {
                // Some entities are already at chunk-size limits; skip optional bootstrap data.
            }
        }
    }
}
