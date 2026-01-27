using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Navigation;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace PureDOTS.Systems.Navigation
{
    /// <summary>
    /// Processes flow field goal requests and marks layers dirty for rebuild.
    /// Note: Runs in InitializationSystemGroup, which executes before SpatialSystemGroup where FlowFieldBuildSystem runs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct FlowFieldRequestSystem : ISystem
    {
        private EntityQuery _goalQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _goalQuery = SystemAPI.QueryBuilder()
                .WithAll<FlowFieldGoalTag, LocalTransform>()
                .WithNone<FlowFieldRequest>()
                .Build();

            state.RequireForUpdate<FlowFieldConfig>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.HasSingleton<FlowFieldConfig>())
            {
                return;
            }

            var configEntity = SystemAPI.GetSingletonEntity<FlowFieldConfig>();
            if (!state.EntityManager.HasBuffer<FlowFieldRequest>(configEntity))
            {
                return;
            }

            var requests = state.EntityManager.GetBuffer<FlowFieldRequest>(configEntity);
            var timeState = SystemAPI.GetSingleton<TimeState>();

            // Add requests for new goals
            foreach (var (goalTag, transform, entity) in SystemAPI.Query<RefRO<FlowFieldGoalTag>, RefRO<LocalTransform>>()
                .WithEntityAccess())
            {
                bool hasRequest = false;
                for (int i = 0; i < requests.Length; i++)
                {
                    if (requests[i].GoalEntity == entity)
                    {
                        hasRequest = true;
                        break;
                    }
                }

                if (!hasRequest)
                {
                    requests.Add(new FlowFieldRequest
                    {
                        GoalEntity = entity,
                        LayerId = goalTag.ValueRO.LayerId,
                        GoalPosition = transform.ValueRO.Position,
                        Priority = goalTag.ValueRO.Priority,
                        ValidityTick = uint.MaxValue, // Never expires
                        IsActive = 1
                    });
                }
            }

            // Mark layers dirty
            if (state.EntityManager.HasBuffer<FlowFieldLayer>(configEntity))
            {
                var layers = state.EntityManager.GetBuffer<FlowFieldLayer>(configEntity);
                for (int i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    for (int j = 0; j < requests.Length; j++)
                    {
                        if (requests[j].LayerId == layer.LayerId && requests[j].IsActive != 0)
                        {
                            layer.IsDirty = 1;
                            layers[i] = layer;
                            break;
                        }
                    }
                }
            }
        }
    }
}


