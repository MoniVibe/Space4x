using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Authority;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Profile;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Emits profile action events when a strike craft obeys or disobeys a wing directive.
    /// </summary>
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftBehaviorSystem))]
    [BurstCompile]
    public partial struct Space4XStrikeCraftOrderDecisionEventSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<StrikeCraftProfile> _profileLookup;
        private ComponentLookup<IssuedByAuthority> _issuedByLookup;
        private BufferLookup<ResolvedControl> _resolvedControlLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftOrderDecision>();
            state.RequireForUpdate<ProfileActionEventStream>();
            state.RequireForUpdate<ProfileActionEventStreamConfig>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _entityLookup = state.GetEntityStorageInfoLookup();
            _profileLookup = state.GetComponentLookup<StrikeCraftProfile>(true);
            _issuedByLookup = state.GetComponentLookup<IssuedByAuthority>(true);
            _resolvedControlLookup = state.GetBufferLookup<ResolvedControl>(true);
        }

        [BurstCompile]
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

            _entityLookup.Update(ref state);
            _profileLookup.Update(ref state);
            _issuedByLookup.Update(ref state);
            _resolvedControlLookup.Update(ref state);

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

                var actor = ResolveActor(entity, pilotLink.ValueRO.Pilot);
                var issuedBy = ResolveIssuedByAuthority(entity);
                if (actor == Entity.Null || !_entityLookup.Exists(actor))
                {
                    actor = entity;
                }

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
                if (profile.Carrier != Entity.Null && _entityLookup.Exists(profile.Carrier) &&
                    _issuedByLookup.HasComponent(profile.Carrier))
                {
                    return _issuedByLookup[profile.Carrier];
                }
            }

            return default;
        }

        private Entity ResolveActor(Entity craftEntity, Entity fallbackPilot)
        {
            if (TryResolveController(craftEntity, AgencyDomain.FlightOps, out var controller))
            {
                if (controller != Entity.Null && _entityLookup.Exists(controller))
                {
                    return controller;
                }

                return craftEntity;
            }

            if (fallbackPilot != Entity.Null && _entityLookup.Exists(fallbackPilot))
            {
                return fallbackPilot;
            }

            return craftEntity;
        }

        private bool TryResolveController(Entity craftEntity, AgencyDomain domain, out Entity controller)
        {
            controller = Entity.Null;
            if (!_resolvedControlLookup.HasBuffer(craftEntity))
            {
                return false;
            }

            var resolved = _resolvedControlLookup[craftEntity];
            for (int i = 0; i < resolved.Length; i++)
            {
                if (resolved[i].Domain == domain)
                {
                    controller = resolved[i].Controller;
                    return true;
                }
            }

            return false;
        }
    }
}
