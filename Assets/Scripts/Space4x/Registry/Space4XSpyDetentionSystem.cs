using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Social;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

using SpatialSystemGroup = PureDOTS.Systems.SpatialSystemGroup;

namespace Space4X.Registry
{
    /// <summary>
    /// Converts high suspicion against spies into custody state.
    /// This is a minimal bridge; later logic should factor doctrine, authority seats, and interrogation policy.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SpatialSystemGroup))]
    [UpdateAfter(typeof(Space4XAffiliationComplianceSystem))]
    public partial struct Space4XSpyDetentionSystem : ISystem
    {
        private const float DetainThreshold = 0.85f;

        private ComponentLookup<CustodyState> _custodyLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<RewindState>();

            _custodyLookup = state.GetComponentLookup<CustodyState>(true);
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

            _custodyLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (suspicion, entity) in SystemAPI.Query<RefRO<SuspicionScore>>()
                         .WithAll<SpyRole>()
                         .WithEntityAccess())
            {
                var score = math.saturate((float)suspicion.ValueRO.Value);
                if (score < DetainThreshold)
                {
                    continue;
                }

                if (_custodyLookup.HasComponent(entity) || state.EntityManager.HasComponent<CustodyState>(entity))
                {
                    continue;
                }

                var captorScope = Entity.Null;
                if (SystemAPI.HasBuffer<AffiliationTag>(entity))
                {
                    var affiliations = SystemAPI.GetBuffer<AffiliationTag>(entity);
                    if (affiliations.Length > 0)
                    {
                        captorScope = affiliations[0].Target;
                    }
                }

                ecb.AddComponent(entity, new CustodyState
                {
                    Kind = CustodyKind.SpyDetention,
                    Status = CustodyStatus.Detained,
                    Flags = CustodyFlags.Interrogation,
                    CaptorScope = captorScope,
                    HoldingEntity = captorScope,
                    OriginalAffiliation = Entity.Null,
                    CapturedTick = timeState.Tick,
                    LastStatusTick = timeState.Tick,
                    IssuedByAuthority = new PureDOTS.Runtime.Authority.IssuedByAuthority
                    {
                        IssuingSeat = Entity.Null,
                        IssuingOccupant = Entity.Null,
                        ActingSeat = Entity.Null,
                        ActingOccupant = Entity.Null,
                        IssuedTick = timeState.Tick
                    }
                });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
