using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Components;
using PureDOTS.Systems;
using Space4X.Runtime.Breakables;
using Space4X.Runtime.Physics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.Breakables
{
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateBefore(typeof(PureDOTS.Systems.Combat.DamageResolutionSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Combat.DamageApplicationSystem))]
    public partial struct Space4XBreakableDamagePulseSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
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

            var tick = timeState.Tick;
            var fixedDt = math.max(1e-6f, timeState.FixedDeltaTime);

            foreach (var (root, pulse, entity) in SystemAPI.Query<RefRO<Space4XBreakableRoot>, RefRW<Space4XBreakableDamagePulse>>()
                         .WithEntityAccess())
            {
                if (!root.ValueRO.Profile.IsCreated)
                {
                    continue;
                }

                if (pulse.ValueRO.Fired != 0)
                {
                    continue;
                }

                if (pulse.ValueRO.TriggerTick == 0u)
                {
                    var delay = math.max(0f, pulse.ValueRO.DelaySeconds);
                    pulse.ValueRW.TriggerTick = tick + SecondsToTicks(delay, fixedDt);
                }

                if (tick < pulse.ValueRO.TriggerTick)
                {
                    continue;
                }

                if (!SystemAPI.HasBuffer<DamageEvent>(entity))
                {
                    state.EntityManager.AddBuffer<DamageEvent>(entity);
                }

                var damageBuffer = SystemAPI.GetBuffer<DamageEvent>(entity);
                damageBuffer.Add(new DamageEvent
                {
                    SourceEntity = entity,
                    TargetEntity = entity,
                    RawDamage = pulse.ValueRO.DamageAmount,
                    Type = pulse.ValueRO.DamageType,
                    Tick = tick,
                    Flags = pulse.ValueRO.DamageFlags
                });

                pulse.ValueRW.Fired = 1;
            }
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            return (uint)math.max(1, math.ceil(seconds / fixedDt));
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(Space4XBreakableDamagePulseSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Combat.DamageResolutionSystem))]
    [UpdateBefore(typeof(PureDOTS.Systems.Combat.DamageApplicationSystem))]
    public partial struct Space4XBreakableDamageRouterSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        private const float DamageToPieceScale = 0.02f;
        private const float InstabilityScale = 0.015f;
        private const float AoEInstabilityMultiplier = 1.5f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
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

            _transformLookup.Update(ref state);

            foreach (var (rootState, rootTransform, entity) in SystemAPI.Query<RefRW<Space4XBreakableRoot>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!rootState.ValueRO.Profile.IsCreated)
                {
                    continue;
                }

                var profile = rootState.ValueRO.Profile.Value;
                if (profile.Pieces.Length == 0)
                {
                    continue;
                }

                if (SystemAPI.HasBuffer<HitEvent>(entity))
                {
                    var hitEvents = SystemAPI.GetBuffer<HitEvent>(entity);
                    if (hitEvents.Length > 0)
                    {
                        RouteHitEvents(entity, rootTransform.ValueRO, profile, hitEvents, ref rootState, ref state);
                        continue;
                    }
                }

                if (!SystemAPI.HasBuffer<DamageEvent>(entity))
                {
                    continue;
                }

                var damageEvents = SystemAPI.GetBuffer<DamageEvent>(entity);
                if (damageEvents.Length == 0)
                {
                    continue;
                }

                RouteDamageEvents(entity, rootTransform.ValueRO, profile, damageEvents, ref rootState, ref state);
            }
        }

        private void RouteHitEvents(
            Entity rootEntity,
            in LocalTransform rootTransform,
            in ShipBreakProfileBlob profile,
            DynamicBuffer<HitEvent> hitEvents,
            ref RefRW<Space4XBreakableRoot> rootState,
            ref SystemState state)
        {
            var inverseRotation = math.inverse(rootTransform.Rotation);
            for (int i = 0; i < hitEvents.Length; i++)
            {
                var hit = hitEvents[i];
                if (hit.HitEntity != rootEntity)
                {
                    continue;
                }

                float3 localHit = math.rotate(inverseRotation, hit.HitPosition - rootTransform.Position);
                ushort pieceId = SelectPieceByLocalPoint(profile, localHit);
                ApplyDamageToPiece(rootEntity, pieceId, hit.DamageAmount, DamageFlags.None, ref rootState, ref state);
            }
        }

        private void RouteDamageEvents(
            Entity rootEntity,
            in LocalTransform rootTransform,
            in ShipBreakProfileBlob profile,
            DynamicBuffer<DamageEvent> damageEvents,
            ref RefRW<Space4XBreakableRoot> rootState,
            ref SystemState state)
        {
            for (int i = 0; i < damageEvents.Length; i++)
            {
                var damage = damageEvents[i];
                if (damage.TargetEntity != Entity.Null && damage.TargetEntity != rootEntity)
                {
                    continue;
                }

                float3 direction = ResolveDamageDirection(rootTransform, damage.SourceEntity, profile);
                ushort pieceId = SelectPieceByDirection(profile, direction);
                ApplyDamageToPiece(rootEntity, pieceId, damage.RawDamage, damage.Flags, ref rootState, ref state);
            }
        }

        private float3 ResolveDamageDirection(in LocalTransform rootTransform, Entity sourceEntity, in ShipBreakProfileBlob profile)
        {
            if (sourceEntity != Entity.Null && _transformLookup.HasComponent(sourceEntity))
            {
                var sourceTransform = _transformLookup[sourceEntity];
                return math.normalizesafe(rootTransform.Position - sourceTransform.Position, new float3(0f, 0f, 1f));
            }

            return math.normalizesafe(math.mul(rootTransform.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
        }

        private static ushort SelectPieceByLocalPoint(in ShipBreakProfileBlob profile, float3 localHit)
        {
            ushort bestPiece = profile.Pieces[0].PieceId;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < profile.Pieces.Length; i++)
            {
                var piece = profile.Pieces[i];
                float distance = math.lengthsq(localHit - piece.LocalOffset);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPiece = piece.PieceId;
                }
            }

            return bestPiece;
        }

        private static ushort SelectPieceByDirection(in ShipBreakProfileBlob profile, float3 direction)
        {
            float3 dir = math.normalizesafe(direction, new float3(0f, 0f, 1f));
            float bestScore = -float.MaxValue;
            ushort bestPiece = profile.Pieces[0].PieceId;
            ushort corePiece = bestPiece;

            for (int i = 0; i < profile.Pieces.Length; i++)
            {
                var piece = profile.Pieces[i];
                if (piece.IsCore != 0)
                {
                    corePiece = piece.PieceId;
                }

                float3 offset = piece.LocalOffset;
                float length = math.length(offset);
                if (length < 0.001f)
                {
                    continue;
                }

                float score = math.dot(offset / length, dir);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestPiece = piece.PieceId;
                }
            }

            return bestScore <= -0.5f ? corePiece : bestPiece;
        }

        private void ApplyDamageToPiece(
            Entity rootEntity,
            ushort pieceId,
            float rawDamage,
            DamageFlags damageFlags,
            ref RefRW<Space4XBreakableRoot> rootState,
            ref SystemState state)
        {
            float damageDelta = rawDamage * DamageToPieceScale;
            float instabilityDelta = rawDamage * InstabilityScale;
            if ((damageFlags & DamageFlags.AoE) != 0)
            {
                instabilityDelta *= AoEInstabilityMultiplier;
            }

            rootState.ValueRW.Damage = math.saturate(rootState.ValueRO.Damage + damageDelta);
            rootState.ValueRW.Instability = math.saturate(rootState.ValueRO.Instability + instabilityDelta);

            foreach (var (piece, pieceState, entity) in SystemAPI.Query<RefRO<Space4XBreakablePiece>, RefRW<Space4XBreakablePieceState>>()
                         .WithEntityAccess())
            {
                if (piece.ValueRO.Root != rootEntity || piece.ValueRO.PieceIndex != pieceId)
                {
                    continue;
                }

                var updated = pieceState.ValueRO;
                updated.Damage01 = math.saturate(updated.Damage01 + damageDelta);
                updated.Instability01 = math.saturate(updated.Instability01 + instabilityDelta);
                pieceState.ValueRW = updated;
                break;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(Space4XBreakableFragmentMotionSystem))]
    public partial struct Space4XBreakableSplitSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = time.Tick;
            var fixedDt = math.max(1e-6f, time.FixedDeltaTime);
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (rootState, rootTransform, rootEntity) in SystemAPI.Query<RefRW<Space4XBreakableRoot>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (rootState.ValueRO.IsBroken != 0)
                {
                    continue;
                }

                if (!rootState.ValueRO.Profile.IsCreated)
                {
                    continue;
                }

                var profile = rootState.ValueRO.Profile.Value;
                if (!state.EntityManager.HasBuffer<Space4XBreakableEdgeState>(rootEntity))
                {
                    continue;
                }

                if (rootState.ValueRO.BreakTick == 0u && profile.BreakDelaySeconds > 0f)
                {
                    rootState.ValueRW.BreakTick = tick + SecondsToTicks(profile.BreakDelaySeconds, fixedDt);
                }

                if (rootState.ValueRO.BreakTick == 0u || tick < rootState.ValueRO.BreakTick)
                {
                    continue;
                }

                var pieceMap = new NativeHashMap<ushort, Entity>(profile.Pieces.Length, Allocator.Temp);
                var pieceOffsetMap = new NativeHashMap<ushort, float3>(profile.Pieces.Length, Allocator.Temp);
                var pieceDamageMap = new NativeHashMap<ushort, float>(profile.Pieces.Length, Allocator.Temp);
                var pieceInstabilityMap = new NativeHashMap<ushort, float>(profile.Pieces.Length, Allocator.Temp);

                var pieceCount = 0;
                foreach (var (piece, pieceState, entity) in SystemAPI.Query<RefRO<Space4XBreakablePiece>, RefRO<Space4XBreakablePieceState>>()
                             .WithEntityAccess())
                {
                    if (piece.ValueRO.Root != rootEntity)
                    {
                        continue;
                    }

                    if (pieceMap.TryAdd(piece.ValueRO.PieceIndex, entity))
                    {
                        pieceCount++;
                    }

                    pieceOffsetMap.TryAdd(piece.ValueRO.PieceIndex, piece.ValueRO.LocalOffset);
                    pieceDamageMap.TryAdd(piece.ValueRO.PieceIndex, pieceState.ValueRO.Damage01);
                    pieceInstabilityMap.TryAdd(piece.ValueRO.PieceIndex, pieceState.ValueRO.Instability01);
                }

                if (pieceCount == 0)
                {
                    pieceMap.Dispose();
                    pieceOffsetMap.Dispose();
                    pieceDamageMap.Dispose();
                    pieceInstabilityMap.Dispose();
                    continue;
                }

                var edgeStates = SystemAPI.GetBuffer<Space4XBreakableEdgeState>(rootEntity);
                if (edgeStates.Length != profile.Edges.Length)
                {
                    pieceMap.Dispose();
                    pieceOffsetMap.Dispose();
                    pieceDamageMap.Dispose();
                    pieceInstabilityMap.Dispose();
                    continue;
                }

                for (int i = 0; i < profile.Edges.Length; i++)
                {
                    var edgeDef = profile.Edges[i];
                    var edgeState = edgeStates[i];
                    if (edgeState.IsBroken != 0)
                    {
                        continue;
                    }

                    float damageA = pieceDamageMap.TryGetValue(edgeDef.PieceA, out var damA) ? damA : 0f;
                    float damageB = pieceDamageMap.TryGetValue(edgeDef.PieceB, out var damB) ? damB : 0f;
                    float instA = pieceInstabilityMap.TryGetValue(edgeDef.PieceA, out var insA) ? insA : 0f;
                    float instB = pieceInstabilityMap.TryGetValue(edgeDef.PieceB, out var insB) ? insB : 0f;

                    float maxDamage = math.max(damageA, damageB);
                    float maxInstability = math.max(instA, instB);

                    if (maxDamage >= edgeDef.BreakDamageThreshold || maxInstability >= edgeDef.BreakInstabilityThreshold)
                    {
                        edgeState.IsBroken = 1;
                        edgeState.BrokenTick = tick;
                        edgeStates[i] = edgeState;
                    }
                }

                var pieceIdToIndex = new NativeHashMap<ushort, int>(profile.Pieces.Length, Allocator.Temp);
                var componentByIndex = new NativeArray<int>(profile.Pieces.Length, Allocator.Temp);
                for (int i = 0; i < componentByIndex.Length; i++)
                {
                    componentByIndex[i] = -1;
                    pieceIdToIndex.TryAdd(profile.Pieces[i].PieceId, i);
                }

                int componentCount = 0;
                var queue = new NativeList<ushort>(Allocator.Temp);
                for (int i = 0; i < profile.Pieces.Length; i++)
                {
                    if (componentByIndex[i] >= 0)
                    {
                        continue;
                    }

                    var startPieceId = profile.Pieces[i].PieceId;
                    componentByIndex[i] = componentCount;
                    queue.Clear();
                    queue.Add(startPieceId);

                    for (int qi = 0; qi < queue.Length; qi++)
                    {
                        var currentId = queue[qi];
                        for (int edgeIndex = 0; edgeIndex < profile.Edges.Length; edgeIndex++)
                        {
                            if (edgeStates[edgeIndex].IsBroken != 0)
                            {
                                continue;
                            }

                            var edge = profile.Edges[edgeIndex];
                            ushort neighbor = 0;
                            if (edge.PieceA == currentId)
                            {
                                neighbor = edge.PieceB;
                            }
                            else if (edge.PieceB == currentId)
                            {
                                neighbor = edge.PieceA;
                            }
                            else
                            {
                                continue;
                            }

                            if (!pieceIdToIndex.TryGetValue(neighbor, out var neighborIndex))
                            {
                                continue;
                            }

                            if (componentByIndex[neighborIndex] < 0)
                            {
                                componentByIndex[neighborIndex] = componentCount;
                                queue.Add(neighbor);
                            }
                        }
                    }

                    componentCount++;
                }

                var componentHasCore = new NativeArray<byte>(componentCount, Allocator.Temp);
                var componentOffsetSum = new NativeArray<float3>(componentCount, Allocator.Temp);
                var componentCountPieces = new NativeArray<int>(componentCount, Allocator.Temp);

                for (int i = 0; i < profile.Pieces.Length; i++)
                {
                    int comp = componentByIndex[i];
                    var pieceDef = profile.Pieces[i];
                    if (comp < 0)
                    {
                        continue;
                    }

                    componentHasCore[comp] = (byte)(componentHasCore[comp] | (pieceDef.IsCore != 0 ? (byte)1 : (byte)0));
                    float3 offset = pieceOffsetMap.TryGetValue(pieceDef.PieceId, out var localOffset) ? localOffset : pieceDef.LocalOffset;
                    componentOffsetSum[comp] += offset;
                    componentCountPieces[comp] += 1;
                }

                var componentRoots = new NativeHashMap<int, Entity>(componentCount, Allocator.Temp);
                for (int comp = 0; comp < componentCount; comp++)
                {
                    if (componentHasCore[comp] != 0)
                    {
                        continue;
                    }

                    float3 avgOffset = componentOffsetSum[comp] / math.max(1, componentCountPieces[comp]);
                    var fragmentRoot = ecb.CreateEntity();
                    ecb.AddComponent(fragmentRoot, new Space4XBreakableFragmentRoot
                    {
                        SourceRoot = rootEntity,
                        AttachmentGroup = comp
                    });

                    ecb.AddComponent(fragmentRoot, LocalTransform.FromPositionRotationScale(
                        rootTransform.ValueRO.Position + avgOffset,
                        quaternion.identity,
                        1f));

                    float3 driftDir = math.normalizesafe(avgOffset, new float3(1f, 0f, 0f));
                    float driftSpeed = 1.5f + comp * 0.35f;
                    ecb.AddComponent(fragmentRoot, new SpaceVelocity
                    {
                        Linear = driftDir * driftSpeed,
                        Angular = float3.zero
                    });

                    componentRoots.TryAdd(comp, fragmentRoot);
                }

                foreach (var (piece, entity) in SystemAPI.Query<RefRW<Space4XBreakablePiece>>().WithEntityAccess())
                {
                    if (piece.ValueRO.Root != rootEntity)
                    {
                        continue;
                    }

                    if (!pieceIdToIndex.TryGetValue(piece.ValueRO.PieceIndex, out var pieceIndex))
                    {
                        continue;
                    }

                    int comp = componentByIndex[pieceIndex];
                    if (comp < 0 || componentHasCore[comp] != 0)
                    {
                        continue;
                    }

                    if (!componentRoots.TryGetValue(comp, out var fragmentRoot))
                    {
                        continue;
                    }

                    float3 avgOffset = componentOffsetSum[comp] / math.max(1, componentCountPieces[comp]);
                    var updated = piece.ValueRO;
                    updated.Root = fragmentRoot;
                    updated.LocalOffset = updated.LocalOffset - avgOffset;
                    piece.ValueRW = updated;
                }

                rootState.ValueRW.IsBroken = 1;

                pieceMap.Dispose();
                pieceOffsetMap.Dispose();
                pieceDamageMap.Dispose();
                pieceInstabilityMap.Dispose();
                pieceIdToIndex.Dispose();
                componentByIndex.Dispose();
                queue.Dispose();
                componentHasCore.Dispose();
                componentOffsetSum.Dispose();
                componentCountPieces.Dispose();
                componentRoots.Dispose();
            }

            ecb.Playback(em);
        }

        private static uint SecondsToTicks(float seconds, float fixedDt)
        {
            return (uint)math.max(1, math.ceil(seconds / fixedDt));
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XBreakableSplitSystem))]
    public partial struct Space4XBreakableCapabilitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            foreach (var (rootState, entity) in SystemAPI.Query<RefRO<Space4XBreakableRoot>>()
                         .WithEntityAccess())
            {
                if (!rootState.ValueRO.Profile.IsCreated)
                {
                    continue;
                }

                var profile = rootState.ValueRO.Profile.Value;
                var provides = ShipCapabilityFlags.None;
                float maxThrust = 0f;
                float powerGen = 0f;
                float sensorMul = 1f;
                int weaponCount = 0;

                for (int i = 0; i < profile.Pieces.Length; i++)
                {
                    var pieceDef = profile.Pieces[i];
                    if (!HasPiece(entity, pieceDef.PieceId))
                    {
                        continue;
                    }

                    provides |= pieceDef.ProvidesFlags;
                    maxThrust += pieceDef.ThrustContribution;
                    powerGen += pieceDef.PowerGeneration;
                    sensorMul = math.max(sensorMul, pieceDef.SensorRangeMultiplier);
                    weaponCount += pieceDef.WeaponHardpointCount;
                }

                var capability = new Space4XShipCapabilityState
                {
                    ProvidesFlags = provides,
                    MaxThrust = maxThrust,
                    PowerGeneration = powerGen,
                    SensorRangeMultiplier = sensorMul,
                    WeaponHardpointCount = (byte)math.clamp(weaponCount, 0, 255),
                    IsAlive = (byte)((provides & profile.AliveRequired) == profile.AliveRequired ? 1 : 0),
                    IsMobile = (byte)((provides & profile.MobileRequiredAny) != 0 ? 1 : 0),
                    IsCombatCapable = (byte)((provides & profile.CombatRequiredAny) != 0 ? 1 : 0),
                    IsFtlCapable = (byte)((provides & profile.FtlRequiredAll) == profile.FtlRequiredAll ? 1 : 0)
                };

                if (state.EntityManager.HasComponent<Space4XShipCapabilityState>(entity))
                {
                    state.EntityManager.SetComponentData(entity, capability);
                }
                else
                {
                    state.EntityManager.AddComponentData(entity, capability);
                }
            }
        }

        private static bool HasPiece(Entity rootEntity, ushort pieceId)
        {
            foreach (var piece in SystemAPI.Query<RefRO<Space4XBreakablePiece>>())
            {
                if (piece.ValueRO.Root == rootEntity && piece.ValueRO.PieceIndex == pieceId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XBreakableSplitSystem))]
    [UpdateBefore(typeof(Space4XBreakablePieceFollowSystem))]
    public partial struct Space4XBreakableFragmentMotionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            float dt = math.max(1e-6f, time.FixedDeltaTime);
            foreach (var (velocity, transform) in SystemAPI.Query<RefRW<SpaceVelocity>, RefRW<LocalTransform>>()
                         .WithAll<Space4XBreakableFragmentRoot>())
            {
                var current = transform.ValueRO;
                current.Position += velocity.ValueRO.Linear * dt;
                transform.ValueRW = current;

                var vel = velocity.ValueRO;
                vel.Linear *= 0.99f;
                velocity.ValueRW = vel;
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XBreakableFragmentMotionSystem))]
    public partial struct Space4XBreakablePieceFollowSystem : ISystem
    {
        private ComponentLookup<LocalTransform> _transformLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.GetSingleton<TimeState>();
            if (time.IsPaused)
            {
                return;
            }

            _transformLookup.Update(ref state);

            foreach (var (piece, transform) in SystemAPI.Query<RefRO<Space4XBreakablePiece>, RefRW<LocalTransform>>())
            {
                if (piece.ValueRO.Root == Entity.Null || !_transformLookup.HasComponent(piece.ValueRO.Root))
                {
                    continue;
                }

                var rootTransform = _transformLookup[piece.ValueRO.Root];
                var current = transform.ValueRO;
                current.Position = rootTransform.Position + piece.ValueRO.LocalOffset;
                transform.ValueRW = current;
            }
        }
    }
}
