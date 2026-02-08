using PureDOTS.Runtime.Profile;
using PureDOTS.Runtime.Authority;
using Space4X.Registry;
using Unity.Entities;

namespace Space4X.SimServer
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct Space4XSimServerFactionProfileBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!Space4XSimServerSettings.IsEnabled())
            {
                state.Enabled = false;
                return;
            }

            state.RequireForUpdate<Space4XFaction>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

            foreach (var (faction, entity) in SystemAPI.Query<RefRO<Space4XFaction>>().WithEntityAccess())
            {
                var leader = ResolveLeader(em, entity);
                if (leader == Entity.Null || em.HasComponent<BehaviorDisposition>(leader))
                {
                    continue;
                }

                var disposition = Space4XSimServerProfileUtility.BuildLeaderDisposition(
                    (float)faction.ValueRO.MilitaryFocus,
                    (float)faction.ValueRO.TradeFocus,
                    (float)faction.ValueRO.ResearchFocus,
                    (float)faction.ValueRO.ExpansionDrive,
                    0.5f,
                    (float)faction.ValueRO.Aggression,
                    (float)faction.ValueRO.RiskTolerance,
                    0.5f);

                ecb.AddComponent(leader, disposition);
            }

            ecb.Playback(em);
        }

        private static Entity ResolveLeader(EntityManager entityManager, Entity factionEntity)
        {
            if (!entityManager.HasComponent<AuthorityBody>(factionEntity))
            {
                return Entity.Null;
            }

            var body = entityManager.GetComponentData<AuthorityBody>(factionEntity);
            if (body.ExecutiveSeat == Entity.Null || !entityManager.HasComponent<AuthoritySeatOccupant>(body.ExecutiveSeat))
            {
                return Entity.Null;
            }

            var occupant = entityManager.GetComponentData<AuthoritySeatOccupant>(body.ExecutiveSeat).OccupantEntity;
            return entityManager.Exists(occupant) ? occupant : Entity.Null;
        }
    }
}
