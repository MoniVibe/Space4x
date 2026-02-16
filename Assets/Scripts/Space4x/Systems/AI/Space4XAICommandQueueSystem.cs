using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Systems.AI
{
    /// <summary>
    /// Processes AI command queues, performs pre-flight checks, and evaluates threats before order execution.
    /// Implements the order pipeline from AICommanders.md: Receive Directive -> Pre-Flight Checks -> Threat Evaluation -> Execution.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4X.Registry.Space4XAffiliationComplianceSystem))]
    public partial struct Space4XAICommandQueueSystem : ISystem
    {
        private ComponentLookup<AlignmentTriplet> _alignmentLookup;
        private ComponentLookup<PreFlightCheck> _preFlightLookup;
        private ComponentLookup<ThreatAssessment> _threatLookup;
        private BufferLookup<ComplianceBreach> _breachLookup;
        private BufferLookup<TopStance> _outlookLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            
            _alignmentLookup = state.GetComponentLookup<AlignmentTriplet>(true);
            _preFlightLookup = state.GetComponentLookup<PreFlightCheck>(true);
            _threatLookup = state.GetComponentLookup<ThreatAssessment>(true);
            _breachLookup = state.GetBufferLookup<ComplianceBreach>(true);
            _outlookLookup = state.GetBufferLookup<TopStance>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            
            // Skip if paused or rewinding (orders must re-evaluate after rewind)
            if (timeState.IsPaused)
            {
                return;
            }

            _alignmentLookup.Update(ref state);
            _preFlightLookup.Update(ref state);
            _threatLookup.Update(ref state);
            _breachLookup.Update(ref state);
            _outlookLookup.Update(ref state);

            var job = new ProcessCommandQueueJob
            {
                CurrentTick = timeState.Tick,
                DeltaTime = timeState.FixedDeltaTime,
                AlignmentLookup = _alignmentLookup,
                PreFlightLookup = _preFlightLookup,
                ThreatLookup = _threatLookup,
                BreachLookup = _breachLookup,
                OutlookLookup = _outlookLookup
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(AICommandQueue))]
        public partial struct ProcessCommandQueueJob : IJobEntity
        {
            public uint CurrentTick;
            public float DeltaTime;
            [ReadOnly] public ComponentLookup<AlignmentTriplet> AlignmentLookup;
            [ReadOnly] public ComponentLookup<PreFlightCheck> PreFlightLookup;
            [ReadOnly] public ComponentLookup<ThreatAssessment> ThreatLookup;
            [ReadOnly] public BufferLookup<ComplianceBreach> BreachLookup;
            [ReadOnly] public BufferLookup<TopStance> OutlookLookup;

            public void Execute(ref DynamicBuffer<AIOrder> orders, in AICommandQueue queue, Entity entity)
            {
                // Process orders in priority order
                for (int i = 0; i < orders.Length; i++)
                {
                    var order = orders[i];
                    
                    // Skip completed/failed/cancelled orders
                    if (order.Status == AIOrderStatus.Completed || 
                        order.Status == AIOrderStatus.Failed || 
                        order.Status == AIOrderStatus.Cancelled)
                    {
                        continue;
                    }

                    // Check expiration
                    if (order.ExpirationTick > 0 && CurrentTick > order.ExpirationTick)
                    {
                        order.Status = AIOrderStatus.Failed;
                        orders[i] = order;
                        continue;
                    }

                    // Process based on status
                    switch (order.Status)
                    {
                        case AIOrderStatus.Pending:
                            // Move to pre-flight check
                            order.Status = AIOrderStatus.PreFlightCheck;
                            orders[i] = order;
                            break;

                        case AIOrderStatus.PreFlightCheck:
                            ProcessPreFlightCheck(ref order, entity);
                            orders[i] = order;
                            break;

                        case AIOrderStatus.ThreatEvaluation:
                            ProcessThreatEvaluation(ref order, entity);
                            orders[i] = order;
                            break;

                        case AIOrderStatus.Executing:
                            // Execution handled by other systems (movement, combat, etc.)
                            // This system just validates readiness
                            break;
                    }
                }

                // Remove completed/failed orders after a delay
                for (int i = orders.Length - 1; i >= 0; i--)
                {
                    var order = orders[i];
                    if ((order.Status == AIOrderStatus.Completed || order.Status == AIOrderStatus.Failed) &&
                        CurrentTick > order.IssueTick + 60) // Keep for 60 ticks for feedback
                    {
                        orders.RemoveAt(i);
                    }
                }
            }

            private void ProcessPreFlightCheck(ref AIOrder order, Entity entity)
            {
                // Get alignment to determine tolerance thresholds
                var alignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);
                
                var lawfulness = AlignmentMath.Lawfulness(alignment);
                var chaos = AlignmentMath.Chaos(alignment);

                // Check if pre-flight check component exists, create if needed
                PreFlightCheck preFlight;
                if (PreFlightLookup.HasComponent(entity))
                {
                    preFlight = PreFlightLookup[entity];
                }
                else
                {
                    // Default values - in real implementation, these would come from vessel state
                    preFlight = new PreFlightCheck
                    {
                        ProvisionsLevel = (half)0.8f,
                        CrewMorale = (half)0.7f,
                        HullIntegrity = (half)0.9f,
                        CheckPassed = 0,
                        CheckTick = 0
                    };
                }

                // Alignment influences tolerance: lawful captains insist on full readiness,
                // chaotic captains may cut corners
                var provisionsThreshold = math.lerp(0.5f, 0.8f, lawfulness);
                var moraleThreshold = math.lerp(0.4f, 0.7f, lawfulness);
                var hullThreshold = math.lerp(0.6f, 0.85f, lawfulness);

                var provisionsOk = (float)preFlight.ProvisionsLevel >= provisionsThreshold;
                var moraleOk = (float)preFlight.CrewMorale >= moraleThreshold;
                var hullOk = (float)preFlight.HullIntegrity >= hullThreshold;

                preFlight.CheckPassed = (byte)(provisionsOk && moraleOk && hullOk ? 1 : 0);
                preFlight.CheckTick = CurrentTick;

                if (preFlight.CheckPassed == 1)
                {
                    order.Status = AIOrderStatus.ThreatEvaluation;
                }
                else
                {
                    // Check compliance breaches - mutiny/desertion may prevent execution
                    if (BreachLookup.HasBuffer(entity))
                    {
                        var breaches = BreachLookup[entity];
                        for (int j = 0; j < breaches.Length; j++)
                        {
                            if (breaches[j].Type == ComplianceBreachType.Mutiny || 
                                breaches[j].Type == ComplianceBreachType.Desertion)
                            {
                                // Severe breach prevents order execution
                                order.Status = AIOrderStatus.Failed;
                                return;
                            }
                        }
                    }

                    // Delay execution - will retry next frame
                    // In practice, captains would request repairs/provisions
                }
            }

            private void ProcessThreatEvaluation(ref AIOrder order, Entity entity)
            {
                // Get alignment for threat tolerance
                var alignment = AlignmentLookup.HasComponent(entity) 
                    ? AlignmentLookup[entity] 
                    : default(AlignmentTriplet);
                
                var chaos = AlignmentMath.Chaos(alignment);

                // Check if threat assessment exists, create if needed
                ThreatAssessment threat;
                if (ThreatLookup.HasComponent(entity))
                {
                    threat = ThreatLookup[entity];
                }
                else
                {
                    // Default values - in real implementation, these would come from exploration/diplomacy data
                    threat = new ThreatAssessment
                    {
                        LocalThreatLevel = (half)0.3f,
                        RouteThreatLevel = (half)0.2f,
                        DefensiveCapability = (half)0.7f,
                        CanProceed = 0,
                        AssessmentTick = 0
                    };
                }

                // Use order's threat tolerance if set, otherwise derive from alignment
                var threatTolerance = (float)order.ThreatTolerance;
                if (threatTolerance <= 0f)
                {
                    // Chaotic captains accept more risk
                    threatTolerance = math.lerp(0.3f, 0.7f, chaos);
                }

                // Captains will never engage mining or hauling if threat exceeds defensive capability
                var maxAcceptableThreat = math.min((float)threat.DefensiveCapability, threatTolerance);
                var routeThreat = math.max((float)threat.LocalThreatLevel, (float)threat.RouteThreatLevel);

                threat.CanProceed = (byte)(routeThreat <= maxAcceptableThreat ? 1 : 0);
                threat.AssessmentTick = CurrentTick;

                if (threat.CanProceed == 1)
                {
                    order.Status = AIOrderStatus.Executing;
                }
                else
                {
                    // Threat too high - escalate or delay
                    // In practice, captains would request escorts or reroute
                    // For now, mark as escalated
                    order.Status = AIOrderStatus.Escalated;
                }
            }
        }
    }
}

