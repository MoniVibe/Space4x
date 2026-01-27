// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.Motivation
{
    /// <summary>
    /// Minimal MVP translating ambition → desire → intent → tasks.
    /// </summary>
    [BurstCompile]
    public partial struct AmbitionFlowSystem : ISystem
    {
        ComponentLookup<AmbitionState> _ambitions;
        BufferLookup<DesireElement> _desires;
        ComponentLookup<IntentState> _intents;
        BufferLookup<TaskElement> _tasks;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _ambitions = state.GetComponentLookup<AmbitionState>();
            _desires = state.GetBufferLookup<DesireElement>();
            _intents = state.GetComponentLookup<IntentState>();
            _tasks = state.GetBufferLookup<TaskElement>();
        }

        [BurstCompile] public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _ambitions.Update(ref state);
            _desires.Update(ref state);
            _intents.Update(ref state);
            _tasks.Update(ref state);

            var ambitions = _ambitions;
            var desires = _desires;
            var intents = _intents;
            var tasks = _tasks;

            foreach (var (ambition, entity) in SystemAPI.Query<RefRW<AmbitionState>>().WithEntityAccess())
            {
                var ambitionsBuffer = ambition.ValueRW;

                if (!desires.HasBuffer(entity))
                {
                    state.EntityManager.AddBuffer<DesireElement>(entity);
                    desires.Update(ref state);
                }

                if (!intents.HasComponent(entity))
                {
                    state.EntityManager.AddComponentData(entity, new IntentState
                    {
                        IntentId = -1,
                        Status = 0
                    });
                    intents.Update(ref state);
                }

                if (!tasks.HasBuffer(entity))
                {
                    state.EntityManager.AddBuffer<TaskElement>(entity);
                    tasks.Update(ref state);
                }

                var desireBuffer = desires[entity];
                var intent = intents[entity];
                var taskBuffer = tasks[entity];

                if (desireBuffer.Length == 0)
                {
                    desireBuffer.Add(new DesireElement
                    {
                        DesireId = ambitionsBuffer.AmbitionId,
                        Priority = ambitionsBuffer.Priority
                    });
                }

                if (intent.Status == 0 && desireBuffer.Length > 0)
                {
                    var picked = desireBuffer[0];
                    desireBuffer.RemoveAtSwapBack(0);
                    intent.IntentId = picked.DesireId;
                    intent.Status = 1;
                    state.EntityManager.SetComponentData(entity, intent);
                    intents.Update(ref state);

                    var task = new TaskElement
                    {
                        TaskId = picked.DesireId,
                        Status = 0
                    };
                    taskBuffer.Add(task);
                }

                for (int i = 0; i < taskBuffer.Length; i++)
                {
                    var task = taskBuffer[i];
                    if (task.Status == 0)
                    {
                        task.Status = 1;
                        taskBuffer[i] = task;
                    }
                }

                float speed = 0.1f + 0.1f * math.saturate(ambitionsBuffer.Priority / 255f);
                ambitionsBuffer.Progress = math.clamp(ambitionsBuffer.Progress + speed * SystemAPI.Time.DeltaTime, 0f, 1f);

                if (ambitionsBuffer.Progress >= 1f)
                {
                    if (intent.Status == 1)
                    {
                        intent.Status = 2;
                        state.EntityManager.SetComponentData(entity, intent);
                        intents.Update(ref state);
                    }
                    if (taskBuffer.Length > 0)
                    {
                        var task = taskBuffer[0];
                        task.Status = 2;
                        taskBuffer[0] = task;
                        taskBuffer.RemoveAtSwapBack(0);
                    }
                    ambition.ValueRW = new AmbitionState
                    {
                        AmbitionId = ambitionsBuffer.AmbitionId,
                        Priority = ambitionsBuffer.Priority,
                        Progress = 0f
                    };
                }
                else
                {
                    ambition.ValueRW = ambitionsBuffer;
                }
            }
        }
    }
}
