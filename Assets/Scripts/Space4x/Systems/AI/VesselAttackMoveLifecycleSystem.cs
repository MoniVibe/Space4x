using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Intent;
using PureDOTS.Runtime.Interrupts;
using PureDOTS.Systems;
using Space4X.Runtime;
using Space4X.Registry;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Space4X.Systems.AI
{
    [BurstCompile]
    [UpdateInGroup(typeof(ResourceSystemGroup))]
    [UpdateAfter(typeof(VesselMovementSystem))]
    public partial struct VesselAttackMoveLifecycleSystem : ISystem
    {
        private ComponentLookup<VesselAimDirective> _aimLookup;
        private ComponentLookup<Space4XEngagement> _engagementLookup;
        private ComponentLookup<EntityIntent> _intentLookup;
        private ComponentLookup<AttackMoveOrigin> _originLookup;
        private ComponentLookup<VesselStanceComponent> _stanceLookup;
        private EntityStorageInfoLookup _entityLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<AttackMoveIntent>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
            _aimLookup = state.GetComponentLookup<VesselAimDirective>(false);
            _engagementLookup = state.GetComponentLookup<Space4XEngagement>(true);
            _intentLookup = state.GetComponentLookup<EntityIntent>(false);
            _originLookup = state.GetComponentLookup<AttackMoveOrigin>(true);
            _stanceLookup = state.GetComponentLookup<VesselStanceComponent>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
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

            _aimLookup.Update(ref state);
            _engagementLookup.Update(ref state);
            _intentLookup.Update(ref state);
            _originLookup.Update(ref state);
            _stanceLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var stanceConfig = Space4XStanceTuningConfig.Default;
            if (SystemAPI.TryGetSingleton<Space4XStanceTuningConfig>(out var stanceConfigSingleton))
            {
                stanceConfig = stanceConfigSingleton;
            }

            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var hasStructuralChanges = false;

            foreach (var (attackMove, movement, transform, entity) in SystemAPI
                         .Query<RefRO<AttackMoveIntent>, RefRO<VesselMovement>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (!ShouldCompleteAttackMove(attackMove.ValueRO, movement.ValueRO, transform.ValueRO.Position))
                {
                    continue;
                }

                if (HasValidEngagementTarget(entity) || HasValidAimTarget(entity))
                {
                    continue;
                }

                var wasPatrolling = _originLookup.HasComponent(entity) && _originLookup[entity].WasPatrolling != 0;
                if (wasPatrolling)
                {
                    var stance = ResolveStance(entity);
                    var tuning = stanceConfig.Resolve(stance);
                    if (tuning.ReturnToPatrolAfterCombat == 0)
                    {
                        wasPatrolling = false;
                    }
                }

                ClearAimDirective(entity);
                ClearAttackMove(ref ecb, entity, ref hasStructuralChanges);
                UpdateIntentAfterCompletion(entity, wasPatrolling);
            }

            if (hasStructuralChanges)
            {
                ecb.Playback(state.EntityManager);
            }
        }

        private static bool ShouldCompleteAttackMove(in AttackMoveIntent intent, in VesselMovement movement, float3 position)
        {
            var arrivalDistance = movement.ArrivalDistance > 0f ? movement.ArrivalDistance : 2f;
            if (intent.DestinationRadius > 0f)
            {
                arrivalDistance = math.max(arrivalDistance, intent.DestinationRadius);
            }

            var distance = math.distance(position, intent.Destination);
            var speed = math.length(movement.Velocity);
            var stopSpeed = math.max(0.05f, movement.BaseSpeed * 0.1f);
            return distance <= arrivalDistance && speed <= stopSpeed;
        }

        private bool HasValidAimTarget(Entity entity)
        {
            if (!_aimLookup.HasComponent(entity))
            {
                return false;
            }

            var aim = _aimLookup[entity];
            return IsValidTarget(aim.AimTarget);
        }

        private bool HasValidEngagementTarget(Entity entity)
        {
            if (!_engagementLookup.HasComponent(entity))
            {
                return false;
            }

            var engagement = _engagementLookup[entity];
            return IsValidTarget(engagement.PrimaryTarget);
        }

        private bool IsValidTarget(Entity target)
        {
            return target != Entity.Null && _entityLookup.Exists(target);
        }

        private void ClearAimDirective(Entity entity)
        {
            if (!_aimLookup.HasComponent(entity))
            {
                return;
            }

            var aim = _aimLookup[entity];
            aim.AimDirection = float3.zero;
            aim.AimWeight = 0f;
            aim.AimTarget = Entity.Null;
            aim.SmoothedDirection = float3.zero;
            aim.SmoothedWeight = 0f;
            aim.LastUpdateTick = 0;
            _aimLookup[entity] = aim;
        }

        private void ClearAttackMove(ref EntityCommandBuffer ecb, Entity entity, ref bool hasStructuralChanges)
        {
            ecb.RemoveComponent<AttackMoveIntent>(entity);
            hasStructuralChanges = true;

            if (_originLookup.HasComponent(entity))
            {
                ecb.RemoveComponent<AttackMoveOrigin>(entity);
                hasStructuralChanges = true;
            }
        }

        private void UpdateIntentAfterCompletion(Entity entity, bool wasPatrolling)
        {
            if (!_intentLookup.HasComponent(entity))
            {
                return;
            }

            var intent = _intentLookup[entity];

            if (wasPatrolling)
            {
                IntentService.ClearIntent(ref intent);
            }
            else
            {
                intent.Mode = IntentMode.Idle;
                intent.TargetEntity = Entity.Null;
                intent.TargetPosition = float3.zero;
                intent.TriggeringInterrupt = InterruptType.None;
                intent.Priority = InterruptPriority.Low;
                intent.IntentSetTick = 0;
                intent.IsValid = 1;
            }

            _intentLookup[entity] = intent;
        }

        private VesselStanceMode ResolveStance(Entity entity)
        {
            if (_stanceLookup.HasComponent(entity))
            {
                return _stanceLookup[entity].CurrentStance;
            }

            return VesselStanceMode.Balanced;
        }
    }
}
