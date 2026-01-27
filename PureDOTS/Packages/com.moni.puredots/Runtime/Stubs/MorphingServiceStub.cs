// [TRI-STUB] This is an ahead-of-time stub. Safe to compile, does nothing at runtime.
using Unity.Entities;

namespace PureDOTS.Runtime.Morphing
{
    public static class MorphingServiceStub
    {
        public static void ApplyDeformation(EntityManager manager, Entity entity, float delta)
        {
            if (!manager.HasComponent<TerrainMorphState>(entity)) return;
            var state = manager.GetComponentData<TerrainMorphState>(entity);
            state.Deformation += delta;
            manager.SetComponentData(entity, state);
        }

        public static void DamageSurface(EntityManager manager, Entity entity, float damage)
        {
            if (!manager.HasComponent<BreakableSurface>(entity)) return;
            var surface = manager.GetComponentData<BreakableSurface>(entity);
            surface.Integrity = surface.Integrity - damage;
            manager.SetComponentData(entity, surface);
        }
    }
}
