using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Emits profile action events when a strike craft obeys or disobeys a wing directive.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftBehaviorSystem))]
    public partial struct Space4XStrikeCraftOrderDecisionEventSystem : ISystem
    {
        private ComponentLookup<StrikeCraftProfile> _profileLookup;
        private ComponentLookup<IssuedByAuthority> _issuedByLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftOrderDecision>();
            state.RequireForUpdate<ProfileActionEventStream>();
            state.RequireForUpdate<ProfileActionEventStreamConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _profileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
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

            _profileLookup.Update(ref state);
            _issuedByLookup.Update(ref state);

            var streamConfig = SystemAPI.GetSingleton<ProfileActionEventStreamConfig>();
            var stream = SystemAPI.GetComponentRW<ProfileActionEventStream>(streamEntity);
            var buffer = SystemAPI.GetBuffer<ProfileActionEvent>(streamEntity);

            foreach (var (decision, pilotLink, entity) in SystemAPI.Query<RefRW<StrikeCraftOrderDecision>, RefRO<StrikeCraftPilotLink>>()
                         .WithEntityAccess())
            {
                var decisionValue = decision.ValueRO;
                if (decisionValue.LastDecision == 0 ||
                    decisionValue.LastDirectiveTick == 0 ||
                    decisionValue.LastDirectiveTick <= decisionValue.LastEmittedTick)
                {
                    continue;
                }

                var token = decisionValue.LastDecision == 1
                    ? ProfileActionToken.ObeyOrder
                    : ProfileActionToken.DisobeyOrder;

                var actor = pilotLink.ValueRO.Pilot != Entity.Null ? pilotLink.ValueRO.Pilot : entity;
                var issuedBy = ResolveIssuedByAuthority(entity);

                var actionEvent = new ProfileActionEvent
                {
                    Token = token,
                    IntentFlags = ProfileActionIntentFlags.None,
                    JustificationFlags = ProfileActionJustificationFlags.None,
                    OutcomeFlags = ProfileActionOutcomeFlags.None,
                    Magnitude = 100,
                    Actor = actor,
                    Target = entity,
                    IssuingSeat = issuedBy.IssuingSeat,
                    IssuingOccupant = issuedBy.IssuingOccupant,
                    ActingSeat = issuedBy.ActingSeat,
                    ActingOccupant = issuedBy.ActingOccupant,
                    Tick = decisionValue.LastDirectiveTick
                };

                ProfileActionEventUtility.TryAppend(ref stream.ValueRW, buffer, actionEvent, streamConfig.MaxEvents);
                decision.ValueRW.LastEmittedTick = decisionValue.LastDirectiveTick;
            }
        }

        private IssuedByAuthority ResolveIssuedByAuthority(Entity craftEntity)
        {
            if (_profileLookup.HasComponent(craftEntity))
            {
                var profile = _profileLookup[craftEntity];
                if (profile.Carrier != Entity.Null && _issuedByLookup.HasComponent(profile.Carrier))
                {
                    return _issuedByLookup[profile.Carrier];
                }
            }

            return default;
        }
    }
}
