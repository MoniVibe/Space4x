using PureDOTS.Runtime;
using PureDOTS.Runtime.Components;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Assigns facility/market assets to businesses based on colony and facility role.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Space4XBusinessSpawnSystem))]
    public partial struct Space4XBusinessAssetOwnershipSystem : ISystem
    {
        private BufferLookup<Space4XBusinessAssetLink> _assetLinkLookup;
        private ComponentLookup<Space4XBusinessAssetOwner> _assetOwnerLookup;
        private ComponentLookup<Space4XMarket> _marketLookup;
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
            state.RequireForUpdate<Space4XBusinessState>();

            _assetLinkLookup = state.GetBufferLookup<Space4XBusinessAssetLink>(false);
            _assetOwnerLookup = state.GetComponentLookup<Space4XBusinessAssetOwner>(false);
            _marketLookup = state.GetComponentLookup<Space4XMarket>(true);
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

            _assetLinkLookup.Update(ref state);
            _assetOwnerLookup.Update(ref state);
            _marketLookup.Update(ref state);
            _entityLookup.Update(ref state);

            var tick = SystemAPI.TryGetSingleton(out TickTimeState tickTime) ? tickTime.Tick : 0u;
            var ecb = new EntityCommandBuffer(Allocator.Temp);
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

            foreach (var (businessState, businessEntity) in SystemAPI.Query<RefRO<Space4XBusinessState>>().WithEntityAccess())
            {
                if (!IsValidEntity(businessEntity))
                {
                    continue;
                }

                var colony = businessState.ValueRO.Colony;
                if (!IsValidEntity(colony))
                {
                    continue;
                }

                if (businessState.ValueRO.FacilityClass != FacilityBusinessClass.None &&
                    TryResolveFacility(colony, businessState.ValueRO.FacilityClass, facilityMap, out var facilityEntity))
                {
                    TryAssignAsset(ref state, ref ecb, businessEntity, facilityEntity, Space4XBusinessAssetType.Facility, tick);
                }

                if (businessState.ValueRO.Kind == Space4XBusinessKind.MarketHub && _marketLookup.HasComponent(colony))
                {
                    TryAssignAsset(ref state, ref ecb, businessEntity, colony, Space4XBusinessAssetType.Market, tick);
                }
            }

            ecb.Playback(state.EntityManager);
            facilityMap.Dispose();
            ecb.Dispose();
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

        private void TryAssignAsset(
            ref SystemState state,
            ref EntityCommandBuffer ecb,
            Entity business,
            Entity asset,
            Space4XBusinessAssetType assetType,
            uint tick)
        {
            if (asset == Entity.Null || business == Entity.Null)
            {
                return;
            }

            var catalogId = default(FixedString64Bytes);
            if (_assetOwnerLookup.HasComponent(asset))
            {
                var owner = _assetOwnerLookup[asset];
                if (owner.Business != business)
                {
                    return;
                }

                if (owner.CatalogId.IsEmpty && !catalogId.IsEmpty)
                {
                    owner.CatalogId = catalogId;
                    _assetOwnerLookup[asset] = owner;
                }
            }
            else
            {
                ecb.AddComponent(asset, new Space4XBusinessAssetOwner
                {
                    Business = business,
                    AssetType = assetType,
                    AssignedTick = tick,
                    CatalogId = catalogId
                });
            }

            if (_assetLinkLookup.HasBuffer(business))
            {
                var links = _assetLinkLookup[business];
                for (int i = 0; i < links.Length; i++)
                {
                    if (links[i].Asset == asset)
                    {
                        if (links[i].CatalogId.IsEmpty && !catalogId.IsEmpty)
                        {
                            var existing = links[i];
                            existing.CatalogId = catalogId;
                            links[i] = existing;
                        }
                        return;
                    }
                }

                links.Add(new Space4XBusinessAssetLink
                {
                    Asset = asset,
                    AssetType = assetType,
                    AssignedTick = tick,
                    CatalogId = catalogId
                });
            }
            else
            {
                var links = ecb.AddBuffer<Space4XBusinessAssetLink>(business);
                links.Add(new Space4XBusinessAssetLink
                {
                    Asset = asset,
                    AssetType = assetType,
                    AssignedTick = tick,
                    CatalogId = catalogId
                });
            }
        }

        private bool IsValidEntity(Entity entity)
        {
            return entity != Entity.Null && _entityLookup.Exists(entity);
        }
    }
}
