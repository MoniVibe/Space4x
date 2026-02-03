using PureDOTS.Runtime;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Synchronizes empire/faction/guild/business memberships into buffers and affiliation tags.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XOrganizationRelationSystem : ISystem
    {
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private BufferLookup<EmpireFactionEntry> _empireMemberLookup;
        private BufferLookup<GuildMemberEntry> _guildMemberLookup;
        private BufferLookup<GuildMembershipEntry> _guildMembershipLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(false);
            _empireMemberLookup = state.GetBufferLookup<EmpireFactionEntry>(false);
            _guildMemberLookup = state.GetBufferLookup<GuildMemberEntry>(false);
            _guildMembershipLookup = state.GetBufferLookup<GuildMembershipEntry>(false);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            _affiliationLookup.Update(ref state);
            _empireMemberLookup.Update(ref state);
            _guildMemberLookup.Update(ref state);
            _guildMembershipLookup.Update(ref state);
            _factionLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (membership, factionEntity) in SystemAPI.Query<RefRO<EmpireMembership>>().WithEntityAccess())
            {
                if (membership.ValueRO.Empire == Entity.Null)
                {
                    continue;
                }

                EnsureAffiliation(factionEntity, AffiliationType.Empire, membership.ValueRO.Empire, membership.ValueRO.Loyalty, ref ecb);
                EnsureEmpireMember(membership.ValueRO.Empire, factionEntity, membership.ValueRO, ref ecb);
            }

            foreach (var (memberships, memberEntity) in SystemAPI.Query<DynamicBuffer<GuildMembershipEntry>>().WithEntityAccess())
            {
                for (int i = 0; i < memberships.Length; i++)
                {
                    var membership = memberships[i];
                    if (membership.Guild == Entity.Null)
                    {
                        continue;
                    }

                    EnsureAffiliation(memberEntity, AffiliationType.Guild, membership.Guild, membership.Loyalty, ref ecb);
                    var memberType = ResolveMemberType(memberEntity);
                    EnsureGuildMember(membership.Guild, memberEntity, memberType, membership, ref ecb);
                }
            }

            foreach (var (link, businessEntity) in SystemAPI.Query<RefRO<BusinessGuildLink>>().WithEntityAccess())
            {
                if (link.ValueRO.Guild == Entity.Null)
                {
                    continue;
                }

                var loyalty = link.ValueRO.RepresentationStrength;
                EnsureAffiliation(businessEntity, AffiliationType.Guild, link.ValueRO.Guild, loyalty, ref ecb);

                var membership = new GuildMembershipEntry
                {
                    Guild = link.ValueRO.Guild,
                    Loyalty = loyalty,
                    Status = 0,
                    JoinedTick = link.ValueRO.LinkedTick
                };

                var memberType = ResolveMemberType(businessEntity);
                EnsureGuildMember(link.ValueRO.Guild, businessEntity, memberType, membership, ref ecb);

                if (_guildMembershipLookup.HasBuffer(businessEntity))
                {
                    var buffer = _guildMembershipLookup[businessEntity];
                    buffer.Clear();
                    buffer.Add(membership);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void EnsureAffiliation(
            Entity entity,
            AffiliationType type,
            Entity target,
            half loyalty,
            ref EntityCommandBuffer ecb)
        {
            if (entity == Entity.Null || target == Entity.Null)
            {
                return;
            }

            if (_affiliationLookup.HasBuffer(entity))
            {
                var buffer = _affiliationLookup[entity];
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Type == type && buffer[i].Target == target)
                    {
                        var entry = buffer[i];
                        entry.Loyalty = loyalty;
                        buffer[i] = entry;
                        return;
                    }
                }

                buffer.Add(new AffiliationTag
                {
                    Type = type,
                    Target = target,
                    Loyalty = loyalty
                });
            }
            else
            {
                var buffer = ecb.AddBuffer<AffiliationTag>(entity);
                buffer.Add(new AffiliationTag
                {
                    Type = type,
                    Target = target,
                    Loyalty = loyalty
                });
            }
        }

        private void EnsureEmpireMember(
            Entity empire,
            Entity faction,
            in EmpireMembership membership,
            ref EntityCommandBuffer ecb)
        {
            if (empire == Entity.Null || faction == Entity.Null)
            {
                return;
            }

            if (_empireMemberLookup.HasBuffer(empire))
            {
                var buffer = _empireMemberLookup[empire];
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Faction != faction)
                    {
                        continue;
                    }

                    var entry = buffer[i];
                    entry.Loyalty = membership.Loyalty;
                    entry.Autonomy = membership.Autonomy;
                    entry.JoinedTick = membership.JoinedTick;
                    buffer[i] = entry;
                    return;
                }

                buffer.Add(new EmpireFactionEntry
                {
                    Faction = faction,
                    Loyalty = membership.Loyalty,
                    Autonomy = membership.Autonomy,
                    JoinedTick = membership.JoinedTick
                });
            }
            else
            {
                var buffer = ecb.AddBuffer<EmpireFactionEntry>(empire);
                buffer.Add(new EmpireFactionEntry
                {
                    Faction = faction,
                    Loyalty = membership.Loyalty,
                    Autonomy = membership.Autonomy,
                    JoinedTick = membership.JoinedTick
                });
            }
        }

        private void EnsureGuildMember(
            Entity guild,
            Entity member,
            AffiliationType memberType,
            in GuildMembershipEntry membership,
            ref EntityCommandBuffer ecb)
        {
            if (guild == Entity.Null || member == Entity.Null)
            {
                return;
            }

            if (_guildMemberLookup.HasBuffer(guild))
            {
                var buffer = _guildMemberLookup[guild];
                for (int i = 0; i < buffer.Length; i++)
                {
                    if (buffer[i].Member != member)
                    {
                        continue;
                    }

                    var entry = buffer[i];
                    entry.MemberType = memberType;
                    entry.Loyalty = membership.Loyalty;
                    entry.Status = membership.Status;
                    entry.JoinedTick = membership.JoinedTick;
                    buffer[i] = entry;
                    return;
                }

                buffer.Add(new GuildMemberEntry
                {
                    Member = member,
                    MemberType = memberType,
                    Loyalty = membership.Loyalty,
                    Status = membership.Status,
                    JoinedTick = membership.JoinedTick
                });
            }
            else
            {
                var buffer = ecb.AddBuffer<GuildMemberEntry>(guild);
                buffer.Add(new GuildMemberEntry
                {
                    Member = member,
                    MemberType = memberType,
                    Loyalty = membership.Loyalty,
                    Status = membership.Status,
                    JoinedTick = membership.JoinedTick
                });
            }
        }

        private AffiliationType ResolveMemberType(Entity entity)
        {
            if (_factionLookup.HasComponent(entity))
            {
                var faction = _factionLookup[entity];
                return faction.Type switch
                {
                    FactionType.Empire => AffiliationType.Empire,
                    FactionType.Corporation => AffiliationType.Corporation,
                    _ => AffiliationType.Faction
                };
            }

            return AffiliationType.Corporation;
        }
    }
}
