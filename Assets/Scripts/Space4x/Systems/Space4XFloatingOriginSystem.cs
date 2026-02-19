using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems
{
    /// <summary>
    /// Recenters the world when entities drift far from origin to keep RenderBounds stable.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PureDOTS.Systems.ResourceSystemGroup), OrderLast = true)]
    public partial struct Space4XFloatingOriginSystem : ISystem
    {
        private EntityQuery _carrierQuery;
        private EntityQuery _asteroidQuery;
        private EntityQuery _playerFlagshipQuery;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _carrierQuery = SystemAPI.QueryBuilder().WithAll<Carrier, LocalTransform>().Build();
            _asteroidQuery = SystemAPI.QueryBuilder().WithAll<Asteroid, LocalTransform>().Build();
            _playerFlagshipQuery = SystemAPI.QueryBuilder().WithAll<PlayerFlagshipTag>().Build();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<Space4XFloatingOriginConfig>(out var config) || config.Enabled == 0)
            {
                return;
            }

            // Avoid sudden world-shift snaps while the player flagship camera is actively coupled to a ship.
            if (!_playerFlagshipQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            var time = SystemAPI.GetSingleton<TimeState>();
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (time.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }
            var currentTick = time.Tick;
            var cooldown = math.max(1u, config.CooldownTicks);

            var stateEntity = EnsureStateEntity(ref state);
            var originState = SystemAPI.GetComponentRW<Space4XFloatingOriginState>(stateEntity);
            if (currentTick - originState.ValueRO.LastShiftTick < cooldown)
            {
                return;
            }

            if (!TryResolveFocus(ref state, out var focus))
            {
                return;
            }

            var threshold = math.max(10f, config.Threshold);
            if (math.lengthsq(focus) < threshold * threshold)
            {
                return;
            }

            var shift = -focus;
            ApplyShift(ref state, shift);
            originState.ValueRW.LastShiftTick = currentTick;
            originState.ValueRW.LastShift = shift;
        }

        private Entity EnsureStateEntity(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingletonEntity<Space4XFloatingOriginState>(out var entity))
            {
                entity = state.EntityManager.CreateEntity();
                state.EntityManager.AddComponent<Space4XFloatingOriginState>(entity);
                state.EntityManager.SetComponentData(entity, new Space4XFloatingOriginState());
            }

            return entity;
        }

        private bool TryResolveFocus(ref SystemState state, out float3 focus)
        {
            if (!_carrierQuery.IsEmptyIgnoreFilter)
            {
                focus = ResolveQueryAverage(ref state, _carrierQuery);
                return true;
            }

            if (!_asteroidQuery.IsEmptyIgnoreFilter)
            {
                focus = ResolveQueryAverage(ref state, _asteroidQuery);
                return true;
            }

            focus = float3.zero;
            return false;
        }

        private float3 ResolveQueryAverage(ref SystemState state, EntityQuery query)
        {
            var sum = float3.zero;
            var count = 0;
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                if (!state.EntityManager.HasComponent<LocalTransform>(entities[i]))
                {
                    continue;
                }

                sum += state.EntityManager.GetComponentData<LocalTransform>(entities[i]).Position;
                count++;
            }

            return count > 0 ? sum / count : float3.zero;
        }

        private void ApplyShift(ref SystemState state, float3 shift)
        {
            foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>())
            {
                var value = transform.ValueRO;
                value.Position += shift;
                transform.ValueRW = value;
            }

            foreach (var center in SystemAPI.Query<RefRW<Space4XAsteroidCenter>>())
            {
                center.ValueRW.Position += shift;
            }

            foreach (var anchorState in SystemAPI.Query<RefRW<Space4XOrbitAnchorState>>())
            {
                anchorState.ValueRW.LastPosition += shift;
            }
        }
    }
}
