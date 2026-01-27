using PureDOTS.Config;
using PureDOTS.Runtime.Villagers;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace PureDOTS.Systems
{
    /// <summary>
    /// Resolves each villager's archetype by combining catalog data, assignments, and layered modifiers.
    /// Runs before AI so behaviour systems consume the updated profile every frame.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(VillagerSystemGroup), OrderFirst = true)]
    public partial struct VillagerArchetypeResolutionSystem : ISystem
    {
        private ComponentLookup<VillagerArchetypeAssignment> _assignmentLookup;
        private BufferLookup<VillagerArchetypeModifier> _modifierLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _assignmentLookup = state.GetComponentLookup<VillagerArchetypeAssignment>(true);
            _modifierLookup = state.GetBufferLookup<VillagerArchetypeModifier>(true);
            state.RequireForUpdate<VillagerArchetypeResolved>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hasCatalog = SystemAPI.TryGetSingleton(out VillagerArchetypeCatalogComponent catalogComponent) &&
                             catalogComponent.Catalog.IsCreated;

            _assignmentLookup.Update(ref state);
            _modifierLookup.Update(ref state);

            VillagerArchetypeDefaults.CreateFallback(out var fallback);

            var job = new ResolveVillagerArchetypeJob
            {
                Catalog = hasCatalog ? catalogComponent.Catalog : default,
                AssignmentLookup = _assignmentLookup,
                ModifierLookup = _modifierLookup,
                Fallback = fallback
            };

            state.Dependency = job.ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        public partial struct ResolveVillagerArchetypeJob : IJobEntity
        {
            public BlobAssetReference<VillagerArchetypeCatalogBlob> Catalog;
            [ReadOnly] public ComponentLookup<VillagerArchetypeAssignment> AssignmentLookup;
            [ReadOnly] public BufferLookup<VillagerArchetypeModifier> ModifierLookup;
            public VillagerArchetypeData Fallback;

            public void Execute(Entity entity, ref VillagerArchetypeResolved resolved)
            {
                var data = Fallback;
                var index = 0;

                if (Catalog.IsCreated && Catalog.Value.Archetypes.Length > 0)
                {
                    ref var archetypes = ref Catalog.Value.Archetypes;
                    if (AssignmentLookup.HasComponent(entity))
                    {
                        var assignment = AssignmentLookup[entity];
                        if (assignment.HasCachedIndex &&
                            assignment.CachedIndex < archetypes.Length &&
                            archetypes[assignment.CachedIndex].ArchetypeName.Equals(assignment.ArchetypeName))
                        {
                            index = assignment.CachedIndex;
                        }
                        else
                        {
                            index = Catalog.Value.FindArchetypeIndex(assignment.ArchetypeName);
                            if (index < 0)
                            {
                                index = 0;
                            }
                        }
                    }

                    // Copy blob array element to local variable (can't pass blob elements by ref)
                    var archetypeFromBlob = archetypes[index];
                    data = archetypeFromBlob;
                }

                if (ModifierLookup.HasBuffer(entity))
                {
                    var modifiers = ModifierLookup[entity];
                    for (var i = 0; i < modifiers.Length; i++)
                    {
                        // Copy buffer element to local variable (can't pass buffer elements by ref)
                        var modifier = modifiers[i];
                        data = VillagerArchetypeDefaults.ApplyModifier(in data, in modifier);
                    }
                }

                resolved.ArchetypeIndex = index;
                resolved.Data = data;
            }
        }
    }
}
