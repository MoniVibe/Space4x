// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using PureDOTS.Runtime.Behavior;

namespace PureDOTS.Runtime.Decision
{
    [BurstCompile]
    public partial struct DecisionPlannerStubSystem : ISystem
    {
        BufferLookup<NeedRequestElement> _needRequests;
        BufferLookup<DecisionRequestElement> _decisionRequests;
        ComponentLookup<DecisionAssignment> _assignments;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _needRequests = state.GetBufferLookup<NeedRequestElement>(true);
            _decisionRequests = state.GetBufferLookup<DecisionRequestElement>();
            _assignments = state.GetComponentLookup<DecisionAssignment>();
        }

        [BurstCompile] public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _needRequests.Update(ref state);
            _decisionRequests.Update(ref state);
            _assignments.Update(ref state);

            var needLookup = _needRequests;
            var decisionLookup = _decisionRequests;
            var assignmentLookup = _assignments;

            foreach (var (ticket, entity) in SystemAPI.Query<RefRW<DecisionTicket>>().WithEntityAccess())
            {
                if (!decisionLookup.HasBuffer(entity))
                {
                    var buffer = SystemAPI.GetBuffer<DecisionRequestElement>(entity);
                    buffer.Clear();
                    decisionLookup.Update(ref state);
                }
            }

            foreach (var (needBuf, entity) in SystemAPI.Query<DynamicBuffer<NeedRequestElement>>().WithEntityAccess())
            {
                if (!decisionLookup.HasBuffer(entity))
                {
                    var buffer = state.EntityManager.AddBuffer<DecisionRequestElement>(entity);
                    buffer.Clear();
                    decisionLookup.Update(ref state);
                }

                var requests = decisionLookup[entity];
                for (int i = 0; i < needBuf.Length; i++)
                {
                    var need = needBuf[i];
                    requests.Add(new DecisionRequestElement
                    {
                        NeedType = need.NeedType,
                        Priority = 0 // [TRI-STUB] Priority removed from NeedRequestElement; stub uses default
                    });
                }
                needBuf.Clear();
            }

            foreach (var (requests, entity) in SystemAPI.Query<DynamicBuffer<DecisionRequestElement>>().WithEntityAccess())
            {
                if (requests.Length == 0)
                    continue;

                if (!assignmentLookup.HasComponent(entity))
                {
                    state.EntityManager.AddComponentData(entity, new DecisionAssignment { ActionId = -1, Status = 0 });
                    assignmentLookup.Update(ref state);
                }

                var assignment = assignmentLookup[entity];
                if (assignment.Status == 0 && requests.Length > 0)
                {
                    var request = requests[0];
                    requests.RemoveAtSwapBack(0);
                    assignment.ActionId = request.NeedType;
                    assignment.Status = 1;
                    state.EntityManager.SetComponentData(entity, assignment);
                    assignmentLookup.Update(ref state);
                }
            }
        }
    }
}
