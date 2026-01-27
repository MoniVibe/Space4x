using PureDOTS.Runtime.Comms;
using PureDOTS.Runtime.Groups;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems.Groups
{
    /// <summary>
    /// Ensures squads have cohesion profiles & state buffers to participate in flank/tighten logic.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SquadCohesionEnsureSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GroupFormation>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GroupFormation>>()
                         .WithNone<SquadCohesionProfile>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, SquadCohesionProfile.Default);
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GroupFormation>>()
                         .WithNone<SquadCohesionState>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new SquadCohesionState
                {
                    NormalizedCohesion = 0f,
                    Flags = 0,
                    LastUpdateTick = 0,
                    LastBroadcastTick = 0,
                    LastTelemetryTick = 0
                });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GroupFormation>>()
                         .WithNone<SquadTacticOrder>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new SquadTacticOrder
                {
                    Kind = SquadTacticKind.None,
                    Issuer = Entity.Null,
                    Target = Entity.Null,
                    FocusBudgetCost = 0f,
                    DisciplineRequired = 0.5f,
                    AckMode = 0,
                    IssueTick = 0
                });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GroupFormation>>()
                         .WithNone<GroupFormationSpread>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new GroupFormationSpread { CohesionNormalized = 0f });
            }

            foreach (var (_, entity) in SystemAPI.Query<RefRO<GroupFormation>>()
                         .WithNone<CommsOutboxEntry>()
                         .WithEntityAccess())
            {
                ecb.AddBuffer<CommsOutboxEntry>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}


