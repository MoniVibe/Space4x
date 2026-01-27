using PureDOTS.Rendering;
using PureDOTS.Runtime.Combat;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace PureDOTS.Rendering
{
    /// <summary>
    /// Chooses which presenter (mesh, sprite, tracer) should be enabled for projectile entities.
    /// Keeps structural changes stable by toggling enableable components only.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ResolveRenderVariantSystem))]
    [UpdateBefore(typeof(TracerPresenterSystem))]
    public partial struct ProjectilePresenterPolicySystem : ISystem
    {
        private ComponentLookup<RenderVariantResolved> _resolvedLookup;
        private ComponentLookup<RenderKey> _renderKeyLookup;

        private const float TracerSpeedThreshold = 40f;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ProjectileTag>();
            state.RequireForUpdate<RenderPresentationCatalog>();
            state.RequireForUpdate<ProjectileActive>();
            _resolvedLookup = state.GetComponentLookup<RenderVariantResolved>(true);
            _renderKeyLookup = state.GetComponentLookup<RenderKey>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _resolvedLookup.Update(ref state);
            _renderKeyLookup.Update(ref state);

            foreach (var (projectile, active, tracerEnabled, meshEnabled, spriteEnabled, entity) in SystemAPI
                         .Query<RefRO<ProjectileEntity>, EnabledRefRO<ProjectileActive>, EnabledRefRW<TracerPresenter>, EnabledRefRW<MeshPresenter>, EnabledRefRW<SpritePresenter>>()
                         .WithAll<ProjectileTag>()
                         .WithEntityAccess())
            {
                if (!active.ValueRO)
                {
                    tracerEnabled.ValueRW = false;
                    meshEnabled.ValueRW = false;
                    spriteEnabled.ValueRW = false;
                    continue;
                }

                var mask = _resolvedLookup.HasComponent(entity)
                    ? _resolvedLookup[entity].LastMask
                    : RenderPresenterMask.Mesh;

                bool tracerAvailable = (mask & RenderPresenterMask.Tracer) != 0;
                bool meshAvailable = (mask & RenderPresenterMask.Mesh) != 0;
                bool spriteAvailable = (mask & RenderPresenterMask.Sprite) != 0;

                var speed = math.length(projectile.ValueRO.Velocity);
                var lod = _renderKeyLookup.HasComponent(entity) ? _renderKeyLookup[entity].LOD : (byte)0;

                var preferTracer = tracerAvailable && speed >= TracerSpeedThreshold;
                var preferSprite = !preferTracer && spriteAvailable && lod > 0;
                var preferMesh = !preferTracer && !preferSprite && meshAvailable;

                if (!preferTracer && !preferSprite && !preferMesh)
                {
                    if (tracerAvailable)
                        preferTracer = true;
                    else if (meshAvailable)
                        preferMesh = true;
                    else if (spriteAvailable)
                        preferSprite = true;
                }

                tracerEnabled.ValueRW = preferTracer;
                meshEnabled.ValueRW = preferMesh;
                spriteEnabled.ValueRW = preferSprite;
            }
        }
    }
}
