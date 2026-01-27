using PureDOTS.Runtime.AI.Actions;
using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Interrupts;
using Unity.Burst;
using Unity.Entities;

namespace PureDOTS.Systems.AI
{
    /// <summary>
    /// System that executes action primitives.
    /// Delegates to capability systems (movement, interaction, etc.).
    /// Phase 3 will route primitives like TraverseEdge and Interact into the navigation/affordance stack.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup))]
    [UpdateAfter(typeof(InterruptSystemGroup))]
    public partial struct ActionExecutionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TimeState>(out var timeState))
            {
                return;
            }

            // Execute action intents
            foreach (var (actionIntent, entityIntent, entity) in SystemAPI.Query<
                RefRO<ActionIntent>,
                RefRW<EntityIntent>>()
                .WithEntityAccess())
            {
                if (actionIntent.ValueRO.IsValid == 0)
                {
                    continue;
                }

                var primitive = actionIntent.ValueRO.Primitive;

                // Map action primitives to EntityIntent modes
                // TODO(Phase3): Validate capabilities (climb/swim/fly) before attempting motion primitives.
                // TODO(Phase3): Interact/TraverseEdge/Craft primitives should delegate to affordance + capability systems.
                switch (primitive)
                {
                    case ActionPrimitive.MoveTo:
                        entityIntent.ValueRW.Mode = IntentMode.MoveTo;
                        entityIntent.ValueRW.TargetPosition = actionIntent.ValueRO.TargetPosition;
                        entityIntent.ValueRW.IsValid = 1;
                        break;

                    case ActionPrimitive.Gather:
                        entityIntent.ValueRW.Mode = IntentMode.Gather;
                        entityIntent.ValueRW.TargetEntity = actionIntent.ValueRO.TargetEntity;
                        entityIntent.ValueRW.IsValid = 1;
                        break;

                    case ActionPrimitive.Deliver:
                        entityIntent.ValueRW.Mode = IntentMode.Deliver;
                        entityIntent.ValueRW.TargetEntity = actionIntent.ValueRO.TargetEntity;
                        entityIntent.ValueRW.IsValid = 1;
                        break;

                    case ActionPrimitive.Attack:
                        entityIntent.ValueRW.Mode = IntentMode.Attack;
                        entityIntent.ValueRW.TargetEntity = actionIntent.ValueRO.TargetEntity;
                        entityIntent.ValueRW.IsValid = 1;
                        break;

                    case ActionPrimitive.Flee:
                        entityIntent.ValueRW.Mode = IntentMode.Flee;
                        entityIntent.ValueRW.TargetPosition = actionIntent.ValueRO.TargetPosition;
                        entityIntent.ValueRW.IsValid = 1;
                        break;

                    case ActionPrimitive.Rest:
                        entityIntent.ValueRW.Mode = IntentMode.Idle;
                        entityIntent.ValueRW.IsValid = 1;
                        break;

                    // Phase 3: Will add TraverseEdge, Interact, etc.
                }
            }
        }
    }
}

