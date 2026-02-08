using IdentityCultureId = PureDOTS.Runtime.Identity.CultureId;
using IdentityRaceId = PureDOTS.Runtime.Identity.RaceId;
using SpaceCultureId = Space4X.Registry.CultureId;
using SpaceRaceId = Space4X.Registry.RaceId;
using PureDOTS.Runtime.Identity;
using Unity.Collections;
using Unity.Entities;

namespace Space4x.Identity
{
    /// <summary>
    /// Bridges Space4X race/culture ids into identity race/culture keys.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4xIdentityRaceCultureBridgeSystem : ISystem
    {
        private static readonly FixedString64Bytes RacePrefix = new FixedString64Bytes("space4x.race.");
        private static readonly FixedString64Bytes CulturePrefix = new FixedString64Bytes("space4x.culture.");
        private EntityQuery _query;

        public void OnCreate(ref SystemState state)
        {
            _query = SystemAPI.QueryBuilder()
                .WithAny<SpaceRaceId, SpaceCultureId>()
                .Build();
            state.RequireForUpdate(_query);
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (race, entity) in SystemAPI.Query<RefRO<SpaceRaceId>>()
                         .WithChangeFilter<SpaceRaceId>()
                         .WithEntityAccess())
            {
                var key = BuildKey(RacePrefix, race.ValueRO.Value);
                ApplyRaceId(ecb, em, entity, key);
            }

            foreach (var (culture, entity) in SystemAPI.Query<RefRO<SpaceCultureId>>()
                         .WithChangeFilter<SpaceCultureId>()
                         .WithEntityAccess())
            {
                var key = BuildKey(CulturePrefix, culture.ValueRO.Value);
                ApplyCultureId(ecb, em, entity, key);
            }

            ecb.Playback(em);
        }

        public void OnDestroy(ref SystemState state) { }

        private static FixedString64Bytes BuildKey(in FixedString64Bytes prefix, ushort id)
        {
            var value = prefix;
            value.Append(id);
            return value;
        }

        private static void ApplyRaceId(EntityCommandBuffer ecb, EntityManager em, Entity entity, FixedString64Bytes race)
        {
            var next = new IdentityRaceId { Value = race };
            if (em.HasComponent<IdentityRaceId>(entity))
            {
                var current = em.GetComponentData<IdentityRaceId>(entity);
                if (!current.Value.Equals(race))
                {
                    ecb.SetComponent(entity, next);
                }
            }
            else
            {
                ecb.AddComponent(entity, next);
            }
        }

        private static void ApplyCultureId(EntityCommandBuffer ecb, EntityManager em, Entity entity, FixedString64Bytes culture)
        {
            var next = new IdentityCultureId { Value = culture };
            if (em.HasComponent<IdentityCultureId>(entity))
            {
                var current = em.GetComponentData<IdentityCultureId>(entity);
                if (!current.Value.Equals(culture))
                {
                    ecb.SetComponent(entity, next);
                }
            }
            else
            {
                ecb.AddComponent(entity, next);
            }
        }
    }
}
