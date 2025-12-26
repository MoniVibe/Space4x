using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Unity.Entities;

namespace Space4X.Registry
{
    /// <summary>
    /// Emits profile action events when captain orders are issued or resolved.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XCaptainOrderSystem))]
    public partial struct Space4XCaptainOrderEventSystem : ISystem
    {
        private ComponentLookup<IssuedByAuthority> _issuedByLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CaptainOrder>();
            state.RequireForUpdate<ProfileActionEventStream>();
            state.RequireForUpdate<ProfileActionEventStreamConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _issuedByLookup = state.GetComponentLookup<IssuedByAuthority>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (rewind.Mode != RewindMode.Record)
            {
                return;
            }

            if (!SystemAPI.TryGetSingletonEntity<ProfileActionEventStream>(out var streamEntity))
            {
                return;
            }

            _issuedByLookup.Update(ref state);

            var streamConfig = SystemAPI.GetSingleton<ProfileActionEventStreamConfig>();
            var stream = SystemAPI.GetComponentRW<ProfileActionEventStream>(streamEntity);
            var buffer = SystemAPI.GetBuffer<ProfileActionEvent>(streamEntity);

            foreach (var (order, entity) in SystemAPI.Query<RefRO<CaptainOrder>>()
                         .WithNone<CaptainOrderEventState>()
                         .WithEntityAccess())
            {
                state.EntityManager.AddComponentData(entity, new CaptainOrderEventState
                {
                    LastStatus = order.ValueRO.Status,
                    LastIssuedTick = order.ValueRO.IssuedTick,
                    LastEmittedTick = 0
                });
            }

            foreach (var (order, eventState, entity) in SystemAPI.Query<RefRO<CaptainOrder>, RefRW<CaptainOrderEventState>>()
                         .WithEntityAccess())
            {
                var issuedBy = ResolveIssuedByAuthority(entity);

                if (order.ValueRO.IssuedTick != 0 && order.ValueRO.IssuedTick != eventState.ValueRO.LastIssuedTick)
                {
                    var issuedEvent = new ProfileActionEvent
                    {
                        Token = ProfileActionToken.OrderIssued,
                        IntentFlags = ProfileActionIntentFlags.None,
                        JustificationFlags = ProfileActionJustificationFlags.None,
                        OutcomeFlags = ProfileActionOutcomeFlags.None,
                        Magnitude = 100,
                        Actor = issuedBy.IssuingOccupant != Entity.Null ? issuedBy.IssuingOccupant : entity,
                        Target = entity,
                        IssuingSeat = issuedBy.IssuingSeat,
                        IssuingOccupant = issuedBy.IssuingOccupant,
                        ActingSeat = issuedBy.ActingSeat,
                        ActingOccupant = issuedBy.ActingOccupant,
                        Tick = order.ValueRO.IssuedTick
                    };
                    ProfileActionEventUtility.TryAppend(ref stream.ValueRW, buffer, issuedEvent, streamConfig.MaxEvents);

                    eventState.ValueRW.LastIssuedTick = order.ValueRO.IssuedTick;
                }

                if (order.ValueRO.Status == eventState.ValueRO.LastStatus)
                {
                    continue;
                }

                var status = order.ValueRO.Status;
                var token = ProfileActionToken.None;
                var justification = ProfileActionJustificationFlags.None;

                switch (status)
                {
                    case CaptainOrderStatus.Completed:
                        token = ProfileActionToken.ObeyOrder;
                        break;
                    case CaptainOrderStatus.Cancelled:
                    case CaptainOrderStatus.Escalated:
                        token = ProfileActionToken.DisobeyOrder;
                        justification = ProfileActionJustificationFlags.Necessity;
                        break;
                    case CaptainOrderStatus.Failed:
                        token = ProfileActionToken.DisobeyOrder;
                        justification = ProfileActionJustificationFlags.Necessity;
                        break;
                }

                if (token != ProfileActionToken.None)
                {
                    var actionEvent = new ProfileActionEvent
                    {
                        Token = token,
                        IntentFlags = ProfileActionIntentFlags.None,
                        JustificationFlags = justification,
                        OutcomeFlags = ProfileActionOutcomeFlags.None,
                        Magnitude = 100,
                        Actor = entity,
                        Target = order.ValueRO.TargetEntity,
                        IssuingSeat = issuedBy.IssuingSeat,
                        IssuingOccupant = issuedBy.IssuingOccupant,
                        ActingSeat = issuedBy.ActingSeat,
                        ActingOccupant = issuedBy.ActingOccupant,
                        Tick = timeState.Tick
                    };
                    ProfileActionEventUtility.TryAppend(ref stream.ValueRW, buffer, actionEvent, streamConfig.MaxEvents);
                    eventState.ValueRW.LastEmittedTick = timeState.Tick;
                }

                eventState.ValueRW.LastStatus = status;
            }
        }

        private IssuedByAuthority ResolveIssuedByAuthority(Entity shipEntity)
        {
            if (_issuedByLookup.HasComponent(shipEntity))
            {
                return _issuedByLookup[shipEntity];
            }

            return default;
        }
    }
}
