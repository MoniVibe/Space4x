using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// Samples hazard grid gradient and computes avoidance steering vector.
    /// Per ship: sample grid gradient (±1 cells), compute V_avoid = -∇Risk.
    /// Applies ReactionSec delay via ring buffer of sampled risks.
    /// Games configure reaction time via AvoidanceProfile.ReactionSec.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(AccumulateHazardGridSystem))]
    public partial struct AvoidanceSenseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            state.RequireForUpdate<HazardGridSingleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var gridSingleton = SystemAPI.GetSingleton<HazardGridSingleton>();
            if (!SystemAPI.HasComponent<HazardGrid>(gridSingleton.GridEntity))
            {
                return;
            }

            var grid = SystemAPI.GetComponent<HazardGrid>(gridSingleton.GridEntity);
            if (!grid.Risk.IsCreated)
            {
                return;
            }

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;
            uint currentTick = timeState.Tick;

            state.Dependency = new AvoidanceSenseJob
            {
                Grid = grid,
                RiskData = grid.Risk.Value.Risk,
                CurrentTick = currentTick,
                DeltaTime = deltaTime
            }.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct AvoidanceSenseJob : IJobEntity
        {
            [ReadOnly] public HazardGrid Grid;
            [ReadOnly] public BlobArray<float> RiskData;
            public uint CurrentTick;
            public float DeltaTime;

            void Execute(
                Entity entity,
                ref HazardAvoidanceState avoidanceState,
                ref AvoidanceReactionState reactionState,
                ref DynamicBuffer<AvoidanceReactionSample> reactionBuffer,
                in AvoidanceProfile profile,
                in LocalTransform transform)
            {
                float3 pos = transform.Position;

                // Sample risk gradient around ship position
                int3 cell = CellOf(pos, Grid);

                // Sample neighboring cells (±1 in each axis)
                float rx = SampleRisk(cell + new int3(1, 0, 0)) - SampleRisk(cell + new int3(-1, 0, 0));
                float ry = SampleRisk(cell + new int3(0, 1, 0)) - SampleRisk(cell + new int3(0, -1, 0));
                float rz = Grid.Size.z > 1
                    ? SampleRisk(cell + new int3(0, 0, 1)) - SampleRisk(cell + new int3(0, 0, -1))
                    : 0f; // 2D grid

                // Compute avoidance vector (negative gradient)
                float3 gradient = new float3(rx, ry, rz);
                float gradientLength = math.length(gradient);

                float3 newAdjustment = float3.zero;
                float newUrgency = 0f;

                if (gradientLength > 1e-6f)
                {
                    float3 vAvoid = -math.normalize(gradient);

                    // Scale by risk magnitude
                    float currentRisk = SampleRisk(cell);
                    float avoidanceWeight = math.saturate(currentRisk / profile.BreakFormationThresh);

                    newAdjustment = vAvoid * avoidanceWeight;
                    newUrgency = avoidanceWeight;
                }

                // Apply ReactionSec delay via ring buffer
                if (profile.ReactionSec > 0f && reactionBuffer.Capacity > 0)
                {
                    // Store current sample in ring buffer
                    var sample = new AvoidanceReactionSample
                    {
                        Adjustment = newAdjustment,
                        Urgency = newUrgency,
                        SampleTick = CurrentTick
                    };

                    // Write to ring buffer
                    int bufferCapacity = reactionBuffer.Capacity;
                    if (reactionBuffer.Length < bufferCapacity)
                    {
                        reactionBuffer.Add(sample);
                        reactionState.SampleCount = (byte)reactionBuffer.Length;
                    }
                    else
                    {
                        // Overwrite oldest entry
                        reactionBuffer[reactionState.WriteIndex] = sample;
                    }
                    reactionState.WriteIndex = (byte)((reactionState.WriteIndex + 1) % bufferCapacity);

                    // Calculate delay in ticks (assuming 60 ticks/sec)
                    uint delayTicks = (uint)(profile.ReactionSec * 60f);
                    uint targetTick = CurrentTick >= delayTicks ? CurrentTick - delayTicks : 0;

                    // Find delayed sample (oldest sample that's at least delayTicks old)
                    float3 delayedAdjustment = float3.zero;
                    float delayedUrgency = 0f;
                    uint bestTickDiff = uint.MaxValue;

                    for (int i = 0; i < reactionBuffer.Length; i++)
                    {
                        var s = reactionBuffer[i];
                        if (s.SampleTick <= targetTick)
                        {
                            uint tickDiff = targetTick - s.SampleTick;
                            if (tickDiff < bestTickDiff)
                            {
                                bestTickDiff = tickDiff;
                                delayedAdjustment = s.Adjustment;
                                delayedUrgency = s.Urgency;
                            }
                        }
                    }

                    // If we found a valid delayed sample, use it
                    if (bestTickDiff != uint.MaxValue)
                    {
                        avoidanceState.CurrentAdjustment = delayedAdjustment;
                        avoidanceState.AvoidanceUrgency = delayedUrgency;
                    }
                    else
                    {
                        // Buffer not filled yet - use immediate response
                        avoidanceState.CurrentAdjustment = newAdjustment;
                        avoidanceState.AvoidanceUrgency = newUrgency;
                    }
                }
                else
                {
                    // No reaction delay configured - immediate response
                    avoidanceState.CurrentAdjustment = newAdjustment;
                    avoidanceState.AvoidanceUrgency = newUrgency;
                }
            }

            private float SampleRisk(int3 cell)
            {
                // Clamp cell to grid bounds
                cell = math.clamp(cell, int3.zero, Grid.Size - 1);

                int index = Flatten(cell, Grid);
                if (index >= 0 && index < RiskData.Length)
                {
                    return RiskData[index];
                }

                return 0f;
            }

            private static int3 CellOf(float3 pos, HazardGrid grid)
            {
                float3 local = (pos - grid.Origin) / grid.Cell;
                return (int3)math.floor(local);
            }

            private static int Flatten(int3 cell, HazardGrid grid)
            {
                return (cell.z * grid.Size.y + cell.y) * grid.Size.x + cell.x;
            }
        }
    }
}

