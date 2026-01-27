using PureDOTS.Runtime.Celestial;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Time;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems.Celestial
{
    /// <summary>
    /// Computes spatial orbit poses from celestial orbital state for system-map placement.
    /// Keeps this separate from day/night orbit parameters.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct CelestialOrbitPoseSystem : ISystem
    {
        private ComponentLookup<CelestialOrbitPose> _poseLookup;
        private ComponentLookup<LocalTransform> _transformLookup;
        private ComponentLookup<CelestialBody> _bodyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _poseLookup = state.GetComponentLookup<CelestialOrbitPose>(true);
            _transformLookup = state.GetComponentLookup<LocalTransform>(true);
            _bodyLookup = state.GetComponentLookup<CelestialBody>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            _poseLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _bodyLookup.Update(ref state);

            var currentTick = timeState.Tick;

            foreach (var (orbitState, pose, entity) in SystemAPI.Query<RefRO<OrbitalState>, RefRW<CelestialOrbitPose>>().WithEntityAccess())
            {
                var parent = orbitState.ValueRO.ParentBody;
                float3 parentPosition = float3.zero;

                if (parent != Entity.Null)
                {
                    if (_poseLookup.TryGetComponent(parent, out var parentPose))
                    {
                        parentPosition = parentPose.Position;
                    }
                    else if (_transformLookup.TryGetComponent(parent, out var parentTransform))
                    {
                        parentPosition = parentTransform.Position;
                    }
                    else if (_bodyLookup.TryGetComponent(parent, out var parentBody))
                    {
                        parentPosition = parentBody.Position;
                    }
                }

                var position = CelestialHelpers.CalculateOrbitalPosition(orbitState.ValueRO, parentPosition, currentTick);
                var toParent = math.normalizesafe(parentPosition - position, new float3(0f, 0f, 1f));

                pose.ValueRW.Position = position;
                pose.ValueRW.Forward = math.normalizesafe(new float3(-toParent.z, 0f, toParent.x), new float3(0f, 0f, 1f));
                pose.ValueRW.Up = new float3(0f, 1f, 0f);
                pose.ValueRW.LastUpdateTick = currentTick;

                if (SystemAPI.HasComponent<LocalTransform>(entity) &&
                    SystemAPI.HasComponent<ApplyCelestialPoseToLocalTransform>(entity))
                {
                    var transform = SystemAPI.GetComponentRW<LocalTransform>(entity);
                    var current = transform.ValueRO;
                    current.Position = position;
                    transform.ValueRW = current;
                }

                if (SystemAPI.HasComponent<CelestialBody>(entity))
                {
                    var body = SystemAPI.GetComponentRW<CelestialBody>(entity);
                    var current = body.ValueRO;
                    current.Position = position;
                    body.ValueRW = current;
                }
            }
        }
    }
}
