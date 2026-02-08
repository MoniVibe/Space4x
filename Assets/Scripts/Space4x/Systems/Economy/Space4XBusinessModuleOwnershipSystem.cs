using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Modules;
using PureDOTS.Systems;
using Space4X.Registry;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace Space4X.Systems.Economy
{
    /// <summary>
    /// Mirrors business ownership from ships to their attached modules.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(AISystemGroup))]
    [UpdateAfter(typeof(Space4X.Systems.Modules.Space4XModuleAttachmentSyncSystem))]
    public partial struct Space4XBusinessModuleOwnershipSystem : ISystem
    {
        private ComponentLookup<ModuleOwner> _moduleOwnerLookup;
        private ComponentLookup<ModuleTypeId> _moduleTypeLookup;
        private ComponentLookup<Space4XBusinessAssetOwner> _assetOwnerLookup;
        private BufferLookup<Space4XBusinessAssetLink> _assetLinkLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ModuleOwner>();
            _moduleOwnerLookup = state.GetComponentLookup<ModuleOwner>(true);
            _moduleTypeLookup = state.GetComponentLookup<ModuleTypeId>(true);
            _assetOwnerLookup = state.GetComponentLookup<Space4XBusinessAssetOwner>(false);
            _assetLinkLookup = state.GetBufferLookup<Space4XBusinessAssetLink>(false);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _moduleOwnerLookup.Update(ref state);
            _moduleTypeLookup.Update(ref state);
            _assetOwnerLookup.Update(ref state);
            _assetLinkLookup.Update(ref state);

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (moduleOwner, moduleEntity) in SystemAPI.Query<RefRO<ModuleOwner>>().WithEntityAccess())
            {
                var ownerEntity = moduleOwner.ValueRO.Owner;
                if (ownerEntity == Entity.Null || !_assetOwnerLookup.HasComponent(ownerEntity))
                {
                    continue;
                }

                var shipOwner = _assetOwnerLookup[ownerEntity];
                if (shipOwner.Business == Entity.Null)
                {
                    continue;
                }

                var catalogId = _moduleTypeLookup.HasComponent(moduleEntity)
                    ? _moduleTypeLookup[moduleEntity].Value
                    : default;

                var hasModuleOwner = _assetOwnerLookup.HasComponent(moduleEntity);
                if (hasModuleOwner)
                {
                    var existing = _assetOwnerLookup[moduleEntity];
                    if (existing.Business == shipOwner.Business)
                    {
                        if (existing.CatalogId.IsEmpty && !catalogId.IsEmpty)
                        {
                            existing.CatalogId = catalogId;
                            _assetOwnerLookup[moduleEntity] = existing;
                        }

                        EnsureLink(ref ecb, shipOwner.Business, moduleEntity, Space4XBusinessAssetType.Module, shipOwner.AssignedTick, catalogId);
                        continue;
                    }

                    RemoveLink(existing.Business, moduleEntity);
                    existing.Business = shipOwner.Business;
                    existing.AssetType = Space4XBusinessAssetType.Module;
                    existing.AssignedTick = shipOwner.AssignedTick;
                    existing.CatalogId = catalogId;
                    _assetOwnerLookup[moduleEntity] = existing;
                }
                else
                {
                    ecb.AddComponent(moduleEntity, new Space4XBusinessAssetOwner
                    {
                        Business = shipOwner.Business,
                        AssetType = Space4XBusinessAssetType.Module,
                        AssignedTick = shipOwner.AssignedTick,
                        CatalogId = catalogId
                    });
                }

                EnsureLink(ref ecb, shipOwner.Business, moduleEntity, Space4XBusinessAssetType.Module, shipOwner.AssignedTick, catalogId);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        private void RemoveLink(Entity business, Entity asset)
        {
            if (business == Entity.Null || asset == Entity.Null)
            {
                return;
            }

            if (!_assetLinkLookup.HasBuffer(business))
            {
                return;
            }

            var links = _assetLinkLookup[business];
            for (int i = 0; i < links.Length; i++)
            {
                if (links[i].Asset == asset)
                {
                    links.RemoveAt(i);
                    return;
                }
            }
        }

        private void EnsureLink(
            ref EntityCommandBuffer ecb,
            Entity business,
            Entity asset,
            Space4XBusinessAssetType assetType,
            uint tick,
            FixedString64Bytes catalogId)
        {
            if (business == Entity.Null || asset == Entity.Null)
            {
                return;
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
    }
}
