using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Economy.Production;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace Space4X.Systems.Interaction
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XInteractionGateBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            EnsureState(ref state);
            EnsureConfig(ref state);
        }

        public void OnUpdate(ref SystemState state)
        {
        }

        private static void EnsureState(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XInteractionGateState>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XInteractionGateState));
            state.EntityManager.SetComponentData(entity, new Space4XInteractionGateState
            {
                IsOpen = 0,
                ContextEntity = Entity.Null,
                Kind = Space4XInteractionGateKind.None
            });
            state.EntityManager.AddBuffer<Space4XInteractionGateTrigger>(entity);
            state.EntityManager.AddBuffer<Space4XInteractionOption>(entity);
            state.EntityManager.AddBuffer<Space4XInteractionChoiceRequest>(entity);
            state.EntityManager.AddBuffer<Space4XYarnGateEvent>(entity);
        }

        private static void EnsureConfig(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonEntity<Space4XInteractionGateConfig>(out _))
            {
                return;
            }

            var entity = state.EntityManager.CreateEntity(typeof(Space4XInteractionGateConfig));
            state.EntityManager.SetComponentData(entity, Space4XInteractionGateConfig.Default);
        }
    }

    /// <summary>
    /// Collects digit key presses (1-5) and appends gate choice requests.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XInteractionGateInputSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<Space4XInteractionGateConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<Space4XInteractionGateConfig>();
            if (config.EnableKeyboardDigitInput == 0)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var slot = ResolveSlot(keyboard);
            if (slot == 0)
            {
                return;
            }

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            if (gateState.IsOpen == 0)
            {
                return;
            }

            var tick = 0u;
            if (SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                tick = timeState.Tick;
            }

            var requests = state.EntityManager.GetBuffer<Space4XInteractionChoiceRequest>(gateEntity);
            requests.Add(new Space4XInteractionChoiceRequest
            {
                Slot = slot,
                Tick = tick
            });
        }

        private static byte ResolveSlot(Keyboard keyboard)
        {
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) return 1;
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) return 2;
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) return 3;
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) return 4;
            if (keyboard.digit5Key.wasPressedThisFrame || keyboard.numpad5Key.wasPressedThisFrame) return 5;
            return 0;
        }
    }

    /// <summary>
    /// Resolves interaction gate triggers and player choices, emitting Yarn gate events.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XInteractionGateResolveSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<Space4XContactStanding> _contactLookup;
        private BufferLookup<FactionRelationEntry> _relationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<Space4XInteractionGateConfig>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _contactLookup = state.GetBufferLookup<Space4XContactStanding>(true);
            _relationLookup = state.GetBufferLookup<FactionRelationEntry>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _contactLookup.Update(ref state);
            _relationLookup.Update(ref state);

            var config = SystemAPI.GetSingleton<Space4XInteractionGateConfig>();
            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            var currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;

            var triggers = state.EntityManager.GetBuffer<Space4XInteractionGateTrigger>(gateEntity);
            var options = state.EntityManager.GetBuffer<Space4XInteractionOption>(gateEntity);
            var requests = state.EntityManager.GetBuffer<Space4XInteractionChoiceRequest>(gateEntity);
            var events = state.EntityManager.GetBuffer<Space4XYarnGateEvent>(gateEntity);

            if (triggers.Length > 0)
            {
                var trigger = triggers[triggers.Length - 1];
                triggers.Clear();
                requests.Clear();
                OpenGate(in trigger, in config, currentTick, ref gateState, ref options);
            }

            if (requests.Length > 0)
            {
                var request = requests[requests.Length - 1];
                requests.Clear();
                ResolveChoice(in request, in config, currentTick, ref gateState, ref options, ref events);
            }

            state.EntityManager.SetComponentData(gateEntity, gateState);
        }

        private void OpenGate(
            in Space4XInteractionGateTrigger trigger,
            in Space4XInteractionGateConfig config,
            uint currentTick,
            ref Space4XInteractionGateState gateState,
            ref DynamicBuffer<Space4XInteractionOption> options)
        {
            options.Clear();

            var hasTarget = trigger.Target != Entity.Null;
            var targetFactionId = trigger.TargetFactionId;
            if (targetFactionId == 0 && hasTarget && TryResolveFactionId(trigger.Target, out var resolvedTargetFactionId))
            {
                targetFactionId = resolvedTargetFactionId;
            }

            var standing = ResolveStanding(trigger.Actor, targetFactionId);
            var baseNodePrefix = ResolveNodePrefix(in trigger);

            gateState.IsOpen = 1;
            gateState.Kind = trigger.Kind;
            gateState.ContextEntity = trigger.ContextEntity;
            gateState.Actor = trigger.Actor;
            gateState.Target = trigger.Target;
            gateState.TargetFactionId = targetFactionId;
            gateState.Standing01 = (half)math.clamp(standing, 0f, 1f);
            gateState.RoomId = trigger.RoomId;
            gateState.OpenTick = currentTick;
            gateState.GateId = trigger.GateId;
            var useMask = trigger.UseOptionMask != 0;
            var optionMask = trigger.OptionMask;

            switch (trigger.Kind)
            {
                case Space4XInteractionGateKind.Hail:
                    if (ShouldIncludeSlot(1, useMask, optionMask)) AddOption(ref options, 1, "Open Channel", 0f, hasTarget, standing, BuildNodeId(baseNodePrefix, 1));
                    if (ShouldIncludeSlot(2, useMask, optionMask)) AddOption(ref options, 2, "Trade Proposal", config.HailTradeStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 2));
                    if (ShouldIncludeSlot(3, useMask, optionMask)) AddOption(ref options, 3, "Mission Intel", config.HailMissionStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 3));
                    if (ShouldIncludeSlot(4, useMask, optionMask)) AddOption(ref options, 4, "Coercive Demand", config.HailCoerciveStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 4));
                    if (ShouldIncludeSlot(5, useMask, optionMask)) AddOption(ref options, 5, "End Hail", 0f, true, standing, BuildNodeId(baseNodePrefix, 5));
                    break;

                case Space4XInteractionGateKind.Docking:
                    if (ShouldIncludeSlot(1, useMask, optionMask)) AddOption(ref options, 1, "Request Dock", config.DockingStandardStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 1));
                    if (ShouldIncludeSlot(2, useMask, optionMask)) AddOption(ref options, 2, "Emergency Dock", 0f, hasTarget, standing, BuildNodeId(baseNodePrefix, 2));
                    if (ShouldIncludeSlot(3, useMask, optionMask)) AddOption(ref options, 3, "Priority Berth", config.DockingPriorityStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 3));
                    if (ShouldIncludeSlot(4, useMask, optionMask)) AddOption(ref options, 4, "Bribe Traffic Control", config.DockingBribeStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 4));
                    if (ShouldIncludeSlot(5, useMask, optionMask)) AddOption(ref options, 5, "Abort Approach", 0f, true, standing, BuildNodeId(baseNodePrefix, 5));
                    break;

                case Space4XInteractionGateKind.RoomEvent:
                    if (ShouldIncludeSlot(1, useMask, optionMask)) AddOption(ref options, 1, "Scout Area", 0f, true, standing, BuildNodeId(baseNodePrefix, 1));
                    if (ShouldIncludeSlot(2, useMask, optionMask)) AddOption(ref options, 2, "Cautious Action", 0.1f, true, standing, BuildNodeId(baseNodePrefix, 2));
                    if (ShouldIncludeSlot(3, useMask, optionMask)) AddOption(ref options, 3, "Risky Play", config.RoomRiskStanding, true, standing, BuildNodeId(baseNodePrefix, 3));
                    if (ShouldIncludeSlot(4, useMask, optionMask)) AddOption(ref options, 4, "All-In Gamble", config.RoomHighRiskStanding, true, standing, BuildNodeId(baseNodePrefix, 4));
                    if (ShouldIncludeSlot(5, useMask, optionMask)) AddOption(ref options, 5, "Leave It", 0f, true, standing, BuildNodeId(baseNodePrefix, 5));
                    break;

                case Space4XInteractionGateKind.Equipping:
                    if (ShouldIncludeSlot(1, useMask, optionMask)) AddOption(ref options, 1, "Install Module", 0f, true, standing, BuildNodeId(baseNodePrefix, 1));
                    if (ShouldIncludeSlot(2, useMask, optionMask)) AddOption(ref options, 2, "Strip Module", 0f, true, standing, BuildNodeId(baseNodePrefix, 2));
                    if (ShouldIncludeSlot(3, useMask, optionMask)) AddOption(ref options, 3, "Station Overhaul", config.EquipOverhaulStanding, true, standing, BuildNodeId(baseNodePrefix, 3));
                    if (ShouldIncludeSlot(4, useMask, optionMask)) AddOption(ref options, 4, "Review Fit", 0f, true, standing, BuildNodeId(baseNodePrefix, 4));
                    if (ShouldIncludeSlot(5, useMask, optionMask)) AddOption(ref options, 5, "Cancel Refit Queue", 0f, true, standing, BuildNodeId(baseNodePrefix, 5));
                    break;

                case Space4XInteractionGateKind.Trade:
                    if (ShouldIncludeSlot(1, useMask, optionMask)) AddOption(ref options, 1, "Buy Offers", 0f, hasTarget, standing, BuildNodeId(baseNodePrefix, 1));
                    if (ShouldIncludeSlot(2, useMask, optionMask)) AddOption(ref options, 2, "Sell Cargo", 0f, hasTarget, standing, BuildNodeId(baseNodePrefix, 2));
                    if (ShouldIncludeSlot(3, useMask, optionMask)) AddOption(ref options, 3, "Negotiate Contract", config.TradeContractStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 3));
                    if (ShouldIncludeSlot(4, useMask, optionMask)) AddOption(ref options, 4, "Request Guild Terms", config.TradeGuildStanding, hasTarget, standing, BuildNodeId(baseNodePrefix, 4));
                    if (ShouldIncludeSlot(5, useMask, optionMask)) AddOption(ref options, 5, "Leave Exchange", 0f, true, standing, BuildNodeId(baseNodePrefix, 5));
                    break;

                case Space4XInteractionGateKind.Production:
                    if (ShouldIncludeSlot(1, useMask, optionMask)) AddOption(ref options, 1, "Queue Batch", 0f, true, standing, BuildNodeId(baseNodePrefix, 1));
                    if (ShouldIncludeSlot(2, useMask, optionMask)) AddOption(ref options, 2, "Assign Specialist", config.ProductionSpecialistStanding, true, standing, BuildNodeId(baseNodePrefix, 2));
                    if (ShouldIncludeSlot(3, useMask, optionMask)) AddOption(ref options, 3, "Rush Job", config.ProductionRushStanding, true, standing, BuildNodeId(baseNodePrefix, 3));
                    if (ShouldIncludeSlot(4, useMask, optionMask)) AddOption(ref options, 4, "Commission Prototype", config.ProductionPrototypeStanding, true, standing, BuildNodeId(baseNodePrefix, 4));
                    if (ShouldIncludeSlot(5, useMask, optionMask)) AddOption(ref options, 5, "Stand Down", 0f, true, standing, BuildNodeId(baseNodePrefix, 5));
                    break;

                default:
                    if (ShouldIncludeSlot(1, useMask, optionMask)) AddOption(ref options, 1, "Proceed", 0f, true, standing, BuildNodeId(baseNodePrefix, 1));
                    break;
            }
        }

        private static void ResolveChoice(
            in Space4XInteractionChoiceRequest request,
            in Space4XInteractionGateConfig config,
            uint currentTick,
            ref Space4XInteractionGateState gateState,
            ref DynamicBuffer<Space4XInteractionOption> options,
            ref DynamicBuffer<Space4XYarnGateEvent> events)
        {
            if (gateState.IsOpen == 0)
            {
                return;
            }

            var found = false;
            var selected = default(Space4XInteractionOption);
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].Slot != request.Slot)
                {
                    continue;
                }

                found = true;
                selected = options[i];
                break;
            }

            if (!found)
            {
                events.Add(new Space4XYarnGateEvent
                {
                    Accepted = 0,
                    Kind = gateState.Kind,
                    ContextEntity = gateState.ContextEntity,
                    Actor = gateState.Actor,
                    Target = gateState.Target,
                    Slot = request.Slot,
                    Tick = currentTick,
                    Reason = Space4XInteractionUnavailableReason.InvalidSelection,
                    GateId = gateState.GateId
                });
                return;
            }

            var accepted = selected.IsEnabled != 0;
            events.Add(new Space4XYarnGateEvent
            {
                Accepted = (byte)(accepted ? 1 : 0),
                Kind = gateState.Kind,
                ContextEntity = gateState.ContextEntity,
                Actor = gateState.Actor,
                Target = gateState.Target,
                Slot = request.Slot,
                Tick = currentTick,
                Reason = accepted ? Space4XInteractionUnavailableReason.None : selected.UnavailableReason,
                GateId = gateState.GateId,
                YarnNodeId = selected.YarnNodeId
            });

            gateState.LastResolvedTick = currentTick;
            if (accepted && config.CloseGateOnAccept != 0)
            {
                gateState.IsOpen = 0;
                gateState.ContextEntity = Entity.Null;
            }
        }

        private void AddOption(
            ref DynamicBuffer<Space4XInteractionOption> options,
            byte slot,
            in FixedString64Bytes label,
            float requiredStanding,
            bool hasTarget,
            float standing,
            in FixedString64Bytes yarnNodeId)
        {
            var enabled = hasTarget && standing + 0.0001f >= requiredStanding;
            var reason = Space4XInteractionUnavailableReason.None;
            if (!hasTarget)
            {
                reason = Space4XInteractionUnavailableReason.MissingTarget;
            }
            else if (!enabled)
            {
                reason = Space4XInteractionUnavailableReason.StandingTooLow;
            }

            options.Add(new Space4XInteractionOption
            {
                Slot = slot,
                IsEnabled = (byte)(enabled ? 1 : 0),
                RequiredStanding = (half)math.clamp(requiredStanding, 0f, 1f),
                UnavailableReason = reason,
                Label = label,
                YarnNodeId = yarnNodeId
            });
        }

        private FixedString64Bytes ResolveNodePrefix(in Space4XInteractionGateTrigger trigger)
        {
            if (!trigger.YarnNodePrefix.IsEmpty)
            {
                return trigger.YarnNodePrefix;
            }

            return trigger.Kind switch
            {
                Space4XInteractionGateKind.Hail => new FixedString64Bytes("space4x.hail"),
                Space4XInteractionGateKind.Docking => new FixedString64Bytes("space4x.docking"),
                Space4XInteractionGateKind.RoomEvent => new FixedString64Bytes("space4x.room_event"),
                Space4XInteractionGateKind.Equipping => new FixedString64Bytes("space4x.equip"),
                Space4XInteractionGateKind.Trade => new FixedString64Bytes("space4x.trade"),
                Space4XInteractionGateKind.Production => new FixedString64Bytes("space4x.production"),
                _ => new FixedString64Bytes("space4x.interaction")
            };
        }

        private static FixedString64Bytes BuildNodeId(in FixedString64Bytes prefix, byte slot)
        {
            var id = prefix;
            id.Append('.');
            id.Append((int)slot);
            return id;
        }

        private static bool ShouldIncludeSlot(byte slot, bool useMask, uint optionMask)
        {
            if (!useMask)
            {
                return true;
            }

            if (slot < 1 || slot > 32)
            {
                return false;
            }

            return (optionMask & (1u << (slot - 1))) != 0u;
        }

        private bool TryResolveFactionId(Entity entity, out ushort factionId)
        {
            factionId = 0;
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out _,
                    out var resolvedFactionId))
            {
                return false;
            }

            factionId = resolvedFactionId;
            return factionId != 0;
        }

        private float ResolveStanding(Entity actor, ushort targetFactionId)
        {
            if (targetFactionId == 0)
            {
                return 0.2f;
            }

            if (!Space4XStandingUtility.TryResolveFaction(
                    actor,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var actorFaction,
                    out _))
            {
                return 0.2f;
            }

            if (_contactLookup.HasBuffer(actorFaction))
            {
                var contacts = _contactLookup[actorFaction];
                for (int i = 0; i < contacts.Length; i++)
                {
                    var entry = contacts[i];
                    if (entry.ContactFactionId == targetFactionId)
                    {
                        return math.clamp((float)entry.Standing, 0f, 1f);
                    }
                }
            }

            if (_relationLookup.HasBuffer(actorFaction))
            {
                var relations = _relationLookup[actorFaction];
                for (int i = 0; i < relations.Length; i++)
                {
                    var relation = relations[i].Relation;
                    if (relation.OtherFactionId == targetFactionId)
                    {
                        var normalized = ((float)relation.Score + 100f) / 200f;
                        return math.clamp(normalized, 0f, 1f);
                    }
                }
            }

            return 0.2f;
        }
    }

    /// <summary>
    /// Bridges accepted comm intents into hail dialogue gates.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.Space4XCommDecisionBridgeSystem))]
    [UpdateBefore(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XHailGateTriggerSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<Space4XCommOrderIntent>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            if (gateState.IsOpen != 0)
            {
                return;
            }

            var triggers = state.EntityManager.GetBuffer<Space4XInteractionGateTrigger>(gateEntity);
            if (triggers.Length > 0)
            {
                return;
            }

            foreach (var (intent, entity) in SystemAPI.Query<RefRO<Space4XCommOrderIntent>>().WithEntityAccess())
            {
                if (!IsPlayerEntity(entity))
                {
                    continue;
                }

                if (intent.ValueRO.ReceivedTick != 0 && intent.ValueRO.ReceivedTick <= gateState.LastResolvedTick)
                {
                    continue;
                }

                var target = intent.ValueRO.Target != Entity.Null ? intent.ValueRO.Target : intent.ValueRO.Sender;
                var targetFactionId = ResolveFactionId(target);
                var gateId = new FixedString64Bytes("space4x.hail.msg.");
                gateId.Append((int)math.max(0u, intent.ValueRO.SourceMessageId));

                if (intent.ValueRO.SourceMessageId == 0)
                {
                    gateId.Append('.');
                    gateId.Append(entity.Index);
                }

                triggers.Add(new Space4XInteractionGateTrigger
                {
                    Kind = Space4XInteractionGateKind.Hail,
                    ContextEntity = entity,
                    Actor = entity,
                    Target = target,
                    TargetFactionId = targetFactionId,
                    GateId = gateId,
                    YarnNodePrefix = new FixedString64Bytes("space4x.hail")
                });
                break;
            }
        }

        private bool IsPlayerEntity(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var factionEntity,
                    out _))
            {
                return false;
            }

            if (factionEntity == Entity.Null || !_factionLookup.HasComponent(factionEntity))
            {
                return false;
            }

            return _factionLookup[factionEntity].Type == FactionType.Player;
        }

        private ushort ResolveFactionId(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out _,
                    out var factionId))
            {
                return 0;
            }

            return factionId;
        }
    }

    /// <summary>
    /// Bridges player docking requests into docking dialogue gates.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.AI.Space4XVesselDockingRequestSystem))]
    [UpdateBefore(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XDockingGateTriggerSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<DockingRequest>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            if (gateState.IsOpen != 0)
            {
                return;
            }

            var triggers = state.EntityManager.GetBuffer<Space4XInteractionGateTrigger>(gateEntity);
            if (triggers.Length > 0)
            {
                return;
            }

            foreach (var (request, entity) in SystemAPI.Query<RefRO<DockingRequest>>().WithEntityAccess())
            {
                if (!IsPlayerEntity(entity))
                {
                    continue;
                }

                if (request.ValueRO.RequestTick != 0 && request.ValueRO.RequestTick <= gateState.LastResolvedTick)
                {
                    continue;
                }

                var target = request.ValueRO.TargetCarrier;
                if (target == Entity.Null || !state.EntityManager.Exists(target))
                {
                    continue;
                }

                var targetFactionId = ResolveFactionId(target);
                var gateId = new FixedString64Bytes("space4x.docking.request.");
                gateId.Append(entity.Index);
                gateId.Append('.');
                gateId.Append((int)request.ValueRO.RequestTick);

                triggers.Add(new Space4XInteractionGateTrigger
                {
                    Kind = Space4XInteractionGateKind.Docking,
                    ContextEntity = entity,
                    Actor = entity,
                    Target = target,
                    TargetFactionId = targetFactionId,
                    GateId = gateId,
                    YarnNodePrefix = new FixedString64Bytes("space4x.docking")
                });
                break;
            }
        }

        private bool IsPlayerEntity(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var factionEntity,
                    out _))
            {
                return false;
            }

            if (factionEntity == Entity.Null || !_factionLookup.HasComponent(factionEntity))
            {
                return false;
            }

            return _factionLookup[factionEntity].Type == FactionType.Player;
        }

        private ushort ResolveFactionId(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out _,
                    out var factionId))
            {
                return 0;
            }

            return factionId;
        }
    }

    /// <summary>
    /// Bridges player refit queues into equipping dialogue gates.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XEquipGateTriggerSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<ModuleRefitRequest>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            if (gateState.IsOpen != 0)
            {
                return;
            }

            var triggers = state.EntityManager.GetBuffer<Space4XInteractionGateTrigger>(gateEntity);
            if (triggers.Length > 0)
            {
                return;
            }

            foreach (var (requests, entity) in SystemAPI.Query<DynamicBuffer<ModuleRefitRequest>>().WithEntityAccess())
            {
                if (!IsPlayerEntity(entity) || requests.Length == 0)
                {
                    continue;
                }

                var hasInstall = false;
                var hasStrip = false;
                uint newestRequestTick = 0;
                for (int i = 0; i < requests.Length; i++)
                {
                    var request = requests[i];
                    newestRequestTick = math.max(newestRequestTick, request.RequestTick);
                    if (request.TargetModule == Entity.Null)
                    {
                        hasStrip = true;
                    }
                    else
                    {
                        hasInstall = true;
                    }
                }

                var signalTick = newestRequestTick;
                if (signalTick != 0 && signalTick <= gateState.LastResolvedTick)
                {
                    continue;
                }

                uint optionMask = 0u;
                if (hasInstall) optionMask |= 1u << 0;
                if (hasStrip) optionMask |= 1u << 1;
                if (state.EntityManager.HasComponent<DockedAtStation>(entity)) optionMask |= 1u << 2;
                optionMask |= 1u << 3;
                optionMask |= 1u << 4;

                var gateId = new FixedString64Bytes("space4x.equip.refit.");
                gateId.Append(entity.Index);
                gateId.Append('.');
                gateId.Append((int)signalTick);
                if (signalTick == 0 && gateState.LastResolvedTick != 0 && gateState.GateId.Equals(gateId))
                {
                    continue;
                }

                triggers.Add(new Space4XInteractionGateTrigger
                {
                    Kind = Space4XInteractionGateKind.Equipping,
                    ContextEntity = entity,
                    Actor = entity,
                    Target = entity,
                    TargetFactionId = ResolveFactionId(entity),
                    OptionMask = optionMask,
                    UseOptionMask = 1,
                    GateId = gateId,
                    YarnNodePrefix = new FixedString64Bytes("space4x.equip")
                });
                break;
            }
        }

        private bool IsPlayerEntity(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var factionEntity,
                    out _))
            {
                return false;
            }

            if (factionEntity == Entity.Null || !_factionLookup.HasComponent(factionEntity))
            {
                return false;
            }

            return _factionLookup[factionEntity].Type == FactionType.Player;
        }

        private ushort ResolveFactionId(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out _,
                    out var factionId))
            {
                return 0;
            }

            return factionId;
        }
    }

    /// <summary>
    /// Bridges docked player entities into trade dialogue gates.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XTradeGateTriggerSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private ComponentLookup<Space4XMarket> _marketLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<TradeOffer> _offerLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<DockedTag>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _marketLookup = state.GetComponentLookup<Space4XMarket>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _offerLookup = state.GetBufferLookup<TradeOffer>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _marketLookup.Update(ref state);
            _affiliationLookup.Update(ref state);
            _offerLookup.Update(ref state);

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            if (gateState.IsOpen != 0)
            {
                return;
            }

            var triggers = state.EntityManager.GetBuffer<Space4XInteractionGateTrigger>(gateEntity);
            if (triggers.Length > 0)
            {
                return;
            }

            foreach (var (docked, entity) in SystemAPI.Query<RefRO<DockedTag>>().WithEntityAccess())
            {
                if (!IsPlayerEntity(entity))
                {
                    continue;
                }

                var marketEntity = docked.ValueRO.CarrierEntity;
                if (marketEntity == Entity.Null || !state.EntityManager.Exists(marketEntity))
                {
                    continue;
                }

                if (!_marketLookup.HasComponent(marketEntity) || !_offerLookup.HasBuffer(marketEntity))
                {
                    continue;
                }

                var market = _marketLookup[marketEntity];
                var offers = _offerLookup[marketEntity];
                var hasBuyDemand = false;
                var hasSellSupply = false;
                var hasContracts = false;

                for (int i = 0; i < offers.Length; i++)
                {
                    var offer = offers[i];
                    if (offer.IsFulfilled != 0)
                    {
                        continue;
                    }

                    switch (offer.Type)
                    {
                        case TradeOfferType.Buy:
                            hasBuyDemand = true;
                            break;
                        case TradeOfferType.Sell:
                            hasSellSupply = true;
                            break;
                        case TradeOfferType.Contract:
                            hasContracts = true;
                            break;
                    }
                }

                var signalTick = market.LastUpdateTick;
                if (signalTick != 0 && signalTick <= gateState.LastResolvedTick)
                {
                    continue;
                }

                uint optionMask = 0u;
                if (hasSellSupply) optionMask |= 1u << 0;
                if (hasBuyDemand) optionMask |= 1u << 1;
                if (hasContracts) optionMask |= 1u << 2;
                optionMask |= 1u << 3;
                optionMask |= 1u << 4;

                var targetFactionId = market.OwnerFactionId;
                if (targetFactionId == 0)
                {
                    targetFactionId = ResolveFactionId(marketEntity);
                }

                var gateId = new FixedString64Bytes("space4x.trade.market.");
                gateId.Append(marketEntity.Index);
                gateId.Append('.');
                gateId.Append((int)signalTick);
                if (signalTick == 0 && gateState.LastResolvedTick != 0 && gateState.GateId.Equals(gateId))
                {
                    continue;
                }

                triggers.Add(new Space4XInteractionGateTrigger
                {
                    Kind = Space4XInteractionGateKind.Trade,
                    ContextEntity = marketEntity,
                    Actor = entity,
                    Target = marketEntity,
                    TargetFactionId = targetFactionId,
                    OptionMask = optionMask,
                    UseOptionMask = 1,
                    GateId = gateId,
                    YarnNodePrefix = new FixedString64Bytes("space4x.trade")
                });
                break;
            }
        }

        private bool IsPlayerEntity(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var factionEntity,
                    out _))
            {
                return false;
            }

            if (factionEntity == Entity.Null || !_factionLookup.HasComponent(factionEntity))
            {
                return false;
            }

            return _factionLookup[factionEntity].Type == FactionType.Player;
        }

        private ushort ResolveFactionId(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out _,
                    out var factionId))
            {
                return 0;
            }

            return factionId;
        }
    }

    /// <summary>
    /// Bridges player production jobs/queues into production dialogue gates.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProductionJobSchedulingSystem))]
    [UpdateBefore(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XProductionGateTriggerSystem : ISystem
    {
        private ComponentLookup<Carrier> _carrierLookup;
        private ComponentLookup<ColonyFacilityLink> _facilityLinkLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private BufferLookup<AffiliationTag> _affiliationLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            _carrierLookup = state.GetComponentLookup<Carrier>(true);
            _facilityLinkLookup = state.GetComponentLookup<ColonyFacilityLink>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _carrierLookup.Update(ref state);
            _facilityLinkLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _affiliationLookup.Update(ref state);

            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            if (gateState.IsOpen != 0)
            {
                return;
            }

            var triggers = state.EntityManager.GetBuffer<Space4XInteractionGateTrigger>(gateEntity);
            if (triggers.Length > 0)
            {
                return;
            }

            var currentTick = SystemAPI.TryGetSingleton<TimeState>(out var timeState) ? timeState.Tick : 0u;
            foreach (var (request, entity) in SystemAPI.Query<RefRO<ProductionJobRequest>>().WithEntityAccess())
            {
                var actor = ResolveActor(entity);
                if (!IsPlayerEntity(actor))
                {
                    continue;
                }

                if (currentTick != 0 && currentTick <= gateState.LastResolvedTick)
                {
                    continue;
                }

                uint optionMask = (1u << 0) | (1u << 2) | (1u << 4);
                if (request.ValueRO.Worker != Entity.Null) optionMask |= 1u << 1;
                if (!request.ValueRO.RecipeId.IsEmpty) optionMask |= 1u << 3;

                var targetFactionId = ResolveFactionId(entity);
                if (targetFactionId == 0)
                {
                    targetFactionId = ResolveFactionId(actor);
                }

                var gateId = new FixedString64Bytes("space4x.production.job.");
                gateId.Append(entity.Index);
                gateId.Append('.');
                gateId.Append((int)currentTick);

                triggers.Add(new Space4XInteractionGateTrigger
                {
                    Kind = Space4XInteractionGateKind.Production,
                    ContextEntity = entity,
                    Actor = actor,
                    Target = entity,
                    TargetFactionId = targetFactionId,
                    OptionMask = optionMask,
                    UseOptionMask = 1,
                    GateId = gateId,
                    YarnNodePrefix = new FixedString64Bytes("space4x.production")
                });
                break;
            }

            if (triggers.Length > 0)
            {
                return;
            }

            foreach (var (queue, entity) in SystemAPI.Query<DynamicBuffer<ProcessingQueueEntry>>().WithEntityAccess())
            {
                if (queue.Length == 0)
                {
                    continue;
                }

                var actor = ResolveActor(entity);
                if (!IsPlayerEntity(actor))
                {
                    continue;
                }

                uint newestQueuedTick = 0;
                byte bestPriority = byte.MaxValue;
                for (int i = 0; i < queue.Length; i++)
                {
                    newestQueuedTick = math.max(newestQueuedTick, queue[i].QueuedTick);
                    bestPriority = math.min(bestPriority, queue[i].Priority);
                }

                var signalTick = newestQueuedTick != 0 ? newestQueuedTick : currentTick;
                if (signalTick != 0 && signalTick <= gateState.LastResolvedTick)
                {
                    continue;
                }

                uint optionMask = (1u << 0) | (1u << 2) | (1u << 3) | (1u << 4);
                if (queue.Length > 1 || bestPriority < 64) optionMask |= 1u << 1;

                var targetFactionId = ResolveFactionId(entity);
                if (targetFactionId == 0)
                {
                    targetFactionId = ResolveFactionId(actor);
                }

                var gateId = new FixedString64Bytes("space4x.production.queue.");
                gateId.Append(entity.Index);
                gateId.Append('.');
                gateId.Append((int)signalTick);

                triggers.Add(new Space4XInteractionGateTrigger
                {
                    Kind = Space4XInteractionGateKind.Production,
                    ContextEntity = entity,
                    Actor = actor,
                    Target = entity,
                    TargetFactionId = targetFactionId,
                    OptionMask = optionMask,
                    UseOptionMask = 1,
                    GateId = gateId,
                    YarnNodePrefix = new FixedString64Bytes("space4x.production")
                });
                break;
            }
        }

        private Entity ResolveActor(Entity entity)
        {
            if (_facilityLinkLookup.HasComponent(entity))
            {
                var colony = _facilityLinkLookup[entity].Colony;
                if (colony != Entity.Null)
                {
                    return colony;
                }
            }

            return entity;
        }

        private bool IsPlayerEntity(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out var factionEntity,
                    out _))
            {
                return false;
            }

            if (factionEntity == Entity.Null || !_factionLookup.HasComponent(factionEntity))
            {
                return false;
            }

            return _factionLookup[factionEntity].Type == FactionType.Player;
        }

        private ushort ResolveFactionId(Entity entity)
        {
            if (!Space4XStandingUtility.TryResolveFaction(
                    entity,
                    in _carrierLookup,
                    in _affiliationLookup,
                    in _factionLookup,
                    out _,
                    out var factionId))
            {
                return 0;
            }

            return factionId;
        }
    }

    /// <summary>
    /// Bridges Space4X dynamic events into the room-event dialogue gate.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XEventLifecycleSystem))]
    [UpdateBefore(typeof(Space4XInteractionGateResolveSystem))]
    public partial struct Space4XRoomEventGateTriggerSystem : ISystem
    {
        private BufferLookup<EventChoice> _choiceLookup;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<Space4XEvent>();
            _choiceLookup = state.GetBufferLookup<EventChoice>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            _choiceLookup.Update(ref state);
            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var gateState = state.EntityManager.GetComponentData<Space4XInteractionGateState>(gateEntity);
            if (gateState.IsOpen != 0)
            {
                return;
            }

            var triggers = state.EntityManager.GetBuffer<Space4XInteractionGateTrigger>(gateEntity);
            if (triggers.Length > 0)
            {
                return;
            }

            foreach (var (evt, entity) in SystemAPI.Query<RefRO<Space4XEvent>>().WithEntityAccess())
            {
                if (evt.ValueRO.Phase != EventPhase.AwaitingChoice || evt.ValueRO.SelectedChoice >= 0)
                {
                    continue;
                }

                uint optionMask = 0u;
                if (_choiceLookup.HasBuffer(entity))
                {
                    var choices = _choiceLookup[entity];
                    for (int i = 0; i < choices.Length; i++)
                    {
                        int slot = math.clamp((int)choices[i].ChoiceIndex + 1, 1, 5);
                        optionMask |= (1u << (slot - 1));
                    }
                }

                var gateId = new FixedString64Bytes("space4x.event.");
                gateId.Append((int)evt.ValueRO.EventTypeId);

                triggers.Add(new Space4XInteractionGateTrigger
                {
                    Kind = Space4XInteractionGateKind.RoomEvent,
                    ContextEntity = entity,
                    Actor = Entity.Null,
                    Target = evt.ValueRO.TargetEntity,
                    TargetFactionId = evt.ValueRO.AffectedFactionId,
                    RoomId = evt.ValueRO.EventTypeId,
                    OptionMask = optionMask,
                    UseOptionMask = 1,
                    GateId = gateId,
                    YarnNodePrefix = gateId
                });
                break;
            }
        }
    }

    /// <summary>
    /// Applies accepted room-event gate choices back into Space4XEvent.SelectedChoice.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XInteractionGateResolveSystem))]
    [UpdateBefore(typeof(Space4X.Registry.Space4XEventOutcomeSystem))]
    public partial struct Space4XRoomEventGateApplyChoiceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Space4XInteractionGateState>();
            state.RequireForUpdate<Space4XEvent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var gateEntity = SystemAPI.GetSingletonEntity<Space4XInteractionGateState>();
            var events = state.EntityManager.GetBuffer<Space4XYarnGateEvent>(gateEntity);
            if (events.Length == 0)
            {
                return;
            }

            for (int i = events.Length - 1; i >= 0; i--)
            {
                var gateEvent = events[i];
                if (gateEvent.Kind != Space4XInteractionGateKind.RoomEvent)
                {
                    continue;
                }

                if (gateEvent.Accepted != 0 &&
                    gateEvent.ContextEntity != Entity.Null &&
                    state.EntityManager.Exists(gateEvent.ContextEntity) &&
                    state.EntityManager.HasComponent<Space4XEvent>(gateEvent.ContextEntity))
                {
                    var evt = state.EntityManager.GetComponentData<Space4XEvent>(gateEvent.ContextEntity);
                    if (evt.Phase == EventPhase.AwaitingChoice && evt.SelectedChoice < 0)
                    {
                        evt.SelectedChoice = (sbyte)math.clamp((int)gateEvent.Slot - 1, 0, 4);
                        evt.IsAcknowledged = 1;
                        state.EntityManager.SetComponentData(gateEvent.ContextEntity, evt);
                    }
                }

                events.RemoveAt(i);
            }
        }
    }
}
