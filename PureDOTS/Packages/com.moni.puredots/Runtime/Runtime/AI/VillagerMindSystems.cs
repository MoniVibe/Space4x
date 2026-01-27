using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Telemetry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Runtime.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerMindSystemGroup))]
    [UpdateBefore(typeof(VillagerNeedDecaySystem))]
    public partial struct VillagerTelemetryAttachSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameWorldTag>();
            state.RequireForUpdate<VillagerNeedState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<VillagerNeedState>>()
                         .WithNone<VillagerCoreTelemetry>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new VillagerCoreTelemetry());
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<VillagerNeedState>>()
                         .WithNone<VillagerThreatState>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new VillagerThreatState
                {
                    ThreatEntity = Entity.Null,
                    ThreatDirection = new float3(0f, 0f, 1f),
                    Urgency = 0f,
                    HasLineOfSight = 0
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerMindSystemGroup))]
    public partial struct VillagerNeedDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameWorldTag>();
            state.RequireForUpdate<VillagerNeedState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out _))
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (needs, tuning) in SystemAPI.Query<RefRW<VillagerNeedState>, RefRO<VillagerNeedTuning>>())
            {
                var maxUrgency = math.max(0.01f, tuning.ValueRO.MaxUrgency);
                var needValue = needs.ValueRO;

                needValue.HungerUrgency = IntegrateNeed(needValue.HungerUrgency, tuning.ValueRO.HungerDecayPerTick, maxUrgency);
                needValue.RestUrgency = IntegrateNeed(needValue.RestUrgency, tuning.ValueRO.RestDecayPerTick, maxUrgency);
                needValue.FaithUrgency = IntegrateNeed(needValue.FaithUrgency, tuning.ValueRO.FaithDecayPerTick, maxUrgency);
                needValue.SafetyUrgency = IntegrateNeed(needValue.SafetyUrgency, tuning.ValueRO.SafetyDecayPerTick, maxUrgency);
                needValue.SocialUrgency = IntegrateNeed(needValue.SocialUrgency, tuning.ValueRO.SocialDecayPerTick, maxUrgency);
                needValue.WorkUrgency = IntegrateNeed(needValue.WorkUrgency, tuning.ValueRO.WorkPressurePerTick, maxUrgency);

                needs.ValueRW = needValue;
            }
        }

        private static float IntegrateNeed(float current, float delta, float max)
        {
            return math.clamp(current + delta, 0f, max);
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerMindSystemGroup))]
    [UpdateAfter(typeof(VillagerNeedDecaySystem))]
    public partial struct VillagerFocusBudgetSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameWorldTag>();
            state.RequireForUpdate<FocusBudget>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var reservationLookup = SystemAPI.GetBufferLookup<FocusBudgetReservation>(false);
            reservationLookup.Update(ref state);

            foreach (var (focus, entity) in SystemAPI.Query<RefRW<FocusBudget>>().WithEntityAccess())
            {
                var focusValue = focus.ValueRO;
                var reserved = AccumulateReservations(ref reservationLookup, entity, timeState.Tick);

                if (focusValue.IsLocked != 0)
                {
                    focusValue.Reserved = reserved;
                    focus.ValueRW = focusValue;
                    continue;
                }

                focusValue.Current = math.min(focusValue.Max, focusValue.Current + focusValue.RegenPerTick);

                focusValue.Reserved = reserved;
                focusValue.Current = math.max(focusValue.Reserved, focusValue.Current);

                focus.ValueRW = focusValue;
            }
        }

        private static float AccumulateReservations(ref BufferLookup<FocusBudgetReservation> lookup, Entity entity, uint currentTick)
        {
            if (!lookup.HasBuffer(entity))
            {
                return 0f;
            }

            float total = 0f;
            var buffer = lookup[entity];

            for (int i = buffer.Length - 1; i >= 0; i--)
            {
                var reservation = buffer[i];
                if (reservation.ExpirationTick != 0 && reservation.ExpirationTick <= currentTick)
                {
                    buffer.RemoveAt(i);
                    continue;
                }

                total += reservation.Amount;
            }

            return total;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerMindSystemGroup))]
    [UpdateAfter(typeof(VillagerFocusBudgetSystem))]
    public partial struct VillagerGoalSelectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameWorldTag>();
            state.RequireForUpdate<VillagerGoalState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return;
            }

            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var threatLookup = SystemAPI.GetComponentLookup<VillagerThreatState>(true);
            threatLookup.Update(ref state);
            var biasLookup = SystemAPI.GetComponentLookup<VillagerNeedBias>(true);
            biasLookup.Update(ref state);

            foreach (var (needs, focus, goal, entity) in SystemAPI
                     .Query<RefRO<VillagerNeedState>, RefRO<FocusBudget>, RefRW<VillagerGoalState>>()
                     .WithEntityAccess())
            {
                var nextGoal = VillagerGoal.Idle;
                float maxUrgency = 0f;

                var canAct = focus.ValueRO.IsLocked == 0 &&
                             focus.ValueRO.Current > (focus.ValueRO.Reserved + 0.01f);

                if (threatLookup.HasComponent(entity))
                {
                    var threat = threatLookup[entity];
                    if (threat.Urgency > 0.01f && canAct)
                    {
                        nextGoal = VillagerGoal.Flee;
                        maxUrgency = threat.Urgency;
                    }
                }

                if (nextGoal != VillagerGoal.Flee && canAct)
                {
                    if (biasLookup.HasComponent(entity))
                    {
                        EvaluateNeeds(needs.ValueRO, biasLookup[entity], ref nextGoal, ref maxUrgency);
                    }
                    else
                    {
                        EvaluateNeeds(needs.ValueRO, ref nextGoal, ref maxUrgency);
                    }
                }

                if (!canAct)
                {
                    nextGoal = VillagerGoal.Idle;
                    maxUrgency = 0f;
                }

                if (nextGoal != goal.ValueRO.CurrentGoal)
                {
                    goal.ValueRW.PreviousGoal = goal.ValueRO.CurrentGoal;
                    goal.ValueRW.CurrentGoal = nextGoal;
                    goal.ValueRW.LastGoalChangeTick = timeState.Tick;
                }

                goal.ValueRW.CurrentGoalUrgency = maxUrgency;
            }
        }

        private static void EvaluateNeeds(in VillagerNeedState needs, ref VillagerGoal nextGoal, ref float maxUrgency)
        {
            SelectGoal(needs.HungerUrgency, VillagerGoal.Eat, ref nextGoal, ref maxUrgency);
            SelectGoal(needs.RestUrgency, VillagerGoal.Sleep, ref nextGoal, ref maxUrgency);
            SelectGoal(needs.FaithUrgency, VillagerGoal.Pray, ref nextGoal, ref maxUrgency);
            SelectGoal(needs.SafetyUrgency, VillagerGoal.SeekShelter, ref nextGoal, ref maxUrgency);
            SelectGoal(needs.SocialUrgency, VillagerGoal.Socialize, ref nextGoal, ref maxUrgency);
            SelectGoal(needs.WorkUrgency, VillagerGoal.Work, ref nextGoal, ref maxUrgency);
        }

        private static void EvaluateNeeds(in VillagerNeedState needs, in VillagerNeedBias bias, ref VillagerGoal nextGoal, ref float maxUrgency)
        {
            SelectGoal(ApplyBias(needs.HungerUrgency, bias.HungerWeight), VillagerGoal.Eat, ref nextGoal, ref maxUrgency);
            SelectGoal(ApplyBias(needs.RestUrgency, bias.RestWeight), VillagerGoal.Sleep, ref nextGoal, ref maxUrgency);
            SelectGoal(ApplyBias(needs.FaithUrgency, bias.FaithWeight), VillagerGoal.Pray, ref nextGoal, ref maxUrgency);
            SelectGoal(ApplyBias(needs.SafetyUrgency, bias.SafetyWeight), VillagerGoal.SeekShelter, ref nextGoal, ref maxUrgency);
            SelectGoal(ApplyBias(needs.SocialUrgency, bias.SocialWeight), VillagerGoal.Socialize, ref nextGoal, ref maxUrgency);
            SelectGoal(ApplyBias(needs.WorkUrgency, bias.WorkWeight), VillagerGoal.Work, ref nextGoal, ref maxUrgency);
        }

        private static float ApplyBias(float urgency, float weight)
        {
            var clampedWeight = weight <= 0f ? 1f : weight;
            return urgency * clampedWeight;
        }

        private static void SelectGoal(float urgency, VillagerGoal goal, ref VillagerGoal nextGoal, ref float maxUrgency)
        {
            if (urgency <= maxUrgency)
            {
                return;
            }

            maxUrgency = urgency;
            nextGoal = goal;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerMindSystemGroup))]
    [UpdateAfter(typeof(VillagerGoalSelectionSystem))]
    public partial struct VillagerFleeIntentSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameWorldTag>();
            state.RequireForUpdate<VillagerFleeIntent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (SystemAPI.TryGetSingleton<RewindState>(out var rewind) && rewind.Mode != RewindMode.Record)
            {
                return;
            }

            foreach (var (goal, threat, intent) in SystemAPI
                     .Query<RefRO<VillagerGoalState>, RefRO<VillagerThreatState>, RefRW<VillagerFleeIntent>>())
            {
                if (goal.ValueRO.CurrentGoal == VillagerGoal.Flee && threat.ValueRO.Urgency > 0f)
                {
                    var exitDir = math.normalizesafe(-threat.ValueRO.ThreatDirection, new float3(0f, 0f, 1f));
                    intent.ValueRW.ThreatEntity = threat.ValueRO.ThreatEntity;
                    intent.ValueRW.ExitDirection = exitDir;
                    intent.ValueRW.Urgency = threat.ValueRO.Urgency;
                    intent.ValueRW.RequiresLineOfSight = threat.ValueRO.HasLineOfSight;
                }
                else
                {
                    intent.ValueRW.Urgency = math.max(0f, intent.ValueRO.Urgency - 0.05f);
                    if (intent.ValueRW.Urgency <= 0f)
                    {
                        intent.ValueRW.ThreatEntity = Entity.Null;
                    }
                }
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(VillagerMindSystemGroup))]
    [UpdateAfter(typeof(VillagerFleeIntentSystem))]
    public partial struct VillagerTelemetryUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameWorldTag>();
            state.RequireForUpdate<VillagerCoreTelemetry>();
            state.RequireForUpdate<VillagerNeedTuning>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (needs, focus, goal, tuning, telemetry) in SystemAPI
                     .Query<RefRO<VillagerNeedState>, RefRO<FocusBudget>, RefRO<VillagerGoalState>, RefRO<VillagerNeedTuning>, RefRW<VillagerCoreTelemetry>>())
            {
                var needMax = math.max(math.max(math.max(needs.ValueRO.HungerUrgency, needs.ValueRO.RestUrgency),
                        math.max(needs.ValueRO.FaithUrgency, needs.ValueRO.SafetyUrgency)),
                    math.max(needs.ValueRO.SocialUrgency, needs.ValueRO.WorkUrgency));

                telemetry.ValueRW.NeedAccumulator += needMax;
                telemetry.ValueRW.NeedSampleCount += 1;
                telemetry.ValueRW.FocusSnapshot = focus.ValueRO.Current;

                var isFleeing = goal.ValueRO.CurrentGoal == VillagerGoal.Flee;
                var wasFleeing = telemetry.ValueRO.WasFleeingLastTick != 0;

                if (isFleeing && !wasFleeing)
                {
                    telemetry.ValueRW.FleeEvents += 1;
                }

                telemetry.ValueRW.WasFleeingLastTick = (byte)(isFleeing ? 1 : 0);

                if (focus.ValueRO.Current < -0.001f)
                {
                    telemetry.ValueRW.FocusNegativeDetected = 1;
                }

                var maxAllowed = tuning.ValueRO.MaxUrgency + 0.001f;
                if (needMax > maxAllowed || needMax < -0.001f)
                {
                    telemetry.ValueRW.NeedExceededDetected = 1;
                }
            }
        }
    }
}
