using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Dynasty;
using PureDOTS.Runtime.Family;
using PureDOTS.Runtime.Individual;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Registry
{
    /// <summary>
    /// Bridges Space4X lineage IDs into PureDOTS family and dynasty entities.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XIndividualNormalizationSystem))]
    public partial struct Space4XLineageBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TimeState>();
            state.RequireForUpdate<LineageId>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var timeState = SystemAPI.GetSingleton<TimeState>();
            if (timeState.IsPaused)
            {
                return;
            }

            var currentTick = timeState.Tick;
            var em = state.EntityManager;
            using var ecb = new EntityCommandBuffer(Allocator.Temp);

            var familyMap = new NativeParallelHashMap<FixedString64Bytes, Entity>(64, Allocator.Temp);
            var dynastyMap = new NativeParallelHashMap<FixedString64Bytes, Entity>(64, Allocator.Temp);

            foreach (var (identity, entity) in SystemAPI.Query<RefRO<FamilyIdentity>>().WithEntityAccess())
            {
                if (identity.ValueRO.FamilyName.Length > 0)
                {
                    familyMap.TryAdd(identity.ValueRO.FamilyName, entity);
                }
            }

            foreach (var (identity, entity) in SystemAPI.Query<RefRO<DynastyIdentity>>().WithEntityAccess())
            {
                if (identity.ValueRO.DynastyName.Length > 0)
                {
                    dynastyMap.TryAdd(identity.ValueRO.DynastyName, entity);
                }
            }

            foreach (var (lineage, _, entity) in SystemAPI.Query<RefRO<LineageId>, RefRO<SimIndividualTag>>().WithEntityAccess())
            {
                var lineageId = lineage.ValueRO.Id;
                if (lineageId.Length == 0)
                {
                    continue;
                }

                var familyEntity = EnsureFamily(ref ecb, em, familyMap, lineageId, entity, currentTick);
                var dynastyEntity = EnsureDynasty(ref ecb, em, dynastyMap, lineageId, entity, currentTick);

                EnsureBelongingEntry(ref ecb, em, entity, BelongingTier.Family, familyEntity, lineageId, 70, 90, currentTick, true);
                EnsureBelongingEntry(ref ecb, em, entity, BelongingTier.Dynasty, dynastyEntity, lineageId, 55, 70, currentTick, false);
            }

            ecb.Playback(em);

            familyMap.Dispose();
            dynastyMap.Dispose();
        }

        private static Entity EnsureFamily(
            ref EntityCommandBuffer ecb,
            EntityManager em,
            NativeParallelHashMap<FixedString64Bytes, Entity> familyMap,
            FixedString64Bytes lineageId,
            Entity member,
            uint currentTick)
        {
            if (!familyMap.TryGetValue(lineageId, out var familyEntity))
            {
                familyEntity = FamilyService.CreateFamily(ref ecb, member, lineageId, currentTick);
                familyMap[lineageId] = familyEntity;
                return familyEntity;
            }

            if (em.HasComponent<FamilyMember>(member))
            {
                var familyMember = em.GetComponentData<FamilyMember>(member);
                if (familyMember.FamilyEntity != familyEntity)
                {
                    return familyEntity;
                }
            }
            else
            {
                FamilyService.AddMember(ref ecb, familyEntity, member, FamilyRole.Extended, currentTick);
            }

            if (!HasFamilyTreeEntry(em, familyEntity, member))
            {
                FamilyService.AddToFamilyTree(ref ecb, familyEntity, member, Entity.Null, Entity.Null, currentTick);
            }

            return familyEntity;
        }

        private static Entity EnsureDynasty(
            ref EntityCommandBuffer ecb,
            EntityManager em,
            NativeParallelHashMap<FixedString64Bytes, Entity> dynastyMap,
            FixedString64Bytes lineageId,
            Entity member,
            uint currentTick)
        {
            if (!dynastyMap.TryGetValue(lineageId, out var dynastyEntity))
            {
                dynastyEntity = DynastyService.CreateDynasty(ref ecb, member, Entity.Null, lineageId, currentTick);
                dynastyMap[lineageId] = dynastyEntity;
                return dynastyEntity;
            }

            if (em.HasComponent<DynastyMember>(member))
            {
                var dynastyMember = em.GetComponentData<DynastyMember>(member);
                if (dynastyMember.DynastyEntity != dynastyEntity)
                {
                    return dynastyEntity;
                }
            }
            else
            {
                DynastyService.AddMember(ref ecb, dynastyEntity, member, DynastyRank.Member, 0.5f, currentTick);
            }

            if (!HasDynastyLineageEntry(em, dynastyEntity, member))
            {
                var founder = em.HasComponent<DynastyIdentity>(dynastyEntity)
                    ? em.GetComponentData<DynastyIdentity>(dynastyEntity).FounderEntity
                    : Entity.Null;

                byte generation = founder != Entity.Null && founder == member ? (byte)0 : (byte)1;
                DynastyService.TrackLineage(ref ecb, dynastyEntity, member, Entity.Null, Entity.Null, currentTick, generation);
            }

            return dynastyEntity;
        }

        private static bool HasFamilyTreeEntry(EntityManager em, Entity familyEntity, Entity member)
        {
            if (!em.HasBuffer<FamilyTree>(familyEntity))
            {
                return false;
            }

            var tree = em.GetBuffer<FamilyTree>(familyEntity);
            for (int i = 0; i < tree.Length; i++)
            {
                if (tree[i].MemberEntity == member)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDynastyLineageEntry(EntityManager em, Entity dynastyEntity, Entity member)
        {
            if (!em.HasBuffer<DynastyLineage>(dynastyEntity))
            {
                return false;
            }

            var lineage = em.GetBuffer<DynastyLineage>(dynastyEntity);
            for (int i = 0; i < lineage.Length; i++)
            {
                if (lineage[i].MemberEntity == member)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureBelongingEntry(
            ref EntityCommandBuffer ecb,
            EntityManager em,
            Entity entity,
            BelongingTier tier,
            Entity target,
            FixedString64Bytes lineageId,
            byte loyalty,
            byte priority,
            uint currentTick,
            bool allowPrimary)
        {
            if (entity == Entity.Null || target == Entity.Null)
            {
                return;
            }

            bool hasBuffer = em.HasBuffer<BelongingEntry>(entity);
            if (!hasBuffer)
            {
                ecb.AddBuffer<BelongingEntry>(entity);
            }

            if (hasBuffer)
            {
                var buffer = em.GetBuffer<BelongingEntry>(entity);
                for (int i = 0; i < buffer.Length; i++)
                {
                    var entry = buffer[i];
                    if (entry.Tier != tier)
                    {
                        continue;
                    }

                    entry.Target = target;
                    entry.Loyalty = loyalty;
                    entry.Priority = priority;
                    entry.TargetName = new FixedString32Bytes(lineageId);
                    buffer[i] = entry;
                    return;
                }
            }

            byte isPrimary = allowPrimary && !HasPrimaryBelonging(em, entity) ? (byte)1 : (byte)0;

            ecb.AppendToBuffer(entity, new BelongingEntry
            {
                Tier = tier,
                Target = target,
                Loyalty = loyalty,
                Priority = priority,
                IsPrimaryIdentity = isPrimary,
                EstablishedTick = currentTick,
                TargetName = new FixedString32Bytes(lineageId)
            });
        }

        private static bool HasPrimaryBelonging(EntityManager em, Entity entity)
        {
            if (!em.HasBuffer<BelongingEntry>(entity))
            {
                return false;
            }

            var buffer = em.GetBuffer<BelongingEntry>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].IsPrimaryIdentity != 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
