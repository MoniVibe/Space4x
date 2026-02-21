using PureDOTS.Runtime;
using PureDOTS.Runtime.Technology;
using Space4X.Registry;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Space4X.Systems.Research
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct Space4XResearchSeedSystem : ISystem
    {
        private EntityQuery _seedCandidateQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ScenarioState>();
            _seedCandidateQuery = SystemAPI.QueryBuilder()
                .WithAny<Space4XColony, StationId>()
                .WithNone<Space4XResearchSeedRequest, ResearchProject>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out ScenarioState scenario) ||
                !scenario.IsInitialized ||
                !scenario.EnableSpace4x ||
                !scenario.EnableEconomy)
            {
                return;
            }

            var em = state.EntityManager;
            if (!_seedCandidateQuery.IsEmptyIgnoreFilter)
            {
                using var entities = _seedCandidateQuery.ToEntityArray(state.WorldUpdateAllocator);
                if (entities.Length > 0)
                {
                    var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        ecb.AddComponent(entities[i], new Space4XResearchSeedRequest { ClearExisting = 0 });
                    }
                    ecb.Playback(em);
                }
            }

            var catalog = Space4XResearchCatalog.LoadOrFallback();
            if (catalog == null || catalog.Nodes == null || catalog.Nodes.Length == 0)
            {
                return;
            }

            foreach (var (request, entity) in SystemAPI.Query<RefRO<Space4XResearchSeedRequest>>().WithEntityAccess())
            {
                var buffer = em.HasBuffer<ResearchProject>(entity)
                    ? em.GetBuffer<ResearchProject>(entity)
                    : em.AddBuffer<ResearchProject>(entity);

                if (request.ValueRO.ClearExisting != 0)
                {
                    buffer.Clear();
                }

                for (int i = 0; i < catalog.Nodes.Length; i++)
                {
                    var node = catalog.Nodes[i];
                    var projectId = new FixedString64Bytes(node.Id ?? string.Empty);
                    if (projectId.IsEmpty)
                    {
                        continue;
                    }

                    var totalCost = ResearchTreeHelpers.CalculateResearchCost(node, 0, 0);
                    var tier = (byte)math.clamp(node.Tier, 0, 255);
                    buffer.Add(new ResearchProject
                    {
                        ProjectId = projectId,
                        Category = new FixedString32Bytes(node.DisciplineId ?? string.Empty),
                        RequiredTier = tier,
                        TotalResearchCost = totalCost,
                        CurrentProgress = 0f,
                        IsCompleted = 0,
                        IsActive = 0
                    });
                }

                em.RemoveComponent<Space4XResearchSeedRequest>(entity);
            }
        }
    }
}
