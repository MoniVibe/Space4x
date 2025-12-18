using System.Text;
using Space4X.Registry;
using Space4X.Telemetry;
using PureDOTS.Runtime.Telemetry;
using PureDOTS.Runtime.Scenarios;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.StrikeCraft
{
    /// <summary>
    /// Emits per-strike craft telemetry for role/profile assignments and attack run phase transitions.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XStrikeCraftSystem))]
    public partial struct StrikeCraftTelemetrySystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        private static readonly FixedString64Bytes SourceId = new FixedString64Bytes("Space4X.StrikeCraft");
        private static readonly FixedString64Bytes EventProfileAssigned = new FixedString64Bytes("BehaviorProfileAssigned");
        private static readonly FixedString64Bytes EventPhaseChanged = new FixedString64Bytes("RoleStateChanged");
        private static readonly FixedString64Bytes EventAttackRunStart = new FixedString64Bytes("AttackRunStart");
        private static readonly FixedString64Bytes EventAttackRunEnd = new FixedString64Bytes("AttackRunEnd");

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeCraftProfile>();
            state.RequireForUpdate<TelemetryStream>();
            state.RequireForUpdate<TelemetryStreamSingleton>();
            state.RequireForUpdate<ScenarioTick>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!TryGetTelemetryEventBuffer(ref state, out var eventBuffer))
            {
                return;
            }

            _transformLookup.Update(ref state);
            var tick = SystemAPI.GetSingleton<ScenarioTick>().Value;

            var missingStateQuery = SystemAPI.QueryBuilder()
                .WithAll<StrikeCraftProfile>()
                .WithNone<StrikeCraftTelemetryState>()
                .Build();
            if (!missingStateQuery.IsEmptyIgnoreFilter)
            {
                state.EntityManager.AddComponent<StrikeCraftTelemetryState>(missingStateQuery);
            }
            missingStateQuery.Dispose();

            foreach (var (profile, config, telemetry, entity) in SystemAPI
                         .Query<RefRO<StrikeCraftProfile>, RefRO<AttackRunConfig>, RefRW<StrikeCraftTelemetryState>>()
                         .WithEntityAccess())
            {
                var profileHash = ComputeProfileHash(profile.ValueRO, config.ValueRO);
                if (telemetry.ValueRO.BehaviorLogged == 0 ||
                    telemetry.ValueRO.ProfileHash != profileHash ||
                    telemetry.ValueRO.LastDeliveryType != config.ValueRO.DeliveryType)
                {
                    var payload = BuildBehaviorProfilePayload(entity, profile.ValueRO, config.ValueRO, profileHash);
                    eventBuffer.AddEvent(EventProfileAssigned, tick, SourceId, payload);

                    telemetry.ValueRW.ProfileHash = profileHash;
                    telemetry.ValueRW.LastDeliveryType = config.ValueRO.DeliveryType;
                    telemetry.ValueRW.BehaviorLogged = 1;
                }

                if (profile.ValueRO.Phase != telemetry.ValueRO.LastPhase)
                {
                    var phaseDuration = telemetry.ValueRO.LastPhaseTick == 0
                        ? 0u
                        : tick - telemetry.ValueRO.LastPhaseTick;
                    var payload = BuildPhaseChangePayload(entity, profile.ValueRO, config.ValueRO,
                        telemetry.ValueRO.LastPhase, phaseDuration);
                    eventBuffer.AddEvent(EventPhaseChanged, tick, SourceId, payload);

                    if (profile.ValueRO.Phase == AttackRunPhase.Execute)
                    {
                        var startPayload = BuildAttackRunPayload(entity, profile.ValueRO, config.ValueRO, "Start", string.Empty);
                        eventBuffer.AddEvent(EventAttackRunStart, tick, SourceId, startPayload);
                        telemetry.ValueRW.AttackRunActive = 1;
                    }
                    else if (telemetry.ValueRO.LastPhase == AttackRunPhase.Execute && telemetry.ValueRO.AttackRunActive == 1)
                    {
                        var outcome = ResolveAttackRunOutcome(profile.ValueRO.Phase);
                        var endPayload = BuildAttackRunPayload(entity, profile.ValueRO, config.ValueRO, "End", outcome);
                        eventBuffer.AddEvent(EventAttackRunEnd, tick, SourceId, endPayload);
                        telemetry.ValueRW.AttackRunActive = 0;
                    }

                    telemetry.ValueRW.LastPhase = profile.ValueRO.Phase;
                    telemetry.ValueRW.LastPhaseTick = tick;
                }
            }
        }

        private bool TryGetTelemetryEventBuffer(ref SystemState state, out DynamicBuffer<TelemetryEvent> buffer)
        {
            buffer = default;
            if (!SystemAPI.TryGetSingleton<TelemetryStreamSingleton>(out var telemetryRef))
            {
                return false;
            }

            if (telemetryRef.Entity == Entity.Null || !state.EntityManager.HasBuffer<TelemetryEvent>(telemetryRef.Entity))
            {
                return false;
            }

            buffer = state.EntityManager.GetBuffer<TelemetryEvent>(telemetryRef.Entity);
            return true;
        }

        private static uint ComputeProfileHash(in StrikeCraftProfile profile, in AttackRunConfig config)
        {
            uint hash = 2166136261u;
            hash = HashStep(hash, (uint)profile.Role);
            hash = HashStep(hash, (uint)config.DeliveryType);
            hash = HashStep(hash, (uint)config.ApproachVector);
            hash = HashStep(hash, math.asuint(config.AttackRange));
            hash = HashStep(hash, math.asuint(config.DisengageRange));
            hash = HashStep(hash, config.MaxPasses);
            hash = HashStep(hash, config.ReattackEnabled);
            hash = HashStep(hash, math.asuint(config.FormationSpacing));
            hash = HashStep(hash, math.asuint((float)config.ApproachSpeedMod));
            hash = HashStep(hash, math.asuint((float)config.AttackSpeedMod));
            return hash;
        }

        private static uint HashStep(uint current, uint value)
        {
            const uint prime = 16777619u;
            return unchecked((current ^ value) * prime);
        }

        private static FixedString128Bytes BuildBehaviorProfilePayload(Entity entity, in StrikeCraftProfile profile, in AttackRunConfig config, uint profileHash)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddEntity("carrier", profile.Carrier);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("deliveryType", config.DeliveryType.ToString());
            writer.AddUInt("profileHash", profileHash);
            writer.AddFloat("attackRange", config.AttackRange);
            writer.AddFloat("disengageRange", config.DisengageRange);
            writer.AddFloat("formationSpacing", config.FormationSpacing);
            writer.AddFloat("approachSpeed", (float)config.ApproachSpeedMod);
            writer.AddFloat("attackSpeed", (float)config.AttackSpeedMod);
            writer.AddInt("maxPasses", config.MaxPasses);
            writer.AddBool("reattackEnabled", config.ReattackEnabled == 1);
            return writer.Build();
        }

        private static FixedString128Bytes BuildPhaseChangePayload(Entity entity, in StrikeCraftProfile profile, in AttackRunConfig config, AttackRunPhase previousPhase, uint duration)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("oldPhase", previousPhase.ToString());
            writer.AddString("newPhase", profile.Phase.ToString());
            writer.AddUInt("phaseDurationTicks", duration);
            writer.AddString("reason", ResolvePhaseReason(previousPhase, profile.Phase));
            writer.AddEntity("target", profile.Target);
            writer.AddEntity("carrier", profile.Carrier);
            writer.AddString("deliveryType", config.DeliveryType.ToString());
            return writer.Build();
        }

        private static FixedString128Bytes BuildAttackRunPayload(Entity entity, in StrikeCraftProfile profile, in AttackRunConfig config, string state, string reason)
        {
            var writer = new TelemetryJsonWriter();
            writer.AddEntity("entity", entity);
            writer.AddString("role", profile.Role.ToString());
            writer.AddString("deliveryType", config.DeliveryType.ToString());
            writer.AddString("phase", profile.Phase.ToString());
            writer.AddString("state", state);
            writer.AddString("reason", reason);
            writer.AddEntity("target", profile.Target);
            writer.AddInt("passCount", profile.PassCount);
            writer.AddBool("weaponsExpended", profile.WeaponsExpended != 0);
            return writer.Build();
        }

        private static string ResolvePhaseReason(AttackRunPhase previous, AttackRunPhase current)
        {
            if (previous == AttackRunPhase.Docked && current == AttackRunPhase.Launching)
                return "LaunchCommand";
            if (previous == AttackRunPhase.Launching && current == AttackRunPhase.FormUp)
                return "LaunchComplete";
            if (previous == AttackRunPhase.FormUp && current == AttackRunPhase.Approach)
                return "WingReady";
            if (previous == AttackRunPhase.Approach && current == AttackRunPhase.Execute)
                return "AttackRangeReached";
            if (previous == AttackRunPhase.Execute && current == AttackRunPhase.Disengage)
                return "PassComplete";
            if (previous == AttackRunPhase.Disengage && current == AttackRunPhase.Approach)
                return "Reattack";
            if (previous == AttackRunPhase.Disengage && current == AttackRunPhase.Return)
                return "ReturnToCarrier";
            if (previous == AttackRunPhase.Return && current == AttackRunPhase.Landing)
                return "CarrierReached";
            if (previous == AttackRunPhase.Landing && current == AttackRunPhase.Docked)
                return "Landed";
            if (current == AttackRunPhase.Disengage)
                return "LostTarget";
            return "Unknown";
        }

        private static string ResolveAttackRunOutcome(AttackRunPhase nextPhase)
        {
            return nextPhase switch
            {
                AttackRunPhase.Disengage => "PassComplete",
                AttackRunPhase.Return => "Aborted",
                AttackRunPhase.Docked => "Cancelled",
                AttackRunPhase.Landing => "RTB",
                _ => "Transition"
            };
        }
    }
}
