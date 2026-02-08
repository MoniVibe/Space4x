using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Space4X.SimServer;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Spawns initial businesses for colonies based on the business catalog.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(Space4XColonyIndustryBootstrapSystem))]
    public partial struct Space4XBusinessSpawnSystem : ISystem
    {
        private BufferLookup<AffiliationTag> _affiliationLookup;
        private ComponentLookup<Space4XFaction> _factionLookup;
        private ComponentLookup<EmpireMembership> _empireLookup;
        private EntityStorageInfoLookup _entityLookup;

        private struct FacilityRecord
        {
            public FacilityBusinessClass Class;
            public Entity Entity;
        }

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            state.RequireForUpdate<Space4XBusinessCatalogSingleton>();
            state.RequireForUpdate<Space4XJobCatalogSingleton>();

            _affiliationLookup = state.GetBufferLookup<AffiliationTag>(true);
            _factionLookup = state.GetComponentLookup<Space4XFaction>(true);
            _empireLookup = state.GetComponentLookup<EmpireMembership>(true);
            _entityLookup = state.GetEntityStorageInfoLookup();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableEconomy ||
                !scenario.EnableSpace4x)
            {
                return;
            }

            if (!SystemAPI.TryGetSingleton(out TickTimeState tickTime))
            {
                return;
            }

            _affiliationLookup.Update(ref state);
            _factionLookup.Update(ref state);
            _empireLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var businessCatalog = SystemAPI.GetSingleton<Space4XBusinessCatalogSingleton>().Catalog;
            if (!businessCatalog.IsCreated)
            {
                return;
            }

            var jobCatalog = SystemAPI.GetSingleton<Space4XJobCatalogSingleton>().Catalog;
            if (!jobCatalog.IsCreated)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            ref var businessDefs = ref businessCatalog.Value.Businesses;
            var facilityMap = new NativeParallelMultiHashMap<Entity, FacilityRecord>(64, Allocator.Temp);

            foreach (var (link, facility) in SystemAPI.Query<RefRO<ColonyFacilityLink>>().WithEntityAccess())
            {
                if (!IsValidEntity(link.ValueRO.Colony) || !IsValidEntity(facility))
                {
                    continue;
                }

                facilityMap.Add(link.ValueRO.Colony, new FacilityRecord
                {
                    Class = link.ValueRO.FacilityClass,
                    Entity = facility
                });
            }

            foreach (var (colony, colonyEntity) in SystemAPI.Query<RefRO<Space4XColony>>()
                         .WithNone<Space4XBusinessSpawnTag>()
                         .WithEntityAccess())
            {
                if (!IsValidEntity(colonyEntity))
                {
                    continue;
                }

                TryResolveColonyFaction(colonyEntity, out var colonyFaction, out _);

                for (int i = 0; i < businessDefs.Length; i++)
                {
                    ref var businessDef = ref businessDefs[i];

                    Entity facilityEntity = Entity.Null;
                    var facilityClass = businessDef.PrimaryFacility;
                    if (facilityClass != FacilityBusinessClass.None &&
                        !TryResolveFacility(colonyEntity, facilityClass, facilityMap, out facilityEntity))
                    {
                        continue;
                    }

                    var ownerEntity = ResolveOwner(ref businessDef, colonyEntity, colonyFaction, tickTime.Tick, ref ecb);
                    if (ownerEntity == Entity.Null && businessDef.OwnerKind == Space4XBusinessOwnerKind.Individual)
                    {
                        continue;
                    }

                    if (!IsValidEntity(colonyEntity))
                    {
                        continue;
                    }

                    var businessEntity = ecb.CreateEntity();
                    ecb.AddComponent<Space4XSimServerTag>(businessEntity);
                    ecb.AddComponent(businessEntity, new Space4XBusinessState
                    {
                        Kind = businessDef.Kind,
                        OwnerKind = businessDef.OwnerKind,
                        Owner = ownerEntity,
                        Colony = colonyEntity,
                        Facility = facilityEntity,
                        FacilityClass = facilityClass,
                        ActiveJobId = default,
                        LastJobTick = 0u,
                        NextJobTick = tickTime.Tick,
                        Credits = math.max(0f, businessDef.StartingCredits)
                    });

                    if (IsValidEntity(colonyFaction))
                    {
                        var affiliations = ecb.AddBuffer<AffiliationTag>(businessEntity);
                        affiliations.Add(new AffiliationTag
                        {
                            Type = AffiliationType.Faction,
                            Target = colonyFaction,
                            Loyalty = (half)0.6f
                        });
                    }

                    var storage = ecb.AddBuffer<ResourceStorage>(businessEntity);
                    SeedBusinessResources(ref storage, ref businessDef, ref jobCatalog.Value);

                }

                ecb.AddComponent<Space4XBusinessSpawnTag>(colonyEntity);
            }

            facilityMap.Dispose();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void TryResolveColonyFaction(Entity colony, out Entity factionEntity, out ushort factionId)
        {
            factionEntity = Entity.Null;
            factionId = 0;

            if (!IsValidEntity(colony) || !_affiliationLookup.HasBuffer(colony))
            {
                return;
            }

            var affiliations = _affiliationLookup[colony];
            for (int i = 0; i < affiliations.Length; i++)
            {
                var tag = affiliations[i];
                if (tag.Type != AffiliationType.Faction || !IsValidEntity(tag.Target))
                {
                    continue;
                }

                if (_factionLookup.HasComponent(tag.Target))
                {
                    factionEntity = tag.Target;
                    factionId = _factionLookup[tag.Target].FactionId;
                    return;
                }
            }
        }

        private static bool TryResolveFacility(
            Entity colony,
            FacilityBusinessClass facilityClass,
            NativeParallelMultiHashMap<Entity, FacilityRecord> facilityMap,
            out Entity facilityEntity)
        {
            facilityEntity = Entity.Null;
            if (!facilityMap.TryGetFirstValue(colony, out var record, out var iterator))
            {
                return false;
            }

            do
            {
                if (record.Class == facilityClass)
                {
                    facilityEntity = record.Entity;
                    return true;
                }
            }
            while (facilityMap.TryGetNextValue(out record, ref iterator));

            return false;
        }

        private Entity ResolveOwner(
            ref Space4XBusinessDefinition businessDef,
            Entity colony,
            Entity colonyFaction,
            uint tick,
            ref EntityCommandBuffer ecb)
        {
            switch (businessDef.OwnerKind)
            {
                case Space4XBusinessOwnerKind.Faction:
                    if (IsValidEntity(colonyFaction))
                    {
                        return colonyFaction;
                    }
                    break;
                case Space4XBusinessOwnerKind.Empire:
                    if (IsValidEntity(colonyFaction) && _empireLookup.HasComponent(colonyFaction))
                    {
                        var membership = _empireLookup[colonyFaction];
                        if (IsValidEntity(membership.Empire))
                        {
                            return membership.Empire;
                        }
                    }
                    if (IsValidEntity(colonyFaction))
                    {
                        return colonyFaction;
                    }
                    break;
                case Space4XBusinessOwnerKind.Group:
                case Space4XBusinessOwnerKind.Individual:
                {
                    var owner = ecb.CreateEntity();
                    ecb.AddComponent<Space4XSimServerTag>(owner);
                    ecb.AddComponent(owner, new Space4XBusinessOwner
                    {
                        Kind = businessDef.OwnerKind,
                        HomeColony = colony,
                        CreatedTick = tick
                    });

                    if (IsValidEntity(colonyFaction))
                    {
                        var affiliations = ecb.AddBuffer<AffiliationTag>(owner);
                        affiliations.Add(new AffiliationTag
                        {
                            Type = AffiliationType.Faction,
                            Target = colonyFaction,
                            Loyalty = (half)0.5f
                        });
                    }

                    return owner;
                }
            }

            return Entity.Null;
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }

        private static void SeedBusinessResources(
            ref DynamicBuffer<ResourceStorage> storage,
            ref Space4XBusinessDefinition businessDef,
            ref Space4XJobCatalogBlob jobCatalog)
        {
            for (int i = 0; i < businessDef.JobIds.Length; i++)
            {
                var jobId = businessDef.JobIds[i];
                if (!TryResolveJob(jobId, ref jobCatalog, out var jobIndex))
                {
                    continue;
                }

                ref var jobDef = ref jobCatalog.Jobs[jobIndex];
                for (int inputIndex = 0; inputIndex < jobDef.Inputs.Length; inputIndex++)
                {
                    var input = jobDef.Inputs[inputIndex];
                    EnsureStorageEntry(ref storage, input.Type, ResolveSeedAmount(input.Type));
                }

                for (int outputIndex = 0; outputIndex < jobDef.Outputs.Length; outputIndex++)
                {
                    var output = jobDef.Outputs[outputIndex];
                    EnsureStorageEntry(ref storage, output.Type, 0f);
                }
            }
        }

        private static float ResolveSeedAmount(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Fuel:
                case ResourceType.Supplies:
                    return 12f;
                case ResourceType.Food:
                case ResourceType.Water:
                    return 6f;
                case ResourceType.Minerals:
                case ResourceType.Ore:
                case ResourceType.OrganicMatter:
                case ResourceType.Volatiles:
                case ResourceType.EnergyCrystals:
                    return 4f;
                default:
                    return 0f;
            }
        }

        private static void EnsureStorageEntry(ref DynamicBuffer<ResourceStorage> storage, ResourceType type, float initialAmount)
        {
            for (int i = 0; i < storage.Length; i++)
            {
                if (storage[i].Type == type)
                {
                    if (initialAmount > 0f)
                    {
                        var entry = storage[i];
                        entry.Amount = math.min(entry.Capacity, math.max(entry.Amount, initialAmount));
                        storage[i] = entry;
                    }
                    return;
                }
            }

            var slot = ResourceStorage.Create(type, 5000f);
            if (initialAmount > 0f)
            {
                slot.Amount = math.min(slot.Capacity, initialAmount);
            }
            storage.Add(slot);
        }

        private static bool TryResolveJob(in FixedString64Bytes jobId, ref Space4XJobCatalogBlob catalog, out int index)
        {
            for (int i = 0; i < catalog.Jobs.Length; i++)
            {
                ref var job = ref catalog.Jobs[i];
                if (job.Id.Equals(jobId))
                {
                    index = i;
                    return true;
                }
            }

            index = -1;
            return false;
        }
    }
}
