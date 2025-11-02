using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Bootstrap to ensure the Hand singleton and its command buffer exist.
    /// </summary>
    [UpdateInGroup(typeof(HandSystemGroup), OrderFirst = true)]
    public partial struct HandBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;
            if (!SystemAPI.HasSingleton<HandSingletonTag>())
            {
                var entity = em.CreateEntity();
                em.AddComponent<HandSingletonTag>(entity);
                em.AddComponent<HandState>(entity);
                var buffer = em.AddBuffer<HandCommand>(entity);
                buffer.EnsureCapacity(8);
            }

            state.RequireForUpdate<HandSingletonTag>();
        }

        public void OnUpdate(ref SystemState state) { }
    }

    /// <summary>
    /// Consumes HandCommand buffer and updates HandState.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HandSystemGroup))]
    [UpdateAfter(typeof(HandBootstrapSystem))]
    public partial struct HandCommandProcessingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandSingletonTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (stateRW, commands) in SystemAPI.Query<RefRW<HandState>, DynamicBuffer<HandCommand>>().WithAll<HandSingletonTag>())
            {
                var s = stateRW.ValueRO;
                s.PrimaryJustPressed = 0;
                s.PrimaryJustReleased = 0;
                s.SecondaryJustPressed = 0;
                s.SecondaryJustReleased = 0;

                var currentWorld = s.WorldPosition;

                for (int i = 0; i < commands.Length; i++)
                {
                    var cmd = commands[i];
                    switch (cmd.Type)
                    {
                        case HandCommand.CommandType.SetScreenPosition:
                            s.ScreenPosition = cmd.Float2Param;
                            break;
                        case HandCommand.CommandType.SetWorldPosition:
                            s.AimDirection = math.normalizesafe(cmd.Float3Param - currentWorld, s.AimDirection);
                            s.WorldPosition = cmd.Float3Param;
                            currentWorld = cmd.Float3Param;
                            break;
                        case HandCommand.CommandType.PrimaryDown:
                            s.PrimaryPressed = 1;
                            s.PrimaryJustPressed = 1;
                            s.IsDragging = 1;
                            break;
                        case HandCommand.CommandType.PrimaryUp:
                            s.PrimaryPressed = 0;
                            s.PrimaryJustReleased = 1;
                            s.IsDragging = 0;
                            break;
                        case HandCommand.CommandType.SecondaryDown:
                            s.SecondaryPressed = 1;
                            s.SecondaryJustPressed = 1;
                            break;
                        case HandCommand.CommandType.SecondaryUp:
                            s.SecondaryPressed = 0;
                            s.SecondaryJustReleased = 1;
                            break;
                    }
                }

                commands.Clear();
                stateRW.ValueRW = s;
            }
        }
    }

    /// <summary>
    /// Determines which entity (if any) the hand is hovering over.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HandSystemGroup))]
    [UpdateAfter(typeof(HandCommandProcessingSystem))]
    public partial struct HandHoverSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandSingletonTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var handEntity = SystemAPI.GetSingletonEntity<HandSingletonTag>();
            var hand = SystemAPI.GetComponent<HandState>(handEntity);
            var cursor = hand.WorldPosition;

            Entity hovered = Entity.Null;
            var hoveredType = HandInteractableType.None;
            float bestDistSq = float.MaxValue;

            foreach (var (interactable, transform, entity) in SystemAPI.Query<RefRO<HandInteractable>, RefRO<LocalTransform>>().WithEntityAccess())
            {
                var radius = math.max(0.01f, interactable.ValueRO.Radius);
                var distSq = math.lengthsq(transform.ValueRO.Position - cursor);
                if (distSq > radius * radius || distSq >= bestDistSq)
                {
                    continue;
                }

                hovered = entity;
                hoveredType = interactable.ValueRO.Type;
                bestDistSq = distSq;
            }

            var handRW = SystemAPI.GetComponentRW<HandState>(handEntity);
            var newState = handRW.ValueRO;
            newState.HoveredEntity = hovered;
            newState.HoveredType = (byte)hoveredType;
            handRW.ValueRW = newState;
        }
    }

    /// <summary>
    /// Handles grabbing and releasing interactable entities.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HandSystemGroup))]
    [UpdateAfter(typeof(HandHoverSystem))]
    public partial struct HandInteractionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandSingletonTag>();
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            var rewindState = SystemAPI.GetSingleton<RewindState>();
            if (rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var handEntity = SystemAPI.GetSingletonEntity<HandSingletonTag>();
            var hand = SystemAPI.GetComponent<HandState>(handEntity);
            var em = state.EntityManager;

            if (hand.PrimaryJustPressed == 1 && hand.HeldEntity == Entity.Null && hand.HoveredEntity != Entity.Null && em.Exists(hand.HoveredEntity))
            {
                if (em.HasComponent<LocalTransform>(hand.HoveredEntity))
                {
                    var transform = em.GetComponentData<LocalTransform>(hand.HoveredEntity);
                    var offset = transform.Position - hand.WorldPosition;

                    if (!em.HasComponent<HandHeldTag>(hand.HoveredEntity))
                    {
                        em.AddComponent<HandHeldTag>(hand.HoveredEntity);
                    }

                    var held = new HandHeld
                    {
                        Type = (HandHeldType)hand.HoveredType,
                        Offset = offset
                    };

                    if (em.HasComponent<HandHeld>(hand.HoveredEntity))
                    {
                        em.SetComponentData(hand.HoveredEntity, held);
                    }
                    else
                    {
                        em.AddComponentData(hand.HoveredEntity, held);
                    }

                    hand.HeldEntity = hand.HoveredEntity;
                    hand.HeldType = (byte)hand.HoveredType;
                    hand.GrabStartTick = timeState.Tick;
                }
            }

            if (hand.HeldEntity != Entity.Null && em.Exists(hand.HeldEntity))
            {
                var transform = em.GetComponentData<LocalTransform>(hand.HeldEntity);
                var held = em.HasComponent<HandHeld>(hand.HeldEntity)
                    ? em.GetComponentData<HandHeld>(hand.HeldEntity)
                    : new HandHeld { Offset = float3.zero, Type = (HandHeldType)hand.HeldType };

                if (hand.PrimaryPressed == 1)
                {
                    transform.Position = hand.WorldPosition + held.Offset;
                    em.SetComponentData(hand.HeldEntity, transform);
                }

                if (hand.PrimaryJustReleased == 1)
                {
                    transform.Position = hand.WorldPosition;
                    em.SetComponentData(hand.HeldEntity, transform);

                    if (em.HasComponent<HandHeldTag>(hand.HeldEntity))
                    {
                        em.RemoveComponent<HandHeldTag>(hand.HeldEntity);
                    }

                    if (em.HasComponent<HandHeld>(hand.HeldEntity))
                    {
                        em.RemoveComponent<HandHeld>(hand.HeldEntity);
                    }

                    hand.HeldEntity = Entity.Null;
                    hand.HeldType = (byte)HandHeldType.None;
                    hand.GrabStartTick = 0;
                }
            }
            else if (hand.HeldEntity != Entity.Null && !em.Exists(hand.HeldEntity))
            {
                hand.HeldEntity = Entity.Null;
                hand.HeldType = (byte)HandHeldType.None;
                hand.GrabStartTick = 0;
            }

            var handRW = SystemAPI.GetComponentRW<HandState>(handEntity);
            handRW.ValueRW = hand;
        }
    }

    /// <summary>
    /// Applies miracle effects and villager interactions triggered by the hand.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HandSystemGroup))]
    [UpdateAfter(typeof(HandInteractionSystem))]
    public partial struct HandMiracleSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<HandSingletonTag>();
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<RewindState>(out var rewindState) || rewindState.Mode != RewindMode.Record)
            {
                return;
            }

            var handEntity = SystemAPI.GetSingletonEntity<HandSingletonTag>();
            var hand = SystemAPI.GetComponent<HandState>(handEntity);
            if (hand.SecondaryJustPressed == 0)
            {
                return;
            }

            var em = state.EntityManager;
            var timeState = SystemAPI.GetSingleton<TimeState>();

            if (hand.HoveredEntity != Entity.Null && em.Exists(hand.HoveredEntity))
            {
                if ((HandInteractableType)hand.HoveredType == HandInteractableType.Villager &&
                    em.HasComponent<VillagerMood>(hand.HoveredEntity))
                {
                    var mood = em.GetComponentData<VillagerMood>(hand.HoveredEntity);
                    mood.Mood = math.min(100f, mood.Mood + 10f);
                    mood.TargetMood = math.min(100f, mood.TargetMood + 10f);
                    em.SetComponentData(hand.HoveredEntity, mood);
                }
            }

            var effect = em.CreateEntity();
            em.AddComponentData(effect, LocalTransform.FromPositionRotationScale(hand.WorldPosition, quaternion.identity, 1f));
            em.AddComponentData(effect, new MiracleEffect
            {
                Lifetime = 1.5f,
                Elapsed = 0f,
                Radius = 3f
            });
            em.AddComponentData(effect, new LastRecordedTick { Tick = timeState.Tick });

            var handRW = SystemAPI.GetComponentRW<HandState>(handEntity);
            hand.SlingshotCharge = 0f;
            handRW.ValueRW = hand;
        }
    }

    /// <summary>
    /// Updates miracle VFX state and cleans up expired effects.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(HandSystemGroup))]
    [UpdateAfter(typeof(HandMiracleSystem))]
    public partial struct HandMiracleDecaySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.GetSingleton<TimeState>().FixedDeltaTime;

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (effect, entity) in SystemAPI.Query<RefRW<MiracleEffect>>().WithEntityAccess())
            {
                var data = effect.ValueRO;
                data.Elapsed += deltaTime;
                if (data.Elapsed >= data.Lifetime)
                {
                    ecb.DestroyEntity(entity);
                }
                else
                {
                    effect.ValueRW = data;
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
