using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Knowledge;
using PureDOTS.Runtime.Physics;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics.Systems;

namespace PureDOTS.Systems.Knowledge
{
    /// <summary>
    /// Emits incident learning events when agents collide with tree entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AfterPhysicsSystemGroup))]
    [UpdateAfter(typeof(PureDOTS.Systems.Physics.PhysicsEventSystem))]
    public partial struct IncidentLearningTreeFallBridgeSystem : ISystem
    {
        private ComponentLookup<TreeTag> _treeLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<IncidentLearningEventBuffer>();

            _treeLookup = state.GetComponentLookup<TreeTag>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            state.Dependency.Complete();

            if (!SystemAPI.TryGetSingletonEntity<IncidentLearningEventBuffer>(out var eventEntity))
            {
                return;
            }

            var events = state.EntityManager.GetBuffer<IncidentLearningEvent>(eventEntity);
            if (!events.IsCreated)
            {
                return;
            }

            _treeLookup.Update(ref state);
            var currentTick = SystemAPI.GetSingleton<TimeState>().Tick;

            foreach (var (collisionEvents, entity) in SystemAPI
                         .Query<DynamicBuffer<PhysicsCollisionEventElement>>()
                         .WithAll<IncidentLearningAgent>()
                         .WithEntityAccess())
            {
                if (collisionEvents.Length == 0)
                {
                    continue;
                }

                for (int i = 0; i < collisionEvents.Length; i++)
                {
                    var collision = collisionEvents[i];
                    var other = collision.OtherEntity;
                    if (!_treeLookup.HasComponent(other))
                    {
                        continue;
                    }

                    var isTrigger = collision.EventType == PhysicsCollisionEventType.TriggerEnter;
                    var category = isTrigger
                        ? IncidentLearningCategories.TreeFallNearMiss
                        : IncidentLearningCategories.TreeFall;
                    var kind = isTrigger ? IncidentLearningKind.NearMiss : IncidentLearningKind.Hit;
                    var severity = ResolveSeverity(collision);

                    events.Add(new IncidentLearningEvent
                    {
                        Target = entity,
                        Source = other,
                        Position = collision.ContactPoint,
                        CategoryId = category,
                        Severity = severity,
                        Kind = kind,
                        Tick = collision.Tick != 0u ? collision.Tick : currentTick
                    });
                }
            }
        }

        private static float ResolveSeverity(in PhysicsCollisionEventElement collision)
        {
            if (collision.EventType == PhysicsCollisionEventType.TriggerEnter)
            {
                return 0.05f;
            }

            return math.saturate(collision.Impulse * 0.02f);
        }
    }
}
