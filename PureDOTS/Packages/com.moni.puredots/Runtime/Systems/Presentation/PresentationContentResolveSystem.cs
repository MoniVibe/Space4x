using PureDOTS.Runtime.Components;
using PureDOTS.Runtime.Core;
using PureDOTS.Runtime.Registry;
using PureDOTS.Rendering;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Resolves RegistryIdentity to presentation bindings and ensures basic render components exist.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PresentationContentResolveSystem : ISystem
    {
        private EntityQuery _resolveQuery;

        public void OnCreate(ref SystemState state)
        {
            _resolveQuery = SystemAPI.QueryBuilder()
                .WithAll<RegistryIdentity>()
                .WithNone<PresentationContentResolved>()
                .Build();

            state.RequireForUpdate(_resolveQuery);
            state.RequireForUpdate<PresentationContentRegistryReference>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_resolveQuery.IsEmptyIgnoreFilter)
            {
                return;
            }

            PresentationContentRegistryReference registryRef = default;
            bool hasRegistry = false;

            foreach (var candidate in SystemAPI.Query<RefRO<PresentationContentRegistryReference>>())
            {
                registryRef = candidate.ValueRO;
                hasRegistry = true;
                if (registryRef.Registry.IsCreated)
                {
                    break;
                }
            }

            if (!hasRegistry || !registryRef.Registry.IsCreated)
            {
                return;
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);
            bool allowRender = !RuntimeMode.IsHeadless;

            foreach (var (identity, entity) in SystemAPI
                         .Query<RefRO<RegistryIdentity>>()
                         .WithNone<PresentationContentResolved>()
                         .WithEntityAccess())
            {
                if (!PresentationContentRegistryUtility.TryGetBinding(ref registryRef, identity.ValueRO.Id, out var binding))
                {
                    continue;
                }

                var resolved = new PresentationContentResolved
                {
                    Id = binding.Id,
                    RenderSemanticKey = binding.RenderSemanticKey,
                    RenderArchetypeId = binding.RenderArchetypeId,
                    DescriptorHash = binding.DescriptorHash,
                    SceneGuid = binding.SceneGuid,
                    BaseScale = binding.BaseScale,
                    BaseTint = binding.BaseTint,
                    Flags = binding.Flags
                };
                ecb.AddComponent(entity, resolved);

                if (!allowRender || (binding.Flags & PresentationContentFlags.HasRenderBinding) == 0)
                {
                    continue;
                }

                ushort semanticKey = binding.RenderSemanticKey != 0
                    ? binding.RenderSemanticKey
                    : binding.RenderArchetypeId;
                ushort archetypeKey = binding.RenderArchetypeId != 0
                    ? binding.RenderArchetypeId
                    : semanticKey;

                if (!SystemAPI.HasComponent<RenderSemanticKey>(entity))
                {
                    ecb.AddComponent(entity, new RenderSemanticKey { Value = semanticKey });
                }

                if (!SystemAPI.HasComponent<RenderKey>(entity))
                {
                    ecb.AddComponent(entity, new RenderKey
                    {
                        ArchetypeId = archetypeKey,
                        LOD = 0
                    });
                }

                if (!SystemAPI.HasComponent<RenderVariantKey>(entity))
                {
                    ecb.AddComponent(entity, new RenderVariantKey { Value = 0 });
                }

                if (!SystemAPI.HasComponent<RenderFlags>(entity))
                {
                    ecb.AddComponent(entity, new RenderFlags
                    {
                        Visible = 1,
                        ShadowCaster = 1,
                        HighlightMask = 0
                    });
                }

                if ((binding.Flags & PresentationContentFlags.HasBaseTint) != 0 &&
                    !SystemAPI.HasComponent<RenderTint>(entity))
                {
                    ecb.AddComponent(entity, new RenderTint { Value = binding.BaseTint });
                }

                if ((binding.Flags & PresentationContentFlags.HasBaseScale) != 0)
                {
                    var scale = new PresentationScaleMultiplier { Value = binding.BaseScale };
                    if (SystemAPI.HasComponent<PresentationScaleMultiplier>(entity))
                    {
                        ecb.SetComponent(entity, scale);
                    }
                    else
                    {
                        ecb.AddComponent(entity, scale);
                    }
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
