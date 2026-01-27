using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Combat;
using PureDOTS.Runtime.Formation;

namespace PureDOTS.Systems.Combat
{
    /// <summary>
    /// System that handles 3D formation positioning and vertical movement.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Formation3DSystem : ISystem
    {
        private EntityStorageInfoLookup _entityLookup;
        private ComponentLookup<Unity.Transforms.LocalTransform> _transformLookup;
        private ComponentLookup<FormationState> _formationLookup;
        private ComponentLookup<VerticalEngagementRange> _rangeLookup;
        private ComponentLookup<Advantage3D> _advantageLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _entityLookup = state.GetEntityStorageInfoLookup();
            _transformLookup = state.GetComponentLookup<Unity.Transforms.LocalTransform>(false);
            _formationLookup = state.GetComponentLookup<FormationState>(true);
            _rangeLookup = state.GetComponentLookup<VerticalEngagementRange>(true);
            _advantageLookup = state.GetComponentLookup<Advantage3D>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _entityLookup.Update(ref state);
            _transformLookup.Update(ref state);
            _formationLookup.Update(ref state);
            _rangeLookup.Update(ref state);
            _advantageLookup.Update(ref state);

            var timeState = SystemAPI.GetSingleton<TimeState>();
            float deltaTime = timeState.DeltaTime;

            // Update vertical movement for entities with VerticalMovementState
            foreach (var (verticalMovement, transformRef, entity) in SystemAPI.Query<
                RefRW<VerticalMovementState>,
                RefRW<Unity.Transforms.LocalTransform>>()
                .WithEntityAccess())
            {
                var movement = verticalMovement.ValueRO;
                var transform = transformRef.ValueRO;

                if (movement.Mode == VerticalMovementMode.None)
                {
                    continue;
                }

                // Update current altitude toward target
                float currentAltitude = transform.Position.y;
                float targetAltitude = movement.TargetAltitude;
                float verticalSpeed = movement.VerticalSpeed;

                // Calculate new altitude
                float altitudeDelta = targetAltitude - currentAltitude;
                float moveDistance = verticalSpeed * deltaTime;

                if (math.abs(altitudeDelta) > moveDistance)
                {
                    currentAltitude += math.sign(altitudeDelta) * moveDistance;
                }
                else
                {
                    currentAltitude = targetAltitude;
                    movement.Mode = VerticalMovementMode.None;
                }

                // Update transform position
                transform.Position = new float3(transform.Position.x, currentAltitude, transform.Position.z);
                transformRef.ValueRW = transform;

                // Update movement state
                movement.CurrentAltitude = currentAltitude;
                verticalMovement.ValueRW = movement;
            }

            // Update 3D advantage for entities in combat
            foreach (var (advantage, transformRef, entity) in SystemAPI.Query<
                RefRW<Advantage3D>,
                RefRO<Unity.Transforms.LocalTransform>>()
                .WithEntityAccess())
            {
                // Advantage is calculated on-demand by Formation3DService.Get3DAdvantageMultiplier
                // This system can update it periodically if needed
            }
        }
    }
}

