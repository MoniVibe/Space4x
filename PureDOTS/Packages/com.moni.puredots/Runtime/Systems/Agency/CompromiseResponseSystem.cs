using PureDOTS.Runtime.Agency;
using PureDOTS.Runtime.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Systems.Agency
{
    [BurstCompile]
    [UpdateInGroup(typeof(GameplaySystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(ControlLinkHealthSystem))]
    public partial struct CompromiseResponseSystem : ISystem
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
            var rewind = SystemAPI.GetSingleton<RewindState>();
            if (timeState.IsPaused || rewind.Mode != RewindMode.Record)
            {
                return;
            }

            var tick = timeState.Tick;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (compromise, doctrine, link, entity) in SystemAPI.Query<RefRO<CompromiseState>, RefRO<CompromiseDoctrine>, RefRW<ControlLinkState>>()
                         .WithEntityAccess())
            {
                bool confirmed = compromise.ValueRO.IsCompromised != 0;
                bool quarantine = compromise.ValueRO.Suspicion >= doctrine.ValueRO.QuarantineThreshold;
                bool purge = compromise.ValueRO.Severity >= doctrine.ValueRO.PurgeThreshold;

                if (confirmed || quarantine)
                {
                    var updatedLink = link.ValueRO;
                    updatedLink.IsLost = 1;
                    link.ValueRW = updatedLink;
                }

                if (confirmed && (doctrine.ValueRO.PreferredResponse == CompromiseResponseMode.ImmediatePurge || purge))
                {
                    if (!state.EntityManager.HasComponent<RogueToolState>(entity))
                    {
                        ecb.AddComponent(entity, new RogueToolState
                        {
                            Reason = RogueToolReason.HostileOverride,
                            SinceTick = tick,
                            AllowFriendlyDestructionNoPenalty = 1,
                            Hackable = 0,
                            Reserved0 = 0
                        });
                    }
                }
            }

            ecb.Playback(state.EntityManager);
        }
    }
}
