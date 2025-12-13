using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Registry
{
    using Debug = UnityEngine.Debug;

    
    /// <summary>
    /// Calculates arrival positions and formations for reinforcing units.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XReinforcementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReinforcementRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (request, tactics, precision, arrival, transform, entity) in
                SystemAPI.Query<RefRW<ReinforcementRequest>, RefRO<ReinforcementTactics>, RefRO<WarpPrecision>, RefRW<ReinforcementArrival>, RefRO<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Skip if already calculated or no request
                if (request.ValueRO.Acknowledged == 0 || arrival.ValueRO.HasArrived == 1)
                {
                    continue;
                }

                // Calculate arrival based on tactic and precision
                CalculateArrival(
                    ref arrival.ValueRW,
                    request.ValueRO,
                    tactics.ValueRO,
                    precision.ValueRO,
                    transform.ValueRO.Position,
                    currentTick,
                    entity
                );
            }
        }

        private void CalculateArrival(
            ref ReinforcementArrival arrival,
            in ReinforcementRequest request,
            in ReinforcementTactics tactics,
            in WarpPrecision precision,
            float3 currentPosition,
            uint currentTick,
            Entity entity)
        {
            float3 basePosition;
            quaternion baseRotation;

            // Calculate base arrival position based on tactic
            switch (tactics.PreferredTactic)
            {
                case ReinforcementTactic.Flanking:
                    // Flank from 90 degrees
                    basePosition = ReinforcementUtility.CalculateFlankingPosition(
                        request.TargetPosition,
                        request.EnemyCenter,
                        90f,
                        tactics.StandoffDistance
                    );
                    baseRotation = quaternion.LookRotationSafe(request.EnemyCenter - basePosition, new float3(0, 1, 0));
                    break;

                case ReinforcementTactic.DefensiveScreen:
                    // Position between allies and enemies
                    basePosition = ReinforcementUtility.CalculateScreenPosition(
                        request.TargetPosition,
                        request.EnemyCenter,
                        0.3f // 30% of distance toward enemy
                    );
                    baseRotation = quaternion.LookRotationSafe(request.EnemyCenter - basePosition, new float3(0, 1, 0));
                    break;

                case ReinforcementTactic.AggressiveDrop:
                    // Drop close to enemies
                    float3 toAllies = math.normalize(request.TargetPosition - request.EnemyCenter);
                    basePosition = request.EnemyCenter + toAllies * precision.MinSafeDistance;
                    baseRotation = quaternion.LookRotationSafe(request.EnemyCenter - basePosition, new float3(0, 1, 0));
                    break;

                case ReinforcementTactic.CautiousApproach:
                    // Arrive far from enemies
                    float3 awayFromEnemy = math.normalize(request.TargetPosition - request.EnemyCenter);
                    basePosition = request.TargetPosition + awayFromEnemy * tactics.StandoffDistance;
                    baseRotation = quaternion.LookRotationSafe(request.EnemyCenter - basePosition, new float3(0, 1, 0));
                    break;

                case ReinforcementTactic.RearAssault:
                    // Arrive behind enemy lines
                    basePosition = ReinforcementUtility.CalculateFlankingPosition(
                        request.TargetPosition,
                        request.EnemyCenter,
                        180f, // Directly behind
                        tactics.StandoffDistance * 0.5f
                    );
                    baseRotation = quaternion.LookRotationSafe(request.EnemyCenter - basePosition, new float3(0, 1, 0));
                    break;

                case ReinforcementTactic.CoordinatedFormation:
                    // Standard position with formation offset
                    float3 toEnemy = math.normalize(request.EnemyCenter - request.TargetPosition);
                    basePosition = request.TargetPosition + toEnemy * tactics.StandoffDistance * 0.5f;
                    baseRotation = quaternion.LookRotationSafe(toEnemy, new float3(0, 1, 0));
                    break;

                case ReinforcementTactic.Scattered:
                default:
                    // Random position near target
                    basePosition = request.TargetPosition;
                    baseRotation = quaternion.LookRotationSafe(
                        math.normalizesafe(request.EnemyCenter - request.TargetPosition),
                        new float3(0, 1, 0)
                    );
                    break;
            }

            // Apply scatter based on tech level
            uint seed = (uint)entity.Index * 31337 + currentTick;
            arrival.ArrivalPosition = ReinforcementUtility.ApplyPositionScatter(
                basePosition,
                precision.PositionScatter,
                seed
            );

            arrival.ArrivalRotation = ReinforcementUtility.ApplyOrientationScatter(
                baseRotation,
                (float)precision.OrientationScatter,
                seed + 1
            );

            // Calculate arrival timing with scatter
            uint baseArrivalDelay = CalculateWarpTime(currentPosition, arrival.ArrivalPosition);
            var random = new Unity.Mathematics.Random(seed + 2);
            int timingScatter = random.NextInt(-precision.TimingScatter, precision.TimingScatter + 1);
            arrival.ArrivalTick = currentTick + baseArrivalDelay + (uint)math.max(0, timingScatter);

            arrival.UsedTactic = tactics.PreferredTactic;
            arrival.HasArrived = 0;
        }

        private uint CalculateWarpTime(float3 from, float3 to)
        {
            float distance = math.distance(from, to);
            // Base 10 ticks + 1 tick per 100 units
            return (uint)(10 + distance / 100f);
        }
    }

    /// <summary>
    /// Processes group reinforcement coordination.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XReinforcementSystem))]
    public partial struct Space4XReinforcementGroupSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReinforcementGroup>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (group, request, tactics, arrival, entity) in
                SystemAPI.Query<DynamicBuffer<ReinforcementGroup>, RefRO<ReinforcementRequest>, RefRO<ReinforcementTactics>, RefRW<ReinforcementArrival>>()
                    .WithEntityAccess())
            {
                if (group.Length == 0 || tactics.ValueRO.CoordinateArrival == 0)
                {
                    continue;
                }

                // Calculate formation offsets for group members
                ApplyGroupFormation(group, tactics.ValueRO, arrival.ValueRO, ref state);
            }
        }

        private void ApplyGroupFormation(
            DynamicBuffer<ReinforcementGroup> group,
            in ReinforcementTactics tactics,
            in ReinforcementArrival leaderArrival,
            ref SystemState state)
        {
            float spacing = tactics.StandoffDistance * 0.1f; // 10% of standoff as spacing

            for (int i = 0; i < group.Length; i++)
            {
                var member = group[i];

                if (!SystemAPI.HasComponent<ReinforcementArrival>(member.Entity))
                {
                    continue;
                }

                var memberArrival = SystemAPI.GetComponent<ReinforcementArrival>(member.Entity);

                // Calculate formation offset
                float3 offset = ReinforcementUtility.CalculateFormationOffset(
                    tactics.Formation,
                    member.Slot,
                    group.Length + 1, // +1 for leader
                    spacing
                );

                // Transform offset to world space based on leader rotation
                float3 worldOffset = math.rotate(leaderArrival.ArrivalRotation, offset);

                // Update member arrival
                memberArrival.ArrivalPosition = leaderArrival.ArrivalPosition + worldOffset;
                memberArrival.ArrivalRotation = leaderArrival.ArrivalRotation;
                memberArrival.ArrivalTick = leaderArrival.ArrivalTick + member.ArrivalDelay;
                memberArrival.FormationSlot = member.Slot;

                SystemAPI.SetComponent(member.Entity, memberArrival);

                // Mark as ready
                member.IsReady = 1;
                group[i] = member;
            }
        }
    }

    /// <summary>
    /// Executes reinforcement arrivals when arrival tick is reached.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XReinforcementGroupSystem))]
    public partial struct Space4XReinforcementArrivalSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReinforcementArrival>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (arrival, transform, entity) in
                SystemAPI.Query<RefRW<ReinforcementArrival>, RefRW<LocalTransform>>()
                    .WithEntityAccess())
            {
                // Check if it's time to arrive and hasn't arrived yet
                if (arrival.ValueRO.HasArrived == 0 && currentTick >= arrival.ValueRO.ArrivalTick)
                {
                    // Teleport to arrival position
                    transform.ValueRW.Position = arrival.ValueRO.ArrivalPosition;
                    transform.ValueRW.Rotation = arrival.ValueRO.ArrivalRotation;

                    arrival.ValueRW.HasArrived = 1;
                }
            }
        }
    }

    /// <summary>
    /// Updates reinforcement tactics based on alignment.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XReinforcementSystem))]
    public partial struct Space4XReinforcementTacticsUpdateSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReinforcementTactics>();
            state.RequireForUpdate<AlignmentTriplet>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Only update occasionally
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;
            if (currentTick % 120 != 0)
            {
                return;
            }

            foreach (var (tactics, precision, alignment, entity) in
                SystemAPI.Query<RefRW<ReinforcementTactics>, RefRO<WarpPrecision>, RefRO<AlignmentTriplet>>()
                    .WithEntityAccess())
            {
                // Check autonomy if captain
                if (SystemAPI.HasComponent<CaptainState>(entity))
                {
                    var captain = SystemAPI.GetComponent<CaptainState>(entity);
                    if (captain.Autonomy < CaptainAutonomy.Operational)
                    {
                        continue; // Can't change tactics
                    }
                }

                // Update tactics based on alignment
                var newTactics = ReinforcementUtility.TacticsFromAlignment(alignment.ValueRO, precision.ValueRO.TechTier);

                // Only update preferred tactic, preserve other settings
                tactics.ValueRW.PreferredTactic = newTactics.PreferredTactic;
                tactics.ValueRW.Aggression = newTactics.Aggression;
            }
        }
    }

    /// <summary>
    /// Handles reinforcement requests and acknowledgments.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XReinforcementSystem))]
    public partial struct Space4XReinforcementRequestSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReinforcementRequest>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var currentTick = (uint)SystemAPI.Time.ElapsedTime;

            foreach (var (request, tactics, precision, entity) in
                SystemAPI.Query<RefRW<ReinforcementRequest>, RefRO<ReinforcementTactics>, RefRO<WarpPrecision>>()
                    .WithEntityAccess())
            {
                // Skip already acknowledged
                if (request.ValueRO.Acknowledged == 1)
                {
                    continue;
                }

                // Auto-acknowledge if we have valid target
                if (request.ValueRO.RequestingEntity != Entity.Null ||
                    math.lengthsq(request.ValueRO.TargetPosition) > 0)
                {
                    request.ValueRW.Acknowledged = 1;

                    // Calculate expected arrival time
                    if (SystemAPI.HasComponent<LocalTransform>(entity))
                    {
                        var transform = SystemAPI.GetComponent<LocalTransform>(entity);
                        float distance = math.distance(transform.Position, request.ValueRO.TargetPosition);
                        uint warpTime = (uint)(10 + distance / 100f);
                        request.ValueRW.ExpectedArrivalTick = currentTick + warpTime;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Telemetry for reinforcement system.
    /// </summary>
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct Space4XReinforcementTelemetrySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ReinforcementTactics>();
        }

        public void OnUpdate(ref SystemState state)
        {
            int totalEntities = 0;
            int pendingArrivals = 0;
            int completedArrivals = 0;
            int flankingTactic = 0;
            int defensiveTactic = 0;
            int aggressiveTactic = 0;

            foreach (var (tactics, arrival) in
                SystemAPI.Query<RefRO<ReinforcementTactics>, RefRO<ReinforcementArrival>>())
            {
                totalEntities++;

                if (arrival.ValueRO.HasArrived == 1)
                {
                    completedArrivals++;
                }
                else if (arrival.ValueRO.ArrivalTick > 0)
                {
                    pendingArrivals++;
                }

                switch (tactics.ValueRO.PreferredTactic)
                {
                    case ReinforcementTactic.Flanking:
                    case ReinforcementTactic.RearAssault:
                        flankingTactic++;
                        break;
                    case ReinforcementTactic.DefensiveScreen:
                    case ReinforcementTactic.CautiousApproach:
                        defensiveTactic++;
                        break;
                    case ReinforcementTactic.AggressiveDrop:
                        aggressiveTactic++;
                        break;
                }
            }

            // Would emit to telemetry stream
            // UnityEngine.Debug.Log($"[Reinforcement] Total: {totalEntities}, Pending: {pendingArrivals}, Completed: {completedArrivals}, Flanking: {flankingTactic}, Defensive: {defensiveTactic}, Aggressive: {aggressiveTactic}");
        }
    }
}

